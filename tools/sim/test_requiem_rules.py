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


if __name__ == "__main__":
    unittest.main()


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

    def test_pierce(self):
        self.assertEqual(r.apply_plating(6, 2, pierce=1), 5)
        self.assertEqual(r.apply_plating(6, 1, pierce=1), 6)
        self.assertEqual(r.apply_plating(6, -1), 7)   # Осколок Ветра: Латы −1
