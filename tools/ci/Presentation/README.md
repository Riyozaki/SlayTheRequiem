# Production presentation-helper smoke — not a game build

`Presentation.csproj` links the **actual** `RequiemNodes.cs` and `RequiemFocus.cs` used by the mod. No game DLL,
mock game assembly, native model, combat hook, art approval, layout screenshot or FPS measurement is involved.
The public CI job uses checksum-pinned **official Godot 4.5.1 mono**, not the game's required MegaDot.
It therefore does **not** satisfy G0/full-mod/PCK/clean-install/native acceptance.

The headless scene actually executes 12 assertions groups in Godot:

- 100 same-frame named-overlay replacements; queued child retirement without resurrecting a doomed node;
- 100 three-child clear cycles; immediate tree-exit signals; null/double/already-freed retirement;
- focus rings that exclude disabled/hidden/retired controls;
- deferred restoration after an offer disappears, without stealing focus from a new owner or queued modal;
- gamepad accept and keyboard/gamepad wrapping, without invoking disabled offers.

Counts describe **helper** cycles only, not 100 openings of the game's windows. Remaining whole-game/manual,
visual/audio and two-client scenarios stay in the final acceptance matrix, at the end of the release cycle.

## Reproduce (optional, separate from building the mod)

Needs .NET **9 SDK** (and the .NET runtime expected by Godot mono; CI installs SDKs 8 and 9) and the official
Godot mono Linux executable. The CI archive is `Godot_v4.5.1-stable_mono_linux_x86_64.zip`, SHA256
`b61697dd6921da76e3d2358ab592263dfaae398d0ac1229e82eab7e4a7c36cbf`.
No engine is stored in Git or downloaded by the Python runner itself.

From repository root, with your local path (do not publish personal paths):

```bash
python tools/ci/presentation_smoke.py --godot "$GODOT_MONO" --out .arena/presentation/run-001
```

The output must be fresh and ignored. Build/import/run errors and timeouts fail the command, including Godot errors
with exit code 0. `summary.json` binds the exact production-helper/harness hashes, commit, engine executable hash,
case list and explicit `actual_native_runs: 0`. Logs and Godot/.NET caches remain ignored.
The small `STR_PRESENTATION_SMOKE_V1` CI annotation is the same report, not a generated replacement for a failed run.
`test_presentation_smoke.py` checks **synthetic report negatives**, separately from real engine execution.

Source declarations are checked for each `.csproj` by `../check_csharp_declarations.py`, in addition to the grammar
gate. These source checks still do not resolve types/members against the game: use
[`tools/g0/README.md`](../../g0/README.md) for the actual legal full build.
