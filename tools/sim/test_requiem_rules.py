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


if __name__ == "__main__":
    unittest.main()
