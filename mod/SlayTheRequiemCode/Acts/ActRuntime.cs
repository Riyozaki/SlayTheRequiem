using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Acts;

// Правила актов (docs/design/13 §3): отметки на карте и их действие, боевые правила акта, «Отогреться».
// Отложено на этап 8 (нужен свой интерфейс карты): Тьма курганов (скрытые узлы), переключение рёбер вентилями,
// порталы Звёздной Башни, святилища «Небесный щит», ночные бонусы героя-вампира, карта «Закрыть клапан».

/// <summary>
/// Постоянные отметки акта — склепы (2а), вентили (2б), лей-линии (4б): выбираются от сида забега и индекса акта,
/// поэтому одинаковы у всех игроков и после загрузки. Врата Бездны появляются по ходу и хранятся в Дорожной карте.
/// </summary>
public static class ActMarks
{
    private static (ActMap Map, Dictionary<int, MapMark> Marks)? _cache;

    /// <summary>Сброс между забегами (<see cref="Rules.RunLifecycle"/>, ревью R-31).</summary>
    internal static void Reset() => _cache = null;

    public static Dictionary<int, MapMark> For(IRunState run, ActMap map, int actIndex)
    {
        if (_cache is { } c && ReferenceEquals(c.Map, map)) return c.Marks;
        var marks = new Dictionary<int, MapMark>();
        var points = map.GetAllMapPoints().OrderBy(p => p.coord.row).ThenBy(p => p.coord.col).ToList();
        var rule = ActRules.RuleOf(ActKinds.Of(run.Acts[actIndex]));
        var rng = new Rng(run.Rng.Seed, $"requiem_marks_{actIndex}");
        switch (rule)
        {
            case ActRule.Frost:
                Pick(points.Where(p => p.PointType == MapPointType.Unknown).ToList(), ActRules.LockedVaultCount(rng.NextInt(100)), MapMark.LockedVault);
                break;
            case ActRule.Steam:
                Pick(points.Where(p => p.PointType == MapPointType.Unknown).ToList(), ActRules.ValveCount(rng.NextInt(100)), MapMark.Valve);
                break;
            case ActRule.LeyLines:
                Pick(points.Where(p => p.PointType == MapPointType.Monster).ToList(), ActRules.LeyLineCount(rng.NextInt(100)), MapMark.LeyLine);
                break;
        }
        // Подтипы узлов (M5, 13 §4.2): Логово, Испытание, Тайник, Засада — свой поток ГСЧ, чтобы отметки правил акта
        // (склепы, вентили, лей-линии) остались прежними. Один узел — одна отметка.
        var slot = ActRules.SlotOf(actIndex, run.Acts.Count);
        var nodeRng = new Rng(run.Rng.Seed, $"requiem_nodes_{actIndex}");
        var elites = points.Where(p => p.PointType == MapPointType.Elite).ToList();
        PickFree(elites, NodeRules.LairCount(slot), MapMark.Lair, nodeRng);
        PickFree(elites, NodeRules.TrialCount(slot, nodeRng.NextInt(100)), MapMark.Trial, nodeRng);
        PickFree(points.Where(p => p.PointType == MapPointType.Treasure).ToList(), NodeRules.CacheCount(slot), MapMark.Cache, nodeRng);
        PickFree(points.Where(p => p.PointType == MapPointType.Monster).ToList(), NodeRules.AmbushCount(slot, nodeRng.NextInt(100)),
            MapMark.Ambush, nodeRng);
        foreach (var ledger in ActRunState.Records(run))
            foreach (var code in ledger.Gates)
                if (MapMarks.Decode(code).Act == actIndex) marks[code] = MapMark.AbyssGate;
        _cache = (map, marks);
        return marks;

        void Pick(List<MapPoint> candidates, int count, MapMark mark)
        {
            var rolls = Enumerable.Range(0, count * 4).Select(_ => rng.NextInt(1000));
            foreach (var i in ActRules.PickDistinct(candidates.Count, count, rolls))
                marks[MapMarks.Encode(actIndex, candidates[i].coord.col, candidates[i].coord.row)] = mark;
        }

        void PickFree(List<MapPoint> candidates, int count, MapMark mark, Rng source)
        {
            var free = candidates.Where(p => !marks.ContainsKey(MapMarks.Encode(actIndex, p.coord.col, p.coord.row))).ToList();
            var rolls = Enumerable.Range(0, Math.Max(0, count) * 4).Select(_ => source.NextInt(1000)).ToList();
            foreach (var i in ActRules.PickDistinct(free.Count, count, rolls))
                marks[MapMarks.Encode(actIndex, free[i].coord.col, free[i].coord.row)] = mark;
        }
    }

    /// <summary>Подтип узла по отметке (<see cref="NodeRules.FromMark"/>); null — обычный узел или решается при входе.</summary>
    public static NodeSubtype? SubtypeAt(IRunState? run, MapCoord? coord) => NodeRules.FromMark(At(run, coord));

    public static MapMark At(IRunState? run, MapCoord? coord)
    {
        if (run?.Map == null || coord is not { } c) return MapMark.None;
        return For(run, run.Map, run.CurrentActIndex).GetValueOrDefault(MapMarks.Encode(run.CurrentActIndex, c.col, c.row));
    }

    /// <summary>Значок отметки — маркер «задания» на узле карты (ванильная иконка; своя — этап 8).</summary>
    public static void Show(ActMap map, int code)
    {
        if (_cache is { } c && c.Marks.TryGetValue(code, out var mark) && NodeRules.FromMark(mark) is { } sub
            && NodeRules.RevealOf(sub) == NodeReveal.Hidden) return; // Засада не видна (Ночное зрение — M6)
        var (_, col, row) = MapMarks.Decode(code);
        var point = map.GetPoint(col, row);
        var model = ModelDb.Relic<ActLedger>();
        if (point != null && !point.Quests.Contains(model)) point.AddQuest(model);
    }

    public static void Hide(ActMap map, int code)
    {
        var (_, col, row) = MapMarks.Decode(code);
        map.GetPoint(col, row)?.RemoveQuest(ModelDb.Relic<ActLedger>());
    }

    /// <summary>Добавить Врата (после <see cref="ActRules.IsRiftTrigger"/>): узел выше текущего, детерминированно.</summary>
    private static (int Key, int? Code)? _lastGate;

    /// <summary>Один и тот же вход в узел даёт одни и те же Врата — у каждой Дорожной карты отряда.</summary>
    public static int? NewGate(IRunState run, MapCoord current)
    {
        var key = MapMarks.Encode(run.CurrentActIndex, current.col, current.row);
        if (_lastGate is { } last && last.Key == key) return last.Code;
        var code = RollGate(run, current);
        _lastGate = (key, code);
        return code;
    }

    private static int? RollGate(IRunState run, MapCoord current)
    {
        var rule = ActKinds.CurrentRule(run);
        if (run.Map == null || !ActRules.IsRiftTrigger(rule, current.row)) return null;
        var marks = For(run, run.Map, run.CurrentActIndex);
        var candidates = run.Map.GetAllMapPoints()
            .Where(p => p.PointType == MapPointType.Monster
                && ActRules.CanBecomeGate(rule, p.coord.row, current.row, isCombat: true, isShopOrRest: false, isSpecial: false)
                && !marks.ContainsKey(MapMarks.Encode(run.CurrentActIndex, p.coord.col, p.coord.row)))
            .OrderBy(p => p.coord.row).ThenBy(p => p.coord.col).ToList();
        // Вторжение Славы — ближайший ряд выше; Трещины Улья — любой бой впереди.
        if (rule == ActRule.AbyssInvasion && candidates.Count > 0)
            candidates = candidates.Where(p => p.coord.row == candidates[0].coord.row).ToList();
        var i = ActRules.PickIndex(new Rng(run.Rng.Seed, $"requiem_gate_{run.CurrentActIndex}_{current.row}_{current.col}").NextInt(1000), candidates.Count);
        if (i < 0) return null;
        var code = MapMarks.Encode(run.CurrentActIndex, candidates[i].coord.col, candidates[i].coord.row);
        marks[code] = MapMark.AbyssGate;
        return code;
    }
}

/// <summary>Врата Бездны: бой на отмеченном узле — прорыв Бездны вместо обычного.</summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEncounter))]
public static class AbyssGatePatch
{
    [HarmonyPostfix]
    public static void Postfix(RoomType roomType, ref EncounterModel __result)
    {
        if (roomType != RoomType.Monster || !RequiemConfig.EnableRequiemSystems) return;
        var run = RequiemNet.State;
        if (ActMarks.At(run, run?.CurrentMapCoord) == MapMark.AbyssGate) __result = ModelDb.Encounter<AbyssRiftNormal>();
    }
}

/// <summary>Боевые правила акта: Стужа (2а), Перегрев (2б), лей-линии (4б).</summary>
public sealed class ActCombatRules() : CustomSingletonModel(CustomSingletonModel.HookType.Combat)
{
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || !RequiemConfig.EnableRequiemSystems) return;
        var run = RequiemNet.State;
        if (run == null) return;
        var rule = ActKinds.CurrentRule(run);
        var ctx = new ThrowingPlayerChoiceContext();
        var round = combatState.RoundNumber;
        if (round == 1) await ApplyNodeSubtype(run, combatState, ctx);

        foreach (var player in combatState.Players)
        {
            var hero = player.Creature;
            if (hero is not { IsAlive: true }) continue;
            var ledger = ActLedger.Of(player);
            switch (rule)
            {
                case ActRule.Frost when round == 1 && ledger is not { WarmthFights: > 0 } && !IsColdProof(hero):
                    await PowerCmd.Apply<ChillPower>(ctx, hero, ActRules.FrostChill, null, null);
                    break;
                case ActRule.Steam when ActRules.IsOverheatRound(round) && ledger is not { ValveRelief: > 0 }:
                    await PowerCmd.Apply<BurningPower>(ctx, hero, ActRules.OverheatBurning, null, null);
                    break;
                case ActRule.LeyLines when round == 1 && ActMarks.At(run, run.CurrentMapCoord) == MapMark.LeyLine:
                    await Mana.Gain(player, ActRules.LeyLineMana);
                    break;
            }
        }
    }

    /// <summary>
    /// Подтипы боевых узлов (M5, 13 §4.2): 🐾 Логово — враги с 70 % ♥ (облегчённая элита-зверь), ⚔ Испытание — элита
    /// с Силой, ! Засада — враги бьют первыми (Сила). Отметка узла одинакова у всех — эффект тоже.
    /// </summary>
    private static async Task ApplyNodeSubtype(IRunState run, ICombatState combatState, ThrowingPlayerChoiceContext ctx)
    {
        var subtype = ActMarks.SubtypeAt(run, run.CurrentMapCoord);
        if (subtype is not (NodeSubtype.Lair or NodeSubtype.Trial or NodeSubtype.Ambush)) return;
        var roomType = run.CurrentRoom?.RoomType;
        if (subtype == NodeSubtype.Ambush ? roomType != RoomType.Monster : roomType != RoomType.Elite) return;
        var slot = ActKinds.Slot(run);
        foreach (var enemy in combatState.Enemies.Where(e => e.IsAlive).ToList())
        {
            switch (subtype)
            {
                case NodeSubtype.Lair:
                    await CreatureCmd.SetMaxHp(enemy, NodeRules.LairHp(enemy.MaxHp));
                    if (enemy.CurrentHp > enemy.MaxHp) await CreatureCmd.SetCurrentHp(enemy, enemy.MaxHp);
                    break;
                case NodeSubtype.Trial:
                    await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(ctx, enemy, NodeRules.TrialStrength(slot), null, null);
                    break;
                case NodeSubtype.Ambush:
                    await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(ctx, enemy, NodeRules.AmbushStrength(slot), null, null);
                    break;
            }
        }
    }

    /// <summary>Холод не берёт: сопротивление холоду или иммунитет к заморозке.</summary>
    public static bool IsColdProof(Creature hero) =>
        ResistancePower.TierOf(hero, DamageType.Frost) > 0 || hero.HasPower<FrozenImmunityPower>();
}

/// <summary>«Отогреться» (13 §3, 2а): 0 очков, у костра — два боя без Стужи.</summary>
public sealed class WarmRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public override string OptionId => "SLAYTHEREQUIEM_WARM";
    public override string FallbackIcon => "HEAL";
    public override CampAction Action => CampAction.Warm;

    public static bool Offered(Player player) =>
        RequiemConfig.EnableRequiemSystems && ActKinds.CurrentRule(player.RunState) == ActRule.Frost && ActLedger.Of(player) != null;

    protected override bool Available => ActLedger.Of(Owner) is { WarmthFights: < ActRules.WarmthFights };

    protected override void Fill(MegaCrit.Sts2.Core.Localization.LocString loc) => loc.Add("Fights", ActRules.WarmthFights);

    protected override Task<bool> Perform()
    {
        if (ActLedger.Of(Owner) is not { } ledger) return Task.FromResult(false);
        ledger.WarmthFights = ActRules.WarmthFights;
        ledger.Flash();
        return Task.FromResult(true);
    }
}
