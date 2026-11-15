namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// 20 подтипов узлов карты (docs/design/13 §4.2). Каждый — подтип ванильного <c>MapPointType</c> (или ребро/старт акта),
/// поэтому генерация карты и сохранения остаются ванильными. Порядок = строки таблицы 13 §4.2.
/// </summary>
public enum NodeSubtype
{
    Shrine,
    DarkAltar,
    Armorer,
    Arcanist,
    Herbalist,
    Jeweler,
    Caravan,
    Fence,
    Tavern,
    Library,
    Lair,
    Trial,
    Cache,
    LockedGate,
    Valve,
    AbyssGate,
    CrimsonPortal,
    MemoryStone,
    EbbStones,
    Ambush,
}

/// <summary>Основа подтипа: ванильный тип узла (или ребро-барьер / старт акта).</summary>
public enum NodeBase { Unknown, Shop, RestSite, Elite, Treasure, Monster, Boss, Edge, ActStart }

/// <summary>Когда решается подтип узла.</summary>
public enum NodeReveal
{
    /// <summary>Отметка на карте от зерна акта — видна сразу (временный значок — маркер «задания» узла, свой — M6).</summary>
    Marked,
    /// <summary>Отметка от зерна, но на карте не видна (Засада — до Ночного зрения, M6).</summary>
    Hidden,
    /// <summary>Решается при входе в узел (бросок «?», вид лавки, Таверна в лагере) — как ванильный «?».</summary>
    OnEnter,
    /// <summary>Всегда на своём месте (Багровый портал — второй босс, Камень Памяти — старт акта).</summary>
    Fixed,
}

/// <summary>
/// Подтипы узлов: основа, акты, способ появления, значок; числа Логова, Испытания, Тайника и Засады (M5, 13 §4.2).
/// Чистые функции — зеркало в tools/sim. Слот акта <c>slot</c>: 0 — акт 1, …, 4 — акт 5 (ActKinds.Slot; в «Коротком забеге» 0/2/4).
/// </summary>
public static class NodeRules
{
    public const int SubtypeCount = 20;

    public static IReadOnlyList<NodeSubtype> All { get; } = Enum.GetValues<NodeSubtype>();

    public static NodeBase BaseOf(NodeSubtype subtype) => subtype switch
    {
        NodeSubtype.Shrine or NodeSubtype.DarkAltar or NodeSubtype.Library or NodeSubtype.Valve or NodeSubtype.EbbStones => NodeBase.Unknown,
        NodeSubtype.Armorer or NodeSubtype.Arcanist or NodeSubtype.Herbalist or NodeSubtype.Jeweler or NodeSubtype.Caravan
            or NodeSubtype.Fence => NodeBase.Shop,
        NodeSubtype.Tavern => NodeBase.RestSite,
        NodeSubtype.Lair or NodeSubtype.Trial => NodeBase.Elite,
        NodeSubtype.Cache => NodeBase.Treasure,
        NodeSubtype.AbyssGate or NodeSubtype.Ambush => NodeBase.Monster,
        NodeSubtype.CrimsonPortal => NodeBase.Boss,
        NodeSubtype.LockedGate => NodeBase.Edge,
        NodeSubtype.MemoryStone => NodeBase.ActStart,
        _ => NodeBase.Unknown,
    };

    public static NodeReveal RevealOf(NodeSubtype subtype) => subtype switch
    {
        NodeSubtype.Lair or NodeSubtype.Trial or NodeSubtype.Cache or NodeSubtype.LockedGate or NodeSubtype.Valve
            or NodeSubtype.AbyssGate => NodeReveal.Marked,
        NodeSubtype.Ambush => NodeReveal.Hidden,
        NodeSubtype.CrimsonPortal or NodeSubtype.MemoryStone => NodeReveal.Fixed,
        _ => NodeReveal.OnEnter,
    };

    /// <summary>Файл значка <c>images/map/icons/{name}.png</c> (art/map/icons).</summary>
    public static string IconName(NodeSubtype subtype) => subtype switch
    {
        NodeSubtype.Shrine => "shrine",
        NodeSubtype.DarkAltar => "dark_altar",
        NodeSubtype.Armorer => "armorer",
        NodeSubtype.Arcanist => "enchanter",
        NodeSubtype.Herbalist => "herbalist",
        NodeSubtype.Jeweler => "jeweler",
        NodeSubtype.Caravan => "caravan",
        NodeSubtype.Fence => "fence",
        NodeSubtype.Tavern => "tavern",
        NodeSubtype.Library => "library",
        NodeSubtype.Lair => "lair",
        NodeSubtype.Trial => "trial",
        NodeSubtype.Cache => "cache",
        NodeSubtype.LockedGate => "locked_gate",
        NodeSubtype.Valve => "valve",
        NodeSubtype.AbyssGate => "abyss_gate",
        NodeSubtype.CrimsonPortal => "crimson_portal",
        NodeSubtype.MemoryStone => "memory_stone",
        NodeSubtype.EbbStones => "ebb_stones",
        NodeSubtype.Ambush => "ambush",
        _ => "shrine",
    };

    /// <summary>
    /// В каком акте может встретиться подтип (13 §4.2; лавки — по пулу 14 §3 в <see cref="ShopRules.ActPool(ActKind,int)"/>).
    /// <paramref name="kind"/> уточняет акты с особыми отметками (склепы — Курганы, вентили — Глубины, Врата — Улей и Слава).
    /// </summary>
    public static bool AvailableIn(NodeSubtype subtype, ActKind kind, int slot) => subtype switch
    {
        NodeSubtype.Shrine or NodeSubtype.DarkAltar or NodeSubtype.Library or NodeSubtype.Trial or NodeSubtype.Ambush => slot >= 1,
        NodeSubtype.EbbStones => slot >= 1, // бросок Пантеона: ванильные ступени 1–2 = акты 2–5
        NodeSubtype.Lair => slot <= 1,
        NodeSubtype.Armorer or NodeSubtype.Herbalist or NodeSubtype.Tavern or NodeSubtype.Cache or NodeSubtype.CrimsonPortal
            or NodeSubtype.MemoryStone => true,
        NodeSubtype.Arcanist or NodeSubtype.Jeweler => ShopRules.ActPool(kind, slot).Contains(ShopOf(subtype)!.Value),
        NodeSubtype.Caravan => ShopRules.ActPool(kind, slot).Contains(ShopType.Caravan),
        NodeSubtype.Fence => true, // в Кровавом Дворе — в пуле; в остальных — у «вне закона» (14 §2.7)
        NodeSubtype.LockedGate => kind == ActKind.FrozenBarrows,
        NodeSubtype.Valve => kind == ActKind.ClockworkDepths,
        NodeSubtype.AbyssGate => kind is ActKind.Hive or ActKind.Glory,
        _ => false,
    };

    /// <summary>Подтип узла лавки по её виду. Странник — ванильная лавка (не подтип), Храм стоит в Святилище.</summary>
    public static NodeSubtype? FromShop(ShopType shop) => shop switch
    {
        ShopType.Armorer => NodeSubtype.Armorer,
        ShopType.Arcanist => NodeSubtype.Arcanist,
        ShopType.Herbalist => NodeSubtype.Herbalist,
        ShopType.Jeweler => NodeSubtype.Jeweler,
        ShopType.Caravan => NodeSubtype.Caravan,
        ShopType.Fence => NodeSubtype.Fence,
        ShopType.Temple => NodeSubtype.Shrine,
        _ => null,
    };

    public static ShopType? ShopOf(NodeSubtype subtype) => subtype switch
    {
        NodeSubtype.Armorer => ShopType.Armorer,
        NodeSubtype.Arcanist => ShopType.Arcanist,
        NodeSubtype.Herbalist => ShopType.Herbalist,
        NodeSubtype.Jeweler => ShopType.Jeweler,
        NodeSubtype.Caravan => ShopType.Caravan,
        NodeSubtype.Fence => ShopType.Fence,
        _ => null,
    };

    /// <summary>Подтип узла по отметке акта (<see cref="MapMark"/>); лей-линия — правило Башни, не подтип.</summary>
    public static NodeSubtype? FromMark(MapMark mark) => mark switch
    {
        MapMark.AbyssGate => NodeSubtype.AbyssGate,
        MapMark.LockedVault => NodeSubtype.LockedGate,
        MapMark.Valve => NodeSubtype.Valve,
        MapMark.Lair => NodeSubtype.Lair,
        MapMark.Trial => NodeSubtype.Trial,
        MapMark.Cache => NodeSubtype.Cache,
        MapMark.Ambush => NodeSubtype.Ambush,
        _ => null,
    };

    /// <summary>Подтип «?» по броску Пантеона (<see cref="PantheonRules.UnknownRoomRoll"/>: 1–3) или Библиотеке.</summary>
    public static NodeSubtype? FromUnknownRoll(int pantheonRoll, bool library) => pantheonRoll switch
    {
        1 => NodeSubtype.Shrine,
        2 => NodeSubtype.DarkAltar,
        3 => NodeSubtype.EbbStones,
        _ => library ? NodeSubtype.Library : null,
    };

    // ───── отметки на карте (число на акт) ─────

    /// <summary>Логово: один элитный узел в актах 1–2.</summary>
    public static int LairCount(int slot) => slot <= 1 ? 1 : 0;

    /// <summary>Испытание: 1–2 элитных узла в актах 2–5 (вторая — при броске roll % 3 == 0).</summary>
    public static int TrialCount(int slot, int roll) => slot < 1 ? 0 : 1 + (Math.Abs(roll) % 3 == 0 ? 1 : 0);

    /// <summary>Тайник: один сундук в акте.</summary>
    public static int CacheCount(int slot) => 1;

    /// <summary>Засада: 1–2 боя в актах 2–5.</summary>
    public static int AmbushCount(int slot, int roll) => slot < 1 ? 0 : 1 + Math.Abs(roll) % 2;

    // ───── Библиотека (Unknown) ─────

    /// <summary>Шанс, что «?» в актах 2–5 (если не Святилище) окажется Библиотекой.</summary>
    public const int LibraryPercent = 9;

    public static bool RollLibrary(int slot, int roll) => slot >= 1 && roll >= 0 && roll < LibraryPercent;

    // ───── Логово ─────

    /// <summary>♥ врагов Логова: 70 % с округлением вверх (облегчённая элита), целочисленно — без ошибок double.</summary>
    public const int LairHpPercent = 70;

    public static int LairHp(int maxHp) => Math.Max(1, (Math.Max(0, maxHp) * LairHpPercent + 99) / 100);

    /// <summary>Реагенты за Логово (мясо и шкура зверя): 2 на актах 1, 3 на акте 2.</summary>
    public static int LairReagents(int slot) => slot <= 0 ? 2 : 3;

    /// <summary>Ступени брони из шкуры: I в акте 1, I–II в акте 2.</summary>
    public static (int Min, int Max) LairHideTiers(int slot) => slot <= 0 ? (1, 1) : (1, 2);

    // ───── Испытание ─────

    /// <summary>Сила врагов Испытания: +1 в актах 2–3, +2 в актах 4–5.</summary>
    public static int TrialStrength(int slot) => slot >= 3 ? 2 : 1;

    // ───── Тайник ─────

    public static int CacheGold(int slot) => 30 + 15 * Math.Max(0, slot);

    /// <summary>Тайник с ловушкой (≈ половина) или запертый (нужен Когтистый ключ). <paramref name="roll"/> ∈ [0, 100).</summary>
    public static bool CacheTrapped(int roll) => Math.Abs(roll) % 100 < 50;

    /// <summary>Урон ловушки Тайника.</summary>
    public static int CacheTrap(int slot) => 4 + 2 * Math.Max(0, slot);

    // ───── Засада ─────

    /// <summary>Засада: враги начинают с Силой (бьют первыми и больнее), зато после боя — золото.</summary>
    public static int AmbushStrength(int slot) => slot >= 3 ? 2 : 1;

    public static int AmbushGold(int slot) => 15 + 10 * Math.Max(0, slot);
}
