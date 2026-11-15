"""python3 -m unittest discover -s tools/sim"""
import os
import sys
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
        self.assertEqual([r.resistance_multiplier(t) for t in (-1, 0, 2, 3)], [1.5, 1.0, 0.5, 0.25])
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


class Enemies(unittest.TestCase):
    def test_poise(self):
        for rank, hp, exp in [("normal", 12, 3), ("normal", 44, 6), ("normal", 400, 20), ("elite", 80, 13), ("boss", 300, 0)]:
            self.assertEqual(r.enemy_poise(rank, hp), exp, (rank, hp))

    def test_stagger(self):
        self.assertEqual(r.stagger(4, 6), (False, 4, 6))
        self.assertEqual(r.stagger(7, 6), (True, 0, 9))

    def test_attack_cost(self):
        self.assertEqual([r.enemy_attack_cost(1, 6), r.enemy_attack_cost(3, 15), r.enemy_attack_cost(1, 24)], [1, 2, 2])

    def test_immunity(self):
        self.assertEqual(r.resistance_multiplier(4), 0.0)
        self.assertEqual(r.resist_stacks(5, 4), 0)
        self.assertEqual(r.resist_stacks(5, -1), 8)


class Equipment(unittest.TestCase):
    def test_heavy(self):
        items = [{"slot": s, "armor": "heavy", "tier": 2} for s in ("head", "body", "hands", "feet")]
        b = r.loadout(items)
        self.assertEqual((b["plating"], b["poise"], b["mana_penalty"], b["dodge"]), (2, 4, 1, 0))

    def test_light_and_duelist(self):
        items = [{"slot": "head", "armor": "light", "tier": 1}, {"slot": "body", "armor": "light", "tier": 1},
                 {"slot": "main", "weapon": "dagger", "tier": 1}]
        b = r.loadout(items)
        self.assertEqual((b["dodge"], b["stamina_pool"], b["stamina_regen"], b["weapon_damage"]), (1, 1, 1, 0))

    def test_weapon_damage(self):
        self.assertEqual([r.weapon_damage("sword", 5), r.weapon_damage("twohanded", 2), r.weapon_damage("crossbow", 1)], [4, 4, 4])


class Mana(unittest.TestCase):
    def test_start_and_regen(self):
        self.assertEqual([r.mana_start(10), r.mana_start(3)], [5, 1])
        self.assertEqual([r.mana_regen_gain(0, 3, 1), r.mana_regen_gain(9, 10, 2), r.mana_regen_gain(14, 10, 2)], [1, 1, 0])

    def test_mastery_and_share(self):
        self.assertEqual(r.starting_mastery("Regent", "destruction"), 2)
        self.assertEqual(r.starting_mastery("Ironclad", "destruction"), 0)
        self.assertAlmostEqual(r.reward_share("Ironclad", 3), 0.15)

    def test_platebearer_firebolt_example(self):
        # 08 §8.2: Латоносец, владение 0, «Огненная стрела» (Новичок, 1 ✦) → 2 ✦
        self.assertEqual(r.cast_cost(1, 1, 0)[2], 2)
        # «Огненный шар» (Адепт, 3 ✦) при владении 2 → 5 ✦
        self.assertEqual(r.cast_cost(3, 3, 2)[2], 5)


class Casters(unittest.TestCase):
    def test_numbers(self):
        self.assertEqual([r.caster_mana_pool("normal"), r.caster_mana_pool("elite"), r.caster_mana_pool("boss")], [4, 6, 8])
        self.assertEqual([r.enemy_spell_cost(1, 8), r.enemy_spell_cost(3, 9)], [2, 3])

    def test_mage_never_casts_forever_with_heavy_spells(self):
        # Тяжёлое заклинание (3 ✦) при приросте 2: обычный маг выдыхается за 2 хода, элита — за 4.
        self.assertEqual(r.caster_turns_until_focus("normal", 3), 2)
        self.assertEqual(r.caster_turns_until_focus("elite", 3), 4)
        # Лёгкое (2 ✦) окупается приростом — давить ✦ нужно Статикой/разрядом.
        self.assertEqual(r.caster_turns_until_focus("normal", 2), 50)

    def test_highborn_discount_applies_after_affinity(self):
        # Регент (владение 2) «Огненный шар» (Адепт) 3 ✦: надбавка +2 (×1.5 вверх), скидка Высокой крови −1 → 4 ✦.
        self.assertEqual(r.spell_cost_with_discount(3, 3, 2, discount=1), 4)
        # Латоносец (0) «Огненная стрела» 1 ✦ → 2 ✦; со скидкой → 1 ✦ (скидка не масштабируется ×1.5).
        self.assertEqual(r.spell_cost_with_discount(1, 1, 0, discount=1), 1)


class Table(unittest.TestCase):
    def test_csv(self):
        t = r.load_stamina_table(CSV)
        self.assertEqual(len(t), 131)
        self.assertEqual(t["StrikeIronclad"]["stamina"], "1")
        self.assertTrue(all(v["stamina"] in {"0", "1", "2", "3", "4", "X"} for v in t.values()))



class Stage3(unittest.TestCase):
    def test_overload(self):
        self.assertEqual(r.overload(30, 32), (0, 0, 0, True))
        self.assertEqual(r.overload(37, 32), (5, 1, 0, True))
        self.assertEqual(r.overload(38, 32), (6, 1, 1, True))
        self.assertEqual(r.overload(43, 32), (11, 1, 1, False))
        self.assertEqual([r.card_weight("Rare"), r.card_weight("Common", is_curse=True), r.card_weight("Common", is_status=True)], [2, 2, 0])

    def test_origins(self):
        self.assertEqual([r.marshbark_heal(h, 80) for h in (80, 40, 39, 19, 0)], [0, 0, 2, 4, 0])
        self.assertEqual([r.knight_plating(h, 100) for h in (95, 89, 69, 49)], [0, 1, 2, 3])
        self.assertEqual([r.wizard_spell_bonus(m, 10) for m in (10, 7, 5, 0)], [0, 1, 2, 4])
        self.assertEqual([r.fugitive_bonus(n) for n in (0, 2, 5)], [0, 2, 3])
        self.assertEqual([r.winddancer_bonus(n) for n in (2, 3, 7)], [0, 1, 2])
        self.assertEqual([r.beast_spirit(n) for n in (0, 1, 4)], [(6, 3), (12, 5), (20, 7)])
        # Кланы Латоносца (M4 п. 1)
        self.assertEqual([r.forge_fury_active(n) for n in (2, 3)], [False, True])
        self.assertEqual([r.dune_rhythm_regen(s, 9) for s in (9, 7, 6, 4, 0)], [0, 0, 1, 2, 2])
        self.assertEqual([r.training_bonus_points(lv) for lv in (1, 2, 3, 6, 15, 20)], [0, 0, 1, 2, 5, 5])
        self.assertEqual([r.conspirator_potency(p) for p in (0, 2, 3, 4, 7)], [0, 3, 4, 5, 9])
        self.assertEqual([r.blade_spirit_bonus(m) for m in (0, 1, 3, 4)], [0, 0, 1, 2])
        self.assertEqual([r.loyal_guard(lv) for lv in (1, 4, 15)], [(10, 2), (16, 3), (38, 5)])

    def test_brewing(self):
        pouch = [0] * 10
        pouch[0] = 3  # Кровяной мох ×3
        brews = r.available_brews(pouch)
        self.assertEqual(brews[0], ("Health", 3, [0, 0, 0]))
        pouch = [0] * 10
        pouch[6], pouch[3] = 2, 1  # 2 поганки + пепельная соль
        names = {b[0]: b[1] for b in r.available_brews(pouch)}
        self.assertEqual(names, {"SnakeBite": 3, "Toxin": 1})
        self.assertEqual(r.brew_tier(2, 1), 3)
        self.assertEqual(r.brew_tier(3, 5), 5)
        self.assertEqual(r.available_brews([0] * 10), [])

    def test_drops_and_poisons(self):
        self.assertEqual([r.reagent_drop_count(k) for k in ("normal", "elite", "boss")], [1, 2, 3])
        self.assertEqual([r.pouch_fit(10, 3), r.pouch_fit(12, 1), r.pouch_fit(0, 2)], [2, 0, 2])
        self.assertEqual([r.poison_potency("Toxin", t) for t in (1, 3, 5)], [2, 4, 7])
        self.assertEqual(r.poison_potency("Paralytic", 5), 100)
        self.assertEqual(len(r.RECIPES), 24)
        self.assertEqual(sum(1 for x in r.RECIPES if x[0] in r.POISONS), 8)

    def test_affixes(self):
        self.assertEqual(r.affix_count("elite", 0, 99, 72), 1)
        self.assertEqual(r.affix_count("elite", 0, 0, 25), 0)  # свита Атаманши
        self.assertEqual(r.affix_count("normal", 0, 0), 0)
        self.assertEqual(r.affix_count("normal", 1, 24), 1)
        self.assertEqual(r.affix_count("normal", 1, 25), 0)
        self.assertEqual(r.affix_count("boss", 3, 0), 0)
        self.assertEqual([r.control_hp_cap(t) for t in ("novice", "expert", "master")], [20, 120, None])


class M5Camp(unittest.TestCase):
    def test_mule_and_trade_name(self):
        self.assertEqual([r.mule_capacity(False), r.mule_capacity(True)], [10, 15])
        self.assertEqual([r.chest_loss(n) for n in (0, 1, 2, 3, 7)], [0, 1, 1, 2, 4])
        self.assertEqual([r.refills_per_visit(t, True) for t in (0, 3, 5)], [1, 4, 4])
        self.assertEqual(r.refills_per_visit(2), 2)

    def test_buildings(self):
        self.assertFalse(r.has_building(0, "Kettle"))
        self.assertTrue(r.has_building(1, "Kettle"))
        self.assertFalse(r.has_building(3, "Altar"))
        self.assertTrue(r.has_building(5, "HerbalistTent"))
        self.assertEqual([r.unlock_after_victory(n) for n in (0, 4, 5)], [1, 5, 5])
        self.assertEqual(r.camp_cost("Forge", False, anvil=True), 1)
        self.assertEqual(r.camp_cost("Enchant", False, anvil=True), 2)
        self.assertEqual(r.camp_cost("Forge", False), 2)


class M5Rollback(unittest.TestCase):
    def test_crypt_candle(self):
        self.assertTrue(r.can_take_crypt_candle(True, True, True, 2, 0))
        self.assertFalse(r.can_take_crypt_candle(True, True, True, 2, 1))
        self.assertFalse(r.can_take_crypt_candle(True, True, True, r.INFINITE, 0))
        self.assertFalse(r.can_take_crypt_candle(False, True, True, 2, 0))
        self.assertFalse(r.can_take_crypt_candle(True, True, False, 0, 0))
        share = sum(r.crypt_candle_in_pool(r.fnv1a(f"seed{i}|crypt_candle", 1)) for i in range(1000)) / 1000
        self.assertTrue(0.2 < share < 0.4, share)

    def test_rejoin(self):
        self.assertEqual([r.rejoin_delay_ms(a) for a in (-1, 0, 1, 2, 3, 4, 9)], [0, 2000, 2500, 3500, 5000, 6000, 6000])
        self.assertEqual(r.rejoin_total_ms(), 49000)
        self.assertTrue(r.should_retry_rejoin(0, False, True))
        self.assertFalse(r.should_retry_rejoin(r.REJOIN_ATTEMPTS, False, True))
        self.assertFalse(r.should_retry_rejoin(1, True, True))
        self.assertFalse(r.should_retry_rejoin(1, False, False))


FULL_RUN = [("Foothills", 0), ("FrozenBarrows", 1), ("ClockworkDepths", 1), ("Hive", 2), ("BloodCourt", 3), ("StarTower", 3),
            ("Glory", 4)]


class M5Nodes(unittest.TestCase):
    def test_twenty_subtypes_have_icons_and_acts(self):
        self.assertEqual(len(r.NODE_SUBTYPES), 20)
        self.assertEqual(len(set(r.NODE_ICONS.values())), 20)
        root = os.path.join(os.path.dirname(__file__), "..", "..", "mod", "SlayTheRequiem", "images", "map", "icons")
        for sub in r.NODE_SUBTYPES:
            self.assertTrue(os.path.exists(os.path.join(root, r.NODE_ICONS[sub] + ".png")), sub)
            self.assertTrue(any(r.node_available(sub, k, slot) for k, slot in FULL_RUN), sub)
            r.node_base(sub)

    def test_every_subtype_has_a_generator(self):
        marks = {r.node_from_mark(m) for m in ("AbyssGate", "LockedVault", "Valve", "Lair", "Trial", "Cache", "Ambush")}
        shops = {r.node_from_shop(t) for t in ("Wanderer", "Armorer", "Arcanist", "Herbalist", "Jeweler", "Temple", "Fence",
                                               "Caravan")} - {None}
        unknown = {r.node_from_unknown_roll(p, lib) for p in (0, 1, 2, 3) for lib in (False, True)} - {None}
        fixed = {s for s in r.NODE_SUBTYPES if r.node_reveal(s) == "Fixed"}
        self.assertEqual(marks | shops | unknown | fixed | {"Tavern"}, set(r.NODE_SUBTYPES))
        self.assertEqual(r.node_reveal("Ambush"), "Hidden")

    def test_all_seven_shop_types_roll(self):
        seen = set()
        for kind, slot in FULL_RUN:
            for h in range(400):
                for n in (0, 1, 2):
                    seen.add(r.roll_shop_type_kind(r.fnv1a("s", h), kind, min(slot, 2), n, None))
        self.assertEqual(seen - {"Wanderer"}, {"Armorer", "Arcanist", "Herbalist", "Jeweler", "Temple", "Fence", "Caravan"})

    def test_counts_and_numbers(self):
        self.assertEqual([r.lair_count(s) for s in range(5)], [1, 1, 0, 0, 0])
        self.assertEqual([r.trial_count(0, 3), r.trial_count(1, 3), r.trial_count(1, 4)], [0, 2, 1])
        self.assertEqual([r.ambush_count(0, 1), r.ambush_count(2, 1), r.ambush_count(2, 2)], [0, 2, 1])
        self.assertEqual([r.lair_hp(100), r.lair_hp(10), r.lair_hp(41), r.lair_hp(1)], [70, 7, 29, 1])
        self.assertFalse(r.roll_library(0, 0))
        self.assertTrue(r.roll_library(1, 8))
        self.assertFalse(r.roll_library(1, 9))
        self.assertEqual([r.cache_gold(0), r.cache_trap(4), r.ambush_gold(4), r.trial_strength(3)], [30, 12, 55, 2])


def _sample_graph():
    paths = [[1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1], [3, 2, 2, 3, 3, 2, 2, 3, 3, 2, 2, 3, 3, 3],
             [4, 5, 5, 4, 4, 5, 5, 4, 4, 5, 5, 4, 4, 3], [6, 6, 6, 6, 5, 6, 6, 6, 5, 6, 6, 6, 6, 5]]
    g = r.Graph(7, 15)
    for p in paths:
        for i, c in enumerate(p):
            row = i + 1
            g.nodes[(c, row)] = {14: "Rest", 8: "Treasure", 6: "Elite", 10: "Elite", 3: "Unknown", 7: "Unknown",
                                 11: "Unknown"}.get(row, "Monster")
            if i:
                g.edges.add(((p[i - 1], row - 1), (c, row)))
    return g


def _seq(seed):
    import random
    rnd = random.Random(seed)
    return lambda n: 0 if n <= 0 else rnd.randrange(n)


class M6MapGraph(unittest.TestCase):
    def test_sample_has_no_crossings(self):
        g = _sample_graph()
        for a, b in g.edges:
            self.assertFalse(any(r.edges_cross(a, b, x, y) for x, y in g.edges))

    def test_tower_portals_end_in_fights(self):
        for seed in range(30):
            g = _sample_graph()
            edges, _, last = r.plan_paths(g, "StarTower", [], _seq(seed))
            portals = [e for e in edges if e[0] == "Portal"]
            self.assertIn(len(portals), (2, 3))
            for _, a, b, _ in portals:
                self.assertEqual(a[1] + 2, b[1])
                self.assertIn(g.nodes[b], ("Monster", "Elite"))
            self.assertEqual(sum(1 for e in edges if e[0] == "Danger"), 1)
            self.assertEqual(last[1], 14)

    def test_valves_never_cut_the_road(self):
        valves = [(0, 3), (5, 7), (2, 11)]
        for seed in range(60):
            g = _sample_graph()
            edges, _, _ = r.plan_paths(g, "ClockworkDepths", valves, _seq(seed))
            self.assertTrue(any(e[0] == "ValveClose" for e in edges))
            by = {(e[1], e[2]): e for e in edges}
            for mask in range(8):
                turned = lambda v, m=mask: bool((m >> valves.index(v)) & 1)
                self.assertTrue(g.reaches_last_row(
                    lambda a, b: r.edge_blocked(by.get((a, b)), False, b, False, False, turned, lambda c: False)), (seed, mask))

    def test_pockets_and_secrets_are_additive(self):
        g = _sample_graph()
        before = set(g.edges)
        edges, pockets, _ = r.plan_paths(g, "FrozenBarrows", [], _seq(3))
        self.assertTrue(before <= g.edges)
        self.assertIn(len(pockets), (1, 2))
        for p in pockets:
            self.assertEqual(g.nodes[p], "Treasure")
            self.assertTrue(any(e[0] == "PocketIn" and e[2] == p and e[1][1] == p[1] - 1 for e in edges))
            self.assertTrue(any(e[0] == "PocketOut" and e[1] == p and e[2][1] == p[1] + 1 for e in edges))
        self.assertFalse(any(e[0] in ("Portal", "ValveOpen", "ValveClose") for e in edges))
        by = {(e[1], e[2]): e for e in edges}
        self.assertTrue(g.reaches_last_row(lambda a, b: r.edge_blocked(by.get((a, b)), False, b, False, False,
                                                                          lambda v: False, lambda c: False)))

    def test_encode_round_trip_and_vanilla_acts(self):
        edges, pockets, last = r.plan_paths(_sample_graph(), "ClockworkDepths", [(0, 3)], _seq(1))
        text = r.encode_plan(edges, pockets, last)
        self.assertEqual(r.encode_plan(*r.decode_plan(text)), text)
        self.assertEqual(r.plan_paths(_sample_graph(), "Hive", [], _seq(1)), ([], [], None))
        self.assertEqual(r.decode_plan("garbage;Z:1,2>3,4;P:x"), ([], [], None))

    def test_blocking_and_sight(self):
        no, yes = (lambda v: False), (lambda v: True)
        self.assertTrue(r.edge_blocked(("Secret", 0, 0, None), False, (1, 2), False, False, no, no))
        self.assertFalse(r.edge_blocked(("Secret", 0, 0, None), False, (1, 2), True, False, no, no))
        self.assertTrue(r.edge_blocked(("ValveOpen", 0, 0, (0, 0)), False, (1, 2), False, False, no, no))
        self.assertTrue(r.edge_blocked(("ValveClose", 0, 0, (0, 0)), False, (1, 2), False, False, yes, no))
        self.assertTrue(r.edge_blocked(None, True, (1, 2), False, False, no, no))
        self.assertFalse(r.edge_blocked(None, True, (1, 2), False, True, no, no))
        self.assertFalse(r.edge_blocked(None, True, (1, 2), False, False, no, yes))
        self.assertTrue(r.night_sight(False, False, True, False))
        self.assertTrue(r.secret_sight(False, True, False))
        self.assertEqual([r.last_rest_boss_hp(100), r.last_rest_boss_hp(50)], [110, 55])
        self.assertEqual([r.node_group("Armorer"), r.node_group("Ambush"), r.node_group("Valve")], ["Trade", "Danger", "Magic"])


class M5Shops(unittest.TestCase):
    def test_placement(self):
        self.assertEqual(r.guaranteed_shop("BloodCourt", 0), "Fence")
        self.assertEqual(r.guaranteed_shop("StarTower", 1), "Arcanist")
        self.assertEqual(r.guaranteed_shop("Glory", 0), "Temple")
        self.assertEqual(r.guaranteed_shop("Foothills", 0), "Wanderer")
        self.assertIsNone(r.guaranteed_shop("StarTower", 2))
        self.assertNotIn("Caravan", r.shop_act_pool_kind("Hive"))
        for h in range(200):
            self.assertNotEqual(r.roll_shop_type_kind(h, "FrozenBarrows", 1, 2, None, caravan_seen=True), "Caravan")
            self.assertNotEqual(r.roll_shop_type_kind(h, "Foothills", 0, 1, None), "Fence")
        self.assertTrue(any(r.roll_shop_type_kind(h, "Foothills", 0, 1, None, outlaw=True) == "Fence" for h in range(200)))
        self.assertEqual(r.equipment_tiers("Fence", 0), (4, 5))
        self.assertEqual(r.equipment_tiers("Jeweler", 2), (3, 4))

    def test_prices(self):
        self.assertEqual([r.scroll_price(t) for t in (1, 2, 3)], [25, 45, 70])
        self.assertEqual([r.staff_price(t) for t in range(1, 6)], [70, 120, 180, 240, 320])
        self.assertEqual([r.staff_tiers(a) for a in (0, 1, 2)], [(1, 1), (2, 3), (4, 5)])
        self.assertEqual(r.enchant_service_price(2, True), 110)
        self.assertEqual(r.enchant_service_price(3, False), 210)
        self.assertEqual([r.soul_stone_price(p) for p in (1, 2, 3)], [40, 80, 140])
        self.assertEqual(r.cleanse_price(True), 35)
        self.assertEqual(r.fence_sell_value(80), 48)
        self.assertFalse(r.can_pay_curse_removal(6))
        self.assertEqual(r.trade_tiers(1), (1, 1))
        self.assertEqual(r.trade_tiers(4), (3, 4))
        self.assertEqual([r.mentor_skill_price(b, True) for b in range(4)], [100, 150, 200, None])
        self.assertEqual(r.mentor_skill_price(1, False), 225)

    def test_clear_name_and_lessons(self):
        spent = [450, 0, 300, 10, 150, 150, 0, 150]
        self.assertEqual(r.clear_name_target(spent), 1)
        self.assertEqual(r.clear_name_target([150] * 6 + [0, 150]), -1)
        mask = r.with_lesson(0, "TwoHanded")
        self.assertTrue(r.is_clumsy("Regent", "TwoHanded", False, False))
        self.assertFalse(r.is_clumsy_with_lessons("Regent", "TwoHanded", False, False, mask))
        self.assertTrue(r.is_clumsy_with_lessons("Regent", "Crossbow", False, False, mask))
        self.assertTrue(r.is_clumsy_with_lessons("Regent", "Sword", True, False, mask))
        self.assertFalse(r.is_clumsy_with_lessons("Regent", "Sword", True, False, r.with_lesson(mask, "Shield")))


class Stage35(unittest.TestCase):
    def test_shop_types(self):
        self.assertEqual(r.roll_shop_type(12345, 0, 0, None), "Wanderer")
        self.assertNotIn("Arcanist", r.shop_act_pool(0))
        self.assertIn("Arcanist", r.shop_act_pool(1))
        for h in range(50):
            t = r.roll_shop_type(h, 1, 2, "Armorer")
            self.assertNotEqual(t, "Armorer")
        # FNV-1a стабилен (значения совпадают с C#-реализацией по построению)
        self.assertEqual(r.fnv1a("ABC", 0), r.fnv1a("ABC", 0))
        self.assertNotEqual(r.fnv1a("ABC", 1), r.fnv1a("ABC", 2))

    def test_prices_and_trust(self):
        self.assertEqual([r.trust(x) for x in (0, 149, 150, 449, 450, 9999)], [0, 0, 1, 2, 3, 3])
        self.assertEqual(r.apply_trust(100, 3), 85)
        self.assertEqual(r.apply_trust(50, 1), 48)  # 47.5 → 48
        self.assertEqual(r.sell_value(80, 0), 28)
        self.assertEqual(r.sell_value(80, 3), 40)
        self.assertEqual([r.equipment_price("Weapon", t) for t in range(1, 6)], [45, 80, 130, 190, 260])
        self.assertEqual(r.equipment_price("Armor", 9), 220)
        self.assertEqual([r.spell_price(t, True) for t in range(1, 6)], [41, 63, 99, 144, 207])
        self.assertEqual(r.spell_price(3, False), 110)
        self.assertEqual([r.brew_price(t, False) for t in (1, 3, 5)], [40, 75, 130])
        self.assertEqual([r.brew_price(t, True) for t in (1, 3, 5)], [35, 70, 125])
        self.assertEqual([r.treatise_price(n) for n in (0, 1, 2)], [120, 160, 200])
        self.assertEqual(r.equipment_tiers("Wanderer", 2), (1, 2))
        self.assertEqual([r.equipment_tiers("Armorer", a) for a in (0, 1, 2, 3)], [(1, 2), (2, 3), (3, 4), (4, 5)])
        self.assertEqual([r.shop_brew_tier(a) for a in (0, 1, 2)], [1, 3, 5])

    def test_combat_gold(self):
        self.assertEqual([r.combat_gold(g) for g in (13, 20, 25, 35, 95, 105)], [17, 26, 33, 46, 124, 137])
        self.assertTrue(r.spell_allowed_in_shop(2, 0))
        self.assertFalse(r.spell_allowed_in_shop(3, 0))
        self.assertTrue(r.spell_allowed_in_shop(4, 3))

    def test_memory_stone(self):
        self.assertEqual([r.candles_for(lv) for lv in (0, 1, 4, 5, 7, 8, 9, 10)], [r.INFINITE, 3, 3, 2, 2, 1, 1, 0])
        self.assertEqual(r.candles_left(0, 99), r.INFINITE)
        self.assertEqual(r.candles_left(3, 1), 2)
        self.assertEqual(r.candles_left(3, 5), 0)
        self.assertEqual(r.candles_left(9, 1, bonus=1), 1)
        self.assertEqual(r.candles_left(0, 0, override=0), 0)
        self.assertEqual(r.candles_left(10, 1, override=2), 1)
        self.assertEqual([r.flesh_scar_max_hp(h) for h in (80, 70, 10, 2, 1)], [75, 65, 9, 1, 1])
        self.assertEqual(r.purse_scar_gold(155), 93)
        self.assertAlmostEqual(r.score_multiplier(2), 0.70)
        self.assertEqual(r.score_multiplier(10), 0.0)
        # повторный возврат к тому же Камню: прежний Шрам плоти + новый
        self.assertEqual(r.scars_to_apply(1, True, 0), 2)
        self.assertEqual(r.scars_to_apply(1, False, 1), 0)


class Stage36(unittest.TestCase):
    def test_trust_orders(self):
        self.assertEqual([r.refills_per_visit(t) for t in (0, 1, 2, 3, 5)], [0, 1, 2, 3, 3])
        self.assertFalse(r.can_order(2))
        self.assertTrue(r.can_order(3))
        self.assertEqual(r.order_price(131), 66)
        self.assertEqual(r.order_tier(0), 2)
        self.assertEqual(r.order_tier(3), 5)
        self.assertEqual(r.order_item_price("Weapon", 0), 80)
        self.assertEqual(r.order_item_price("Potion", 0), 130)

    def test_theft(self):
        self.assertEqual([r.theft_chance(p) for p in (0, 49, 50, 149, 400, 1000)], [80, 80, 70, 60, 0, 0])
        self.assertTrue(r.theft_succeeds(79, 40))
        self.assertFalse(r.theft_succeeds(80, 40))
        self.assertFalse(r.theft_succeeds(0, 400))
        self.assertEqual(r.theft_fail_damage(80), 16)
        self.assertEqual(r.theft_fail_damage(71), 15)
        self.assertEqual(r.theft_fail_damage(0), 1)

    def test_services(self):
        self.assertEqual([r.sharpen_price(t) for t in (1, 2, 3, 4, 5)], [60, 60, 90, 130, 130])
        self.assertEqual([r.reagent_kit_price(a) for a in (0, 1, 2, 3, 4)], [20, 25, 30, 35, 35])
        self.assertEqual(r.reagent_sell_value(5), 15)
        self.assertEqual(r.removal_price(2), 125)
        self.assertEqual(r.death_echo_surcharge(100, 40), 40)
        self.assertEqual(r.death_echo_surcharge(100, 500), 100)

    def test_upgrades_beyond_v(self):
        self.assertEqual(r.weapon_damage("sword", 5), 4)
        self.assertEqual(r.weapon_damage("sword", 7), 6)
        self.assertEqual(r.weapon_damage("twohanded", 6), 10)

    def test_coop_stone(self):
        self.assertTrue(r.vote_approved(1, 1))   # ничья — решает хост (его голос «за»)
        self.assertFalse(r.vote_approved(1, 2))
        self.assertEqual(r.shared_candles_left([3, 1, 2]), 1)
        self.assertEqual(r.shared_candles_left([]), 0)
        self.assertEqual(r.shared_candles_used([0, 2, 1]), 2)
        self.assertEqual(r.map_rng_name(0, 1), "act_1_map_requiem_1")


class Pantheon(unittest.TestCase):
    def test_piety_steps(self):
        self.assertEqual(r.piety_sin(r.RIGHTEOUS), r.TAINTED)
        self.assertEqual(r.piety_sin(r.TAINTED), r.APOSTATE)
        self.assertEqual(r.piety_sin(r.APOSTATE), r.APOSTATE)
        self.assertEqual(r.piety_redeem(r.APOSTATE), r.TAINTED)
        self.assertEqual(r.piety_redeem(r.TAINTED), r.RIGHTEOUS)

    def test_effective(self):
        self.assertEqual(r.piety_effective(r.RIGHTEOUS, True, False, False), r.TAINTED)
        self.assertEqual(r.piety_effective(r.APOSTATE, True, False, False), r.APOSTATE)
        self.assertEqual(r.piety_effective(r.RIGHTEOUS, False, True, False), r.APOSTATE)
        self.assertEqual(r.piety_effective(r.RIGHTEOUS, False, False, True), r.APOSTATE)
        self.assertEqual(r.blessing_strength(r.RIGHTEOUS, False), r.BLESSING_FULL)
        self.assertEqual(r.blessing_strength(r.TAINTED, False), r.BLESSING_DIMMED)
        self.assertEqual(r.blessing_strength(r.APOSTATE, False), r.BLESSING_NONE)
        self.assertEqual(r.blessing_strength(r.RIGHTEOUS, True), r.BLESSING_NONE)

    def test_redemption(self):
        self.assertEqual([r.redemption_cost(a) for a in (0, 1, 2)], [50, 75, 100])
        self.assertEqual([r.robbery_gold(a) for a in (0, 1, 4)], [40, 60, 120])
        self.assertTrue(r.can_redeem(r.TAINTED, -1, 1, False))
        self.assertFalse(r.can_redeem(r.TAINTED, 1, 1, False))   # раз в акт
        self.assertFalse(r.can_redeem(r.RIGHTEOUS, -1, 1, False))
        self.assertFalse(r.can_redeem(r.APOSTATE, -1, 1, True))   # Отверженный

    def test_unknown_rooms(self):
        self.assertEqual(r.unknown_room_roll(0, 0), 0)
        self.assertEqual([r.unknown_room_roll(1, x) for x in (0, 13, 14, 21, 22, 29, 30)], [1, 1, 2, 2, 3, 3, 0])
        self.assertEqual(r.unknown_room_roll(3, 25), 0)            # Камни — только акты 2–3
        self.assertEqual(r.unknown_room_roll(3, 5), 1)

    def test_omen_offer(self):
        self.assertEqual(r.omen_offer_count(0), 3)
        self.assertEqual(r.omen_offer_count(8), 10)
        picks = r.pick_distinct(10, 3, lambda n: n - 1)
        self.assertEqual(len(set(picks)), 3)
        self.assertNotIn(4, r.pick_distinct(6, 5, lambda n: 0, exclude={4}))
        self.assertEqual(sorted(r.pick_distinct(10, 10, lambda n: 0)), list(range(10)))

    def test_prince_helpers(self):
        self.assertTrue(r.has_divine_spirit(r.BLESSING_FULL, True, True, True))
        self.assertFalse(r.has_divine_spirit(r.BLESSING_DIMMED, True, True, True))
        self.assertEqual(r.outcast_after_combat_heal(False), 2)
        self.assertEqual(r.outcast_after_combat_heal(True), 4)
        self.assertTrue(r.below_half(9, 20))
        self.assertFalse(r.below_half(10, 20))
        self.assertEqual(r.star_marks_after_act(2, False), 3)
        self.assertEqual(r.star_marks_after_act(2, True), 0)

    def test_tribunal_heirs(self):
        # Ашборн: ослабленная Цена; Кочевник: ступень вверх; чужие Князья не меняются.
        self.assertEqual(r.tribunal_gift(4, False, False), {"attack": 2, "pierce": 1, "summon_taken": 50})
        self.assertEqual(r.tribunal_gift(4, True, False)["summon_taken"], 25)
        self.assertEqual(r.tribunal_gift(2, True, False)["rest_heal"], -15)
        nomad = r.tribunal_gift(4, True, True)
        self.assertEqual((nomad["attack"], nomad["pierce"], nomad["summon_taken"]), (3, 2, 25))
        star = r.tribunal_gift(7, True, True)
        self.assertEqual((star["max_hp"], star["mana_pool"], star["fire"], star["frost"]), (9, 3, 2, 2))
        self.assertEqual(r.tribunal_gift(6, True, True), {})
        self.assertEqual(r.heir_star_marks(1), 0)
        self.assertEqual(r.heir_star_marks(3), 2)
        self.assertEqual((r.web_leech_cap(False), r.web_leech_cap(True)), (5, 8))
        self.assertEqual((r.web_below_half_bonus(False), r.web_below_half_bonus(True)), (2, 3))
        # «Без условий»: в святилище всегда есть Тройственный.
        self.assertEqual(r.heir_shrine_picks([0, 1], None, lambda n: 0), [0, 4])
        self.assertEqual(r.heir_shrine_picks([0, 1], 4, lambda n: 0), [0, 2])
        self.assertEqual(r.heir_shrine_picks([0, 7], None, lambda n: 0), [0, 7])
        self.assertTrue(r.ordinator_may_accept(7))
        self.assertFalse(r.ordinator_may_accept(0))

    def test_ordinator_forms_and_paths(self):
        for form in ("Righteous", "Vampire", "Werewolf"):
            self.assertFalse(r.can_take_worldview("None", form, True, True, False))
        self.assertFalse(r.can_take_worldview("Herald", "Lich", False, True, False))
        self.assertTrue(r.can_take_worldview("None", "DarkPact", False, True, False))
        self.assertTrue(r.path_allowed("Paladin", "None", False, False, ordinator=True))
        self.assertFalse(r.path_allowed("BlackKnight", "None", True, False, ordinator=True))
        self.assertIn("Paladin", r.native_paths("Regent", "None", False, False, ordinator=True))

    def test_pierce(self):
        self.assertEqual(r.apply_plating(6, 2, pierce=1), 5)
        self.assertEqual(r.apply_plating(6, 1, pierce=1), 6)
        self.assertEqual(r.apply_plating(6, -1), 7)   # Осколок Ветра: Латы −1


class Progression(unittest.TestCase):
    def test_levels(self):
        self.assertEqual([r.renown_to_reach(l) for l in (1, 2, 3, 4, 15)], [0, 3, 7, 12, 133])
        self.assertEqual([r.level_for(x) for x in (0, 2, 3, 6, 7, 12, 132, 133, 999)], [1, 1, 2, 2, 3, 4, 14, 15, 15])
        self.assertEqual(r.renown_to_next(4), 3)
        self.assertEqual(r.renown_to_next(500), 0)
        self.assertEqual(r.stat_points_for(1), 0)
        self.assertEqual(r.stat_points_for(15), 28)
        self.assertEqual([r.skill_points_for(l) for l in (1, 2, 3, 15)], [0, 1, 1, 7])
        self.assertEqual(r.renown_for("boss"), 6)
        self.assertEqual(r.retained_renown(10, 21), 15)
        self.assertEqual(r.retained_renown(10, 8), 10)

    def test_tiers(self):
        self.assertEqual([r.stat_tier(p) for p in (0, 2, 3, 6, 9, 20)], [0, 0, 1, 2, 3, 3])
        self.assertEqual(r.with_meditation(3, True), 3)
        self.assertEqual(r.with_meditation(1, True), 2)
        t = r.tiers(3, 2, 1)
        self.assertEqual((t["poison_res"], t["burden"], t["plating"]), (1, 6, 1))
        self.assertEqual((t["stamina_regen"], t["attack_damage"], t["free_first_attack"]), (1, 1, False))
        self.assertEqual((t["mana_regen"], t["first_spell_discount"], t["ward"]), (1, 0, 0))

    def test_clumsy(self):
        self.assertTrue(r.is_clumsy("Regent", "Crossbow", False, False))
        self.assertTrue(r.is_clumsy("Regent", "Sword", True, False))
        self.assertFalse(r.is_clumsy("Regent", "TwoHanded", False, True))
        self.assertTrue(r.is_clumsy("Silent", "TwoHanded", False, False))
        self.assertFalse(r.is_clumsy("Silent", "Crossbow", True, False))
        self.assertFalse(r.is_clumsy("Ironclad", "TwoHanded", True, False))

    def test_skill_tree(self):
        self.assertEqual(len(r.SKILL_NODES), 69)
        self.assertEqual(len(r.branches_for("Regent")), 8)
        self.assertEqual(len(r.branches_for("Ironclad")), 12)
        br = r.branches_for("Ironclad")
        self.assertTrue(r.can_learn("Fighter", set(), br))
        self.assertFalse(r.can_learn("Fencer", set(), br))
        self.assertTrue(r.can_learn("Fencer", {"Fighter"}, br))
        self.assertFalse(r.can_learn("Bloodletter", set(), br))     # чужая ветка героя
        self.assertFalse(r.can_learn("Wayfarer", {"CampKettle"}, br))
        self.assertTrue(r.can_learn("Wayfarer", {"CampKettle", "FirstAid"}, br))
        self.assertFalse(r.can_learn("CampMaster", {"CampKettle", "FirstAid", "Wayfarer"}, br))
        self.assertTrue(r.can_learn("CampMaster", {"CampKettle", "FirstAid", "Wayfarer", "TradeName"}, br))
        self.assertEqual(r.skill_cost("Destruction1", False), 2)
        self.assertEqual(r.skill_cost("Destruction1", True), 1)
        self.assertEqual(r.skill_cost("Fighter", False), 1)
        one = r.learnable("Ironclad", set(), 1, lambda b: False)
        self.assertNotIn("Destruction1", one)
        self.assertIn("Destruction1", r.learnable("Ironclad", set(), 2, lambda b: False))


class Camp(unittest.TestCase):
    def test_points(self):
        self.assertEqual(r.camp_budget(False, False), 2)
        self.assertEqual(r.camp_budget(True, True), 4)
        self.assertEqual(r.camp_cost("Forge", False), 2)
        self.assertEqual(r.camp_cost("Forge", True), 1)
        self.assertEqual(r.camp_cost("Chest", False), 0)
        self.assertEqual(r.camp_cost("DIG", False), 2)
        self.assertEqual(r.short_rest_percent(True), 20)

    def test_fatigue_and_stones(self):
        self.assertEqual([r.fatigue_stacks(n, True) for n in (0, 2, 3, 8, 9, 30)], [0, 0, 1, 2, 3, 3])
        self.assertEqual(r.fatigue_stacks(9, False), 0)
        self.assertEqual(r.soul_stone_for("normal", 0), 0)
        self.assertEqual(r.soul_stone_for("elite", 1), 1)
        self.assertEqual(r.soul_stone_for("elite", 2), 2)
        self.assertEqual(r.soul_stone_for("boss", 4), 3)

    def test_food(self):
        feast = r.dish_effect("Feast")
        self.assertEqual((feast["max_hp"], feast["frost_res"], feast["regen"], feast["opening_draw"]), (6, 1, 1, 1))
        meals = r.eat([], "FishSoup", 1)
        meals = r.eat(meals, "VenisonRoast", 1)
        self.assertEqual(meals, [("VenisonRoast", 3)])
        meals = r.eat(meals, "FishSoup", 2)
        self.assertEqual(len(meals), 2)
        meals = r.meals_after_combat(r.meals_after_combat(meals))
        self.assertEqual(meals, [("VenisonRoast", 1), ("FishSoup", 1)])
        self.assertEqual(r.meals_after_combat(meals), [])
        pouch = [3, 0, 1, 0, 0, 0, 0, 0, 5, 1]
        self.assertEqual(sorted(r.feast_ingredients(pouch)), [0, 0, 0, 2, 9])
        self.assertIsNone(r.feast_ingredients([1, 0, 0, 0, 0, 0, 0, 0, 9, 1]))

    def test_enchant(self):
        self.assertTrue(r.enchant_allowed("Health", "Body"))
        self.assertFalse(r.enchant_allowed("Health", "Ring"))
        self.assertEqual(r.enchant_value("Magic", 3), 6)
        self.assertEqual(r.enchant_value("FireResistance", 2), 1)
        self.assertEqual(r.enchant_value("FireResistance", 3), 2)
        self.assertEqual(r.enchant_charges(2), 12)
        e = r.apply_enchant([], "Health", 1, 1)
        e = r.apply_enchant(e, "Stamina", 2, 1)
        self.assertEqual(e, [("Stamina", 2, 0)])
        e = r.apply_enchant(e, "FireBrand", 3, 2)
        self.assertEqual(e, [("Stamina", 2, 0), ("FireBrand", 3, 16)])


class ShoutAndRevelTests(unittest.TestCase):
    def test_shout_cooldown(self):
        self.assertEqual(r.shout_cooldown(3, 0), 3)
        self.assertEqual(r.shout_cooldown(3, 2), 1)
        self.assertEqual(r.shout_cooldown(3, 5), 1)
        self.assertEqual(r.shout_cooldown(4, -1), 4)
        self.assertEqual(r.shout_shelf_capacity(True), 2)
        self.assertEqual(r.shout_shelf_capacity(False), 1)

    def test_soul_tear(self):
        self.assertTrue(r.soul_tear_executes(24, 100, False))
        self.assertFalse(r.soul_tear_executes(25, 100, False))
        self.assertFalse(r.soul_tear_executes(10, 100, True))
        self.assertFalse(r.soul_tear_executes(0, 100, False))

    def test_revel(self):
        self.assertEqual(r.revel_second_property("Health", 1, False), ("Stamina", 1))
        self.assertEqual(r.revel_second_property("SecondWind", 3, False), ("Mana", 2))
        self.assertEqual(r.revel_second_property("Clarity", 5, False), ("Heal", 6))
        self.assertEqual(r.revel_second_property("Giant", 3, False), ("Block", 6))
        self.assertEqual(r.revel_second_property("SnakeBite", 5, True), ("None", 0))


class Stage6(unittest.TestCase):
    def test_compatibility(self):
        self.assertTrue(r.can_take_worldview("None", "Vampire", False, False, False))
        self.assertFalse(r.can_take_worldview("Vampire", "Werewolf", False, False, False))
        self.assertFalse(r.can_take_worldview("None", "Righteous", False, False, False))
        self.assertTrue(r.can_take_worldview("None", "Righteous", True, False, False))
        self.assertTrue(r.can_take_worldview("DarkPact", "Herald", False, False, False))
        self.assertFalse(r.can_take_worldview("None", "Lich", False, False, False))
        self.assertFalse(r.can_take_worldview("None", "Vampire", False, False, True))
        self.assertTrue(r.can_take_clockwork("Righteous", False))
        self.assertFalse(r.can_take_clockwork("Vampire", False))

    def test_righteous(self):
        self.assertTrue(r.halo_triggers(39, 80, False))
        self.assertFalse(r.halo_triggers(40, 80, False))
        self.assertFalse(r.halo_triggers(10, 80, True))
        self.assertEqual(r.righteous_step_after_act(1, True), 2)
        self.assertEqual(r.righteous_step_after_act(2, True), 2)
        self.assertEqual(r.righteous_step_after_act(1, False), 1)

    def test_thirst(self):
        self.assertEqual(r.thirst_row(0)[:2], (1, -1))
        self.assertEqual(r.thirst_row(3)[3], 1.5)
        self.assertTrue(r.thirst_row(9)[6])
        self.assertEqual(r.thirst_after_combat(4, False), 4)
        self.assertEqual(r.thirst_after_combat(3, True), 0)
        self.assertEqual(r.lifesteal(9, 25), 2)

    def test_beast(self):
        self.assertEqual([r.moon_call_duration(n) for n in range(4)], [3, 4, 4, 5])
        self.assertEqual(r.hits_per_extension(2), 2)
        self.assertEqual(r.extension_turns(2, 7, 3), 2)
        self.assertEqual([r.beast_attack_bonus(n) for n in range(4)], [4, 6, 6, 8])
        self.assertEqual([r.alpha_wolf_hp(n) for n in range(4)], [0, 8, 12, 16])
        self.assertEqual(r.beast_nodes_earned(5), 2)
        self.assertEqual(r.beast_nodes_earned(40), 9)

    def test_pact(self):
        self.assertEqual([r.next_stance(s) for s in range(3)], [1, 2, 0])
        self.assertEqual(r.lich_max_hp_loss(75), 22)
        self.assertEqual(r.lich_max_hp_loss(1), 0)
        self.assertTrue(r.can_become_lich("Herald", 10))
        self.assertFalse(r.can_become_lich("DarkPact", 99))
        self.assertEqual(r.phylactery_revive_hp(70), 21)

    def test_heal(self):
        self.assertEqual(r.heal_percent([-50, -100]), -100)
        self.assertEqual(r.apply_heal(10, 50, False), 15)
        self.assertEqual(r.apply_heal(7, -50, False), 3)
        self.assertEqual(r.apply_heal(10, 0, True), 0)

    def test_events(self):
        self.assertEqual(r.event_forms_for(0, 3), ["Werewolf", "DarkPact"])
        self.assertIn("Lich", r.event_forms_for(3, 5))
        self.assertNotIn("Lich", r.event_forms_for(2, 5))
        self.assertIn("Lich", r.event_forms_for(2, 3))
        self.assertEqual(r.unknown_room_event(0, 3, 12, 0, 0), "None")
        self.assertEqual(r.unknown_room_event(0, 3, 0, 1, 0), "DarkPact")
        seen = 1 << r.FORMS.index("Werewolf") | 1 << r.FORMS.index("DarkPact")
        self.assertEqual(r.unknown_room_event(0, 3, 0, 0, seen), "None")

    def test_paths(self):
        self.assertEqual(r.path_offer_act_index(5), 2)
        self.assertEqual(r.path_offer_act_index(3), 1)
        self.assertTrue(r.path_offer_due(8, 2, 5, False))
        self.assertFalse(r.path_offer_due(7, 2, 5, False))
        self.assertFalse(r.path_offer_due(9, 1, 5, False))
        self.assertFalse(r.path_allowed("Paladin", "Vampire", True, False))
        self.assertTrue(r.path_allowed("BlackKnight", "Vampire", True, False))
        offer = r.offer_paths("Ironclad", "None", False, False, lambda n: 0)
        self.assertEqual(offer[:2], ["Champion", "Barbarian"])
        self.assertEqual(len(offer), 3)
        self.assertNotIn(offer[2], ("Champion", "Barbarian", "Battlemage", "Paladin", "BlackKnight", "Monk"))
        self.assertEqual(r.monk_unarmed_damage(8), 8)
        self.assertTrue(r.fear_turn(4))
        self.assertFalse(r.fear_turn(3))
        self.assertEqual(r.reflected(9), 4)
        self.assertEqual(r.champion_unshakeable_stagger(3), 2)

    def test_card_enchants(self):
        self.assertTrue(r.can_enchant_card("Lightened", False, False))
        self.assertFalse(r.can_enchant_card("Flaming", False, True))
        self.assertFalse(r.can_enchant_card("Double", True, False))
        for a in r.CARD_ENCHANTS[:5]:
            for b in r.CARD_ENCHANTS[:5]:
                if r.valid_double(a, b):
                    self.assertEqual(r.decode_double(r.encode_double(a, b)), (a, b))
        self.assertFalse(r.valid_double("Storm", "Storm"))
        self.assertEqual(r.card_weight_after(0, True), 0)
        self.assertEqual(r.stamina_after(2, True), 1)

    def test_score(self):
        self.assertEqual(r.run_score(1000, 0), 1000)
        self.assertEqual(r.run_score(1000, 2), 700)
        self.assertEqual(r.run_score(1000, 9), 0)


class Stage7(unittest.TestCase):
    def test_levels(self):
        self.assertEqual(r.resolve_level(-1, 0), 1)
        self.assertEqual(r.resolve_level(-1, 20), 10)
        self.assertEqual(r.resolve_level(0, 9), 0)
        self.assertEqual(r.level_damage_taken(0), 0.75)
        self.assertEqual(r.level_damage_taken(7), 1.25)
        self.assertEqual(r.level_damage_dealt(10), 0.8)
        self.assertEqual(r.level_enemy_hp(0), 0.75)
        self.assertFalse(r.candles_allowed(10))
        self.assertEqual(r.candles_for(10), 0)

    def test_elite_affixes(self):
        self.assertEqual(r.affix_count("elite", 0, 99, 72), 1)
        self.assertEqual(r.affix_count("elite", 0, 10, 72, requiem_level=7), 3)
        self.assertEqual(r.affix_count("elite", 0, 60, 72, requiem_level=7), 2)
        self.assertEqual(r.affix_count("elite", 4, 10, 72, requiem_level=7, elite_bonus=1), 4)
        self.assertEqual(r.affix_count("elite", 4, 10, 30, requiem_level=10, elite_bonus=1), 0)
        picked = r.pick_affixes(3, [1, 1, 1])
        self.assertEqual(picked[0], "Fiery")
        self.assertEqual(len([a for a in picked if a in r.ELEMENTAL_AFFIXES]), 1)
        self.assertEqual(len(set(picked)), 3)
        self.assertTrue(r.is_frenzied(49, 100))
        self.assertFalse(r.is_frenzied(50, 100))

    def test_acts(self):
        self.assertEqual(r.act_slots(True), [0, 2, 4])
        self.assertEqual([r.vanilla_tier(i, 5) for i in range(5)], [0, 1, 1, 2, 2])
        self.assertEqual(r.vanilla_tier(5, 6), 2)   # Безымянный предел
        self.assertEqual([r.vanilla_tier(i, 3) for i in range(3)], [0, 1, 2])
        self.assertEqual([r.slot_of(i, 3) for i in range(3)], [0, 2, 4])
        self.assertEqual(r.map_width("BloodCourt"), 9)
        self.assertEqual(r.map_width("Hive"), 7)
        self.assertEqual(r.enemy_hp_bonus_percent("Hive", True), 5)
        self.assertEqual(r.enemy_hp_bonus_percent("Hive", False), 0)
        self.assertEqual(r.enemy_hp_bonus_percent("FrozenBarrows", True), 35)
        self.assertEqual(r.elite_affix_bonus("Glory", True), 1)
        self.assertEqual(r.soul_stone_for("elite", r.slot_of(2, 3)), 3)

    def test_act_rules(self):
        self.assertEqual([r.is_night(row) for row in range(1, 8)], [False, False, False, True, True, True, False])
        self.assertTrue(r.is_overheat_round(4))
        self.assertTrue(r.is_overheat_warning_round(3))
        self.assertTrue(r.hidden_by_darkness(5, 2, False))
        self.assertFalse(r.hidden_by_darkness(5, 2, True))
        self.assertTrue(r.is_rift_trigger("AbyssRifts", 6))
        self.assertFalse(r.is_rift_trigger("AbyssRifts", 4))
        self.assertTrue(r.can_become_gate("AbyssInvasion", 5, 4, False, False, False))
        self.assertFalse(r.can_become_gate("AbyssRifts", 5, 4, False, False, False))
        self.assertEqual(r.act_pick_distinct(5, 2, [7, 7]), [2, 4])
        self.assertTrue(r.roll_rare_elite(14, False))
        self.assertFalse(r.roll_rare_elite(14, True))
        self.assertEqual(r.decode_mark(r.encode_mark(4, 8, 12)), (4, 8, 12))

    def test_superbosses(self):
        self.assertEqual(r.SUPERBOSS_HP["NamelessShadow"], 700)
        mask = sum(1 << i for i in range(5))
        self.assertEqual(r.count_superbosses(mask | (1 << 7)), 5)
        self.assertTrue(r.nameless_unlocked(mask, True))
        self.assertFalse(r.nameless_unlocked(mask, False))
        self.assertTrue(r.portal_mandatory(False, 10))
        self.assertAlmostEqual(r.binding_multiplier(5), 1.45)
        self.assertFalse(r.bypasses_ghostly(False, "physical", False))



class VanillaRelics(unittest.TestCase):
    def test_rework(self):
        self.assertEqual(r.relic_regen_penalty("Ironclad", 2), (2, 0))
        self.assertEqual(r.relic_regen_penalty("Regent", 1), (0, 1))
        self.assertTrue(r.iron_skin_active(1))
        self.assertFalse(r.iron_skin_active(0))

    def test_shipped_table(self):
        import csv, os
        path = os.path.join(os.path.dirname(__file__), "..", "..", "mod", "SlayTheRequiem", "data", "vanilla_relics.csv")
        rows = list(csv.DictReader(l for l in open(path, encoding="utf-8") if not l.startswith("#")))
        by = {row["key"]: row for row in rows}
        self.assertEqual(by["Vajra"]["replacement"], "RingOfFuryStrike")
        self.assertEqual(sum(1 for row in rows if row["change"] == "regen-1"), 12)
        self.assertTrue(all(0 <= int(row["weight"]) <= 3 for row in rows))


class DeferredEnchants(unittest.TestCase):
    def test_slots_and_values(self):
        self.assertTrue(r.enchant_allowed("CraftIllusion", "Amulet"))
        self.assertFalse(r.enchant_allowed("CraftIllusion", "Ring"))
        self.assertTrue(r.enchant_allowed("Paralysis", "MainHand"))
        self.assertEqual(r.enchant_value("CraftDestruction", 3), 3)
        self.assertEqual(r.enchant_value("AbsorbHealth", 2), 2)
        self.assertEqual(r.enchant_value("Banish", 3), 1)
        self.assertEqual(r.enchant_value("FrostResistance", 3), 2)
        self.assertEqual(r.apply_enchant([], "Banish", 2, 1), [("Banish", 2, 12)])
        self.assertEqual(r.craft_discount(2, 1), 1)
        self.assertEqual(r.craft_discount(2, 2), 0)
        self.assertEqual(r.paralysis_tier(1), 2)
        self.assertEqual(r.paralysis_tier(3), 4)


class Jewelry(unittest.TestCase):
    def test_numbers(self):
        self.assertEqual(r.drunkard_potency(2), 3)
        self.assertEqual(r.drunkard_potency(10), 15)
        self.assertEqual(r.drunkard_potency(0), 0)
        self.assertEqual(r.lightning_rod_mana(7), 3)
        self.assertTrue(r.regeneration_active(19, 40))
        self.assertFalse(r.regeneration_active(20, 40))
        self.assertEqual(r.steam_damage(4, 3), 7)
        self.assertEqual(r.steam_damage(0, 3), 0)
        self.assertEqual(r.viceroy_replays(True), 2)

    def test_combos(self):
        self.assertEqual(r.ring_combos(["RingOfHoarfrost", "RingOfThePyromancer"]), ["Steam"])
        self.assertEqual(r.ring_combos(["RingOfQuickBreath"]), [])
        self.assertEqual(r.ring_combos(["RingOfQuickBreath", "RingOfFuryStrike"]), ["QuickFury"])


class Backpack(unittest.TestCase):
    def test_kettle(self):
        self.assertEqual(r.kettle_boost({"MaxHp": 6, "Regen": 0}), {"MaxHp": 7, "Regen": 0})

    def test_barrow(self):
        self.assertEqual(r.barrow_indices(2, 3), [2, 0, 1])
        self.assertEqual(r.barrow_indices(0, 0), [])


class BossPowers(unittest.TestCase):
    def test_oath(self):
        self.assertEqual(r.oath_multiplier(40, True), 0.6)
        self.assertEqual(r.oath_multiplier(40, False), 1.0)
        self.assertEqual(r.oath_multiplier(150, True), 0.0)

    def test_stance(self):
        self.assertEqual(r.stance_multiplier(False, "Physical"), 0.5)
        self.assertEqual(r.stance_multiplier(False, "Light"), 1.0)
        self.assertEqual(r.stance_multiplier(True, "Light"), 1.5)
        self.assertEqual(r.stance_multiplier(True, "Fire"), 1.0)

    def test_constants_match_csharp(self):
        import re
        src = open(os.path.join(ROOT, "mod", "SlayTheRequiemCode", "Powers", "BossPowers.cs"), encoding="utf-8").read()
        self.assertEqual(int(re.search(r"SunStanceReductionPercent = (\d+)", src).group(1)), r.SUN_STANCE_REDUCTION_PERCENT)
        self.assertEqual(float(re.search(r"MoonStanceLightMultiplier = ([\d.]+)m", src).group(1)), r.MOON_STANCE_LIGHT_MULTIPLIER)


class ActDoors(unittest.TestCase):
    def test_has_doors(self):
        self.assertTrue(r.has_doors(1, 5))
        self.assertTrue(r.has_doors(3, 6))
        self.assertFalse(r.has_doors(2, 5))
        self.assertFalse(r.has_doors(1, 3))

    def test_resolve(self):
        for votes, cur, tie, exp in [([0, 0, 1], 1, 1, 0), ([1], 0, 0, 1), ([-1, -1], 1, 0, 1), ([], 0, 1, 0),
                                     ([0, 1], 0, 1, 1), ([0, 1, -1, -1], 1, 0, 0), ([1, -1], 0, 0, 1)]:
            self.assertEqual(r.door_resolve(votes, cur, tie), exp, (votes, cur, tie))

    def test_tie_break_matches_csharp(self):
        self.assertEqual(r.door_tie_break("ABC123", 1), 1)
        self.assertEqual(r.door_tie_break("SEED", 3), 0)


class M6Summons(unittest.TestCase):
    def test_slots_and_dismissals(self):
        self.assertEqual(r.summon_slots(0), 1)
        self.assertEqual(r.summon_slots(2), 3)
        self.assertEqual(r.summon_slots(-3), 1)
        self.assertEqual(r.summon_dismissals(1, 1), 1)
        self.assertEqual(r.summon_dismissals(1, 2), 0)
        self.assertEqual(r.summon_dismissals(3, 2), 2)
        self.assertEqual(r.summon_dismissals(0, 0), 0)

    def test_absorb_and_loss(self):
        self.assertEqual(r.summon_absorb(10, 8, 0), 8)
        self.assertEqual(r.summon_absorb(5, 8, 0), 5)
        self.assertEqual(r.summon_absorb(5, 8, 6), 2)
        self.assertEqual(r.summon_absorb(5, 8, 9), 0)
        self.assertEqual(r.summon_scaled_loss(10, 0), 10)
        self.assertEqual(r.summon_scaled_loss(10, 50), 15)
        self.assertEqual(r.summon_scaled_loss(3, 50), 5)
        self.assertEqual(r.summon_scaled_loss(0, 50), 0)

    def test_raise_dead(self):
        self.assertEqual(r.raised_dead_hp(40, 50), 20)
        self.assertEqual(r.raised_dead_hp(40, 65), 26)
        self.assertEqual(r.raised_dead_hp(1, 50), 1)
        self.assertEqual(r.raised_dead_hp(0, 50), 0)
        self.assertTrue(r.can_raise(True, False))
        self.assertFalse(r.can_raise(True, True))
        self.assertFalse(r.can_raise(False, False))

    def test_kinds_moves_art(self):
        self.assertEqual([r.beast_kind(n) for n in (-1, 0, 1, 2, 5)], ["BeastWolf", "BeastWolf", "BeastSabertooth", "BeastBear", "BeastBear"])
        self.assertEqual(r.summon_move("AbyssGuardian", 0), "Slumber")
        self.assertEqual(r.summon_move("AbyssGuardian", 1), "Strike")
        self.assertEqual(r.summon_move("SpiritWolf", 0), "Strike")
        self.assertEqual([k for k in r.SUMMON_KINDS if r.summon_is_undead(k)], ["RaisedDead", "BoneThrall"])
        self.assertEqual([k for k in r.SUMMON_KINDS if r.summon_is_companion(k)], ["LoyalGuard"])
        self.assertEqual(r.summon_art_key("BeastSabertooth"), "summon_beast_sabertooth")
        self.assertEqual(r.summon_layout_x(100, 0, False), 160)
        self.assertEqual(r.summon_layout_x(100, 2, True), 680)

    def test_matches_csharp(self):
        import re
        src = open(os.path.join(ROOT, "mod", "SlayTheRequiemCode", "Core", "SummonRules.cs"), encoding="utf-8").read()
        consts = {k: int(v) for k, v in re.findall(r"public const int (\w+) = (\d+);", src)}
        self.assertEqual(consts["BaseSlots"], r.SUMMON_BASE_SLOTS)
        self.assertEqual((consts["RaiseDeadPercent"], consts["RaiseDeadPercentUpgrade"], consts["RaisedDeadDamage"]),
                         (r.RAISE_DEAD_PERCENT, r.RAISE_DEAD_PERCENT_UPGRADE, r.RAISED_DEAD_DAMAGE))
        self.assertEqual((consts["AbyssGuardianHp"], consts["AbyssGuardianHpUpgrade"], consts["AbyssGuardianDamage"], consts["AbyssGuardianSlumberTurns"]),
                         (r.ABYSS_GUARDIAN_HP, r.ABYSS_GUARDIAN_HP_UPGRADE, r.ABYSS_GUARDIAN_DAMAGE, r.ABYSS_GUARDIAN_SLUMBER_TURNS))
        self.assertEqual((consts["SpectralKnightHp"], consts["SpectralKnightHpUpgrade"], consts["SpectralKnightDamage"], consts["SpectralKnightAttackBonus"]),
                         (r.SPECTRAL_KNIGHT_HP, r.SPECTRAL_KNIGHT_HP_UPGRADE, r.SPECTRAL_KNIGHT_DAMAGE, r.SPECTRAL_KNIGHT_ATTACK_BONUS))
        self.assertEqual((consts["BoneThrallHp"], consts["BoneThrallDamage"]), (r.BONE_THRALL_HP, r.BONE_THRALL_DAMAGE))
        kinds = re.search(r"enum SummonKind\s*\{([^}]*)\}", src).group(1)
        self.assertEqual([k.strip() for k in kinds.split(",") if k.strip()], r.SUMMON_KINDS)
        for kind in r.SUMMON_KINDS:
            self.assertIn(f'SummonKind.{kind} => "{r.summon_art_key(kind)}"' if kind != "BoneThrall" else '_ => "summon_bone_thrall"', src)

    def test_every_kind_has_sprite_and_creature(self):
        creatures = open(os.path.join(ROOT, "mod", "SlayTheRequiemCode", "Monsters", "SummonCreatures.cs"), encoding="utf-8").read()
        for kind in r.SUMMON_KINDS:
            key = r.summon_art_key(kind)
            self.assertTrue(os.path.exists(os.path.join(ROOT, "art", "monsters", key + ".svg")), key)
            self.assertTrue(os.path.exists(os.path.join(ROOT, "mod", "SlayTheRequiem", "images", "monsters", key + ".png")), key)
            self.assertIn(f"public sealed class {kind}Summon : SummonCreature", creatures)
            self.assertIn(f'ArtKey => "{key}"', creatures)


class M6HeroLooks(unittest.TestCase):
    CODE = os.path.join(ROOT, "mod", "SlayTheRequiemCode")

    def test_looks(self):
        self.assertEqual(r.hero_look("Lich", False), "Lich")
        self.assertEqual(r.hero_look("Vampire", False), "Vampire")
        self.assertEqual(r.hero_look("Werewolf", False), "Werewolf")
        self.assertEqual(r.hero_look("Righteous", True), "Clockwork")
        self.assertEqual(r.hero_look("None", True), "Clockwork")
        self.assertEqual(r.hero_look("Herald", False), "Hero")
        self.assertEqual(r.hero_look("DarkPact", False), "Hero")
        self.assertEqual([l for l in r.HERO_LOOKS if r.look_borrows_model(l)], ["Lich", "Clockwork"])
        self.assertEqual([l for l in r.HERO_LOOKS if r.look_sprite_key(l)], ["Vampire", "Werewolf"])
        self.assertEqual(r.look_sprite_variant("Werewolf", True, False), "beast")
        self.assertIsNone(r.look_sprite_variant("Werewolf", False, True))
        self.assertEqual(r.look_sprite_variant("Vampire", False, True), "lord")
        self.assertIsNone(r.look_sprite_variant("Lich", True, True))
        self.assertEqual([l for l in r.HERO_LOOKS if r.look_keeps_tint(l)], ["Hero"])
        self.assertEqual(r.look_beast_scale("Werewolf"), 1.0)
        self.assertEqual(r.look_beast_scale("Hero"), r.LOOK_BEAST_SCALE)

    def test_orbs(self):
        self.assertEqual(r.orb_sequence(1, 1, 1), ["Fire", "Shock", "Frost"])
        self.assertEqual(r.orb_sequence(0, 5, 0), ["Shock"] * 3)
        self.assertEqual(r.orb_sequence(-1, 0, 2), ["Frost", "Frost"])
        self.assertEqual([r.orb_art(e) for e in ("Fire", "Shock", "Frost")], ["PlasmaOrb", "LightningOrb", "FrostOrb"])
        x, y = r.orb_slot(0, 3, True)
        self.assertAlmostEqual(x, 194.86, places=1)
        self.assertAlmostEqual(y, -112.5, places=1)
        x, y = r.orb_slot(2, 3, True)
        self.assertAlmostEqual(x, -203.92, places=1)
        self.assertAlmostEqual(y, -95.09, places=1)
        x, y = r.orb_slot(0, 3, False)
        self.assertAlmostEqual(y, -112.5 * 0.75, places=1)
        self.assertEqual(r.orb_slot(0, 1, True), r.orb_slot(5, 1, True))

    def test_path_signs(self):
        self.assertEqual(r.path_sign_key("BlackKnight"), "black_knight")
        self.assertEqual(r.path_sign_key("MysticArcher"), "mystic_archer")
        self.assertIsNone(r.path_sign_key("None"))
        self.assertIsNone(r.path_sign_key("Reborn"))
        import re
        signs = open(os.path.join(self.CODE, "Transformations", "PathSigns.cs"), encoding="utf-8").read()
        paths = re.findall(r"HeroPath Path => HeroPath\.(\w+);", signs)
        self.assertEqual(len(paths), 12)
        self.assertEqual(len(set(paths)), 12)
        for path in paths:
            key = r.path_sign_key(path)
            self.assertTrue(os.path.exists(os.path.join(ROOT, "art", "ui", "path_signs", key + ".svg")), key)
            self.assertTrue(os.path.exists(os.path.join(ROOT, "mod", "SlayTheRequiem", "images", "ui", "path_signs", key + ".png")), key)

    def test_sprites_exist(self):
        for look in r.HERO_LOOKS:
            key = r.look_sprite_key(look)
            if not key:
                continue
            variants = [None, r.look_sprite_variant(look, True, True)]
            for v in variants:
                name = key + (f"_{v}" if v else "")
                self.assertTrue(os.path.exists(os.path.join(ROOT, "art", "monsters", name + ".svg")), name)
                self.assertTrue(os.path.exists(os.path.join(ROOT, "mod", "SlayTheRequiem", "images", "monsters", name + ".png")), name)

    def test_matches_csharp(self):
        import re
        src = open(os.path.join(self.CODE, "Core", "HeroLookRules.cs"), encoding="utf-8").read()
        looks = re.search(r"enum HeroLook\s*\{([^}]*)\}", src).group(1)
        self.assertEqual([k.strip() for k in looks.split(",") if k.strip()], r.HERO_LOOKS)
        rules = open(os.path.join(self.CODE, "Core", "TransformationRules.cs"), encoding="utf-8").read()
        paths = re.search(r"enum HeroPath\s*\{([^}]*)\}", rules).group(1)
        self.assertEqual([k.strip() for k in paths.split(",") if k.strip()], r.HERO_PATHS)
        self.assertIn(f"MaxOrbs = {r.LOOK_MAX_ORBS};", rules)
        self.assertIn(f"BeastScale = {r.LOOK_BEAST_SCALE}f;", src)
        for look in ("Vampire", "Werewolf"):
            self.assertIn(f'HeroLook.{look} => "{r.look_sprite_key(look)}"', src)
        for element, art in (("Fire", "PlasmaOrb"), ("Shock", "LightningOrb")):
            self.assertIn(f'Element.{element} => "{art}"', src)


class M6PaperDoll(unittest.TestCase):
    CODE = os.path.join(ROOT, "mod", "SlayTheRequiemCode")

    def test_material(self):
        self.assertEqual([r.doll_material(t) for t in (0, 1, 2, 3, 4, 5, 9)],
                         ["Iron", "Iron", "Steel", "Silver", "Obsidian", "EternalIce", "EternalIce"])
        self.assertEqual(r.doll_material(5, abyssal=True), "Abyssal")
        self.assertEqual(r.doll_material(1, abyssal=True, explicit="ScaledSteel"), "ScaledSteel")
        self.assertEqual(r.doll_material_color("None"), r.doll_material_color("Iron"))
        self.assertEqual([m for m in r.DOLL_MATERIAL_COLOR if r.doll_glint(m)], ["Silver", "Obsidian", "EternalIce"])
        self.assertEqual([m for m in r.DOLL_MATERIAL_COLOR if r.doll_smoke(m)], ["Abyssal"])
        # множитель не гасит рисунок: каждый канал > 0,1, хотя бы один ≥ 0,45
        for m, c in r.DOLL_MATERIAL_COLOR.items():
            self.assertTrue(all(0.1 < x <= 1 for x in c) and max(c) >= 0.45, m)

    def test_aura(self):
        self.assertEqual(r.doll_aura([]), ("None", "None", 0.0))
        self.assertEqual(r.doll_aura([("None", 3, "None")]), ("None", "None", 0.0))
        cls, mat, strength = r.doll_aura([("Heavy", 1, "None"), ("Heavy", 3, "None"), ("Light", 5, "None")])
        self.assertEqual((cls, mat), ("Heavy", "Silver"))
        self.assertAlmostEqual(strength, 0.85)
        self.assertEqual(r.doll_aura([("Robe", 2, "None"), ("Light", 1, "None")])[0], "Light")
        self.assertEqual(r.doll_aura([("Robe", 2, "None"), ("Heavy", 1, "None")])[0], "Heavy")
        self.assertEqual(r.doll_aura([("Robe", 2, "Abyssal")])[1], "Abyssal")
        self.assertAlmostEqual(r.doll_aura([("Light", 1, "Iron")] * 4)[2], 1.0)
        self.assertAlmostEqual(r.doll_aura([("Light", 1, "Iron")])[2], 0.55)

    def test_hands(self):
        self.assertEqual([k for k in r.DOLL_WEAPON_ART if r.doll_blocks_off_hand(k)], ["TwoHanded", "Bow", "Crossbow"])
        self.assertIsNone(r.doll_weapon_art("None"))
        self.assertTrue(r.doll_shows_weapons(False, False))
        self.assertFalse(r.doll_shows_weapons(True, False))
        self.assertFalse(r.doll_shows_weapons(False, True))
        w, h = r.DOLL_CANVAS
        for kind in r.DOLL_WEAPON_ART:
            x, y = r.doll_pivot(kind)
            self.assertTrue(0 < x < w and 0 < y < h, kind)
            self.assertTrue(0.5 <= r.doll_scale(kind) <= 1.0, kind)
            self.assertLess(abs(r.doll_rest_angle(kind)), 0.6, kind)

    def test_art_exists(self):
        for art in r.DOLL_WEAPON_ART.values():
            for layer in ("", "_metal"):
                self.assertTrue(os.path.exists(os.path.join(ROOT, "art", "ui", "doll", art + layer + ".svg")), art + layer)
                self.assertTrue(os.path.exists(os.path.join(ROOT, "mod", "SlayTheRequiem", "images", "ui", "doll", art + layer + ".png")), art + layer)

    def test_matches_doll_art(self):
        import sys
        sys.path.insert(0, os.path.join(ROOT, "tools", "art", "monsters"))
        import doll
        self.assertEqual((doll.DOLL_W, doll.DOLL_H), r.DOLL_CANVAS)
        self.assertEqual(sorted(doll.DOLL), sorted(r.DOLL_WEAPON_ART.values()))
        for kind, art in r.DOLL_WEAPON_ART.items():
            self.assertEqual(tuple(doll.PIVOTS.get(art, doll.GRIP)), r.doll_pivot(kind), kind)

    def test_matches_csharp(self):
        import re
        src = open(os.path.join(self.CODE, "Core", "PaperDollRules.cs"), encoding="utf-8").read()
        for m, (cr, cg, cb) in r.DOLL_MATERIAL_COLOR.items():
            nums = re.search(rf"ItemMaterial\.{m} => \(([^)]*)\)", src)
            if m == "Iron":
                nums = re.search(r"_ => \(([\d.f, ]*)\)", src)
            vals = [float(v.strip().rstrip("f")) for v in nums.group(1).split(",")]
            self.assertEqual([round(v, 2) for v in vals], [cr, cg, cb], m)
        for kind, art in r.DOLL_WEAPON_ART.items():
            self.assertIn(f'WeaponKind.{kind} => "{art}"', src)
        for cls, (style, alpha, period) in r.DOLL_AURA.items():
            self.assertIn(f"ArmorClass.{cls} => AuraStyle.{style}", src)
            self.assertIn(f"AuraStyle.{style} => {alpha}f", src)
            self.assertIn(f"AuraStyle.{style} => {period}f", src)
        w, h = r.DOLL_CANVAS
        self.assertIn(f"CanvasWidth = {w};", src)
        self.assertIn(f"CanvasHeight = {h};", src)
        self.assertIn("0.4f + 0.15f * list.Count", src)
        items = open(os.path.join(self.CODE, "Equipment", "Items.cs"), encoding="utf-8").read()
        self.assertIn("Material => ItemMaterial.ScaledSteel", items)


class M6ShopUi(unittest.TestCase):
    CODE = os.path.join(ROOT, "mod", "SlayTheRequiemCode")

    def test_tiers_match_burden(self):
        for excess in range(-3, 20):
            self.assertEqual(r.overload_tier(excess) > 0, excess > 0)
        self.assertEqual([r.overload_tier(e) for e in (0, 1, 5, 6, 10, 11)], [0, 1, 1, 2, 2, 3])

    def test_preview_and_warning(self):
        p = r.burden_preview(18, 20, 2)
        self.assertEqual((p["after"], p["tier_after"], p["worsens"]), (20, 0, False))
        self.assertFalse(r.needs_overload_warning(p))
        p = r.burden_preview(19, 20, 2)
        self.assertTrue(p["worsens"] and r.needs_overload_warning(p))
        self.assertEqual(p["excess_after"], 1)
        p = r.burden_preview(22, 20, 3)  # 2 → 5: та же ступень
        self.assertFalse(r.needs_overload_warning(p))
        p = r.burden_preview(25, 20, 1)  # 5 → 6
        self.assertTrue(r.needs_overload_warning(p))
        p = r.burden_preview(31, 20, -2)  # продажа: 11 → 9
        self.assertTrue(p["improves"])
        self.assertFalse(r.needs_overload_warning(p))
        self.assertFalse(r.needs_overload_warning(r.burden_preview(40, 20, 0)))
        self.assertEqual(r.burden_preview(1, 20, -5)["after"], 0)

    def test_tabs(self):
        self.assertTrue(r.shop_tab_enabled("Buy", 0, 0))
        self.assertFalse(r.shop_tab_enabled("Sell", 0, 3))
        self.assertTrue(r.shop_tab_enabled("Sell", 1, 0))
        self.assertFalse(r.shop_tab_enabled("Services", 4, 0))
        self.assertEqual([r.shop_tab_badge(t, 2, 5) for t in r.SHOP_TABS], [0, 2, 5])

    def test_sell_order_and_percent(self):
        self.assertEqual(r.shop_sell_order([(10, 1), (30, 2), (30, 4), (10, 1)]), [2, 1, 0, 3])
        self.assertEqual(r.shop_sell_order([]), [])
        self.assertEqual([r.sell_percent(t, False) for t in (0, 2, 3)], [35, 35, 50])
        self.assertEqual(r.sell_percent(0, True), 60)
        # подпись совпадает с реальной выручкой
        for price in (40, 100, 333):
            self.assertEqual(r.sell_value(price, 0), max(1, price * 35 // 100))
            self.assertEqual(r.fence_sell_value(price), max(1, price * 60 // 100))

    def test_matches_csharp(self):
        src = open(os.path.join(self.CODE, "Core", "ShopUiRules.cs"), encoding="utf-8").read()
        import re
        tabs = re.search(r"enum ShopTab\s*\{([^}]*)\}", src).group(1)
        self.assertEqual([t.strip() for t in tabs.split(",") if t.strip()], r.SHOP_TABS)
        self.assertIn(f"SellRowsVisible = {r.SHOP_SELL_ROWS_VISIBLE};", src)
        self.assertIn("preview.Delta > 0 && preview.Worsens", src)
        shop = open(os.path.join(self.CODE, "Core", "ShopRules.cs"), encoding="utf-8").read()
        self.assertIn("fence ? 60 : trust >= MaxTrust ? 50 : 35", shop)
        tabs_cs = open(os.path.join(self.CODE, "Shops", "ShopTabs.cs"), encoding="utf-8").read()
        for tab in r.SHOP_TABS:
            self.assertIn(f"ShopTab.{tab}", tabs_cs)
        services = open(os.path.join(self.CODE, "Shops", "ShopServices.cs"), encoding="utf-8").read()
        self.assertNotIn('HarmonyPatch(typeof(NMerchantInventory), "Close")', services)


class M7SuperbossLooks(unittest.TestCase):
    ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
    CODE = os.path.join(ROOT, "mod", "SlayTheRequiemCode")

    def test_variants(self):
        self.assertEqual(r.superboss_variant("BlackWanderer", True, False, False), "guard")
        self.assertIsNone(r.superboss_variant("BlackWanderer", False, True, True))
        self.assertEqual(r.superboss_variant("BloodCourtLord", False, True, False), "cocoon")
        self.assertIsNone(r.superboss_variant("BloodCourtLord", True, False, True))
        self.assertIsNone(r.superboss_variant("WorldEater", False, False, True))
        self.assertEqual(r.superboss_variant("WorldEater", False, False, False), "landed")
        self.assertIsNone(r.superboss_variant("NamelessShadow", False, True, False))
        self.assertEqual(r.superboss_variant("NamelessShadow", False, False, False), "exposed")
        for b in ("BarrowReaper", "DeepMasterSmith", "GatebreakerEnvoy", "FirstSummoner"):
            self.assertIsNone(r.superboss_variant(b, True, True, False))
        # каждый возможный вариант объявлен для подгрузки
        for b in r.SUPERBOSSES:
            got = {r.superboss_variant(b, g, i, f) for g in (0, 1) for i in (0, 1) for f in (0, 1)} - {None}
            self.assertEqual(got, set(r.SUPERBOSS_VARIANTS.get(b, [])), b)

    def test_art_files(self):
        img = os.path.join(self.ROOT, "mod", "SlayTheRequiem", "images", "monsters")
        svg = os.path.join(self.ROOT, "art", "monsters")
        keys = [r.ECHO_KNIGHT_ART] + list(r.SUPERBOSS_ART.values())
        keys += [f"{r.SUPERBOSS_ART[b]}_{v}" for b, vs in r.SUPERBOSS_VARIANTS.items() for v in vs]
        for k in keys:
            self.assertTrue(os.path.isfile(os.path.join(img, k + ".png")), k)
            self.assertTrue(os.path.isfile(os.path.join(svg, k + ".svg")), k)
        # варианты на том же холсте — смена картинки не двигает хитбокс
        import struct
        def size(path):
            with open(path, "rb") as f:
                head = f.read(24)
            return struct.unpack(">II", head[16:24])
        for b, vs in r.SUPERBOSS_VARIANTS.items():
            base = size(os.path.join(img, r.SUPERBOSS_ART[b] + ".png"))
            for v in vs:
                self.assertEqual(size(os.path.join(img, f"{r.SUPERBOSS_ART[b]}_{v}.png")), base, (b, v))

    def test_court_tints(self):
        self.assertEqual(set(r.COURT_TINT), set(r.SUPERBOSSES))
        for b, (h, *rgb) in r.COURT_TINT.items():
            self.assertTrue(0 <= h < 1, b)
            self.assertTrue(all(r.COURT_MIN_TINT <= c <= 1 for c in rgb), b)
            self.assertGreaterEqual(max(rgb), 0.75, b)  # цвет фракции, а не просто потемнение (Тень — нарочно тёмная)
        self.assertEqual(r.court_of("GargoyleServant"), "BloodCourtLord")
        self.assertIsNone(r.court_of("AbyssGateObject"))  # Врата общие с элитами Бездны
        self.assertAlmostEqual(r.combined_hue(0.96, 0.05), 0.01, places=5)
        self.assertAlmostEqual(r.combined_hue(0.6, 0.0), 0.6)
        # свита каждого супербосса, кроме Посланника (у него только общие Врата), перекрашена
        self.assertEqual(set(r.SUPERBOSS_COURT.values()), set(r.SUPERBOSSES) - {"GatebreakerEnvoy"})

    def test_matches_csharp(self):
        import re
        src = open(os.path.join(self.CODE, "Core", "SuperbossLookRules.cs"), encoding="utf-8").read()
        acts = open(os.path.join(self.CODE, "Core", "ActRules.cs"), encoding="utf-8").read()
        enum = re.search(r"enum Superboss\s*\{([^}]*)\}", acts).group(1)
        self.assertEqual([x.strip() for x in re.sub(r"///.*", "", enum).split(",") if x.strip()], r.SUPERBOSSES)
        for b, key in r.SUPERBOSS_ART.items():
            self.assertRegex(src, rf'(Superboss\.{b} =>|_ =>) "{key}"')
        self.assertIn(f'EchoKnightArt = "{r.ECHO_KNIGHT_ART}"', src)
        for b, (h, rr, g, bb) in r.COURT_TINT.items():
            self.assertRegex(src, rf"(Superboss\.{b} =>|_ =>) new\({h:.2f}f, {rr:.2f}f, {g:.2f}f, {bb:.2f}f\)")
        for m, b in r.SUPERBOSS_COURT.items():
            self.assertIn(f'"{m}"', src)
        self.assertIn(f"MinTintChannel = {r.COURT_MIN_TINT}f", src)
        bosses = open(os.path.join(self.CODE, "Monsters", "Superbosses.cs"), encoding="utf-8").read()
        self.assertIn("abstract class SuperbossMonster : OwnArtMonster", bosses)
        self.assertIn("class EchoKnight : OwnArtMonster", bosses)
        self.assertGreaterEqual(bosses.count("RefreshLook();"), 7)
        # каждая свита из таблицы — реальный класс-миньон
        for m in r.SUPERBOSS_COURT:
            self.assertIn(f"class {m} : RequiemMinion", bosses)
        sprites = open(os.path.join(self.ROOT, "tools", "art", "monsters", "superbosses.py"), encoding="utf-8").read()
        for b, key in r.SUPERBOSS_ART.items():
            vs = tuple([""] + r.SUPERBOSS_VARIANTS.get(b, []))
            self.assertIn(f'"{key}": (draw_{key}, {vs!r})'.replace("'", '"'), sprites)


class M7ActLooks(unittest.TestCase):
    ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
    CODE = os.path.join(ROOT, "mod", "SlayTheRequiemCode")

    @staticmethod
    def size(path):
        import struct
        with open(path, "rb") as f:
            data = f.read()
        if data[:8] == b"\x89PNG\r\n\x1a\n":
            return struct.unpack(">II", data[16:24])
        i = 2  # JPEG: ищем SOF0/SOF2
        while i < len(data):
            marker, length = data[i + 1], struct.unpack(">H", data[i + 2:i + 4])[0]
            if marker in (0xC0, 0xC2):
                h, w = struct.unpack(">HH", data[i + 5:i + 9])
                return w, h
            i += 2 + length
        raise ValueError(path)

    def test_paths(self):
        self.assertEqual(r.act_piece_path("BloodCourt", "camp.jpg"), "acts/blood_court/camp.jpg")
        for vanilla in ("Foothills", "Hive", "Glory", "Other"):
            self.assertIsNone(r.act_piece_path(vanilla, "combat.jpg"))
        self.assertEqual(set(r.ACT_CHEST_TINT), set(r.ACT_ART))
        self.assertEqual(set(r.ACT_PIECE_SIZE), set(r.ACT_PIECES))

    def test_camp_nodes(self):
        self.assertEqual(r.camp_target("underdocks"), "RestSiteBG2")
        self.assertEqual(r.camp_target("glory"), "RestSiteBG")
        for keep in ("RestSiteLighting", "RestSiteFireLogs3", "RestSiteLLog", "light_small3", "RestSiteBG"):
            self.assertTrue(r.camp_keeps(keep, "RestSiteBG"), keep)
        for hide in ("RestSiteForeground2", "Foliage4", "overlay_vfx", "water_reflection6", "stars", "b_blob", "RestSiteBG"):
            self.assertFalse(r.camp_keeps(hide, "RestSiteBG2"), hide)

    def test_files(self):
        img = os.path.join(self.ROOT, "mod", "SlayTheRequiem", "images")
        for kind, key in r.ACT_ART.items():
            for piece in r.ACT_PIECES:
                path = os.path.join(img, r.act_piece_path(kind, piece))
                self.assertTrue(os.path.isfile(path), path)
                self.assertEqual(self.size(path), r.ACT_PIECE_SIZE[piece], path)
                svg = os.path.join(self.ROOT, "art", "acts", key, os.path.splitext(piece)[0] + ".svg")
                self.assertTrue(os.path.isfile(svg), svg)
        # слой боя сохраняет пропорции текстуры (иначе фон растянется)
        self.assertAlmostEqual(r.COMBAT_HALF[0] / r.COMBAT_HALF[1], 2048 / 960, places=3)
        x0, y0, x1, y1 = r.CAMP_RECT
        self.assertAlmostEqual((x1 - x0) / (y1 - y0), 3072 / 1440, places=2)

    def test_matches_csharp(self):
        src = open(os.path.join(self.CODE, "Core", "ActLookRules.cs"), encoding="utf-8").read()
        for kind, key in r.ACT_ART.items():
            self.assertIn(f'ActKind.{kind} => "{key}"', src)
        for kind, (rr, g, b, hexc) in r.ACT_CHEST_TINT.items():
            self.assertIn(f'ActKind.{kind} => new({rr:.2f}f, {g:.2f}f, {b:.2f}f, "{hexc}")', src)
        for piece in r.ACT_PIECES:
            self.assertIn(f'"{piece}"', src)
        self.assertIn(f"CombatHalfWidth = {r.COMBAT_HALF[0]}f", src)
        self.assertIn(f"CombatHalfHeight = {r.COMBAT_HALF[1]}f", src)
        self.assertIn("CampRect = [-444f, -139f, 2320.8f, 1157f]", src)
        looks = open(os.path.join(self.CODE, "Acts", "ActLooks.cs"), encoding="utf-8").read()
        for patch in ("EncounterModel), nameof(EncounterModel.CreateBackground)", "ActModel), nameof(ActModel.CreateRestSiteBackground)",
                      "NTreasureRoom), nameof(NTreasureRoom._Ready)"):
            self.assertIn(patch, looks)
        acts = open(os.path.join(self.CODE, "Acts", "RequiemActs.cs"), encoding="utf-8").read()
        self.assertEqual(acts.count("ActLooks.MapPiece("), 6)
        gen = open(os.path.join(self.ROOT, "tools", "art", "acts", "build_acts.py"), encoding="utf-8").read()
        for key in r.ACT_ART.values():
            self.assertIn(f'"{key}"', gen)


class M7OriginLooks(unittest.TestCase):
    ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
    CODE = os.path.join(ROOT, "mod", "SlayTheRequiemCode")

    def classes(self):
        import re
        out = []
        for name in ("Origins.cs", "Backgrounds.cs"):
            src = open(os.path.join(self.CODE, "Relics", name), encoding="utf-8").read()
            out += re.findall(r"public sealed class (\w+(?:Origin|Background))\b", src)
        return out

    @staticmethod
    def snake(name):
        import re
        return re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", name).lower()

    def test_headings(self):
        self.assertEqual(r.origin_heading(["FrostbornOrigin", "CitadelOrigin"]), "ORIGIN_PICK.clan")
        self.assertEqual(r.origin_heading(["MoonbornOrigin", "MarshbornOrigin", "ThicketbornOrigin"]), "ORIGIN_PICK.tribe")
        self.assertEqual(r.origin_heading(["AshbornOrigin"]), "ORIGIN_PICK.dynasty")
        self.assertEqual(r.origin_heading(["SkaldBackground", "VagrantBackground"]), "ORIGIN_PICK.background")
        self.assertIsNone(r.origin_heading([]))
        self.assertIsNone(r.origin_heading(["FrostbornOrigin", "MoonbornOrigin"]))  # разные герои — экран обычный
        self.assertIsNone(r.origin_heading(["Anchor", "FrostbornOrigin"]))
        self.assertIsNone(r.origin_heading(["Anchor", "Vajra"]))

    def test_every_origin_and_background_has_icons(self):
        import struct
        classes = self.classes()
        self.assertEqual(len([c for c in classes if c.endswith("Origin")]), 10)
        self.assertEqual(len([c for c in classes if c.endswith("Background")]), 31)
        self.assertEqual({c for c in classes if c.endswith("Origin")}, set(r.ORIGIN_CREST))
        img = os.path.join(self.ROOT, "mod", "SlayTheRequiem", "images", "relics")

        def size(path):
            with open(path, "rb") as f:
                return struct.unpack(">II", f.read(24)[16:24])
        for c in classes:
            key = self.snake(c)
            for kind, path in (("icon", os.path.join(img, key + ".png")), ("outline", os.path.join(img, key + "_outline.png")),
                               ("big", os.path.join(img, "big", key + ".png"))):
                self.assertTrue(os.path.isfile(path), path)
                n = r.RELIC_ICON_SIZES[kind]
                self.assertEqual(size(path), (n, n), path)
            self.assertTrue(os.path.isfile(os.path.join(self.ROOT, "art", "relics", key + ".svg")), key)
        gen = open(os.path.join(self.ROOT, "tools", "art", "relics", "draw_origins.py"), encoding="utf-8").read()
        for c in classes:
            self.assertIn(f'"{self.snake(c)}":', gen)

    def test_matches_csharp(self):
        src = open(os.path.join(self.CODE, "Core", "OriginLookRules.cs"), encoding="utf-8").read()
        origins = open(os.path.join(self.CODE, "Relics", "Origins.cs"), encoding="utf-8").read()
        for crest, key in r.ORIGIN_HEADING.items():
            members = [o for o, c in r.ORIGIN_CREST.items() if c == crest]
            listed = ", ".join(f'"{o}"' for o in members)
            self.assertIn(f"{crest}Origins = [{listed}]", src)
            self.assertIn(f'OriginCrest.{crest} => "{key}"' if crest != "Dynasty" else f'_ => "{key}"', src)
            # тот же состав, что у экрана выбора героя в Origins.cs
            self.assertIn(", ".join(f"typeof({o})" for o in members), origins)
        self.assertIn('return "ORIGIN_PICK.background"', src)
        self.assertIn("NChooseARelicSelection), nameof(NChooseARelicSelection._Ready)",
                      open(os.path.join(self.CODE, "Relics", "OriginLooks.cs"), encoding="utf-8").read())
        import sys
        sys.path.insert(0, os.path.join(self.ROOT, "tools", "loc"))
        from loc_m7origins import ORIGIN_TIPS
        for lang in ("eng", "rus"):
            self.assertEqual(set(ORIGIN_TIPS[lang]), set(r.ORIGIN_HEADING.values()) | {"ORIGIN_PICK.background"})


class M7VfxLooks(unittest.TestCase):
    ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
    CODE = os.path.join(ROOT, "mod", "SlayTheRequiemCode")

    def src(self, *parts):
        return open(os.path.join(self.CODE, *parts), encoding="utf-8").read()

    def test_hit_rules(self):
        self.assertIsNone(r.vfx_hit("Physical", True, 10))
        self.assertEqual(r.vfx_hit("Fire", True, 0), ("FireBurst", "FF7A2E", 1.0))  # атака в блок — эффект есть
        self.assertIsNone(r.vfx_hit("Fire", False, 0))  # тик в ноль — нет
        self.assertEqual(r.vfx_hit("Poison", False, 3), ("PoisonCloud", "7BC043", 0.6))
        self.assertEqual(r.vfx_hit("Blight", True, 5)[1], "8C9A4E")  # споры светлее числа урона
        self.assertEqual(len({r.vfx_hit(t, True, 1)[0] for t in r.VFX_HIT_KIND}), 8)  # 8 разных эффектов
        self.assertFalse(r.vfx_throttled(0, 50))
        self.assertTrue(r.vfx_throttled(1000, 1149))
        self.assertFalse(r.vfx_throttled(1000, 1150))
        self.assertEqual((r.vfx_shout(True, False), r.vfx_shout(False, True), r.vfx_shout(False, False)), ("War", "Rally", "Guard"))
        self.assertTrue(r.vfx_portal_pulse(True, True))
        self.assertFalse(r.vfx_portal_pulse(True, False))
        self.assertFalse(r.vfx_portal_pulse(False, True))

    def test_rewind_cracks(self):
        import math
        cracks = r.vfx_rewind_cracks(12345)
        self.assertEqual(len(cracks), 7)
        for crack in cracks:
            self.assertEqual(len(crack), 5)
            radii = [math.hypot(x, y) for x, y in crack]
            self.assertTrue(all(b > a for a, b in zip(radii, radii[1:])))  # от центра к краю
            self.assertTrue(all(-1 <= c <= 1 for p in crack for c in p))
        self.assertEqual(cracks, r.vfx_rewind_cracks(12345))
        self.assertNotEqual(cracks, r.vfx_rewind_cracks(54321))
        # трещины расходятся во все стороны: углы первых точек по возрастанию в пределах круга
        angles = [math.atan2(c[0][1], c[0][0]) % math.tau for c in cracks]
        self.assertEqual(angles, sorted(angles))

    def test_matches_csharp(self):
        import re
        src = self.src("Core", "VfxRules.cs")
        for t, hexv in r.VFX_DAMAGE_TINT.items():
            self.assertIn(f'DamageType.{t} => "{hexv}"', src)
        for t, kind in r.VFX_HIT_KIND.items():
            self.assertIn(f"DamageType.{t} => HitFxKind.{kind}", src)
        self.assertIn(f"HitThrottleMs = {r.VFX_HIT_THROTTLE_MS};", src)
        self.assertIn(f"TickScale = {r.VFX_TICK_SCALE}f;", src)
        self.assertIn(f'"{r.VFX_BLIGHT_FX_TINT}"', src)
        for name, sec in r.VFX_SECONDS.items():
            self.assertIn(f"{name}Seconds = {sec}f;", src)
        for fx, hexv in r.VFX_SHOUT_TINT.items():
            self.assertIn(f'ShoutFx.{fx} => "{hexv}"' if fx != "Guard" else f'_ => "{hexv}"', src)
        kinds = re.findall(r"^\s+(\w+),", self.src("Core", "SummonRules.cs").split("enum SummonKind")[1].split("}")[0], re.M)
        self.assertEqual(set(kinds), set(r.VFX_SUMMON_TINT))
        for kind, hexv in r.VFX_SUMMON_TINT.items():
            line = next((l for l in src.splitlines() if f"SummonKind.{kind}" in l), None)
            self.assertEqual(hexv, "FFE680" if line is None else re.search(r'"([0-9A-F]{6})"', line).group(1), kind)
        self.assertIn(f'PortalTint = "{r.VFX_PORTAL_TINT}"', src)
        self.assertIn("state * 1664525u + 1013904223u", src)
        # цвет числа урона берётся из тех же правил
        self.assertIn("VfxRules.DamageTint(type)", self.src("UI", "RequiemUi.cs"))

    def test_wiring(self):
        """Эффекты подключены к событиям, настройка и надпись «Уворот!» есть."""
        self.assertIn("RequiemVfx.Overstrain(player.Creature)", self.src("Rules", "RequiemCombatRules.cs"))
        self.assertIn("RequiemVfx.Quiet()", self.src("Rules", "RequiemCombatRules.cs"))
        self.assertIn("RequiemVfx.Broken(Owner)", self.src("Powers", "EnemyPowers.cs"))
        self.assertIn("RequiemVfx.Dodge(Owner)", self.src("Powers", "ArmorPowers.cs"))
        self.assertIn("RequiemVfx.Summon(pet, Kind)", self.src("Powers", "SummonPowers.cs"))
        self.assertIn("RequiemVfx.Shout(", self.src("Progression", "HeroRecord.cs"))
        self.assertIn("RequiemVfx.Rewind(", self.src("Rollback", "MemoryStone.cs"))
        self.assertEqual(self.src("Rollback", "CoopMemoryStone.cs").count("RequiemVfx.Rewind("), 2)
        patches = self.src("UI", "RequiemVfxPatches.cs")
        for target in ("CombatHistory), nameof(CombatHistory.DamageReceived)", "NDamageBlockedVfx), nameof(NDamageBlockedVfx._Ready)",
                       "NBossMapPoint), nameof(NBossMapPoint._Ready)", "NCombatRoom), nameof(NCombatRoom._Ready)"):
            self.assertIn(target, patches)
        self.assertIn("public static bool CombatVfx { get; set; } = true;", self.src("Config", "RequiemConfig.cs"))
        sys.path.insert(0, os.path.join(self.ROOT, "tools", "loc"))
        from loc_m7vfx import VFX_TIPS
        self.assertEqual(VFX_TIPS["rus"]["VFX.dodge"], "Уворот!")
        for lang in ("eng", "rus"):
            path = os.path.join(self.ROOT, "mod", "SlayTheRequiem", "localization", lang, "settings_ui.json")
            self.assertIn("SLAYTHEREQUIEM-COMBAT_VFX.title", open(path, encoding="utf-8").read())


class M7SfxRules(unittest.TestCase):
    ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
    CODE = os.path.join(ROOT, "mod", "SlayTheRequiemCode")
    TABLE = os.path.join(ROOT, "mod", "SlayTheRequiem", "data", "sfx.csv")

    def src(self, *parts):
        return open(os.path.join(self.CODE, *parts), encoding="utf-8").read()

    def test_hit_rules(self):
        self.assertIsNone(r.sfx_hit_event("Physical"))
        self.assertEqual(r.sfx_hit_volume("Physical", True, 10), 0.0)
        self.assertEqual(r.sfx_hit_volume("Fire", True, 0), 1.0)  # атака в блок — звук есть
        self.assertEqual(r.sfx_hit_volume("Fire", False, 0), 0.0)  # тик в ноль — нет
        self.assertEqual(r.sfx_hit_volume("Poison", False, 3), r.SFX_TICK_VOLUME)
        self.assertEqual(len(set(r.SFX_HIT_EVENT.values())), 8)
        # звук удара есть ровно там, где есть вспышка (M7 п. 4)
        for t in ["Physical"] + list(r.SFX_HIT_EVENT):
            for powered, dmg in ((True, 0), (False, 0), (False, 4)):
                self.assertEqual(r.sfx_hit_volume(t, powered, dmg) > 0, r.vfx_hit(t, powered, dmg) is not None, (t, powered, dmg))
        self.assertFalse(r.sfx_throttled(0, 50))
        self.assertTrue(r.sfx_throttled(1000, 1119))
        self.assertFalse(r.sfx_throttled(1000, 1120))
        self.assertEqual([r.sfx_shout_event(v) for v in ("War", "Guard", "Rally")], ["shout.war", "shout.guard", "shout.rally"])
        self.assertEqual(r.sfx_layer_volume(1.5, 2.0), r.SFX_MAX_VOLUME)
        self.assertEqual(r.sfx_layer_volume(0.8, 0.5), 0.4)

    def test_table(self):
        layers = r.sfx_parse(open(self.TABLE, encoding="utf-8").read())
        self.assertEqual(set(layers), set(r.SFX_EVENTS))  # у каждого события есть звук
        sources = {src for ls in layers.values() for src, _, _ in ls}
        self.assertEqual(sources, set(r.SFX_SOURCES))  # и ванильные события, и файлы игры, и свои
        own = {snd for ls in layers.values() for src, snd, _ in ls if src == "own"}
        for name in own:
            self.assertTrue(os.path.isfile(os.path.join(self.ROOT, "mod", "SlayTheRequiem", "audio", "sfx", name)), name)
        with open(os.path.join(self.ROOT, "tools", "audio", "vanilla_fmod_events.txt"), encoding="utf-8") as f:
            known = {l.strip() for l in f if l.strip() and not l.startswith("#")}
        for ls in layers.values():
            for src, snd, _ in ls:
                if src == "fmod":
                    self.assertIn(snd, known)
        head = "event,source,sound,volume,note\n"
        for bad in ("hit.physical,fmod,event:/sfx/heal,1,", "dodge,own,event:/sfx/heal,1,", "dodge,fmod,whoosh.wav,1,",
                    "dodge,own,whoosh.wav,3,", "dodge,own,whoosh.wav,громко,", "dodge,midi,whoosh.wav,1,"):
            with self.assertRaises(ValueError, msg=bad):
                r.sfx_parse(head + bad)
        self.assertEqual(r.sfx_parse("# комментарий\n" + head + "dodge,own,whoosh.wav,0.5,x"), {"dodge": [("own", "whoosh.wav", 0.5)]})

    def test_matches_csharp(self):
        import re
        src = self.src("Core", "SfxRules.cs")
        consts = dict(re.findall(r'public const string (\w+) = "([a-z.]+)";', src))
        order = re.findall(r"\b([A-Z]\w+)\b", src.split("IReadOnlyList<string> All =")[1].split("];")[0])
        self.assertEqual([consts[n] for n in order], r.SFX_EVENTS)
        for t, ev in r.SFX_HIT_EVENT.items():
            name = next(k for k, v in consts.items() if v == ev)
            self.assertIn(f"DamageType.{t} => SfxEvents.{name}", src)
        self.assertIn(f"HitThrottleMs = {r.SFX_HIT_THROTTLE_MS};", src)
        self.assertIn(f"TickVolume = {r.SFX_TICK_VOLUME}f;", src)
        self.assertIn(f"MaxVolume = {int(r.SFX_MAX_VOLUME)}f;", src)
        for s in r.SFX_SOURCES:
            self.assertIn(f'"{s}" => SfxSource.', src)

    def test_wiring(self):
        """Звуки подключены к тем же событиям, что и эффекты, у таблицы есть .import, настройка и подписи есть."""
        vfx = self.src("UI", "RequiemVfx.cs")
        for call in ("SfxEvents.Overstrain", "SfxEvents.Broken", "SfxEvents.Dodge", "RequiemSfx.Shout(variant)", "SfxEvents.Summon",
                     "SfxEvents.PortalEnter", "SfxEvents.Rewind"):
            self.assertIn(call, vfx)
        # звук не прячется за настройкой эффектов: стоит до Safe(...) / до проверки Enabled
        self.assertLess(vfx.index("RequiemSfx.Play(SfxEvents.Rewind)"), vfx.index("if (!Enabled || NGame.Instance == null)"))
        self.assertIn("RequiemSfx.Hit(receiver, type, powered, total)", self.src("UI", "RequiemVfxPatches.cs"))
        ledger = self.src("Acts", "ActLedger.cs")
        self.assertIn("RequiemSfx.Play(SfxEvents.CacheTrap)", ledger)
        self.assertIn("RequiemSfx.Play(SfxEvents.CacheOpen)", ledger)
        self.assertIn('Sfx = Read("sfx.csv", SfxTable.Parse);', self.src("Rules", "RequiemData.cs"))
        sfx = self.src("UI", "RequiemSfx.cs")
        self.assertIn('new("SFX")', sfx)  # шина звуков игры — действует ползунок громкости
        self.assertIn("RequiemConfig.RequiemSounds", sfx)
        self.assertTrue(os.path.isfile(self.TABLE + ".import"))
        self.assertIn('importer="keep"', open(self.TABLE + ".import", encoding="utf-8").read())
        self.assertIn("public static bool RequiemSounds { get; set; } = true;", self.src("Config", "RequiemConfig.cs"))
        sys.path.insert(0, os.path.join(self.ROOT, "tools", "loc"))
        import loc_ui
        self.assertEqual(loc_ui.UI_SETTINGS["rus"]["REQUIEM_SOUNDS.title"], "Звуки Реквиема")
        self.assertIn("REQUIEM_SOUNDS.hover.desc", loc_ui.UI_SETTINGS["eng"])

    def test_own_sounds_match_synth(self):
        sys.path.insert(0, os.path.join(self.ROOT, "tools", "audio"))
        try:
            import build_sfx
        except ImportError as e:  # numpy нет — проверка синтеза идёт в CI (build_sfx.py --check)
            self.skipTest(str(e))
        self.assertEqual(build_sfx.check_table(build_sfx.read_table()), [])
        self.assertEqual(set(build_sfx.SOUNDS), {snd for ls in r.sfx_parse(open(self.TABLE, encoding="utf-8").read()).values()
                                                 for src, snd, _ in ls if src == "own"})


class M7OwnRelicAndCardArt(unittest.TestCase):
    """M7 п. 6: свои значки реликвий и портреты карт там, где ванильная замена сбивает с толку."""
    ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
    RES = os.path.join(ROOT, "mod", "SlayTheRequiem")
    CODE = os.path.join(ROOT, "mod", "SlayTheRequiemCode")
    PORTRAITS = ["blade_spirit", "call_spirit_knight", "conjure_abyss_guardian", "lesser_ward", "raise_dead", "blood_lord_stance",
                 "storm_call", "toxic_coating", "bound_blade", "sparks", "firebolt", "blizzard", "terror", "poison_spray",
                 "herald_stance_card", "whirlwind", "miasma", "focused_aim", "rebound", "devour"]

    @staticmethod
    def size(path):
        import struct
        with open(path, "rb") as f:
            return struct.unpack(">II", f.read(24)[16:24])

    def test_visibility(self):
        self.assertTrue(r.vanilla_relic_visible("Shared", "keep"))
        self.assertTrue(r.vanilla_relic_visible("Event", "rework"))
        self.assertTrue(r.vanilla_relic_visible("Shared+Event", "keep"))
        self.assertFalse(r.vanilla_relic_visible("Defect", "keep"))
        self.assertFalse(r.vanilla_relic_visible("Necrobinder", "keep"))
        self.assertFalse(r.vanilla_relic_visible("Shared", "jewelry"))  # заменена украшением Реквиема — не выпадает

    def test_conflicts(self):
        borrowed = {"A": "prismatic_gem", "B": "prismatic_gem", "C": "akabeko", "D": "lantern", "E": None, "F": "clockwork"}
        errors, allowed = r.relic_icon_conflicts(borrowed, visible={"akabeko"})
        self.assertEqual(errors, ["A", "B", "C"])  # дубль внутри мода + видимая ванильная
        self.assertEqual(allowed, [])
        errors, allowed = r.relic_icon_conflicts(borrowed, {"akabeko"}, {"C": "дубль: тот же предмет", "A": "просто заметка"})
        self.assertEqual((errors, allowed), (["A", "B"], ["C"]))
        self.assertEqual(r.relic_icon_conflicts({"X": None, "Y": None}, {"any"}), ([], []))  # свои файлы не спорят

    def test_repo_has_no_conflicts(self):
        import sys
        sys.path.insert(0, os.path.join(self.ROOT, "tools", "art"))
        import art_fit
        errors, allowed, own = art_fit.audit_relics()
        self.assertEqual(errors, [])
        self.assertGreaterEqual(own, 197)
        self.assertEqual(art_fit.audit()[0], [])
        self.assertEqual(art_fit.VISIBLE_RELIC_BUCKETS, r.VISIBLE_RELIC_BUCKETS)
        self.assertEqual(art_fit.HIDDEN_RELIC_POOLS, r.HIDDEN_RELIC_POOLS)

    def test_card_portraits(self):
        for key in self.PORTRAITS:
            self.assertEqual(self.size(os.path.join(self.RES, "images", "card_portraits", key + ".png")), r.CARD_PORTRAIT_SIZES["small"], key)
            self.assertEqual(self.size(os.path.join(self.RES, "images", "card_portraits", "big", key + ".png")), r.CARD_PORTRAIT_SIZES["big"], key)
            self.assertTrue(os.path.isfile(os.path.join(self.ROOT, "art", "cards", key + ".svg")), key)

    def test_own_file_wins_in_code(self):
        ext = open(os.path.join(self.CODE, "Extensions", "StringExtensions.cs"), encoding="utf-8").read()
        self.assertIn('Res("card_portraits", "big", path), Res("card_portraits", path), borrowed', ext)
        self.assertIn('Res("relics", path), borrowed', ext)
        self.assertIn('Res("relics", "big", path), borrowed', ext)
        relic = open(os.path.join(self.CODE, "Relics", "RequiemRelic.cs"), encoding="utf-8").read()
        self.assertIn("Id.Entry.RemovePrefix().ToLowerInvariant()", relic)


if __name__ == "__main__":
    unittest.main()
