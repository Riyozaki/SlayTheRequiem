"""Negative fixtures for the static B2.4 matrix; not native card/reward execution."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest

PATH = Path(__file__).resolve().parents[1] / "balance/build_directions.py"
SPEC = importlib.util.spec_from_file_location("build_directions", PATH)
bd = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(bd)


class BuildDirectionsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.deps = bd.inputs()
        cls.original = json.loads(bd.RECIPES.read_text())

    def changed(self, edit):
        data = copy.deepcopy(self.original)
        edit(data["directions"][0])
        return bd.validate(data, self.deps)

    def test_real_nine_candidates(self):
        self.assertEqual([], bd.validate(self.original, self.deps))

    def test_cross_hero_card_is_not_access(self):
        errors = self.changed(lambda b: b["enablers"].append("mod:HuntingShot"))
        self.assertTrue(any("inaccessible hero pool" in e for e in errors), errors)

    def test_cut_native_cannot_be_a_draw_engine(self):
        errors = self.changed(lambda b: b["draw"].append("vanilla:Ironclad/DemonForm"))
        self.assertTrue(any("cut/gated" in e for e in errors), errors)

    def test_wrong_identity_pair(self):
        errors = self.changed(lambda b: b.update(background="HunterBackground"))
        self.assertTrue(any("background is not offered" in e for e in errors), errors)

    def test_no_missing_resource_or_rare_only_enabler(self):
        errors = self.changed(lambda b: b.update(resources=[], enablers=["mod:MountainBreaker"]))
        self.assertTrue(any("missing role resources" in e for e in errors), errors)
        self.assertTrue(any("no accessible common/uncommon enabler" in e for e in errors), errors)

    def test_no_invented_gear(self):
        errors = self.changed(lambda b: b["gear"].append("NonexistentBow"))
        self.assertTrue(any("unknown gear" in e for e in errors), errors)
