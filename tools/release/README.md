# Release tools — identified candidate, not automatic publication

These stdlib Python ≥3.11 tools implement Б3.4/Б3.5. **There is no newly accepted DLL/PCK, release ZIP or native PASS here.**
The committed [plan](../../docs/release/final-matrix.plan.json) has 257 `NOT_RUN` requirements. Manual, visual-in-client
and two-client runs are performed only at the final end, per the owner. Source/Core/tool tests do not replace them.

## Contracts

- `catalogue.py`: deterministic requirements from the full regression checklist, 21 encounter classes, forms/Paths,
  nine build recipes, 12 full solo routes, separate short routes, co-op, display, window cycles and final reviews.
  `requirements_sha256` binds documentary changes separately from the build input hash. No skip/waiver closes a case.
- `package_release.py candidate`: checks the real G0 bundle, clean checkout/source hash, and its committed source tree.
  Packages only matched mod DLL/PCK/manifest and a nested-allowlisted public build passport, installation docs and frozen
  sources including editable art. Historical review evidence/contact sheets are deliberately not shipped; no game DLLs,
  saves, private logs/binlogs, `.git` or local caches. This is a deterministic ZIP for identical inputs/Python/zlib, not a
  promise of cross-machine bit-identical compilation or a signed/legal provenance certificate.
- `package_release.py verify`: no extraction/installation. Checks bounded size/count, relative paths, symlinks, case
  collisions, duplicate/non-finite JSON, contents/hashes, G0 proof and the frozen source digest. Lifted doc links are
  deterministically retargeted; `source/docs/GUIDE.md` remains in its original relative tree.
- `acceptance.py init/record/check/seal`: binds observations to the exact ZIP/source/DLL/PCK/public-passport/requirements.
  PASS needs the case setup, hashed private evidence and matching actual runtime passports. Seed/run nonce, solo/party,
  distinct Host/Client identities and the exact loaded BaseLib version/hash are checked. PERF-01/04 also require a frozen
  pre-change native baseline/result with consistent metrics and recorded configuration, plus a human comparison review.
  A late source/archive/requirement change requires a new matrix; old PASS is not copied. Failure attempts remain in history.
- `seal` writes a small public completeness/identity summary without copying private evidence. **It does not publish,
  merge, push, play the game, establish a winrate or certify rights/truth.** Operator observations can be fabricated;
  schema/hash checks are not a cryptographic attestation that gameplay actually happened.

## Final-only usage

First use [G0](../g0/README.md) on an owned legal game/MegaDot/.NET 9 machine. Missing references/engine means BLOCKED,
never a stub build. All output paths below must be fresh; `.arena/` is private ignored scratch, not public evidence.

```bash
python tools/g0/mod_build.py build --game-dir "$STS2_GAME_DIR" --godot "$MEGADOT" --output .arena/builds/final-001
python tools/release/package_release.py candidate --bundle .arena/builds/final-001 --out .arena/releases/final-001.zip
python tools/release/package_release.py verify .arena/releases/final-001.zip
python tools/release/acceptance.py init --archive .arena/releases/final-001.zip --out .arena/acceptance/final-001/matrix.json
# After the real observation; result/evidence paths are relative to this PRIVATE matrix directory:
python tools/release/acceptance.py record --archive .arena/releases/final-001.zip --matrix .arena/acceptance/final-001/matrix.json --case CARD-01 --result .arena/acceptance/final-001/result.json
python tools/release/acceptance.py check --archive .arena/releases/final-001.zip --matrix .arena/acceptance/final-001/matrix.json
# Only when every case actually has current PASS evidence; publication still needs separate owner authorization:
python tools/release/acceptance.py seal --archive .arena/releases/final-001.zip --matrix .arena/acceptance/final-001/matrix.json --out .arena/releases/final-001.acceptance.json
```

Full context/evidence format and native performance protocol: [FINAL_ACCEPTANCE](../../docs/release/FINAL_ACCEPTANCE.md).
Record `str_passport` as JSON separately from log prefixes. Unredacted player IDs/logs/saves stay private; make a separate
sanitized copy for an issue, never edit the hashed original. `NOT_RUN/PASS/FAIL/BLOCKED` are the only states.

## Safe tests now

```bash
python -m unittest discover -s tools/release -v
python -m unittest discover -s tools/ci -v
```

Package/acceptance tests use visibly labelled synthetic non-assembly/non-pack bytes and fabricated observations in
`TemporaryDirectory`; all are deleted. A successful contract assertion is **not** a usable package, an accepted mod,
a native run or an assertion that a human reviewed art/rights. Content tests also check current plan/CSV/doc links.
