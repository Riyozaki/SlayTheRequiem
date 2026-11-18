"""Synthetic contracts only: no usable game reference, full build, engine or PCK export is exercised."""
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import mod_build as b


class BundleTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.bundle = self.root / "bundle"
        self.bundle.mkdir()
        for name in b.ARTIFACTS:
            (self.bundle / name).write_bytes(b"synthetic test fixture: " + name.encode())
        b.write_json(self.bundle / f"{b.MOD_ID}.json", {"id": b.MOD_ID, "has_dll": True, "has_pck": True})
        self.passport = {"schema": 1, "game_version": b.GAME_VERSION, "baselib_version": b.BASELIB_VERSION,
                         "save_schema": b.SAVE_SCHEMA, "shop_protocol": b.SHOP_PROTOCOL,
                         "session_protocol": b.SESSION_PROTOCOL, "creation_schema": b.CREATION_SCHEMA, "economy_revision": b.ECONOMY_REVISION,
                         "git_sha": "a" * 40, "source_tree_sha256": "b" * 64, "dirty": False,
                         "artifacts": {name: {"sha256": b.sha(self.bundle / name), "bytes": (self.bundle / name).stat().st_size}
                                       for name in b.ARTIFACTS},
                         "proof": {"full_mod_build": True, "core_tests": True, "core_test_results": {"total": 1, "executed": 1, "passed": 1, "failed": 0}, "pack_export": True,
                                   "pack_resource_loader": {"checked": 1, "failed": [], "isolated_project": True}}}
        self.save()
        self.mods = self.root / "game/mods"

    def save(self):
        b.write_json(self.bundle / b.PASSPORT, self.passport)

    def test_matching_pair_verifies(self):
        self.assertEqual(self.passport, b.verify_bundle(self.bundle))

    def test_tampering_or_stale_pck_fails(self):
        (self.bundle / f"{b.MOD_ID}.pck").write_bytes(b"old pack")
        with self.assertRaises(b.BuildError):
            b.verify_bundle(self.bundle)

    def test_missing_core_tests_fails(self):
        del self.passport["proof"]["core_tests"]
        self.save()
        with self.assertRaises(b.BuildError):
            b.verify_bundle(self.bundle)

    def test_source_backed_pack_check_is_not_isolated(self):
        self.passport["proof"]["pack_resource_loader"]["isolated_project"] = False
        self.save()
        with self.assertRaises(b.BuildError):
            b.verify_bundle(self.bundle)

    def test_malformed_passport_is_a_clean_failure(self):
        for content in [[], None, "unexpected"]:
            b.write_json(self.bundle / b.PASSPORT, content)
            with self.assertRaises(b.BuildError):
                b.verify_bundle(self.bundle)

    def test_malformed_artifact_metadata_is_a_clean_failure(self):
        for value in [None, {}, {"bytes": True, "sha256": "x"}]:
            self.passport["artifacts"][b.ARTIFACTS[0]] = value
            self.save()
            with self.assertRaises(b.BuildError):
                b.verify_bundle(self.bundle)

    def test_loose_legacy_pair_is_never_left_as_a_duplicate_mod(self):
        self.mods.mkdir(parents=True)
        (self.mods / b.ARTIFACTS[0]).write_bytes(b"old dll")
        with self.assertRaises(b.BuildError):
            b.install(self.bundle, self.mods, game_closed=True)
        self.assertEqual(b"old dll", (self.mods / b.ARTIFACTS[0]).read_bytes())

    def test_second_mod_id_in_another_directory_blocks_install(self):
        duplicate = self.mods / "old-version"
        duplicate.mkdir(parents=True)
        b.write_json(duplicate / "manifest.json", {"id": b.MOD_ID})
        with self.assertRaises(b.BuildError):
            b.install(self.bundle, self.mods, game_closed=True)
        self.assertFalse((self.mods / b.MOD_ID).exists())

    def test_missing_export_proof_fails(self):
        self.passport["proof"]["pack_export"] = False
        self.save()
        with self.assertRaises(b.BuildError):
            b.verify_bundle(self.bundle)

    def test_empty_or_failed_pack_check_fails(self):
        for checked, failed in [(0, []), (1, ["missing portrait"])]:
            self.passport["proof"]["pack_resource_loader"] = {"checked": checked, "failed": failed}
            self.save()
            with self.assertRaises(b.BuildError):
                b.verify_bundle(self.bundle)

    def test_manifest_path_injection_not_used(self):
        self.passport["artifacts"]["../../private"] = {"sha256": "x", "bytes": 1}
        self.save()
        with self.assertRaises(b.BuildError):
            b.verify_bundle(self.bundle)

    def test_wrong_dependency_fails(self):
        self.passport["baselib_version"] = "0.0.0"
        self.save()
        with self.assertRaises(b.BuildError):
            b.verify_bundle(self.bundle)

    def test_wrong_or_missing_release_contracts_fail(self):
        for key in ("save_schema", "shop_protocol", "session_protocol", "creation_schema", "economy_revision"):
            for value in (None, -1, "3", True):
                with self.subTest(key=key, value=value):
                    original = self.passport[key]
                    self.passport[key] = value
                    self.save()
                    with self.assertRaises(b.BuildError):
                        b.verify_bundle(self.bundle)
                    self.passport[key] = original

    def test_missing_or_malformed_source_identity_cannot_verify(self):
        for key, values in (("git_sha", (None, "unrecorded", "a" * 39)),
                            ("source_tree_sha256", (None, "b" * 63, True)), ("dirty", (None, 0, "false"))):
            for value in values:
                with self.subTest(key=key, value=value):
                    original = self.passport[key]
                    self.passport[key] = value
                    self.save()
                    with self.assertRaises(b.BuildError):
                        b.verify_bundle(self.bundle)
                    self.passport[key] = original

    def test_artifact_list_must_be_an_object(self):
        for value in (None, [], list(b.ARTIFACTS)):
            self.passport["artifacts"] = value
            self.save()
            with self.assertRaises(b.BuildError):
                b.verify_bundle(self.bundle)

    def test_manifest_boolean_flags_are_not_strings(self):
        for value in ("true", "false", 1, None):
            b.write_json(self.bundle / f"{b.MOD_ID}.json", {"id": b.MOD_ID, "has_dll": value, "has_pck": True})
            name = f"{b.MOD_ID}.json"
            self.passport["artifacts"][name] = {"sha256": b.sha(self.bundle / name), "bytes": (self.bundle / name).stat().st_size}
            self.save()
            with self.assertRaises(b.BuildError):
                b.verify_bundle(self.bundle)

    def test_game_closed_ack_required(self):
        with self.assertRaises(b.BuildError):
            b.install(self.bundle, self.mods, game_closed=False)
        self.assertFalse(self.mods.exists())

    def test_clean_install_does_not_keep_stale_files(self):
        target = b.install(self.bundle, self.mods, game_closed=True)
        (target / "obsolete.dll").write_text("stale", encoding="utf-8")
        target2 = b.install(self.bundle, self.mods, game_closed=True)
        self.assertEqual(target, target2)
        self.assertFalse((target2 / "obsolete.dll").exists())
        self.assertEqual(self.passport, b.verify_bundle(target2))
        backups = list((self.mods.parent / ".requiem-backups").iterdir())
        self.assertEqual(1, len(backups))
        self.assertTrue((backups[0] / "obsolete.dll").exists())
        self.assertFalse(list(self.mods.glob(".requiem-install-*")))
        receipt = json.loads((self.bundle / "install-receipt.json").read_text())
        self.assertFalse(receipt["runtime_verified"])

    def test_unrecognized_existing_directory_is_preserved(self):
        target = self.mods / b.MOD_ID
        target.mkdir(parents=True)
        (target / "valuable.txt").write_text("keep", encoding="utf-8")
        with self.assertRaises(b.BuildError):
            b.install(self.bundle, self.mods, game_closed=True)
        self.assertEqual("keep", (target / "valuable.txt").read_text())

    def test_non_object_existing_manifest_is_preserved(self):
        target = self.mods / b.MOD_ID
        target.mkdir(parents=True)
        (target / "valuable.txt").write_text("keep", encoding="utf-8")
        for value in ([], None, "not a manifest"):
            b.write_json(target / f"{b.MOD_ID}.json", value)
            with self.assertRaises(b.BuildError):
                b.install(self.bundle, self.mods, game_closed=True)
            self.assertEqual("keep", (target / "valuable.txt").read_text())
            self.assertFalse(list(self.mods.glob(".requiem-install-*")))

    def test_failed_swap_restores_old_installation(self):
        target = b.install(self.bundle, self.mods, game_closed=True)
        (target / "keep.txt").write_text("old", encoding="utf-8")
        rename = Path.rename

        def fail_staging(path, dest):
            if path.name.startswith(".requiem-install-"):
                raise OSError("injected disk failure")
            return rename(path, dest)

        with patch.object(Path, "rename", fail_staging):
            with self.assertRaises(OSError):
                b.install(self.bundle, self.mods, game_closed=True)
        self.assertEqual("old", (target / "keep.txt").read_text())
        self.assertEqual(self.passport, b.verify_bundle(target))

    def test_symlinked_target_is_not_followed(self):
        self.mods.mkdir(parents=True)
        target = self.mods / b.MOD_ID
        try:
            target.symlink_to(self.bundle, target_is_directory=True)
        except (OSError, NotImplementedError):
            self.skipTest("Symlinks unavailable")
        with self.assertRaises(b.BuildError):
            b.install(self.bundle, self.mods, game_closed=True)
        self.assertEqual(self.passport, b.verify_bundle(self.bundle))


class SourceContractTests(unittest.TestCase):
    """Source declarations only, not compiled assembly metadata or actual dependency binaries."""

    def test_save_schema_and_protocol_agree_in_installer_project_and_runtime_source(self):
        import re
        import xml.etree.ElementTree as ET
        project = ET.parse(b.ROOT / "mod/SlayTheRequiem.csproj").getroot()
        metadata = {node.attrib["Include"]: node.attrib["Value"] for node in project.findall(".//AssemblyMetadata")}
        runtime = (b.ROOT / "mod/SlayTheRequiemCode/Debugging/RunDiagnostics.cs").read_text(encoding="utf-8")
        operations = (b.ROOT / "mod/SlayTheRequiemCode/Shops/ShopOperations.cs").read_text(encoding="utf-8")
        self.assertEqual(str(b.SAVE_SCHEMA), re.search(r"const int SaveSchema = (\d+);", runtime).group(1))
        self.assertEqual(str(b.SHOP_PROTOCOL), re.search(r"const int Protocol = (\d+);", operations).group(1))
        self.assertEqual(str(b.SAVE_SCHEMA), metadata["SaveSchema"])
        self.assertEqual(str(b.SHOP_PROTOCOL), metadata["ShopProtocol"])

    def test_external_core_compile_links_are_present_in_the_isolated_build(self):
        import xml.etree.ElementTree as ET
        project = b.ROOT / "mod/SlayTheRequiem.Tests/SlayTheRequiem.Tests.csproj"
        external = {(project.parent / node.attrib["Include"]).resolve().relative_to(b.ROOT).as_posix()
                    for node in ET.parse(project).findall(".//Compile")
                    if "**" not in node.attrib["Include"] and "%(" not in node.attrib["Include"]
                    and not (project.parent / node.attrib["Include"]).resolve().is_relative_to(b.ROOT / "mod")}
        self.assertEqual(set(b.TEST_SUPPORT), external)
        with tempfile.TemporaryDirectory() as directory:
            bundle = Path(directory)
            staged = b.stage_project(bundle)
            self.assertTrue((staged / "SlayTheRequiem.Tests/SlayTheRequiem.Tests.csproj").is_file())
            for relative in b.TEST_SUPPORT:
                self.assertEqual((b.ROOT / relative).read_bytes(), (bundle / relative).read_bytes())
            self.assertFalse((bundle / "tools/ci").exists())
            self.assertFalse((staged / "bin").exists())

    def test_release_contract_metadata_agrees_without_a_protocol_bump(self):
        import re
        import xml.etree.ElementTree as ET
        project = ET.parse(b.ROOT / "mod/SlayTheRequiem.csproj").getroot()
        metadata = {node.attrib["Include"]: node.attrib["Value"] for node in project.findall(".//AssemblyMetadata")}
        startup = (b.ROOT / "mod/SlayTheRequiemCode/Core/StartupState.cs").read_text()
        economy = (b.ROOT / "mod/SlayTheRequiemCode/Core/EconomyRules.cs").read_text()
        for name, value, text, declaration in (("SessionProtocol", b.SESSION_PROTOCOL, startup, "SessionProtocol"),
                                             ("CreationSchema", b.CREATION_SCHEMA, startup, "Protocol"),
                                             ("EconomyRevision", b.ECONOMY_REVISION, economy, "CurrentRevision")):
            self.assertEqual(str(value), metadata[name])
            self.assertEqual(str(value), re.search(rf"const int {declaration} = (\d+);", text).group(1))

    def test_pinned_dependencies_agree_with_preflight_and_manifest(self):
        import xml.etree.ElementTree as ET
        project = ET.parse(b.ROOT / "mod/SlayTheRequiem.csproj").getroot()
        packages = {node.attrib["Include"]: node.attrib["Version"] for node in project.findall(".//PackageReference")}
        self.assertEqual("[" + b.BASELIB_VERSION + "]", packages["Alchyr.Sts2.BaseLib"])
        self.assertEqual("[0.2.1]", packages["Alchyr.Sts2.ModAnalyzers"])
        self.assertEqual("Godot.NET.Sdk/" + b.GODOT_VERSION, project.attrib["Sdk"])
        manifest = b.read_object(b.ROOT / "mod/SlayTheRequiem.json")
        self.assertEqual(b.GAME_VERSION, manifest["min_game_version"])
        self.assertEqual(b.BASELIB_VERSION, next(d["min_version"] for d in manifest["dependencies"] if d["id"] == "BaseLib"))


class PreflightTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.game = Path(self.tmp.name)
        self.data = self.game / "legal-reference-fixtures"
        self.data.mkdir()
        for name in ("sts2.dll", "0Harmony.dll"):
            (self.data / name).write_bytes(b"synthetic fixture, not a usable game reference")
        self.mods = self.game / "mods"
        (self.mods / "BaseLib").mkdir(parents=True)
        (self.mods / "BaseLib/BaseLib.dll").write_bytes(b"synthetic fixture")
        b.write_json(self.mods / "BaseLib/BaseLib.json", {"version": b.BASELIB_VERSION})
        b.write_json(self.game / "release_info.json", {"version": b.GAME_VERSION})
        self.godot = self.game / "megadot-fixture"
        self.godot.touch()
        from argparse import Namespace
        self.args = Namespace(game_dir=self.game, game_data=self.data, mods_dir=self.mods, dotnet="dotnet", godot=self.godot)
        self.outputs = {"dotnet": "9.0.308", str(self.godot): "4.5.1.stable.mono.custom_build.fixture"}

    def probe(self):
        with patch.object(b.shutil, "which", return_value="dotnet"), patch.object(b, "output", side_effect=lambda cmd: self.outputs[cmd[0]]):
            return b.preflight(self.args)

    def test_exact_versions_are_required(self):
        self.assertEqual([], self.probe()["errors"])
        b.write_json(self.game / "release_info.json", {"version": "0.108.0"})
        self.assertTrue(self.probe()["errors"])

    def test_installed_baselib_must_match(self):
        b.write_json(self.mods / "BaseLib/BaseLib.json", {"version": "3.4.9"})
        self.assertTrue(self.probe()["errors"])

    def test_sdk_cannot_be_replaced_with_a_runtime_or_next_major(self):
        self.outputs["dotnet"] = "10.0.100"
        self.assertTrue(self.probe()["errors"])

    def test_engine_needs_exact_mono_version(self):
        for version in ["4.5.1.stable.official", "4.6.stable.mono.official", "4.5.1"]:
            self.outputs[str(self.godot)] = version
            self.assertTrue(self.probe()["errors"])

    def test_missing_game_binary_blocks_build_preflight(self):
        (self.data / "sts2.dll").unlink()
        self.assertTrue(self.probe()["errors"])

    def test_non_object_installed_manifests_are_explicit_failures(self):
        for path in (self.game / "release_info.json", self.mods / "BaseLib/BaseLib.json"):
            original = path.read_bytes()
            for value in ([], None, "not a manifest"):
                b.write_json(path, value)
                with self.assertRaises(b.BuildError):
                    self.probe()
            path.write_bytes(original)


class StageResultTests(unittest.TestCase):
    def test_empty_skipped_or_failed_core_report_cannot_be_a_successful_build(self):
        for counters in (None, {}, {"total": 0, "executed": 0, "passed": 0},
                         {"total": 2, "executed": 1, "passed": 1, "notExecuted": 1},
                         {"total": 2, "executed": 2, "passed": 1, "failed": 1},
                         {"total": True, "executed": True, "passed": True}):
            with self.subTest(counters=counters), self.assertRaises(b.BuildError):
                b.require_core_results(counters)

    def test_real_trx_counter_shape_is_parsed(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "core.trx"
            path.write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultSummary>'
                            '<Counters total="17" executed="17" passed="17" failed="0" notExecuted="0" />'
                            '</ResultSummary></TestRun>', encoding="utf-8")
            self.assertEqual(17, b.core_results(path)["passed"])
            path.write_text("not xml", encoding="utf-8")
            with self.assertRaises(b.BuildError):
                b.core_results(path)

    def test_zero_exit_code_cannot_hide_a_godot_script_or_resource_error(self):
        import sys
        with tempfile.TemporaryDirectory() as directory:
            log = Path(directory) / "engine.log"
            for message in ("ERROR: missing imported texture", "SCRIPT ERROR: bad parser"):
                with self.subTest(message=message), self.assertRaises(b.BuildError):
                    b.run([sys.executable, "-c", "print(" + repr(message) + ")"], log, engine=True)
            self.assertIn("import complete", b.run([sys.executable, "-c", "print('import complete')"], log, engine=True))


if __name__ == "__main__":
    unittest.main()
