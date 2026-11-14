"""python3 -m unittest discover -s tools/sim"""
import os
import unittest

import requiem_rules as r

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CSV = os.path.join(ROOT, "mod", "SlayTheRequiem", "data", "vanilla_attacks_stamina.csv")


class Stamina(unittest.TestCase):
    def test_states(self):
        for cur, pool, cost, exp in [(8, 8, 2, r.RESTED), (4, 8, 2, r.RESTED), (3, 8, 2, r.TIRED),
                                     (1, 8, 2, r.EXHAUSTED), (0, 8, 0, r.TIRED)]:
            self.assertEqual(r.stamina_state(cur, pool, cost), exp, (cur, pool, cost))

    def test_overstrain(self):
        self.assertEqual(r.pay(1, 8, 3), r.Payment(1, 2, 4, r.EXHAUSTED, 0.5))

    def test_regen(self):
        self.assertEqual(r.regenerate(7, 8, 3), 8)


class Combat(unittest.TestCase):
    def test_plating(self):
        for args, exp in [((6, 2), 4), ((2, 5), 1), ((6, 3, 2), 5), ((6, 3, 5), 6), ((6, 2, 0, 4), 8), ((0, 2), 0)]:
            self.assertEqual(r.apply_plating(*args), exp, args)

    def test_resist(self):
        self.assertEqual([r.resistance_multiplier(t) for t in (-1, 0, 2, 5)], [1.5, 1.0, 0.5, 0.25])
        self.assertEqual(r.resist_stacks(6, 1), 4)


class Statuses(unittest.TestCase):
    def test_doc_examples(self):
        self.assertEqual(r.burning_total(12), 22)
        self.assertEqual(r.poison_total(12), 78)

    def test_burning_block(self):
        self.assertEqual(r.burning_vs_block(5, 0), (0, 5))
        self.assertEqual(r.burning_vs_block(5, 4), (4, 3))
        self.assertEqual(r.burning_vs_block(5, 20), (10, 0))

    def test_static(self):
        self.assertEqual(r.static_tick(5, 2), (2, 3, 4))


class Spells(unittest.TestCase):
    def test_gap_table(self):
        for tier, mastery, state, mana in [(1, 3, "ease", 1), (2, 2, "free", 2), (3, 2, "clumsy", 3),
                                           (3, 1, "strained", 4), (4, 0, "impossible", 2)]:
            got = r.cast_cost(2, tier, mastery)
            self.assertEqual((got[0], got[2]), (state, mana), (tier, mastery))

    def test_scroll(self):
        self.assertEqual(r.cast_cost(4, 5, 0, from_scroll=True)[:3], ("free", True, 0))


class Table(unittest.TestCase):
    def test_csv(self):
        t = r.load_stamina_table(CSV)
        self.assertEqual(len(t), 131)
        self.assertEqual(t["StrikeIronclad"]["stamina"], "1")
        self.assertTrue(all(v["stamina"] in {"0", "1", "2", "3", "4", "X"} for v in t.values()))


if __name__ == "__main__":
    unittest.main()
