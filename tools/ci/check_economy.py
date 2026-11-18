#!/usr/bin/env python3
"""B2.5 source wiring contracts. NOT a C# semantic compiler or native economy/battle test."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
METHODS = "EquipmentTiers|BrewTier|StaffTiers|ScrollTier|SoulStonePower|OrderTier|OrderItemPrice|ReagentKitPrice"


def check(root=ROOT):
    code = root / "mod/SlayTheRequiemCode"
    errors = []
    for name in ("ShopStockPatches.cs", "ShopServices.cs", "ShopActionPolicy.cs"):
        source = (code / "Shops" / name).read_text()
        if re.search(r"ShopRules\.(?:" + METHODS + r")\(", source):
            errors.append(name + ": bypasses saved route policy")
        if "ShopEconomy.Context(player.RunState)" not in source:
            errors.append(name + ": missing shared route coordinates")
    setup = (code / "Rules/EnemySetup.cs").read_text()
    if "Monsters.ActBossMonster or Monsters.SuperbossMonster" not in setup or "EconomyRules.EnemyHpMultiplier" not in setup:
        errors.append("Authored boss budget is not wired through actual EnemySetup")
    if "RunSettings.EconomyRevision" not in setup:
        errors.append("EnemySetup does not honor saved economy revision")
    trace = (code / "Debugging/BalanceTrace.cs").read_text()
    if 'producer = "native-game"' not in trace or "!CardRewardTrace.Enabled" not in trace or "RunDiagnostics.BinaryIdentity()" not in trace:
        errors.append("Native trace lacks opt-in, producer or artifact identity")
    if re.search(r"\bNext(?:Int|Double|Item)\s*\(|InventoryIdentity\.Ensure\(|\bCanPlay\(|InventoryGate\.For\(|EnemySetup\.RoleOf\(", trace):
        errors.append("Balance observation must not consume RNG, repair identities, recover gates/register roles or re-query card playability")
    for file, token in {
        "Progression/HeroRecord.cs": '"combat:victory-pre-rewards"',
        "Debugging/CardRewardDiagnostics.cs": 'BalanceTrace.Snapshot("draw:after"',
        "Alchemy/CampOptions.cs": 'BalanceTrace.Snapshot("camp:result"',
        "Shops/ShopOperations.cs": 'BalanceTrace.Snapshot("shop:result"',
    }.items():
        if token not in (code / file).read_text():
            errors.append(file + ": missing actual boundary observation")
    console = (root / "tools/balance/Trace/Scenario.cs").read_text()
    if '"core-scenario"' not in console or "Every fight is ASSUMED won" not in console:
        errors.append("Core scenario must remain explicitly distinct from native execution")
    return errors


if __name__ == "__main__":
    errors = check()
    for error in errors:
        print("FAIL", error)
    print(f"Economy source wiring: {len(errors)} errors (stock/quote/executor, saved revision, authored HP, read-only trace; NOT semantic/native acceptance)")
    sys.exit(bool(errors))
