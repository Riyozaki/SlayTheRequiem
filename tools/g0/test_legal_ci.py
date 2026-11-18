"""Contracts of the legal-runner wrapper; NOT a full game build or an engine test."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

_spec = importlib.util.spec_from_file_location("legal_game_ci", Path(__file__).resolve().parents[1] / "ci/legal_game_build.py")
ci = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ci)


class PublicReportTests(unittest.TestCase):
    def test_only_allowlisted_fields_leave_the_runner(self):
        passport = {"git_sha": "a" * 40, "dirty": False, "source_tree_sha256": "b" * 64,
                    "game_version": "0.107.1", "baselib_version": "3.4.8", "dotnet_sdk": "9.0.308",
                    "artifacts": {"SlayTheRequiem.dll": {"sha256": "c" * 64, "bytes": 100}},
                    "installed_mods": [{"id": "private-unreleased-mod"}], "local_path": "/home/private-user/Steam",
                    "godot": "4.5.1.stable.mono.custom-build.private-user",
                    "logs": ["/home/private-user/build.binlog"],
                    "proof": {"full_mod_build": True, "core_tests": True, "core_test_results": {"total": 517, "passed": 517, "executed": 517},
                              "pack_export": True, "pack_resource_loader": {"checked": 941, "failed": [], "isolated_project": True,
                                                                            "game_runtime": False, "engine": "private-build-name"}}}
        public = ci.public_passport(passport)
        text = json.dumps(public)
        for private in ("private-user", "private-unreleased-mod", "private-build-name", "installed_mods", "binlog", "local_path"):
            self.assertNotIn(private, text)
        self.assertFalse(public["proof"]["game_runtime"])
        self.assertFalse(public["proof"]["co_op"])
        self.assertEqual(941, public["proof"]["pack_resource_loader"]["checked"])
        self.assertEqual(64, len(public["godot_version_sha256"]))

    def test_warning_summary_never_contains_source_paths_or_messages(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "build.log"
            path.write_text("/home/private/code.cs(1,2): warning CS8602: message containing local data\n"
                            "private\\code.cs(1,2): warning STS2001: another private message\n", encoding="utf-8")
            self.assertEqual({"CS8602": 1, "STS2001": 1}, ci.warning_counts(path))

    def test_workflow_is_manual_read_only_and_uploads_no_private_directory(self):
        workflow = (ci.ROOT / ".github/workflows/legal-game-build.yml").read_text(encoding="utf-8")
        self.assertIn("workflow_dispatch:", workflow)
        self.assertNotIn("pull_request:", workflow)
        self.assertNotIn("pull_request_target:", workflow)
        self.assertIn("persist-credentials: false", workflow)
        self.assertIn("contents: read", workflow)
        upload = workflow.split("uses: actions/upload-artifact@v4", 1)[1]
        self.assertNotIn("private", upload)
        self.assertNotIn("**", upload)
        self.assertIn("public-result.json", upload)


if __name__ == "__main__":
    unittest.main()
