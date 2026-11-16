#!/usr/bin/env python3
"""Звуки Реквиема (M7 п. 5): свои звуки синтезом + проверка таблицы data/sfx.csv.

Основа — ванильные события FMOD игры (data/sfx.csv, source=fmod) и её файлы res://debug_audio (source=game). Там, где у игры
нет подходящего звука (сердцебиение надрыва, треск брони, свист Уворота, барабан и рог Кличей, откат, портал), звук
синтезируется здесь — программно, без чужих сэмплов и лицензий: numpy → WAV 22 050 Гц моно 16 бит →
mod/SlayTheRequiem/audio/sfx/{name}.wav (Godot импортирует WAV при экспорте, игра играет его на шине SFX — громкость
«Звуки» из настроек игры действует).

    python3 tools/audio/build_sfx.py --render   # пересобрать WAV
    python3 tools/audio/build_sfx.py --check    # WAV совпадают с синтезом (±2 единицы — libm), таблица корректна

Проверка таблицы: события — ровно из Core/SfxRules.cs (SfxEvents), источник fmod — событие из списка известных событий
игры (tools/audio/vanilla_fmod_events.txt, выписан из декомпиляции v0.107.1), game — известный файл игры, own — файл,
который делает этот скрипт; громкость 0…2.
"""
from __future__ import annotations

import argparse
import csv
import io
import os
import re
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "mod", "SlayTheRequiem", "audio", "sfx")
TABLE = os.path.join(ROOT, "mod", "SlayTheRequiem", "data", "sfx.csv")
RULES = os.path.join(ROOT, "mod", "SlayTheRequiemCode", "Core", "SfxRules.cs")
KNOWN_FMOD = os.path.join(HERE, "vanilla_fmod_events.txt")
SR = 22050
PEAK = 0.8
SOURCES = ("fmod", "game", "own")
# Файлы res://debug_audio, которые игра сама называет (TmpSfx.cs v0.107.1)
KNOWN_GAME = {
    "battle_start_1.mp3",
    "battle_start_2.mp3",
    "blunt_attack.mp3",
    "card_deal.mp3",
    "card_exhaust.mp3",
    "card_select.mp3",
    "card_smith.mp3",
    "character_unlock.mp3",
    "character_unlock_charge.mp3",
    "dagger_throw.mp3",
    "deny.mp3",
    "doll_room_amb.mp3",
    "doom_apply.mp3",
    "enemy_turn.mp3",
    "gain_potion.mp3",
    "heavy_attack.mp3",
    "hey.mp3",
    "hiss.mp3",
    "lightning_orb_evoke.mp3",
    "map_open.mp3",
    "map_ping.mp3",
    "map_split_tick.mp3",
    "player_turn.mp3",
    "potion_slosh_1.mp3",
    "potion_slosh_2.mp3",
    "potion_slosh_3.mp3",
    "relic_get.mp3",
    "slash_attack.mp3",
    "sts_sfx_shovel_v1.mp3",
    "victory.mp3",
}


# ───── примитивы ─────

def t_axis(seconds: float) -> np.ndarray:
    return np.arange(int(SR * seconds)) / SR


def rng(seed: int) -> np.random.Generator:
    return np.random.Generator(np.random.PCG64(seed))


def env_exp(t: np.ndarray, start: float, attack: float, decay: float) -> np.ndarray:
    """Огибающая: линейная атака, экспоненциальный спад; до start — ноль."""
    x = t - start
    e = np.where(x < 0, 0.0, np.where(x < attack, x / max(attack, 1e-6), np.exp(-(x - attack) / decay)))
    return e


def sweep(t: np.ndarray, f0: float, f1: float, start: float, length: float) -> np.ndarray:
    """Синус с экспоненциальным скольжением частоты f0 → f1 за length секунд (фаза — интеграл частоты)."""
    x = np.clip(t - start, 0, None)
    k = np.log(f1 / f0) / length
    phase = np.where(x < length, f0 * (np.exp(k * x) - 1) / k, f0 * (np.exp(k * length) - 1) / k + f1 * (x - length))
    return np.sin(2 * np.pi * phase)


def one_pole(x: np.ndarray, cutoff) -> np.ndarray:
    """ФНЧ первого порядка; cutoff — число или массив частот по времени."""
    c = np.broadcast_to(np.asarray(cutoff, dtype=float), x.shape)
    a = 1 - np.exp(-2 * np.pi * c / SR)
    y = [0.0] * len(x)
    s = 0.0
    for i, (ai, xi) in enumerate(zip(a.tolist(), x.tolist())):
        s += ai * (xi - s)
        y[i] = s
    return np.array(y)


def band(x: np.ndarray, low, high) -> np.ndarray:
    """Полоса: два каскада ФНЧ(high), затем срез низа двумя каскадами ФНЧ(low) — крутизна ~12 дБ/окт."""
    y = one_pole(one_pole(x, high), high)
    return y - one_pole(one_pole(y, low), low)


def partials(t: np.ndarray, freqs, amps, start: float, decay: float) -> np.ndarray:
    """Металлический звон: неритмичные обертоны со спадом."""
    out = np.zeros_like(t)
    for f, a in zip(freqs, amps):
        out += a * np.sin(2 * np.pi * f * np.clip(t - start, 0, None))
    return out * env_exp(t, start, 0.002, decay)


def normalize(x: np.ndarray, fade: float = 0.01) -> np.ndarray:
    n = int(SR * fade)
    if n:
        x = x.copy()
        x[:n] *= np.linspace(0, 1, n)
        x[-n:] *= np.linspace(1, 0, n)
    peak = float(np.max(np.abs(x))) or 1.0
    return x * (PEAK / peak)


# ───── звуки ─────

def heartbeat() -> np.ndarray:
    """Надрыв: «тук-тук» — два глухих удара сердца с лёгким щелчком."""
    t = t_axis(0.75)
    out = np.zeros_like(t)
    for start, gain in ((0.02, 1.0), (0.24, 0.75)):
        out += gain * sweep(t, 75, 42, start, 0.12) * env_exp(t, start, 0.006, 0.09)
        click = one_pole(rng(int(start * 1000)).standard_normal(len(t)), 900) * env_exp(t, start, 0.001, 0.012)
        out += 0.35 * gain * click
    return normalize(one_pole(out, 1400))


def armor_crack() -> np.ndarray:
    """Слом: треск брони — щелчки, шумовой удар и звон металла."""
    t = t_axis(0.6)
    r = rng(7)
    noise = r.standard_normal(len(t))
    out = 0.9 * (noise - one_pole(noise, 1200)) * env_exp(t, 0.0, 0.001, 0.05)
    for start in np.sort(r.uniform(0.0, 0.16, 9)):
        out += 0.6 * (noise - one_pole(noise, 2500)) * env_exp(t, float(start), 0.0005, 0.006)
    out += 0.5 * partials(t, (812, 1937, 3121, 4410), (1.0, 0.6, 0.4, 0.25), 0.0, 0.22)
    out += 0.6 * sweep(t, 110, 60, 0.0, 0.15) * env_exp(t, 0.0, 0.002, 0.08)
    return normalize(out)


def whoosh() -> np.ndarray:
    """Уворот: свист воздуха — полоса шума, скользящая вверх и вниз."""
    t = t_axis(0.42)
    x = t / t[-1]
    cutoff = 500 + 2600 * np.sin(np.pi * x) ** 2
    out = band(rng(11).standard_normal(len(t)), cutoff * 0.35, cutoff) * np.sin(np.pi * x) ** 1.5
    return normalize(out)


def war_drum() -> np.ndarray:
    """Клич-навык: два удара боевого барабана и звон щита."""
    t = t_axis(0.85)
    noise = rng(13).standard_normal(len(t))
    out = np.zeros_like(t)
    for start, gain in ((0.0, 1.0), (0.3, 0.85)):
        out += gain * sweep(t, 95, 52, start, 0.18) * env_exp(t, start, 0.003, 0.16)
        out += 0.4 * gain * one_pole(noise, 700) * env_exp(t, start, 0.001, 0.04)
    out += 0.22 * partials(t, (523, 1307, 2091, 2846), (1.0, 0.7, 0.45, 0.3), 0.0, 0.35)
    return normalize(out)


def war_horn() -> np.ndarray:
    """Клич-сила: боевой рог — медная квинта ре–ля с подъёмом в начале и лёгким вибрато."""
    t = t_axis(1.05)
    bend = np.where(t < 0.09, 2 ** (-1 / 12 * (1 - t / 0.09)), 1.0)
    vibrato = 1 + 0.006 * np.sin(2 * np.pi * 5.2 * t) * np.clip((t - 0.2) / 0.3, 0, 1)
    out = np.zeros_like(t)
    for base, gain in ((146.83, 1.0), (220.0, 0.55)):
        phase = 2 * np.pi * np.cumsum(base * bend * vibrato) / SR
        for n in range(1, 13):
            out += gain * np.sin(n * phase) / n * np.exp(-n * 0.12)
    shape = np.clip(t / 0.08, 0, 1) * np.where(t > 0.72, np.clip((1.05 - t) / 0.33, 0, 1), 1.0)
    brightness = 900 + 1800 * np.clip(t / 0.15, 0, 1) * np.exp(-np.clip(t - 0.15, 0, None) * 1.2)
    return normalize(one_pole(out * shape, brightness))


def rewind() -> np.ndarray:
    """Откат Камня Памяти: треск стекла, тиканье ускоряется, звук «втягивается» вверх и обрывается мягким ударом."""
    t = t_axis(1.6)
    r = rng(17)
    noise = r.standard_normal(len(t))
    out = 0.7 * partials(t, (2350, 3710, 5120), (1.0, 0.7, 0.5), 0.0, 0.18)
    out += 0.6 * (noise - one_pole(noise, 3000)) * env_exp(t, 0.0, 0.0005, 0.03)
    swell = np.clip((t - 0.1) / 1.3, 0, 1) ** 2.2 * (t < 1.42)
    out += 0.8 * band(noise, 300 + 2500 * swell, 900 + 5000 * swell) * swell
    out += 0.35 * sweep(t, 180, 1100, 0.15, 1.27) * swell * (1 + 0.4 * np.sin(2 * np.pi * 9 * t))
    tick, gap = 0.2, 0.17
    while tick < 1.38:
        out += 0.5 * partials(t, (3200, 4700), (1.0, 0.5), tick, 0.012)
        tick += gap
        gap = max(gap * 0.8, 0.035)
    out += 0.9 * sweep(t, 90, 45, 1.43, 0.12) * env_exp(t, 1.43, 0.003, 0.07)
    return normalize(out)


def portal() -> np.ndarray:
    """Багровый портал: удар суббаса, тёмный гул квинтой с биениями и нарастающий вихрь."""
    t = t_axis(1.35)
    noise = rng(19).standard_normal(len(t))
    out = 1.0 * sweep(t, 70, 32, 0.0, 0.4) * env_exp(t, 0.0, 0.004, 0.35)
    drone = np.zeros_like(t)
    for f, g in ((55.0, 1.0), (55.6, 0.8), (82.4, 0.6), (110.0, 0.3)):
        drone += g * np.sin(2 * np.pi * f * t)
    shape = np.clip(t / 0.25, 0, 1) * np.clip((1.35 - t) / 0.45, 0, 1)
    out += 0.6 * one_pole(np.tanh(1.8 * drone), 600) * shape
    rise = np.clip(t / 1.0, 0, 1) ** 1.6 * np.clip((1.35 - t) / 0.3, 0, 1)
    out += 0.5 * band(noise, 150 + 900 * rise, 500 + 2600 * rise) * rise
    return normalize(out, fade=0.02)


SOUNDS = {
    "heartbeat.wav": heartbeat,
    "armor_crack.wav": armor_crack,
    "whoosh.wav": whoosh,
    "war_drum.wav": war_drum,
    "war_horn.wav": war_horn,
    "rewind.wav": rewind,
    "portal.wav": portal,
}


# ───── WAV ─────

def to_pcm(x: np.ndarray) -> np.ndarray:
    return np.clip(np.round(x * 32767), -32768, 32767).astype("<i2")


def wav_bytes(x: np.ndarray) -> bytes:
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(to_pcm(x).tobytes())
    return buf.getvalue()


def read_pcm(path: str) -> tuple[int, np.ndarray]:
    with wave.open(path, "rb") as w:
        if w.getnchannels() != 1 or w.getsampwidth() != 2:
            raise ValueError(f"{path}: не моно 16 бит")
        return w.getframerate(), np.frombuffer(w.readframes(w.getnframes()), dtype="<i2")


# ───── таблица ─────

def csharp_events() -> list[str]:
    src = open(RULES, encoding="utf-8").read()
    block = src.split("public static readonly IReadOnlyList<string> All =")[1].split("];")[0]
    names = re.findall(r"\b([A-Z]\w+)\b", block)
    consts = dict(re.findall(r'public const string (\w+) = "([a-z.]+)";', src))
    return [consts[n] for n in names]


def read_table(path: str = TABLE) -> list[dict]:
    lines = [l for l in open(path, encoding="utf-8").read().splitlines() if l.strip() and not l.startswith("#")]
    return list(csv.DictReader(lines))


def check_table(rows: list[dict]) -> list[str]:
    errors = []
    events = csharp_events()
    known_fmod = {l.strip() for l in open(KNOWN_FMOD, encoding="utf-8") if l.strip() and not l.startswith("#")}
    seen = set()
    for i, row in enumerate(rows, 2):
        ev, src, sound = row.get("event", ""), row.get("source", ""), row.get("sound", "")
        if ev not in events:
            errors.append(f"sfx.csv:{i}: неизвестное событие «{ev}» (нет в SfxEvents)")
        if src not in SOURCES:
            errors.append(f"sfx.csv:{i}: источник «{src}» — нужен один из {SOURCES}")
        elif src == "fmod" and sound not in known_fmod:
            errors.append(f"sfx.csv:{i}: событие FMOD «{sound}» не из списка известных (vanilla_fmod_events.txt)")
        elif src == "game" and sound not in KNOWN_GAME:
            errors.append(f"sfx.csv:{i}: файл игры «{sound}» неизвестен")
        elif src == "own" and sound not in SOUNDS:
            errors.append(f"sfx.csv:{i}: свой звук «{sound}» этот скрипт не делает")
        try:
            vol = float(row.get("volume", ""))
            if not 0 <= vol <= 2:
                raise ValueError
        except ValueError:
            errors.append(f"sfx.csv:{i}: громкость «{row.get('volume')}» — нужно число 0…2")
        seen.add(ev)
    for ev in events:
        if ev not in seen:
            errors.append(f"sfx.csv: у события «{ev}» нет ни одной строки")
    used_own = {r["sound"] for r in rows if r.get("source") == "own"}
    for name in SOUNDS:
        if name not in used_own:
            errors.append(f"sfx.csv: свой звук «{name}» нигде не используется")
    return errors


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--render", action="store_true")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    errors = check_table(read_table())
    if args.render:
        os.makedirs(OUT, exist_ok=True)
        for name, fn in SOUNDS.items():
            with open(os.path.join(OUT, name), "wb") as f:
                f.write(wav_bytes(fn()))
    total = 0.0
    for name, fn in SOUNDS.items():
        path = os.path.join(OUT, name)
        if not os.path.isfile(path):
            errors.append(f"{name}: нет файла (запустите --render)")
            continue
        rate, pcm = read_pcm(path)
        want = to_pcm(fn())
        total += len(pcm) / rate
        if rate != SR or len(pcm) != len(want):
            errors.append(f"{name}: другой формат/длина — пересоберите (--render)")
        elif int(np.max(np.abs(pcm.astype(int) - want.astype(int)))) > 2:
            errors.append(f"{name}: не совпадает с синтезом — пересоберите (--render)")
    for e in errors:
        print("ОШИБКА:", e)
    rows = read_table()
    print(f"звуки: событий {len(csharp_events())}, строк таблицы {len(rows)} "
          f"(fmod {sum(r['source'] == 'fmod' for r in rows)}, game {sum(r['source'] == 'game' for r in rows)}, "
          f"own {sum(r['source'] == 'own' for r in rows)}); своих звуков {len(SOUNDS)}, {total:.1f} с; ошибок: {len(errors)}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
