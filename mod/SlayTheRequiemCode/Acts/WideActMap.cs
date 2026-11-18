using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Acts;

/// <summary>
/// Карта новых актов шириной 9 (docs/design/13 §4). Ванильная <see cref="StandardActMap"/> запечатана и прибита к 7 колонкам,
/// поэтому здесь — её алгоритм с шириной-параметром: <c>Width</c> путей от случайных стартов, раскладка типов узлов
/// с теми же ограничениями (лагерь/элита не внизу, лагерь не у босса, не дважды подряд), прунинг и пост-обработка игры.
/// Ряд сокровищ — за 7 рядов до конца, последний ряд — лагеря, как в ванили.
/// </summary>
public sealed class WideActMap : ActMap
{
    private readonly int _width;
    private readonly bool _replaceTreasureWithElites;
    private readonly int _mapLength;
    private readonly Rng _rng;
    private readonly MapPointTypeCounts _pointTypeCounts;

    private static readonly HashSet<MapPointType> LowerRestrictions = [MapPointType.RestSite, MapPointType.Elite];
    private static readonly HashSet<MapPointType> UpperRestrictions = [MapPointType.RestSite];
    private static readonly HashSet<MapPointType> ParentRestrictions =
        [MapPointType.Elite, MapPointType.RestSite, MapPointType.Treasure, MapPointType.Shop];
    private static readonly HashSet<MapPointType> ChildRestrictions =
        [MapPointType.Elite, MapPointType.RestSite, MapPointType.Treasure, MapPointType.Shop];
    private static readonly HashSet<MapPointType> SiblingRestrictions =
        [MapPointType.RestSite, MapPointType.Monster, MapPointType.Unknown, MapPointType.Elite, MapPointType.Shop];

    public override MapPoint BossMapPoint { get; }
    public override MapPoint StartingMapPoint { get; }
    public override MapPoint? SecondBossMapPoint { get; }
    protected override MapPoint?[,] Grid { get; }

    public WideActMap(Rng mapRng, ActModel act, int width, bool isMultiplayer, bool replaceTreasureWithElites, bool hasSecondBoss)
    {
        _width = Math.Max(ActRules.VanillaMapWidth, width);
        _replaceTreasureWithElites = replaceTreasureWithElites;
        _mapLength = act.GetNumberOfRooms(isMultiplayer) + 1;
        _rng = mapRng;
        var baseCounts = act.GetMapPointTypes(mapRng);
        // Шире карта — больше элит (6 вместо 5), как и узлов вообще.
        _pointTypeCounts = new MapPointTypeCounts(baseCounts.NumOfUnknowns, baseCounts.NumOfRests)
        {
            NumOfElites = baseCounts.NumOfElites + (_width - ActRules.VanillaMapWidth) / 2,
        };
        Grid = new MapPoint?[_width, _mapLength];
        BossMapPoint = new MapPoint(GetColumnCount() / 2, GetRowCount());
        StartingMapPoint = new MapPoint(GetColumnCount() / 2, 0);
        if (hasSecondBoss) SecondBossMapPoint = new MapPoint(GetColumnCount() / 2, GetRowCount() + 1);

        GenerateMap();
        AssignPointTypes(replaceTreasureWithElites);
        MapPathPruning.PruneAndRepair(Grid, startMapPoints, this, _pointTypeCounts, _rng, IsValidPointType);
        Grid = MapPostProcessing.CenterGrid(Grid);
        Grid = MapPostProcessing.SpreadAdjacentMapPoints(Grid);
        Grid = MapPostProcessing.StraightenPaths(Grid);
    }

    public static WideActMap CreateFor(RunState runState, bool replaceTreasureWithElites, int width) =>
        new(new Rng(runState.Rng.Seed, $"act_{runState.CurrentActIndex + 1}_map"), runState.Act, width,
            runState.Players.Count > 1, replaceTreasureWithElites, runState.Act.HasSecondBoss);

    /// <summary>Rollback changes only the RNG stream, not this map's width/treasure/second-boss policy.</summary>
    public WideActMap Reroll(Rng rng, ActModel act, bool multiplayer) =>
        new(rng, act, _width, multiplayer, _replaceTreasureWithElites, SecondBossMapPoint != null);

    private MapPoint GetOrCreatePoint(int col, int row)
    {
        if (Grid[col, row] is { } existing) return existing;
        var point = new MapPoint(col, row);
        Grid[col, row] = point;
        return point;
    }

    private void GenerateMap()
    {
        // Путей столько же, сколько колонок (ваниль: 7 при ширине 7) — у широкой карты 9 стартов, 7–8 различимых путей.
        for (var i = 0; i < _width; i++)
        {
            var start = GetOrCreatePoint(_rng.NextInt(0, _width), 1);
            if (i == 1)
                while (startMapPoints.Contains(start))
                    start = GetOrCreatePoint(_rng.NextInt(0, _width), 1);
            startMapPoints.Add(start);
            PathGenerate(start);
        }
        foreach (var p in Row(GetRowCount() - 1)) p.AddChildPoint(BossMapPoint);
        if (SecondBossMapPoint != null) BossMapPoint.AddChildPoint(SecondBossMapPoint);
        foreach (var p in Row(1)) StartingMapPoint.AddChildPoint(p);
    }

    private IEnumerable<MapPoint> Row(int row)
    {
        for (var c = 0; c < Grid.GetLength(0); c++)
            if (Grid[c, row] is { } p) yield return p;
    }

    private void PathGenerate(MapPoint start)
    {
        var current = start;
        while (current.coord.row < _mapLength - 1)
        {
            var next = NextCoord(current);
            var point = GetOrCreatePoint(next.col, next.row);
            current.AddChildPoint(point);
            current = point;
        }
    }

    private MapCoord NextCoord(MapPoint current)
    {
        var col = current.coord.col;
        var left = Math.Max(0, col - 1);
        var right = Math.Min(col + 1, _width - 1);
        var steps = new List<int> { -1, 0, 1 };
        steps.StableShuffle(_rng);
        foreach (var step in steps)
        {
            var target = step switch { -1 => left, 0 => col, _ => right };
            if (!HasInvalidCrossover(current, target))
                return new MapCoord { col = target, row = current.coord.row + 1 };
        }
        throw new InvalidOperationException($"Cannot find next node: seed={_rng.Seed}");
    }

    private bool HasInvalidCrossover(MapPoint current, int targetX)
    {
        var dx = targetX - current.coord.col;
        if (dx == 0 || dx == _width) return false;
        var neighbour = Grid[targetX, current.coord.row];
        if (neighbour == null) return false;
        return neighbour.Children.Any(child => child.coord.col - neighbour.coord.col == -dx);
    }

    private void AssignPointTypes(bool replaceTreasureWithElites)
    {
        foreach (var p in Row(GetRowCount() - 1))
        {
            p.PointType = MapPointType.RestSite;
            p.CanBeModified = false;
        }
        foreach (var p in Row(GetRowCount() - 7))
        {
            p.PointType = replaceTreasureWithElites ? MapPointType.Elite : MapPointType.Treasure;
            p.CanBeModified = false;
        }
        foreach (var p in Row(1))
        {
            p.PointType = MapPointType.Monster;
            p.CanBeModified = false;
        }

        var queue = new Queue<MapPointType>();
        for (var i = 0; i < _pointTypeCounts.NumOfRests; i++) queue.Enqueue(MapPointType.RestSite);
        for (var i = 0; i < _pointTypeCounts.NumOfShops; i++) queue.Enqueue(MapPointType.Shop);
        for (var i = 0; i < _pointTypeCounts.NumOfElites; i++) queue.Enqueue(MapPointType.Elite);
        for (var i = 0; i < _pointTypeCounts.NumOfUnknowns; i++) queue.Enqueue(MapPointType.Unknown);

        for (var pass = 0; pass < 3 && queue.Count > 0; pass++)
        {
            var free = GetAllMapPoints().Where(p => p.PointType == MapPointType.Unassigned).ToList();
            free.StableShuffle(_rng);
            foreach (var point in free)
            {
                if (queue.Count == 0) break;
                point.PointType = NextValidType(queue, point);
            }
        }
        foreach (var point in GetAllMapPoints().Where(p => p.PointType == MapPointType.Unassigned))
            point.PointType = MapPointType.Monster;

        BossMapPoint.PointType = MapPointType.Boss;
        StartingMapPoint.PointType = MapPointType.Ancient;
        if (SecondBossMapPoint != null) SecondBossMapPoint.PointType = MapPointType.Boss;
    }

    private MapPointType NextValidType(Queue<MapPointType> queue, MapPoint point)
    {
        for (var i = 0; i < queue.Count; i++)
        {
            var type = queue.Dequeue();
            if (_pointTypeCounts.ShouldIgnoreMapPointRulesForMapPointType(type) || IsValidPointType(type, point)) return type;
            queue.Enqueue(type);
        }
        return MapPointType.Unassigned;
    }

    public bool IsValidPointType(MapPointType type, MapPoint point)
    {
        if (point.coord.row >= _mapLength - 3 && UpperRestrictions.Contains(type)) return false;
        if (point.coord.row < 6 && LowerRestrictions.Contains(type)) return false;
        if (ParentRestrictions.Contains(type) && point.parents.Concat(point.Children).Any(p => p.PointType == type)) return false;
        if (ChildRestrictions.Contains(type) && point.Children.Any(p => p.PointType == type)) return false;
        if (SiblingRestrictions.Contains(type) &&
            point.parents.SelectMany(p => p.Children).Any(s => !Equals(s, point) && s.PointType == type)) return false;
        return true;
    }
}
