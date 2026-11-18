#!/usr/bin/env python3
"""Inspect opt-in native RequiemBalance logs beside Core economic workloads, WITHOUT conflating them.

No inference of winrate, completed runs, deaths or same native RNG from a shared seed label.
A native last snapshot may precede rewards; never subtract it from a synthetic post-boss balance.
The program fails closed on malformed/mixed producers and conflicting duplicate observations.
"""
from __future__ import annotations

import argparse
import collections
import json
from pathlib import Path
import sys

MARKER = "[RequiemBalance] "
PHASES = {"trace:enabled", "combat:start-hero-hook", "combat:victory-pre-rewards", "room:hero-hook", "camp:begin", "camp:result", "shop:enter-before-stock", "shop:result", "draw:after"}
NUMBERS = ("sequence", "act_index", "act_count", "route_slot", "floor", "economy_revision", "requiem_level", "ascension", "gold", "hp", "max_hp", "renown", "level", "weight", "capacity", "excess", "pending_cards", "stored_count")


def validate_native(row):
    if not isinstance(row, dict) or type(row.get("schema")) is not int or row["schema"] != 1 or row.get("producer") != "native-game":
        raise ValueError("Expected schema=1 / producer=native-game; a Core scenario is never native evidence")
    for key in ("seed", "hero", "segment", "player", "phase"):
        if not isinstance(row.get(key), str) or not row[key]:
            raise ValueError("Missing native context " + key)
    for key in ("run_id", "operation_timeline"):
        if key in row and not isinstance(row[key], str):
            raise ValueError("Invalid native context " + key)
    if row["hero"] not in ("Ironclad", "Silent", "Regent") or row["phase"] not in PHASES:
        raise ValueError("Unknown hero/phase; update the explicit observation contract")
    for key in NUMBERS:
        if type(row.get(key)) is not int or row[key] < 0:
            raise ValueError("Invalid native integer " + key)
    if row["act_count"] < 1 or row["act_index"] >= row["act_count"] or row["level"] not in range(1, 16):
        raise ValueError("Native route/level out of range")
    if row["economy_revision"] not in (0, 1) or row["requiem_level"] > 10:
        raise ValueError("Future/invalid balance policy")
    if row["hp"] > row["max_hp"] or row["excess"] != max(0, row["weight"] - row["capacity"]):
        raise ValueError("Native HP/Burden fields contradict one another")
    if type(row.get("operation_blocked")) is not bool:
        raise ValueError("Missing native operation-block status")


def load_native(paths):
    rows = []
    seen = {}
    last = {}
    for path in paths:
        with path.open(encoding="utf-8") as stream:
            for number, line in enumerate(stream, 1):
                if MARKER not in line:
                    continue
                try:
                    row = json.loads(line.partition(MARKER)[2])
                    validate_native(row)
                    key = (row["segment"], row["player"], row["sequence"])
                    if key in seen:
                        if seen[key] != row:
                            raise ValueError("Conflicting duplicate trace row")
                        continue  # repeated copy of the same file/line is not another observation
                    stream_key = key[:2]
                    if row["sequence"] <= last.get(stream_key, -1):
                        raise ValueError("Out-of-order trace stream; do not silently stitch Continue/rollback segments")
                    last[stream_key] = row["sequence"]
                    seen[key] = row
                    rows.append(row)
                except (ValueError, TypeError) as exc:
                    raise ValueError(f"{path.name}:{number}: {exc}") from exc
    if not rows:
        raise ValueError("No native RequiemBalance snapshots supplied. Core scenarios/checker fixtures do not fill this gap")
    return rows


def load_scenario(path):
    data = json.loads(path.read_text(encoding="utf-8"))
    if (not isinstance(data, dict) or data.get("producer") != "core-scenario" or type(data.get("schema")) is not int
            or data["schema"] != 1 or not isinstance(data.get("runs"), list) or not data["runs"]):
        raise ValueError("Expected a separate actual Core-scenario artifact, not native observations or an old Burden proxy")
    return data


def baseline_compatible(row):
    """A seed match does not authorize comparing a short/different-HP/difficulty run to this baseline.

    This extracts only the required coordinates, NOT a Python replacement for RunSettingsData.Decode.
    Missing/ambiguous settings disable the side-by-side rather than silently supplying current menu defaults.
    """
    if row["act_count"] != 5 or row["requiem_level"] != 1 or row["ascension"] != 0 or not isinstance(row.get("settings"), str):
        return False
    settings = {}
    for part in filter(None, row["settings"].split(";")):
        key, sep, value = part.partition("=")
        if not sep or not key or key in settings:
            return False
        settings[key] = value
    return (all(settings.get(k) == v for k, v in {"v": "1", "sys": "1", "five": "1", "short": "0", "hp": "110"}.items())
            and settings.get("economy", "0") == str(row["economy_revision"]))


def inline(text):
    return str(text).replace("`", "\\`").replace("|", "\\|").replace("\r", " ").replace("\n", " ")


def render(rows, scenario=None):
    streams = collections.defaultdict(list)
    for row in rows:
        key = (row.get("run_id", ""), row["seed"], row["hero"], row["player"], row["segment"], row.get("operation_timeline", ""), row["economy_revision"])
        streams[key].append(row)
    out = ["# Б2.5 — наблюдения движка и отдельная ограниченная модель", "",
           "Ниже **переданные** native-game снимки. Метка producer и SHA в логе не являются подписанной аттестацией происхождения.",
           "Это не доказательство полного прохождения/принятых боссов/двух клиентов. Победы и потери не угадываются по пропускам.",
           "Core-сценарий имеет producer=core-scenario, предполагает победы и не генерирует настоящий маршрут/награды.",
           "Одинаковый seed — ключ сопоставления наблюдений, **не одинаковые native RNG/workload**. Параметры и состав надо проверять отдельно.", ""]
    for key, items in sorted(streams.items()):
        _, seed, hero, _, segment, timeline, revision = key
        out += [f"## {hero} · `{inline(seed)}` · economy {revision} · segment `{inline(segment)}`", "",
                f"Timeline: `{inline(timeline) if timeline else 'unrecorded'}`. Снимков: {len(items)}. Границы: `{items[0]['phase']}` → `{items[-1]['phase']}`; этажи {items[0]['floor']}…{items[-1]['floor']}.",
                "Число снимков не является числом ходов/боёв/отказов. Отдельные segment/timeline не склеиваются автоматически с Continue или rollback.", ""]
        phases = collections.Counter(r["phase"] for r in items)
        out += ["Границы: " + ", ".join(f"`{k}`={v}" for k, v in sorted(phases.items())) + ".", ""]
        matching = [] if scenario is None else [s for s in scenario["runs"] if s["hero"] == hero and s["seed"] == seed and s["revision"] == revision and not s["short_run"]]
        core = matching[0] if len(matching) == 1 else None
        if core is None:
            out += ["Нет единственного Core-сценария с теми же seed/героем/revision. Экстраполяция не выполнялась.", ""]
        elif not all(baseline_compatible(row) for row in items):
            out += ["Seed найден, но режим/число актов/сложность/HP отличаются либо настройки отсутствуют/неоднозначны. Прямое числовое сравнение выключено.", ""]
            core = None
        else:
            out += ["Seed найден в отдельном Core-сценарии. Его закупка/распределение комнат/победы заданы искусственно;",
                    "личность, реальные награды, лечебные расходы и момент снятия наград могут отличаться. Числовой delta не вычисляется.", ""]
        out += ["| Акт | Последняя наблюдаемая граница | Этаж | Золото native | Груз/вместимость | Уровень | Отложенные карты | Core synthetic end gold (включая босса) |",
                "|---|---|---|---|---|---|---|---|"]
        for act in sorted({r["act_index"] for r in items}):
            row = [r for r in items if r["act_index"] == act][-1]
            candidates = [] if core is None else [a for a in core["acts"] if a["act_index"] == act]
            synthetic = str(candidates[0]["budget_end"]) if len(candidates) == 1 else "—"
            out.append(f"| {act + 1} | `{row['phase']}` | {row['floor']} | {row['gold']} | {row['weight']}/{row['capacity']} | {row['level']} | {row['pending_cards']} | {synthetic} |")
        out += ["", "`combat:victory-pre-rewards` — **до сбора наград**. Его нельзя вычесть из synthetic end gold и объявить ошибкой золота.",
                f"Наблюдались заблокированные операции: {'да' if any(r['operation_blocked'] for r in items) else 'нет в переданных снимках'}.",
                "Сверять `artifacts`/`settings` с паспортом сборки и сохранять исходные логи. Этот отчёт не аттестует DLL/PCK самостоятельно.", ""]
    return "\n".join(out)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("native", type=Path, nargs="+")
    parser.add_argument("--scenario", type=Path)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    try:
        text = render(load_native(args.native), load_scenario(args.scenario) if args.scenario else None)
    except (OSError, ValueError, KeyError, TypeError) as exc:
        print("FAIL", exc, file=sys.stderr)
        return 1
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8")
    else:
        print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
