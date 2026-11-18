"""Static localization checker regressions; these do not run the game's formatter or Godot UI."""
from copy import deepcopy
from pathlib import Path
import json
import re
import tempfile
import unittest
from unittest.mock import patch

import check_loc


class CreationLocalizationTests(unittest.TestCase):
    def test_literal_conditional_switch_and_enum_keys_are_all_covered(self):
        source = '''
            Text("selectedOrigin", ("Name", name));
            Text(rebirth ? "rebirth" : "heading");
            Text(sources.IsIdentitySchool(s) ? "native" : "foreign");
            Text("stage." + plan.Stage); Text("school." + school);
            Text(bonus switch { StartingBonus.Gold => "gold", _ => "none" });
        '''
        ui = 'RequiemUi.TipText("CREATION.controls.title")'
        self.assertEqual({"selectedOrigin", "rebirth", "heading", "native", "foreign", "stage.Origin", "stage.Review",
                          "school.Destruction", "school.Illusion", "gold", "none", "controls"},
                         check_loc.creation_keys(source, ui, ["Origin", "Review"], ["Destruction", "Illusion"]))

    def test_current_generated_texts_include_every_dynamic_key(self):
        errors = []
        self.assertGreaterEqual(check_loc.check_creation_keys(errors), 55)
        self.assertEqual([], errors)

    def test_missing_dynamic_key_and_parameter_drift_are_detected(self):
        root = Path(check_loc.LOC)
        tables = {lang: json.loads((root / lang / "static_hover_tips.json").read_text()) for lang in ("eng", "rus")}
        for failure in ("missing", "parameter"):
            bad = deepcopy(tables)
            name = check_loc.PREFIX + "CREATION.resources.title"
            if failure == "missing":
                del bad["rus"][name]
            else:
                bad["rus"][name] = bad["rus"][name].replace("{OldHp}", "{Typo}")
            with tempfile.TemporaryDirectory() as temp:
                for lang, table in bad.items():
                    dest = Path(temp) / lang / "static_hover_tips.json"
                    dest.parent.mkdir()
                    dest.write_text(json.dumps(table))
                with patch.object(check_loc, "LOC", temp):
                    errors = []
                    check_loc.check_creation_keys(errors)
                self.assertTrue(errors)
                self.assertTrue(any(name in error for error in errors))

    def test_translated_gold_amount_matches_the_production_bonus_constant(self):
        code = Path(check_loc.CODE, "Core", "HeroCreationPlan.cs").read_text()
        amount = re.search(r"const int GoldGift = (\d+);", code)[1]
        for lang in ("eng", "rus"):
            table = json.loads(Path(check_loc.LOC, lang, "static_hover_tips.json").read_text())
            self.assertIn(amount, table[check_loc.PREFIX + "CREATION.gold.title"])

    def test_enumerated_dynamic_keys_follow_new_enum_members(self):
        with tempfile.TemporaryDirectory() as temp:
            dest = Path(temp) / "Types.cs"
            dest.write_text("public enum Phase { One, Two = 2, // a comment\n Three, }")
            names = check_loc.enum_names(str(dest), "Phase")
            self.assertEqual(["One", "Two", "Three"], names)
            self.assertEqual({"stage.One", "stage.Two", "stage.Three"},
                             check_loc.creation_keys('Text("stage." + phase)', "", names, []))
            with self.assertRaises(ValueError):
                check_loc.enum_names(str(dest), "Missing")


if __name__ == "__main__":
    unittest.main()
