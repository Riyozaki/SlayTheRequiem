namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Тип узла для правил графа карты (зеркало ванильного <c>MapPointType</c> без зависимости от игры).</summary>
public enum NodeKind { Monster, Elite, Unknown, Shop, Rest, Treasure, Other }

/// <summary>Клетка сетки карты: колонка и ряд (ряд 0 — старт акта, последний ряд — лагеря перед боссом).</summary>
public readonly record struct Cell(int Col, int Row)
{
    public override string ToString() => $"{Col},{Row}";
}

/// <summary>Особые рёбра карты (docs/design/13 §4.1).</summary>
public enum EdgeKind
{
    /// <summary>Портал Башни: прыжок через ряд (r → r+2), в конце всегда бой.</summary>
    Portal,
    /// <summary>Тайная тропа: видна и проходима только с «тайным зрением».</summary>
    Secret,
    /// <summary>Опасная тропа: короткий путь (r → r+2) к элите или бою, после боя — Тайник.</summary>
    Danger,
    /// <summary>Вход в боковой карман (тупиковый узел-сокровище).</summary>
    PocketIn,
    /// <summary>Выход из кармана обратно на ветку через ряд.</summary>
    PocketOut,
    /// <summary>Труба, которую вентиль открывает (до поворота — закрыта).</summary>
    ValveOpen,
    /// <summary>Труба, которую вентиль закрывает (до поворота — открыта).</summary>
    ValveClose,
}

/// <summary>Ребро плана: вид, откуда, куда; <see cref="Valve"/> — клетка вентиля для труб (иначе — null).</summary>
public readonly record struct PlanEdge(EdgeKind Kind, Cell From, Cell To, Cell? Valve = null);

/// <summary>
/// Граф карты для планировщика: узлы с типами и рёбра (только между узлами сетки; старт и босс сюда не входят).
/// Планировщик дописывает сюда свои рёбра и узлы, чтобы следующие шаги их учитывали.
/// </summary>
public sealed class GraphView(int cols, int rows)
{
    public int Cols { get; } = cols;
    /// <summary>Рядов в сетке (как <c>ActMap.GetRowCount</c>); узлы — в рядах 1..Rows−1.</summary>
    public int Rows { get; } = rows;
    public Dictionary<Cell, NodeKind> Nodes { get; } = new();
    public HashSet<(Cell From, Cell To)> Edges { get; } = new();

    public int LastRow => Rows - 1;

    public GraphView Node(int col, int row, NodeKind kind)
    {
        Nodes[new Cell(col, row)] = kind;
        return this;
    }

    public GraphView Edge(int c1, int r1, int c2, int r2)
    {
        Edges.Add((new Cell(c1, r1), new Cell(c2, r2)));
        return this;
    }

    public int OutDegree(Cell c) => Edges.Count(e => e.From == c);
    public int InDegree(Cell c) => Edges.Count(e => e.To == c);

    /// <summary>Упорядоченные узлы (ряд, колонка) — чтобы броски давали одно и то же у всех игроков.</summary>
    public IEnumerable<Cell> Ordered => Nodes.Keys.OrderBy(c => c.Row).ThenBy(c => c.Col);
}

/// <summary>Особенности карты акта: рёбра, новые узлы-карманы, «Последний привал». Хранится в Дорожной карте строкой.</summary>
public sealed class MapPlan
{
    public List<PlanEdge> Edges { get; } = new();
    public List<Cell> Pockets { get; } = new();
    public Cell? LastRest { get; set; }

    public PlanEdge? EdgeOf(Cell from, Cell to)
    {
        foreach (var e in Edges)
            if (e.From == from && e.To == to) return e;
        return null;
    }

    public bool IsEmpty => Edges.Count == 0 && Pockets.Count == 0 && LastRest == null;

    /// <summary>«P:1,2>3,4;V:1,2>2,3@5,1;K:4,5;L:3,15» — кратко, без пробелов (сохраняемое свойство).</summary>
    public string Encode()
    {
        var parts = new List<string>();
        foreach (var e in Edges)
            parts.Add($"{Letter(e.Kind)}:{e.From}>{e.To}" + (e.Valve is { } v ? $"@{v}" : ""));
        foreach (var p in Pockets) parts.Add($"K:{p}");
        if (LastRest is { } l) parts.Add($"L:{l}");
        return string.Join(";", parts);
    }

    public static MapPlan Decode(string? text)
    {
        var plan = new MapPlan();
        if (string.IsNullOrWhiteSpace(text)) return plan;
        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = part.IndexOf(':');
            if (colon != 1) continue;
            var body = part[2..];
            switch (part[0])
            {
                case 'K' when TryCell(body, out var k):
                    plan.Pockets.Add(k);
                    break;
                case 'L' when TryCell(body, out var l):
                    plan.LastRest = l;
                    break;
                default:
                    if (KindOf(part[0]) is not { } kind) break;
                    Cell? valve = null;
                    var at = body.IndexOf('@');
                    if (at >= 0)
                    {
                        if (TryCell(body[(at + 1)..], out var v)) valve = v;
                        body = body[..at];
                    }
                    var arrow = body.IndexOf('>');
                    if (arrow < 0 || !TryCell(body[..arrow], out var from) || !TryCell(body[(arrow + 1)..], out var to)) break;
                    plan.Edges.Add(new PlanEdge(kind, from, to, valve));
                    break;
            }
        }
        return plan;
    }

    private static char Letter(EdgeKind kind) => kind switch
    {
        EdgeKind.Portal => 'P',
        EdgeKind.Secret => 'S',
        EdgeKind.Danger => 'D',
        EdgeKind.PocketIn => 'I',
        EdgeKind.PocketOut => 'O',
        EdgeKind.ValveOpen => 'V',
        _ => 'C',
    };

    private static EdgeKind? KindOf(char letter) => letter switch
    {
        'P' => EdgeKind.Portal,
        'S' => EdgeKind.Secret,
        'D' => EdgeKind.Danger,
        'I' => EdgeKind.PocketIn,
        'O' => EdgeKind.PocketOut,
        'V' => EdgeKind.ValveOpen,
        'C' => EdgeKind.ValveClose,
        _ => null,
    };

    private static bool TryCell(string text, out Cell cell)
    {
        cell = default;
        var comma = text.IndexOf(',');
        if (comma < 0 || !int.TryParse(text[..comma], out var c) || !int.TryParse(text[(comma + 1)..], out var r)) return false;
        cell = new Cell(c, r);
        return true;
    }
}

/// <summary>Состояние отряда, от которого зависит проходимость особых рёбер.</summary>
public readonly record struct TravelState(bool SecretSight, bool HasClawKey, Func<Cell, bool> ValveTurned, Func<Cell, bool> VaultOpened);

/// <summary>
/// Вариации путей карты (docs/design/13 §4.1, M6 п. 1): порталы Башни, вентили Глубин, Тайная и Опасная тропа, боковые
/// карманы, Запертые врата, «Последний привал». Чистые функции — без зависимостей от игры, проверяются xUnit и tools/sim.
/// Броски — через <c>next(n)</c> ∈ [0, n) из потока ГСЧ карты, поэтому план одинаков у всех игроков коопа.
/// </summary>
public static class MapGraphRules
{
    // ───── сколько чего ─────

    public static int PortalCount(int roll) => 2 + Math.Abs(roll) % 2;
    public static int SecretCount(int roll) => 1 + Math.Abs(roll) % 2;
    public static int PocketCount(int roll) => 1 + Math.Abs(roll) % 2;
    public const int DangerCount = 1;

    /// <summary>Есть ли в акте вариации путей (новые акты 2а, 2б, 4а, 4б).</summary>
    public static bool HasPathVariations(ActKind kind) =>
        kind is ActKind.FrozenBarrows or ActKind.ClockworkDepths or ActKind.BloodCourt or ActKind.StarTower;

    /// <summary>«Последний привал»: босс получает +10 % ♥, привал даёт Камень души.</summary>
    public const int LastRestBossHpPercent = 10;

    public static int LastRestBossHp(int maxHp) => Math.Max(1, maxHp + (Math.Max(0, maxHp) * LastRestBossHpPercent + 99) / 100);

    /// <summary>«Небесный щит» (Слава): Святилищ-знамён на карте.</summary>
    public const int CelestialBannerCount = 2;

    /// <summary>Оберег «Небесного щита» в начале каждого боя до конца акта (только Праведнику).</summary>
    public const int CelestialWard = 3;

    // ───── зрение ─────

    /// <summary>Ночное зрение (Тьма курганов на 4 ряда, Засады видны): Лунная порода, Знамение Тени, Факел, зелье Ночного зрения.</summary>
    public static bool NightSight(bool moonborn, bool shadeOmen, bool torch, bool potion) => moonborn || shadeOmen || torch || potion;

    /// <summary>Тайная тропа: Ночное зрение, «Карта Курганов» или Знамение Ловкача.</summary>
    public static bool SecretSight(bool nightSight, bool barrowMap, bool trickster) => nightSight || barrowMap || trickster;

    // ───── проходимость ─────

    /// <summary>
    /// Закрыто ли ребро для перехода: тайная тропа без зрения, закрытая труба (вентиль ещё не повёрнут — для «открывающей»,
    /// уже повёрнут — для «закрывающей»), Запертые врата без Когтистого ключа (если склеп ещё не открыт).
    /// </summary>
    public static bool IsBlocked(PlanEdge? edge, bool intoLockedVault, Cell to, TravelState state)
    {
        if (intoLockedVault && !state.HasClawKey && !state.VaultOpened(to)) return true;
        if (edge is not { } e) return false;
        return e.Kind switch
        {
            EdgeKind.Secret => !state.SecretSight,
            EdgeKind.ValveOpen => e.Valve is not { } v || !state.ValveTurned(v),
            EdgeKind.ValveClose => e.Valve is { } v2 && state.ValveTurned(v2),
            _ => false,
        };
    }

    /// <summary>Рисовать ли ребро: тайная тропа без зрения не видна.</summary>
    public static bool IsVisible(PlanEdge? edge, bool secretSight) => edge is not { Kind: EdgeKind.Secret } || secretSight;

    // ───── геометрия ─────

    /// <summary>Пересекаются ли два ребра между одними и теми же соседними рядами.</summary>
    public static bool Crosses(Cell a, Cell b, Cell x, Cell y) =>
        a.Row == x.Row && b.Row == y.Row && b.Row == a.Row + 1 && (a.Col - x.Col) * (b.Col - y.Col) < 0;

    /// <summary>Можно ли добавить ребро a → b в соседний ряд: оба узла есть, рядом по колонке, ребра нет, пересечений нет.</summary>
    public static bool CanAddAdjacent(GraphView g, Cell a, Cell b) =>
        g.Nodes.ContainsKey(a) && g.Nodes.ContainsKey(b) && b.Row == a.Row + 1 && Math.Abs(a.Col - b.Col) <= 1
        && !g.Edges.Contains((a, b)) && !g.Edges.Any(e => Crosses(a, b, e.From, e.To));

    private static bool FreeOfCrossings(GraphView g, Cell a, Cell b) => !g.Edges.Any(e => Crosses(a, b, e.From, e.To));

    private static bool IsCombat(NodeKind kind) => kind is NodeKind.Monster or NodeKind.Elite;

    /// <summary>Узел — ответвление: у каждого родителя есть и другой ребёнок (Запертые врата не отрезают путь к боссу).</summary>
    public static bool IsBranchNode(GraphView g, Cell node)
    {
        var parents = g.Edges.Where(e => e.To == node).Select(e => e.From).ToList();
        return parents.Count > 0 && parents.All(p => g.OutDegree(p) >= 2);
    }

    // ───── планировщик ─────

    /// <summary>
    /// План вариаций путей для свежей карты (<paramref name="g"/> дополняется по ходу). <paramref name="valves"/> — клетки
    /// вентилей Глубин (отметки акта), для них строятся трубы. В ванильных актах — пустой план.
    /// </summary>
    public static MapPlan Plan(GraphView g, ActKind kind, IReadOnlyList<Cell> valves, Func<int, int> next)
    {
        var plan = new MapPlan();
        if (!HasPathVariations(kind)) return plan;
        if (kind == ActKind.StarTower) AddPortals(g, plan, PortalCount(next(100)), next);
        AddDanger(g, plan, next);
        AddSecrets(g, plan, SecretCount(next(100)), next);
        AddPockets(g, plan, PocketCount(next(100)), next);
        PickLastRest(g, plan, next);
        if (kind == ActKind.ClockworkDepths) AddValvePipes(g, plan, valves, next);
        return plan;
    }

    /// <summary>Порталы Башни: r → r+2, в конце бой (обычный или элита), не ближе 2 рядов к лагерям.</summary>
    public static void AddPortals(GraphView g, MapPlan plan, int count, Func<int, int> next)
    {
        var used = new HashSet<Cell>();
        for (var i = 0; i < count; i++)
        {
            var candidates = (from a in g.Ordered
                              where a.Row >= 2 && a.Row + 2 <= g.LastRow - 2 && !used.Contains(a)
                              from b in g.Ordered
                              where b.Row == a.Row + 2 && Math.Abs(a.Col - b.Col) <= 2 && IsCombat(g.Nodes[b]) && !used.Contains(b)
                                    && !g.Edges.Contains((a, b))
                              select (a, b)).ToList();
            if (candidates.Count == 0) return;
            var (from, to) = candidates[next(candidates.Count)];
            Add(g, plan, new PlanEdge(EdgeKind.Portal, from, to));
            used.Add(from);
            used.Add(to);
        }
    }

    /// <summary>Опасная тропа: короткий путь r → r+2 к элите (или к бою, если элит рядом нет).</summary>
    public static void AddDanger(GraphView g, MapPlan plan, Func<int, int> next)
    {
        var pairs = (from a in g.Ordered
                     where a.Row >= 2 && a.Row + 2 <= g.LastRow - 2 && !plan.Edges.Any(e => e.From == a)
                     from b in g.Ordered
                     where b.Row == a.Row + 2 && Math.Abs(a.Col - b.Col) <= 1 && IsCombat(g.Nodes[b])
                           && !plan.Edges.Any(e => e.To == b) && !g.Edges.Contains((a, b))
                     select (a, b)).ToList();
        var elites = pairs.Where(p => g.Nodes[p.b] == NodeKind.Elite).ToList();
        var pool = elites.Count > 0 ? elites : pairs;
        if (pool.Count == 0) return;
        var (from, to) = pool[next(pool.Count)];
        Add(g, plan, new PlanEdge(EdgeKind.Danger, from, to));
    }

    /// <summary>Тайные тропы: новые рёбра в соседний ряд без пересечений.</summary>
    public static void AddSecrets(GraphView g, MapPlan plan, int count, Func<int, int> next)
    {
        for (var i = 0; i < count; i++)
        {
            var candidates = (from a in g.Ordered
                              where a.Row >= 2 && a.Row + 1 <= g.LastRow - 1
                              from b in g.Ordered
                              where CanAddAdjacent(g, a, b)
                              select (a, b)).ToList();
            if (candidates.Count == 0) return;
            var (from, to) = candidates[next(candidates.Count)];
            Add(g, plan, new PlanEdge(EdgeKind.Secret, from, to));
        }
    }

    /// <summary>
    /// Боковые карманы: новый узел-сокровище в пустой клетке рядом с веткой; вход из ряда ниже, выход в ряд выше
    /// (возвращает на ветку через ряд). Карманы не соседствуют друг с другом.
    /// </summary>
    public static void AddPockets(GraphView g, MapPlan plan, int count, Func<int, int> next)
    {
        for (var i = 0; i < count; i++)
        {
            var candidates = new List<(Cell Cell, Cell Parent, Cell Child)>();
            for (var r = 3; r <= g.LastRow - 3; r++)
                for (var c = 0; c < g.Cols; c++)
                {
                    var cell = new Cell(c, r);
                    if (g.Nodes.ContainsKey(cell) || plan.Pockets.Any(p => Math.Abs(p.Row - r) <= 2)) continue;
                    var parent = Nearest(g, r - 1, c, p => FreeOfCrossings(g, p, cell));
                    var child = Nearest(g, r + 1, c, q => FreeOfCrossings(g, cell, q));
                    if (parent is { } p0 && child is { } q0) candidates.Add((cell, p0, q0));
                }
            if (candidates.Count == 0) return;
            var (pocket, from, to) = candidates[next(candidates.Count)];
            g.Nodes[pocket] = NodeKind.Treasure;
            plan.Pockets.Add(pocket);
            Add(g, plan, new PlanEdge(EdgeKind.PocketIn, from, pocket));
            Add(g, plan, new PlanEdge(EdgeKind.PocketOut, pocket, to));
        }
    }

    private static Cell? Nearest(GraphView g, int row, int col, Func<Cell, bool> ok)
    {
        foreach (var dc in new[] { 0, -1, 1 })
        {
            var cell = new Cell(col + dc, row);
            if (g.Nodes.TryGetValue(cell, out var kind) && kind != NodeKind.Treasure && ok(cell)) return cell;
        }
        return null;
    }

    /// <summary>«Последний привал» — один из лагерей последнего ряда (если их хотя бы два: второй остаётся обычным).</summary>
    public static void PickLastRest(GraphView g, MapPlan plan, Func<int, int> next)
    {
        var rests = g.Ordered.Where(c => c.Row == g.LastRow && g.Nodes[c] == NodeKind.Rest).ToList();
        if (rests.Count >= 2) plan.LastRest = rests[next(rests.Count)];
    }

    /// <summary>
    /// Трубы вентилей Глубин: вентиль переключает магистраль у одного узла над собой — закрывает его существующую трубу
    /// a → b и открывает новую a → b′ (поворот делает это одновременно). Так у узла всегда есть открытый выход вверх,
    /// и путь к боссу не обрывается, в каком бы порядке ни поворачивали вентили; не больше одного переключения на узел.
    /// </summary>
    public static void AddValvePipes(GraphView g, MapPlan plan, IReadOnlyList<Cell> valves, Func<int, int> next)
    {
        var switched = new HashSet<Cell>();
        foreach (var v in valves.OrderBy(c => c.Row).ThenBy(c => c.Col))
        {
            bool Near(Cell a) => a.Row > v.Row && a.Row <= Math.Min(v.Row + 5, g.LastRow - 1) && Math.Abs(a.Col - v.Col) <= 3;
            var swaps = (from e in g.Edges
                         where Near(e.From) && e.To.Row == e.From.Row + 1 && !switched.Contains(e.From) && plan.EdgeOf(e.From, e.To) == null
                         from b in g.Ordered
                         where CanAddAdjacent(g, e.From, b)
                         orderby e.From.Row, e.From.Col, e.To.Col, b.Col
                         select (From: e.From, Old: e.To, New: b)).ToList();
            if (swaps.Count == 0) continue;
            var pick = swaps[next(swaps.Count)];
            Add(g, plan, new PlanEdge(EdgeKind.ValveOpen, pick.From, pick.New, v));
            plan.Edges.Add(new PlanEdge(EdgeKind.ValveClose, pick.From, pick.Old, v));
            switched.Add(pick.From);
        }
    }

    private static void Add(GraphView g, MapPlan plan, PlanEdge edge)
    {
        g.Edges.Add((edge.From, edge.To));
        plan.Edges.Add(edge);
    }

    // ───── проверка связности (тесты, отладка) ─────

    /// <summary>
    /// Доходит ли путь от узлов первого ряда до последнего ряда, если закрыты <paramref name="blocked"/> рёбра.
    /// </summary>
    public static bool ReachesLastRow(GraphView g, Func<Cell, Cell, bool> blocked)
    {
        var firstRow = g.Nodes.Keys.Where(c => c.Row > 0).Select(c => c.Row).DefaultIfEmpty(1).Min();
        var frontier = new Queue<Cell>(g.Nodes.Keys.Where(c => c.Row == firstRow));
        var seen = new HashSet<Cell>(frontier);
        while (frontier.Count > 0)
        {
            var c = frontier.Dequeue();
            if (c.Row == g.LastRow) return true;
            foreach (var e in g.Edges.Where(e => e.From == c && !blocked(e.From, e.To)))
                if (seen.Add(e.To)) frontier.Enqueue(e.To);
        }
        return false;
    }
}
