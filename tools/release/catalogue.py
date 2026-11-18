"""Deterministic FINAL acceptance requirements. A plan/fixture is never a completed native run.

Shared logs may support several cases, but routes/balance observations keep their real run IDs.
The build/source/requirements binding is enforced by acceptance.py. No waived/skip state closes a case.
"""
from __future__ import annotations

import hashlib
import itertools
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
CODE = ROOT / "mod/SlayTheRequiemCode"
REGRESSION = "docs/review/regression-checklist-2026-11-18.md"
PASSPORTS = "docs/review/stage-two-combat-passports-2026-11-18.md"
HEROES = ("Ironclad", "Silent", "Regent")
DOORS_2 = ("FrozenBarrows", "ClockworkDepths")
DOORS_4 = ("BloodCourt", "StarTower")
SCREENS = ("hero_creation", "hero_sheet", "chest", "camp", "shop", "act_doors", "card_rewards")


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def digest(value):
    return hashlib.sha256(canonical(value)).hexdigest()


def enum(name):
    source = (CODE / "Core/TransformationRules.cs").read_text(encoding="utf-8")
    body = re.search(rf"public enum {name}\s*\{{([^}}]+)}}", source).group(1)
    return re.findall(r"^\s*(\w+)\s*,", body, re.M)


def bosses():
    names = set()
    for path in (CODE / "Monsters").glob("*.cs"):
        names.update(re.findall(r"public sealed class (\w+)\s*:\s*(?:ActBossMonster|SuperbossMonster)\b", path.read_text(encoding="utf-8")))
    names.add("EchoKnight")
    return sorted(names)


def requirements():
    cases = []

    def add(ident, category, expected, *, context=None, clients=1, evidence=None, source=None):
        cases.append(dict(id=ident, category=category, expected=expected, context=context or {},
                          minimum_clients=clients, evidence=evidence or (["native_log"] if clients else ["review"]),
                          source=source or "docs/plan/next-playable-release-2026-11-18.md"))

    # Preserve ALL previously promised data/UI/failure cases instead of replacing them with a short smoke list.
    for line in (ROOT / REGRESSION).read_text(encoding="utf-8").splitlines():
        fields = [part.strip() for part in line.split("|")]
        if len(fields) < 5 or not re.fullmatch(r"[A-Z]+-\d{2}", fields[1]):
            continue
        ident, scenario, expected = fields[1:4]
        clients = 0 if ident in ("BUILD-01", "BUILD-02") else 2 if ident.startswith("COOP-") or ident == "ENEMY-04" else 1
        evidence = ["review"] if not clients else ["native_log", "metrics"] if ident.startswith("PERF-") else ["native_log", "screenshot"] if ident.startswith(("UI-", "ART-", "REWARD-")) else ["native_log"]
        if ident in ("PERF-01", "PERF-04"):
            evidence += ["baseline_metrics", "review"]
        add(ident, "regression", scenario + " → " + expected, clients=clients, evidence=evidence, source=REGRESSION)

    for hero, door2, door4 in itertools.product(HEROES, DOORS_2, DOORS_4):
        add(f"FULL-{hero}-{door2}-{door4}", "full-route",
            "Completed five-act SOLO victory, all transitions/final rewards; no debug skips or console rescue. Smoke, not balance proof.",
            context=dict(hero=hero, acts=["Foothills", door2, "Hive", door4, "Glory"], completed=True, debug_used=False))
    for hero in HEROES:
        add(f"SHORT-{hero}", "short-route", "Completed short victory; Path after boss 1 without level-8 gate, final state clean. Not one of the 12 full routes.",
            context=dict(hero=hero, acts=["Foothills", "Hive", "Glory"], completed=True, debug_used=False))
        for variant in range(1, 4):
            add(f"BETA-A1-{hero}-{variant}", "first-act", "Real instrumented first act, distinct origin/background/start bonus; explain every choice and denial. See B2 exit criteria.",
                context=dict(hero=hero, act_completed=True, debug_used=False))
        for run in range(1, 11):
            add(f"BALANCE-{hero}-{run:02}", "balance-observation", "Real run observation: seed, deck/economy/act data and death/dead-hand/decline reason OR victory. PASS means recorded, not won or calibrated winrate.",
                context=dict(hero=hero, debug_used=False))
    add("COOP-FULL-FIVE-ACTS", "coop-route", "Two actual processes, host and peer: full five-act victory, Continue and Memory Stone, final state and hashes agree; no debug skips.",
        clients=2, context=dict(completed=True, act_count=5, debug_used=False))

    for boss in bosses():
        add("ENCOUNTER-" + boss, "encounter", "All cycles/thresholds from combat passport, exact equality and multiple crossed thresholds, preview=hit, counter access/full hand, cap 5, prevented/real death, entourage and victory cleanup. Fixtures allowed; not route credit.",
            context=dict(boss=boss), source=PASSPORTS)
    for form in enum("FormKind"):
        if form != "None":
            add("FORM-" + form, "form", "Acquire → play → save/Continue → leave/restore visuals. Check form restrictions, grants and no repeated gifts; Righteous/Clockwork combinations and conflicts explained.", context=dict(form=form))
    for path in enum("HeroPath"):
        if path != "None":
            add("PATH-" + path, "path", "Legitimate offer/Essence/signature/Sign, pending grant/retry/reload and final effects. Reborn retains skills/treatises/loot and refunds only paid characteristics/mastery. Fixtures allowed, not a played build.", context=dict(path=path))
    for variant in ("beast_hunter", "beast_stalker", "beast_alpha", "vampire_thirst_and_lord", "herald_three_stances", "lich_necromancy", "clockwork_orbs"):
        add("VARIANT-" + variant, "form-variant", "Exercise the specific form branch/stance/resource and transition in both directions, including death/Revive and Continue; no stale sprite/bonus.", context=dict(variant=variant))
    for direction in json.loads((ROOT / "tools/balance/build_directions.json").read_text(encoding="utf-8"))["directions"]:
        add("BUILD-DIRECTION-" + direction["id"], "build-direction", "Play actual enabler/payoff/defense/draw/resource loop and resistance answer without mandatory rare. Record obtained access, rough turns and costs; recipe/source fixture alone is insufficient.",
            context=dict(hero=direction["hero"], direction=direction["id"]))

    for height, scale, language, control in itertools.product((720, 1080, 1440), (1.0, 1.25), ("eng", "rus"), ("keyboard", "gamepad")):
        add(f"DISPLAY-{height}-{int(scale * 100)}-{language}-{control}", "display",
            "Every new screen, long labels, focused confirm/back, scrolling, hover and resize; combat chips/intents/resources readable. Also inspect 32/64px icons on actual map and camp, not contact sheets.",
            context=dict(resolution={720: "1280x720", 1080: "1920x1080", 1440: "2560x1440"}[height], ui_scale=scale, language=language, input=control), evidence=["native_log", "screenshot"])
    for screen in SCREENS:
        add("CYCLES-" + screen, "window-cycles", "100 real open/close cycles after warm-up in disposable fixtures. Log stable checkpoints, node/subscription/managed/native memory counts after cleanup; no monotonic growth. Synthetic rebind test is not this case.",
            context=dict(screen=screen, cycles=100), evidence=["native_log", "metrics"])
    for setting in ("reduced_motion", "vfx_off", "sound_off", "all_off", "compact_toggle"):
        add("PRESENTATION-" + setting, "presentation", "Change setting while idle/attack/death/phase/shelf-return/window transition is active; re-enter tree/Continue. Essential text/intents and mechanics unchanged, loops stopped/restored, no multihit/AoE audio pile-up. Listen to actual mix.",
            context=dict(setting=setting), evidence=["native_log", "screenshot"])
    for ident, expected in {
        "AUTOMATION": "Fresh full build + all Core/content/tool tests on this exact input hash, no skipped/failed suites; CI is not the full mod build.",
        "WARNINGS": "Review complete compiler/analyzer warnings with private log hash; no silently suppressed new nullability/API problems.",
        "RIGHTS": "Owner reviewed retained AI provenance and existing/third-party materials, license/distribution/attribution decisions explicit. No invented license or copied game resources.",
        "BLOCKERS": "No open P0, no loss/dupe/wrong payment or progression-critical P1; record known noncritical limits and all remaining repros. Unresolved user nope/reward repro cannot be waved away.",
        "BETA": "B1/B2 exit criteria accepted: start/recovery, numeric combat, three viable directions per hero and actual economy observations; smoke is not balance proof.",
        "PACKAGE": "Clean-install/extract this immutable archive, only BaseLib+mod; all heroes new/Continue; no loose stale DLL/PCK. Record archive SHA and patch report. No source-backed pack-load substitute for runtime.",
    }.items():
        add("FINAL-" + ident, "final-review", expected, clients=1 if ident == "PACKAGE" else 0)
    cases.sort(key=lambda case: case["id"])
    if len(cases) != len({case["id"] for case in cases}):
        raise ValueError("Duplicate acceptance case ID")
    return cases


def plan():
    cases = requirements()
    return dict(schema=1, producer="final-acceptance-plan-NOT-executed", requirements_sha256=digest(cases),
                note="All NOT_RUN. Manual/visual/two-client acceptance only at the final end. No accepted build or release is declared.",
                cases=[{**case, "status": "NOT_RUN"} for case in cases])
