"""Synthetic transport negatives only; actual Godot helper execution is a separate CI job."""
import copy
import unittest

import presentation_smoke as smoke


class PresentationSmokeReportTests(unittest.TestCase):
    def setUp(self):
        self.commit = "a" * 40
        self.hashes = {name: "b" * 64 for name in smoke.SOURCES}
        self.engine = "c" * 64
        self.info = dict(schema=1, producer="actual-godot-presentation-helper-smoke", scope=smoke.SCOPE,
                         source_commit=self.commit, source_sha256=self.hashes, godot_executable_sha256=self.engine,
                         engine="4.5.1.stable.mono.official.fixture", passed=smoke.CASES.copy(),
                         actual_native_runs=0, same_frame_replacement_cycles=100, child_clear_cycles=100)

    def verify(self, info):
        smoke.validate_report(info, self.commit, self.hashes, self.engine)

    def test_complete_report_matches_only_its_source_and_scope(self):
        self.verify(self.info)

    def test_missing_duplicate_unknown_or_reordered_cases_are_rejected(self):
        for passed in (smoke.CASES[:-1], smoke.CASES + [smoke.CASES[0]], list(reversed(smoke.CASES)), ["native-game-PASS"]):
            info = dict(self.info, passed=passed)
            with self.assertRaisesRegex(ValueError, "helper cases"):
                self.verify(info)

    def test_scope_engine_and_native_claims_are_not_promoted(self):
        for key, value in (("scope", "full-mod-build"), ("actual_native_runs", 1), ("actual_native_runs", False),
                           ("engine", "4.6.stable.mono.official"), ("engine", "4.5.1.stable.official"),
                           ("same_frame_replacement_cycles", True), ("child_clear_cycles", 99)):
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                self.verify(dict(self.info, **{key: value}))

    def test_build_source_or_engine_mismatch_fails(self):
        for key, value in (("source_commit", "d" * 40), ("godot_executable_sha256", "e" * 64), ("source_sha256", {})):
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, "identity|engine hash"):
                self.verify(dict(self.info, **{key: value}))
        info = copy.deepcopy(self.info); info["source_sha256"][smoke.SOURCES[0]] = "f" * 64
        with self.assertRaisesRegex(ValueError, "identity"):
            self.verify(info)

    def test_malformed_schema_is_not_a_successful_engine_run(self):
        for value in (None, [], {}, dict(self.info, schema=True)):
            with self.subTest(value=value), self.assertRaises(ValueError):
                self.verify(value)


if __name__ == "__main__":
    unittest.main()
