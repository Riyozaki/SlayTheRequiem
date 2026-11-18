# B2.5 — actual Core arithmetic on limited economic scenarios

**NOT native gameplay.** 60 synthetic ledgers / 30 same-seed revision pairs / 300 act rows, not 60 played runs. Every fight is assumed won.

Only two policy groups changed: five-stage stock timing and the second act HP overlay on authored bosses.
Gold/price/Burden/stat/resource/difficulty numbers stay unchanged. Revision 0 is the saved legacy contract.

## Assumptions

- Not a native map, shop, reward or combat simulation. Every fight is assumed won; no damage/deaths/winrate.
- Ten fixed seeds per hero, same workloads in revision 0 vs 1, no identity/Neow/event/treasure/bonus-relic effects.
- Room counts from ActRules; synthetic allocation: 1–2 elites, 1–2 shops, 2 camps, 2 events, 1 treasure; remaining normal fights, then boss. Act 2/4 variants cover all four branch combinations, not native door probabilities.
- Native 0.107.1 default gold ranges 10–20 normal, 35–45 elite, 100 boss; no Poverty ascension or encounter overrides. Boss gold cannot fund same-act shopping.
- Hypothetical top-specialist basket, representative dagger/sword/staff weight 1/2/2, instant sale of old weapon at trust 0. Not availability of a particular item. Purchased treatises are consumed, not extra Burden.
- Half normal wins plus each elite wants a sample card; 1/5 synthetic Rare weight; one sample 1-weight relic per elite. Skip over excess 5; no chest/mule free-weight assumption.
- Spend up to 2 earned stat points on Burden next act if overloaded. No automatic stat refunds/mastery grants; other points remain unspent in this ledger.
- Camp plan is only opportunity cost (Forge; Short Rest+Brew if materials), not actual material drops, potion crafting or HP sustain.
- Primary boss reference: authored in new act variants 2/4, borrowed native boss in 1/3/5. Separate authored-boss factor is a hypothetical optional-superboss reference, not an extra battle or a win.
- Baseline requiem level 1 / HP config 110. Short/difficulty smoke regressions are separate, not calibrated winrates.

## Same-seed workload: before vs after

| Hero | Revision | End gold min–max | Total spent min–max | End level | Min level boss 3 | Desired cards skipped (10 seeds) | Gear baskets not acquired | End excess max |
|---|---|---|---|---|---|---|---|---|
| Ironclad | 0 | 139–288 | 1620–1795 | 11–11 | 8 | 0 | 0 | 5 |
| Ironclad | 1 | 149–301 | 1580–1905 | 11–11 | 8 | 1 | 0 | 5 |
| Silent | 0 | 139–288 | 1620–1795 | 11–11 | 8 | 0 | 0 | 5 |
| Silent | 1 | 149–301 | 1580–1905 | 11–11 | 8 | 0 | 0 | 5 |
| Regent | 0 | 144–251 | 1685–1940 | 11–11 | 8 | 1 | 0 | 5 |
| Regent | 1 | 138–245 | 1580–1860 | 11–11 | 8 | 0 | 0 | 5 |

Large gold leftovers here do NOT prove that gameplay is easy: shops are late, HP/healing/real choices/deaths are absent. Skipped sample cards do NOT measure actual refusals.

## Actual policy schedule

| Act | Armorer rev 0 → 1 | Wanderer rev 0 → 1 | Staff rev 0 → 1 | Brew rev 0 → 1 | Primary boss reference factor rev 0 → 1 | Optional authored-boss factor rev 0 → 1 |
|---|---|---|---|---|---|---|
| 1 | 1–2 → 1–2 | 1–2 → 1–2 | 1–1 → 1–1 | 1 → 1 | 1.1 → 1.1 | 1.1 → 1.1 |
| 2 | 2–3 → 2–3 | 1–2 → 1–2 | 2–3 → 2–2 | 3 → 1 | 1.485 → 1.1 | 1.485 → 1.1 |
| 3 | 2–3 → 3–4 | 1–2 → 2–3 | 2–3 → 2–3 | 3 → 3 | 1.155 → 1.155 | 1.155 → 1.1 |
| 4 | 3–4 → 4–5 | 1–2 → 3–4 | 4–5 → 3–4 | 5 → 3 | 1.32 → 1.1 | 1.32 → 1.1 |
| 5 | 3–4 → 4–5 | 1–2 → 4–5 | 4–5 → 4–5 | 5 → 5 | 1.265 → 1.265 | 1.265 → 1.1 |

## Native comparison still required at the end of the release cycle

Enable str_trace on and preserve str_passport. RequiemBalance JSON snapshots have producer=native-game and explicit event boundaries; this file has producer=core-scenario.
Use tools/balance/compare_traces.py for bounded observations matched by seed/hero/economy revision. It must never relabel this scenario as native evidence.
No native traces have been supplied/executed by this tool. Manual/visual/two-client tests remain final-cycle work.

Scenario JSON SHA256: `76afa0960095cacc7254c568273cfcc0fe18209e32cc545b2d6abd6a74c32331`.

