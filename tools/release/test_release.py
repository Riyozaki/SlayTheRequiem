"""SYNTHETIC TOOL CONTRACTS ONLY. Dummy DLL/PCK/passport/observations are NOT usable game files or native runs.
No release acceptance is produced by this suite. All fixtures live in TemporaryDirectory and are removed.
"""
import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

import acceptance as a
import catalogue as c
import package_release as p

b = p.build


class CatalogueTests(unittest.TestCase):
    def test_all_promised_source_bosses_forms_paths_and_regressions_remain_required(self):
        cases = c.requirements()
        self.assertEqual(257, len(cases))
        self.assertEqual(257, len({case["id"] for case in cases}))
        self.assertEqual(21, len(c.bosses()))
        self.assertEqual(12, sum(case["category"] == "full-route" for case in cases))
        self.assertEqual(3, sum(case["category"] == "short-route" for case in cases))
        self.assertEqual(24, sum(case["category"] == "display" for case in cases))
        self.assertEqual(103, sum(case["category"] == "regression" for case in cases))
        self.assertEqual(7, sum(case["category"] == "form" for case in cases))
        self.assertEqual(13, sum(case["category"] == "path" for case in cases))
        self.assertEqual(30, sum(case["category"] == "balance-observation" for case in cases))
        self.assertEqual({"NOT_RUN"}, {case["status"] for case in c.plan()["cases"]})
        passport = (c.ROOT / c.PASSPORTS).read_text(encoding="utf-8")
        for boss in c.bosses():
            self.assertIn("| " + boss, passport)

    def test_committed_plan_is_current_and_does_not_contain_game_acceptance(self):
        plan = json.loads((c.ROOT / "docs/release/final-matrix.plan.json").read_bytes())
        self.assertEqual(c.plan(), plan)

    def test_public_docs_have_resolvable_lifted_links_and_coverage_csv_is_current(self):
        originals = set(p.PUBLIC_DOCS.values())
        snapshot = {path.relative_to(c.ROOT).as_posix(): path.read_bytes() if path.relative_to(c.ROOT).as_posix() in originals else b""
                    for path in (c.ROOT / "docs").rglob("*") if path.is_file()
                    and not path.relative_to(c.ROOT).as_posix().startswith(("docs/review/evidence/", "docs/review/assets/"))}
        for target, original in p.PUBLIC_DOCS.items():
            lifted = p.public_document(target, original, snapshot)
            self.assertTrue(lifted, original)
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "coverage.csv"
            result = subprocess.run([sys.executable, str(c.ROOT / "tools/assets/asset_manifest.py"), "--csv", str(output), "--check"], capture_output=True)
            self.assertEqual(0, result.returncode, result.stdout.decode(errors="replace") + result.stderr.decode(errors="replace"))
            self.assertEqual((c.ROOT / "docs/release/asset-coverage.csv").read_bytes(), output.read_bytes())

    def test_build_identity_includes_editable_art_not_only_runtime_exports(self):
        inputs = {name.decode() for name in b.source_inputs()}
        self.assertIn("art/illustrations/masters/king_under_barrow.png", inputs)
        self.assertIn("tools/release/acceptance.py", inputs)
        self.assertNotIn("docs/release/final-matrix.plan.json", inputs)  # documentary case changes have their OWN binding


class SyntheticPackageFixture(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.bundle = self.root / "synthetic-bundle"
        self.bundle.mkdir()
        self.sources = {"mod/synthetic.cs": b"// synthetic fixture; not an actual build\n", "art/synthetic.svg": b"synthetic source"}
        self.sources.update({original: ("SYNTHETIC DOCUMENT, NO RELEASE: " + original).encode() for original in p.PUBLIC_DOCS.values()})
        self.sources["docs/release/final-matrix.plan.json"] = p.encoded(c.plan())  # requirement schema only; every case NOT_RUN
        for name in b.ARTIFACTS:
            (self.bundle / name).write_bytes(b"SYNTHETIC NON-ASSEMBLY/NON-PACK " + name.encode())
        b.write_json(self.bundle / f"{b.MOD_ID}.json", dict(id=b.MOD_ID, has_dll=True, has_pck=True, version="synthetic-test-only"))
        self.passport = dict(schema=1, git_sha="a" * 40, source_tree_sha256=p.source_digest(self.sources), dirty=False,
            game_version=b.GAME_VERSION, baselib_version=b.BASELIB_VERSION, save_schema=b.SAVE_SCHEMA, shop_protocol=b.SHOP_PROTOCOL,
            session_protocol=b.SESSION_PROTOCOL, creation_schema=b.CREATION_SCHEMA, economy_revision=b.ECONOMY_REVISION,
            godot="4.5.1.mono.PRIVATE-VENDOR-LABEL", dotnet_sdk="9.0.308", installed_mods=[{"id": "PRIVATE-MOD"}],
            logs=["/private/home/build.binlog"],
            dependencies={"sts2.dll": "2" * 64, "0Harmony.dll": "3" * 64, "installed_baselib_sha256": "4" * 64, "private": "/private/home/dependency"},
            artifacts={name: dict(sha256=b.sha(self.bundle / name), bytes=(self.bundle / name).stat().st_size, private_path="/private/home/artifact") for name in b.ARTIFACTS},
            proof=dict(full_mod_build=True, core_tests=True, core_test_results=dict(total=1, executed=1, passed=1), pack_export=True,
                       pack_resource_loader=dict(checked=1, failed=[], isolated_project=True)))
        b.write_json(self.bundle / b.PASSPORT, self.passport)
        (self.bundle / "private.binlog").write_bytes(b"PRIVATE-LOG")
        self.identity = dict(source_tree_sha256=self.passport["source_tree_sha256"], dirty=False)
        self.archive = self.root / "synthetic-NOT-A-RELEASE.zip"
        with patch.object(b, "source_identity", return_value=self.identity), patch.object(p, "frozen_source", return_value=self.sources):
            p.create(self.bundle, self.archive)

    def contents(self):
        with zipfile.ZipFile(self.archive) as archive:
            return {name: archive.read(name) for name in archive.namelist()}

    def corrupted(self, update):
        files = self.contents()
        update(files)
        target = self.root / "corrupt.zip"
        if target.exists():
            target.unlink()  # only this test's freshly created scratch archive
        p.write_archive(files, target)
        return target


class PackageContracts(SyntheticPackageFixture):
    def test_matching_archive_has_only_public_data_and_is_not_accepted(self):
        verified = p.verify(self.archive)
        self.assertEqual(self.passport["source_tree_sha256"], verified["passport"]["source_tree_sha256"])
        self.assertFalse(verified["passport"]["proof"]["game_runtime"])
        self.assertFalse(verified["passport"]["proof"]["co_op"])
        files = self.contents()
        self.assertFalse(any("binlog" in name for name in files))
        runtime = files[p.MOD_DIR + b.PASSPORT].decode()
        for secret in ("PRIVATE-MOD", "/private/home", "PRIVATE-VENDOR-LABEL", "PRIVATE-LOG"):
            self.assertNotIn(secret, runtime)
        self.assertNotIn("accepted", p.read_json(files[p.MANIFEST]))

    def test_packaging_is_byte_stable_and_never_overwrites_an_archive(self):
        target = self.root / "second.zip"
        with patch.object(b, "source_identity", return_value=self.identity), patch.object(p, "frozen_source", return_value=self.sources):
            p.create(self.bundle, target)
            self.assertEqual(b.sha(self.archive), b.sha(target))
            with self.assertRaises(b.BuildError):
                p.create(self.bundle, target)

    def test_stale_pair_wrong_source_and_dirty_build_cannot_be_packaged(self):
        for identity in (dict(self.identity, dirty=True), dict(self.identity, source_tree_sha256="f" * 64)):
            with patch.object(b, "source_identity", return_value=identity), self.assertRaises(b.BuildError):
                p.create(self.bundle, self.root / "not-written.zip")
            self.assertFalse((self.root / "not-written.zip").exists())
        (self.bundle / f"{b.MOD_ID}.pck").write_bytes(b"stale fixture")
        with self.assertRaises(b.BuildError):
            p.create(self.bundle, self.root / "not-written.zip")

    def test_tampered_archive_and_source_snapshot_fail_before_installation(self):
        for name in (p.MOD_DIR + f"{b.MOD_ID}.pck", "source/mod/synthetic.cs"):
            with self.subTest(name=name), self.assertRaises(b.BuildError):
                p.verify(self.corrupted(lambda files: files.__setitem__(name, b"altered")))
        def changed_source_with_updated_manifest(files):
            name = "source/mod/synthetic.cs"
            files[name] = b"forged source"
            manifest = p.read_json(files[p.MANIFEST])
            manifest["entries"][name] = dict(bytes=len(files[name]), sha256=p.digest(files[name]))
            files[p.MANIFEST] = p.encoded(manifest)
        with self.assertRaisesRegex(b.BuildError, "compiled input hash"):
            p.verify(self.corrupted(changed_source_with_updated_manifest))

    def test_lifted_public_docs_retarget_internal_links_without_moving_the_guide(self):
        source = {"docs/release/INSTALL.md": b"[guide](../GUIDE.md) [policy](COMPATIBILITY.md#saves)",
                  "docs/release/COMPATIBILITY.md": b"synthetic", "docs/GUIDE.md": b"[design](design/)",
                  "docs/design/spec.md": b"synthetic"}
        self.assertEqual(b"[guide](source/docs/GUIDE.md) [policy](COMPATIBILITY.md#saves)",
                         p.public_document("INSTALL.md", "docs/release/INSTALL.md", source))
        self.assertEqual(source["docs/GUIDE.md"], p.public_document("source/docs/GUIDE.md", "docs/GUIDE.md", source))
        source["docs/release/INSTALL.md"] = b"[missing](../missing.md)"
        with self.assertRaises(b.BuildError): p.public_document("INSTALL.md", "docs/release/INSTALL.md", source)

    def test_nonfinite_duplicate_json_and_case_collisions_are_not_candidates(self):
        for data in (b'{"schema":1,"schema":1}', b'{"value":NaN}'):
            with self.assertRaises(b.BuildError): p.read_json(data)
        with self.assertRaises(b.BuildError):
            p.verify(self.corrupted(lambda files: files.__setitem__("source/mod/SYNTHETIC.cs", b"collision")))
        for name in ("name\n.md", "docs/trailing .md ", "docs/a./file.md"):
            with self.assertRaises(b.BuildError): p.safe_name(name)

    def test_private_binary_symlink_duplicate_and_path_injection_are_not_archived(self):
        for name in ("../escape", "/absolute", "C:/private", "source/../../escape", "source/game.dll", "build.binlog", "mods/extra.json"):
            with self.subTest(name=name), self.assertRaises(b.BuildError):
                p.verify(self.corrupted(lambda files: files.__setitem__(name, b"synthetic")))
        target = self.root / "duplicate.zip"
        with zipfile.ZipFile(target, "w") as archive:
            archive.writestr(p.MANIFEST, b"{}")
            archive.writestr(p.MANIFEST, b"{}")
        with self.assertRaises(b.BuildError):
            p.verify(target)
        target = self.root / "symlink.zip"
        with zipfile.ZipFile(target, "w") as archive:
            entry = zipfile.ZipInfo(p.MANIFEST)
            entry.external_attr = 0o120777 << 16
            archive.writestr(entry, b"../private")
        with self.assertRaises(b.BuildError):
            p.verify(target)


class AcceptanceContracts(SyntheticPackageFixture):
    def setUp(self):
        self.spec = next(row for row in c.requirements() if row["category"] == "full-route")
        self.requirements_patch = patch.object(c, "requirements", return_value=[self.spec])
        self.requirements_patch.start()
        self.addCleanup(self.requirements_patch.stop)
        super().setUp()
        with patch.object(b, "source_identity", return_value=self.identity):
            self.matrix = a.create_matrix(self.archive)

    def refreeze_synthetic_requirements(self):
        self.sources["docs/release/final-matrix.plan.json"] = p.encoded(c.plan())
        self.archive = self.root / (self.archive.stem + "-new-requirements.zip")
        with patch.object(b, "source_identity", return_value=self.identity), patch.object(p, "frozen_source", return_value=self.sources):
            p.create(self.bundle, self.archive)
            self.matrix = a.create_matrix(self.archive)

    def passport_fixture(self, *, role="Singleplayer", net_id=0):
        candidate = self.matrix["candidate"]
        return dict(schema=1, game=b.GAME_VERSION, seed="SYNTHETIC-NOT-A-RUN", run_id="1" * 32, degraded=False, failed_patches=[],
            network=dict(role=role, local_net_id=net_id),
            players=[dict(id=net_id, hero="IRONCLAD")] if role == "Singleplayer" else [dict(id=1, hero="IRONCLAD"), dict(id=2, hero="SILENT")],
            artifacts=dict(git_sha=candidate["git_sha"], source_tree_sha256=candidate["source_tree_sha256"], dirty="false",
                dll_sha256=candidate["artifacts"][b.MOD_ID + ".dll"]["sha256"], pck_sha256=candidate["artifacts"][b.MOD_ID + ".pck"]["sha256"],
                build_passport_sha256=candidate["passport_sha256"],
                loaded_mods=[dict(id=b.MOD_ID), dict(id="BaseLib", version=b.BASELIB_VERSION, dll_sha256=candidate["dependencies"]["installed_baselib_sha256"])],
                build=dict(contracts_match_dll=True, source_matches_dll=True, files_match_passport=True)))

    def observation(self):
        (self.root / "native-fixture.txt").write_text("SYNTHETIC LOG, NOT ACTUAL GAMEPLAY", encoding="utf-8")
        b.write_json(self.root / "passport-fixture.json", self.passport_fixture())
        return dict(status="PASS", reviewer="synthetic-test", notes="Fabricated contract fixture only", observed_at_utc="2026-11-18T00:00:00Z", executed_in_game=True,
                    context={**self.spec["context"], "run_id": "1" * 32, "seed": "SYNTHETIC-NOT-A-RUN"},
                    evidence=[dict(kind="native_log", path="native-fixture.txt"), dict(kind="passport", path="passport-fixture.json")])

    def check(self, matrix):
        return a.validate_matrix(matrix, self.archive, self.root, check_source=False)

    def test_prepared_matrix_never_silently_passes(self):
        report = self.check(self.matrix)
        self.assertFalse(report["complete"])
        self.assertEqual({"NOT_RUN": 1}, report["counts"])

    def test_observation_hashes_and_passport_binding_are_rechecked_not_just_the_word_pass(self):
        updated = a.record(self.matrix, self.spec["id"], self.observation(), self.root)
        self.assertTrue(self.check(updated)["complete"])  # structural assertion about a SYNTHETIC record, not game proof
        (self.root / "native-fixture.txt").write_text("changed", encoding="utf-8")
        with self.assertRaisesRegex(b.BuildError, "Changed/unhashed"):
            self.check(updated)

    def test_ci_or_plan_cannot_replace_native_execution(self):
        result = self.observation()
        result["executed_in_game"] = False
        with self.assertRaises(b.BuildError):
            a.record(self.matrix, self.spec["id"], result, self.root)
        result = self.observation()
        b.write_json(self.root / "passport-fixture.json", dict(producer="core-scenario", actual_native_runs=0))
        with self.assertRaises(b.BuildError):
            a.record(self.matrix, self.spec["id"], result, self.root)

    def test_native_wrong_binary_seed_hero_and_dirty_or_degraded_fail(self):
        for change in ("pck", "seed", "hero", "dirty", "degraded", "passport"):
            with self.subTest(change=change):
                result = self.observation()
                data = self.passport_fixture()
                if change == "pck": data["artifacts"]["pck_sha256"] = "0" * 64
                if change == "seed": data["seed"] = "other seed"
                if change == "hero": data["players"] = [dict(hero="DEFECT")]
                if change == "dirty": data["artifacts"]["dirty"] = "true"
                if change == "degraded": data["degraded"] = True
                if change == "passport": data["artifacts"]["build_passport_sha256"] = "0" * 64
                b.write_json(self.root / "passport-fixture.json", data)
                with self.assertRaises(b.BuildError):
                    a.record(self.matrix, self.spec["id"], result, self.root)

    def test_claimed_run_nonce_baselib_binary_and_party_are_checked_not_just_seed(self):
        for change in ("run_id", "baselib-version", "baselib-hash", "bool-schema", "missing-local", "duplicate-player", "bad-hash-type"):
            with self.subTest(change=change):
                result = self.observation()
                data = self.passport_fixture()
                if change == "run_id": result["context"]["run_id"] = "5" * 32
                if change == "baselib-version": data["artifacts"]["loaded_mods"][1]["version"] = "old"
                if change == "baselib-hash": data["artifacts"]["loaded_mods"][1]["dll_sha256"] = "5" * 64
                if change == "bool-schema": data["schema"] = True
                if change == "missing-local": data["players"][0]["id"] = 999
                if change == "duplicate-player": data["players"] *= 2
                if change == "bad-hash-type": data["artifacts"]["pck_sha256"] = 27
                b.write_json(self.root / "passport-fixture.json", data)
                with self.assertRaises(b.BuildError): a.record(self.matrix, self.spec["id"], result, self.root)

    def test_failure_history_retained_and_missing_or_waived_case_blocks(self):
        result = self.observation()
        result.update(status="FAIL", notes="Synthetic failure")
        failed = a.record(self.matrix, self.spec["id"], result, self.root)
        self.assertFalse(self.check(failed)["complete"])
        passed = a.record(failed, self.spec["id"], self.observation(), self.root)
        self.assertEqual("FAIL", passed["cases"][0]["history"][0]["status"])
        passed["cases"][0]["status"] = "WAIVED"
        with self.assertRaises(b.BuildError): self.check(passed)
        with self.assertRaises(b.BuildError): self.check(dict(self.matrix, cases=[]))

    def test_late_source_fix_requirement_change_or_archive_change_reopens_acceptance(self):
        with patch.object(b, "source_identity", return_value=dict(source_tree_sha256="0" * 64)):
            with self.assertRaisesRegex(b.BuildError, "Late source fix"):
                a.validate_matrix(self.matrix, self.archive, self.root)
        with self.assertRaises(b.BuildError): self.check(dict(self.matrix, requirements_sha256="0" * 64))
        changed = copy.deepcopy(self.matrix)
        changed["candidate"]["archive_sha256"] = "0" * 64
        with self.assertRaises(b.BuildError): self.check(changed)

    def test_archived_requirements_cannot_silently_disagree_with_current_matrix_requirements(self):
        self.spec["expected"] += " changed documentary requirement"
        self.matrix["requirements_sha256"] = c.digest([self.spec])
        with self.assertRaisesRegex(b.BuildError, "stale requirements/docs"):
            self.check(self.matrix)

    def test_two_copies_of_host_are_not_two_clients(self):
        self.spec["minimum_clients"] = 2
        self.spec["category"] = "coop-route"
        self.refreeze_synthetic_requirements()  # a new SYNTHETIC archive/matrix, never an old PASS or a real waiver
        result = self.observation()
        b.write_json(self.root / "passport-fixture.json", self.passport_fixture(role="Host", net_id=1))
        b.write_json(self.root / "peer-fixture.json", self.passport_fixture(role="Host", net_id=1))
        result["evidence"].append(dict(kind="passport", path="peer-fixture.json"))
        with self.assertRaisesRegex(b.BuildError, "distinct native"):
            a.record(self.matrix, self.spec["id"], result, self.root)
        b.write_json(self.root / "peer-fixture.json", self.passport_fixture(role="Client", net_id=2))
        updated = a.record(self.matrix, self.spec["id"], result, self.root)
        self.assertTrue(self.check(updated)["complete"])

    def paired_metrics_observation(self):
        self.spec.update(id="PERF-04", category="regression", context={},
                         evidence=["native_log", "metrics", "baseline_metrics", "review"])
        self.refreeze_synthetic_requirements()
        result = self.observation()
        result["context"]["performance_setup"] = "SYNTHETIC setup; NOT executed native performance"
        passport = self.passport_fixture()
        passport.update(language="eng", act=0, floor=1, effective_settings="v=1;hp=110;run=" + "1" * 32,
                        display=dict(window="(1280, 720)", viewport="(1280, 720)", canvas_scale="(1, 1)", content_scale_factor=1,
                                     vsync="Enabled", screen_refresh_hz=60, max_fps=60),
                        presentation=dict(reduced_motion=False, combat_vfx=True, sounds=True, compact_enemy_statuses=True,
                                          paper_doll=True, metrics=False, balance_trace=False))
        passport["players"][0].update(requiem_level=1, hero_record_level=1, origin="SYNTHETIC", background="SYNTHETIC")
        passport["artifacts"]["loaded_mods"][0].update(version="synthetic", dll_sha256=passport["artifacts"]["dll_sha256"])
        memory = dict(AllocatedBytes=2000, ManagedBytes=1000, Gen0=0, Gen1=0, Gen2=0, Nodes=100, OrphanNodes=0, EngineStaticBytes=5000)
        candidate = dict(schema=2, producer="native-game-opt-in", enabled=False, window=1024, unit="milliseconds", elapsed_seconds=1,
                         total_sampled_frames=60, frame=dict(Samples=60, MedianMs=16, P95Ms=17, MaxMs=18),
                         reward_refresh=dict(Samples=0, MedianMs=0, P95Ms=0, MaxMs=0),
                         trait_rebuild=dict(Samples=1, MedianMs=.1, P95Ms=.1, MaxMs=.1), trait_rebuilds=1, trait_redraw_requests=1,
                         memory_before=memory, memory_after=dict(memory, AllocatedBytes=2100), allocated_bytes_delta=100,
                         gc_delta=dict(gen0=0, gen1=0, gen2=0),
                         machine=dict(os="SYNTHETIC", runtime="SYNTHETIC", architecture="X64", logical_processors=4, cpu="SYNTHETIC", gpu="SYNTHETIC"), passport=passport)
        baseline = copy.deepcopy(candidate)
        baseline["passport"]["artifacts"].update(git_sha="8" * 40, source_tree_sha256="9" * 64, dll_sha256="7" * 64,
                                                pck_sha256="6" * 64, build_passport_sha256="5" * 64)
        baseline["passport"]["artifacts"]["loaded_mods"][0]["dll_sha256"] = "7" * 64
        b.write_json(self.root / "candidate-metrics.json", candidate)
        b.write_json(self.root / "baseline-metrics.json", baseline)
        (self.root / "comparison-review.txt").write_text("SYNTHETIC comparison review, NOT gameplay acceptance", encoding="utf-8")
        result["evidence"] += [dict(kind="metrics", path="candidate-metrics.json"), dict(kind="baseline_metrics", path="baseline-metrics.json"),
                               dict(kind="review", path="comparison-review.txt")]
        return result, baseline, candidate

    def test_native_performance_requires_both_frozen_records_and_explicit_setup_review(self):
        result, _, _ = self.paired_metrics_observation()
        self.assertTrue(self.check(a.record(self.matrix, self.spec["id"], result, self.root))["complete"])
        missing = copy.deepcopy(result)
        missing["evidence"] = [row for row in missing["evidence"] if row["kind"] != "baseline_metrics"]
        with self.assertRaises(b.BuildError): a.record(self.matrix, self.spec["id"], missing, self.root)
        del result["context"]["performance_setup"]
        with self.assertRaises(b.BuildError): a.record(self.matrix, self.spec["id"], result, self.root)

    def test_duplicate_candidate_as_baseline_or_changed_metrics_configuration_blocks(self):
        result, baseline, candidate = self.paired_metrics_observation()
        b.write_json(self.root / "baseline-metrics.json", candidate)
        with self.assertRaisesRegex(b.BuildError, "pre-change baseline"):
            a.record(self.matrix, self.spec["id"], result, self.root)
        baseline["passport"]["display"]["vsync"] = "Disabled"
        b.write_json(self.root / "baseline-metrics.json", baseline)
        with self.assertRaisesRegex(b.BuildError, "same-configuration"):
            a.record(self.matrix, self.spec["id"], result, self.root)

    def test_metrics_arithmetic_and_native_candidate_nonce_are_not_unchecked_attachments(self):
        result, baseline, candidate = self.paired_metrics_observation()
        candidate["allocated_bytes_delta"] += 1
        b.write_json(self.root / "candidate-metrics.json", candidate)
        with self.assertRaisesRegex(b.BuildError, "delta"):
            a.record(self.matrix, self.spec["id"], result, self.root)
        candidate["allocated_bytes_delta"] -= 1
        candidate["passport"]["run_id"] = "2" * 32
        candidate["passport"]["effective_settings"] = candidate["passport"]["effective_settings"].replace("1" * 32, "2" * 32)
        b.write_json(self.root / "candidate-metrics.json", candidate)
        with self.assertRaisesRegex(b.BuildError, "another run/seed"):
            a.record(self.matrix, self.spec["id"], result, self.root)

    def test_evidence_cannot_escape_or_be_a_symlink(self):
        with self.assertRaises(b.BuildError): a.evidence_file(self.root, "../private.txt")
        (self.root / "actual.txt").write_text("synthetic", encoding="utf-8")
        (self.root / "link.txt").symlink_to(self.root / "actual.txt")
        with self.assertRaises(b.BuildError): a.evidence_file(self.root, "link.txt")


if __name__ == "__main__":
    unittest.main()
