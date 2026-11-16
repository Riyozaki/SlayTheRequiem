using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Pantheon;
using SlayTheRequiem.SlayTheRequiemCode.Relics;

namespace SlayTheRequiem.SlayTheRequiemCode.Acts;

/// <summary>
/// Вариации путей карты (docs/design/13 §4.1, M6 п. 1) поверх ванильного графа: порталы Башни, трубы вентилей Глубин,
/// Тайная и Опасная тропа, боковые карманы, «Последний привал»; Запертые врата и тайные рёбра закрываются для перехода.
/// <para>
/// План строится один раз для свежей карты (<see cref="MapGraphRules.Plan"/>, поток ГСЧ <c>requiem_paths_N</c>) и
/// дописывается в саму карту — новые рёбра и узлы-карманы, — поэтому сохранение карты игры уже содержит топологию.
/// Сам план (какое ребро чем является) хранится в Дорожной карте каждого героя: после загрузки он нужен, чтобы знать,
/// какие рёбра закрыты и как их рисовать. Одинаков у всех игроков коопа: зерно, карта и отметки общие.
/// </para>
/// </summary>
public static class MapGraph
{
    private static (ActMap Map, MapPlan Plan)? _cache;

    /// <summary>Сброс между забегами (<see cref="Rules.RunLifecycle"/>).</summary>
    internal static void Reset() => _cache = null;

    private static readonly PropertyInfo? GridProperty = AccessTools.Property(typeof(ActMap), "Grid");

    /// <summary>План текущей карты (пустой — ванильный акт или план ещё не построен).</summary>
    public static MapPlan PlanOf(ActMap? map) => _cache is { } c && map != null && ReferenceEquals(c.Map, map) ? c.Plan : Empty;

    private static readonly MapPlan Empty = new();

    public static MapPlan Current(IRunState? run) => PlanOf(run?.Map);

    /// <summary>
    /// Подготовить план для только что созданной карты (вызывается из <see cref="ActLedger.AfterMapGenerated"/> каждого героя):
    /// свежая карта — построить и дописать в неё; сохранённая — взять из Дорожной карты. Повторный вызов для той же карты
    /// возвращает уже готовый план — второй герой коопа ничего не меняет.
    /// </summary>
    public static MapPlan Prepare(IRunState run, ActMap map, int actIndex)
    {
        if (_cache is { } c && ReferenceEquals(c.Map, map)) return c.Plan;
        MapPlan plan;
        if (map is SavedActMap)
        {
            var text = ActRunState.Records(run).Where(r => r.MapPlanAct == actIndex + 1).Select(r => r.MapPlanText)
                .FirstOrDefault(t => !string.IsNullOrEmpty(t));
            plan = MapPlan.Decode(text);
        }
        else
        {
            var kind = ActKinds.Of(run.Acts[actIndex]);
            plan = new MapPlan();
            if (MapGraphRules.HasPathVariations(kind) && RunSettings.EnableRequiemSystems)
            {
                // Отметки — по базовой карте (до плана), как и после загрузки (ActMarks вычитает рёбра и карманы плана).
                ActMarks.Reset();
                var valves = ActMarks.For(run, map, actIndex)
                    .Where(kv => kv.Value == MapMark.Valve)
                    .Select(kv => MapMarks.Decode(kv.Key)).Select(d => new Cell(d.Col, d.Row)).ToList();
                var rng = new Rng(run.Rng.Seed, $"requiem_paths_{actIndex}");
                plan = MapGraphRules.Plan(ViewOf(map, null), kind, valves, n => n <= 0 ? 0 : rng.NextInt(n));
                Apply(map, plan);
            }
        }
        _cache = (map, plan);
        ActMarks.Reset();
        return plan;
    }

    /// <summary>Граф карты для правил; <paramref name="without"/> — вычесть рёбра и карманы плана (базовая карта).</summary>
    public static GraphView ViewOf(ActMap map, MapPlan? without)
    {
        var g = new GraphView(map.GetColumnCount(), map.GetRowCount());
        var pockets = without?.Pockets.ToHashSet() ?? [];
        foreach (var p in map.GetAllMapPoints())
        {
            var cell = CellOf(p);
            if (pockets.Contains(cell)) continue;
            g.Nodes[cell] = KindOf(p.PointType);
        }
        foreach (var p in map.GetAllMapPoints())
            foreach (var child in p.Children)
            {
                var (from, to) = (CellOf(p), CellOf(child));
                if (!g.Nodes.ContainsKey(from) || !g.Nodes.ContainsKey(to)) continue;
                if (without?.EdgeOf(from, to) is { Kind: not EdgeKind.ValveClose }) continue;
                g.Edges.Add((from, to));
            }
        return g;
    }

    /// <summary>Дописать план в карту: узлы-карманы (сокровища) и новые рёбра. Повторно — без изменений.</summary>
    public static void Apply(ActMap map, MapPlan plan)
    {
        if (plan.Pockets.Count > 0 && GridProperty?.GetValue(map) is MapPoint?[,] grid)
            foreach (var cell in plan.Pockets)
                if (cell.Col >= 0 && cell.Col < grid.GetLength(0) && cell.Row >= 0 && cell.Row < grid.GetLength(1) && grid[cell.Col, cell.Row] == null)
                    grid[cell.Col, cell.Row] = new MapPoint(cell.Col, cell.Row) { PointType = MapPointType.Treasure };
        foreach (var edge in plan.Edges)
        {
            if (edge.Kind == EdgeKind.ValveClose) continue; // существующая труба — закрывается правилом перехода
            var from = map.GetPoint(edge.From.Col, edge.From.Row);
            var to = map.GetPoint(edge.To.Col, edge.To.Row);
            if (from != null && to != null && !from.Children.Contains(to)) from.AddChildPoint(to);
        }
    }

    public static Cell CellOf(MapPoint point) => new(point.coord.col, point.coord.row);

    public static NodeKind KindOf(MapPointType type) => type switch
    {
        MapPointType.Monster => NodeKind.Monster,
        MapPointType.Elite => NodeKind.Elite,
        MapPointType.Unknown => NodeKind.Unknown,
        MapPointType.Shop => NodeKind.Shop,
        MapPointType.RestSite => NodeKind.Rest,
        MapPointType.Treasure => NodeKind.Treasure,
        _ => NodeKind.Other,
    };

    // ───── зрение отряда ─────

    /// <summary>Ночное зрение хоть у кого-то в отряде: Лунная порода, Знамение Тени, Факел, зелье Ночного зрения (на этот акт).</summary>
    public static bool NightSight(IRunState? run) =>
        run != null && run.Players.Any(p => MapGraphRules.NightSight(
            p.Relics.OfType<MoonbornOrigin>().Any(), p.Relics.OfType<OmenOfTheShade>().Any(), p.Relics.OfType<Torch>().Any(),
            ActLedger.Of(p)?.NightSightAct == run.CurrentActIndex + 1));

    /// <summary>Тайная тропа видна: Ночное зрение, «Карта Курганов» или Знамение Ловкача у кого-то в отряде.</summary>
    public static bool SecretSight(IRunState? run) =>
        run != null && MapGraphRules.SecretSight(NightSight(run), run.Players.Any(p => p.Relics.OfType<BarrowMap>().Any()),
            run.Players.Any(p => p.Relics.OfType<OmenOfTheTrickster>().Any()));

    public static TravelState StateOf(IRunState run)
    {
        var act = run.CurrentActIndex;
        return new TravelState(
            SecretSight(run),
            ActRunState.Records(run).Any(r => r.ClawKeys > 0),
            v => ActRunState.AnyHas(run, r => r.TurnedValves, MapMarks.Encode(act, v.Col, v.Row)),
            c => ActRunState.AnyHas(run, r => r.OpenedVaults, MapMarks.Encode(act, c.Col, c.Row)));
    }

    /// <summary>Закрыт ли переход <paramref name="from"/> → <paramref name="to"/> (тайная тропа, труба, Запертые врата).</summary>
    public static bool Blocked(IRunState run, MapPoint from, MapPoint to, TravelState? state = null)
    {
        if (!RunSettings.EnableRequiemSystems || run.Map == null) return false;
        var plan = Current(run);
        var (a, b) = (CellOf(from), CellOf(to));
        var intoVault = ActMarks.At(run, to.coord) == MapMark.LockedVault;
        if (plan.IsEmpty && !intoVault) return false;
        return MapGraphRules.IsBlocked(plan.EdgeOf(a, b), intoVault, b, state ?? StateOf(run));
    }

    /// <summary>Конец Опасной тропы: после боя — Тайник.</summary>
    public static bool IsDangerEnd(IRunState? run, MapCoord? coord) =>
        coord is { } c && Current(run).Edges.Any(e => e.Kind == EdgeKind.Danger && e.To == new Cell(c.col, c.row));

    /// <summary>«Последний привал» на этой клетке.</summary>
    public static bool IsLastRest(IRunState? run, MapCoord? coord) =>
        coord is { } c && Current(run).LastRest == new Cell(c.col, c.row);

    public static bool IsPocket(IRunState? run, MapCoord? coord) =>
        coord is { } c && Current(run).Pockets.Contains(new Cell(c.col, c.row));
}

/// <summary>Переход по карте: закрытые рёбра (тайные без зрения, трубы, Запертые врата без ключа) не проходимы.</summary>
[HarmonyPatch(typeof(MapTravel), nameof(MapTravel.GetTravelablePointsFrom))]
public static class MapTravelPatch
{
    [HarmonyPostfix]
    public static void Postfix(IRunState runState, MapPoint currentPoint, ref IEnumerable<MapPoint> __result)
    {
        if (!RunSettings.EnableRequiemSystems || runState?.Map == null) return;
        var state = MapGraph.StateOf(runState);
        var list = __result.ToList();
        var open = list.Where(p => !MapGraph.Blocked(runState, currentPoint, p, state)).ToList();
        // Страховка: план не должен оставлять тупиков, но если все пути закрыты — пропускаем ванильные.
        __result = open.Count > 0 ? open : list;
    }
}
