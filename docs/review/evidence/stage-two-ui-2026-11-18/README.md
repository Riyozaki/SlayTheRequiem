# Б2.3 source checkpoint evidence — 18.11.2026

`checks.json` binds 376 source/table/test/tool inputs by SHA256 and all 27 local check logs.
`base_commit` is the parent of the tested dirty snapshot, **not** a claim that the parent contains these inputs.
All 27 checks exited 0, including the formerly failing Python sim. Grammar/Harmony checks are source-only.
These logs are NOT full-mod compilation, Godot layout/controller/PCK-load, native fixtures, two-client or playtest proof.
Core execution is recorded separately from the subsequent GitHub CI; no local SDK was used here.
No credentials, game binaries, private saves or generated build artifacts are included.

Subsequent CI for `04569f66e2bbf7420a8ae5c554ac231cbc95d2b2`: [44246021705](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44246021705),
all four jobs SUCCESS; **710 executed / 710 passed / 0 failed / 0 notExecuted** from check 134605061484's
TRX-derived annotation (not a downloaded TRX). It does not validate later B2.4 changes.
