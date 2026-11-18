# Б2.3 source checkpoint evidence — 18.11.2026

`checks.json` binds 376 source/table/test/tool inputs by SHA256 and all 27 local check logs.
`base_commit` is the parent of the tested dirty snapshot, **not** a claim that the parent contains these inputs.
All 27 checks exited 0, including the formerly failing Python sim. Grammar/Harmony checks are source-only.
These logs are NOT full-mod compilation, Godot layout/controller/PCK-load, native fixtures, two-client or playtest proof.
Core execution is recorded separately from the subsequent GitHub CI; no local SDK was used here.
No credentials, game binaries, private saves or generated build artifacts are included.
