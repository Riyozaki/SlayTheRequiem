namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Материал предмета (docs/design/07 §2) — по нему красится кукла: оружие, щит и аура брони (07 §8).</summary>
public enum ItemMaterial
{
    None,
    Iron,
    Steel,
    Silver,
    ScaledSteel,
    Obsidian,
    DragonBone,
    EternalIce,
    Abyssal,
}

/// <summary>Облик ауры брони по классу: латы — ровное сияние, лёгкая — мерцание, одеяния — переливы.</summary>
public enum AuraStyle { None, Steady, Flicker, Shimmer }

/// <summary>Аура брони героя: класс (стиль), материал (цвет) и сила (сколько частей брони надето).</summary>
public readonly record struct ArmorAura(ArmorClass Class, ItemMaterial Material, float Strength)
{
    public static readonly ArmorAura None = new(ArmorClass.None, ItemMaterial.None, 0f);
    public bool IsVisible => Class != ArmorClass.None && Strength > 0f;
}

/// <summary>
/// Кукла героя в бою (docs/design/07 §8, M6 п. 4), MVP: оружие и щит/посох в руках + цветовая аура брони.
/// Чистые правила (зеркало — tools/sim/requiem_rules.py, раздел M6 п. 4); узлы Godot — Equipment/PaperDoll.cs.
/// </summary>
public static class PaperDollRules
{
    // ═════════════ материал и цвет ═════════════

    /// <summary>Материал по ступени: I железо, II сталь, III небесное серебро, IV чёрный обсидиан, V вечный лёд; Бездонное — всегда своё.</summary>
    public static ItemMaterial MaterialFor(int tier, bool abyssal, ItemMaterial explicitMaterial = ItemMaterial.None)
    {
        if (explicitMaterial != ItemMaterial.None) return explicitMaterial;
        if (abyssal) return ItemMaterial.Abyssal;
        return tier switch
        {
            <= 1 => ItemMaterial.Iron,
            2 => ItemMaterial.Steel,
            3 => ItemMaterial.Silver,
            4 => ItemMaterial.Obsidian,
            _ => ItemMaterial.EternalIce,
        };
    }

    /// <summary>
    /// Цвет материала (07 §8): железо — серый, сталь — голубоватый, серебро — белый с бликом, обсидиан — чёрный с зелёным
    /// отливом, драконья кость — слоновая кость, вечный лёд — бирюзовый, Бездонное — красно-чёрный с дымом.
    /// Чешуйчатая сталь (эльфийско-двемерская) — бронзово-золотая. Множитель для светлого серого спрайта: тёмные материалы
    /// не уходят в чёрный, чтобы читался рисунок.
    /// </summary>
    public static (float R, float G, float B) MaterialColor(ItemMaterial material) => material switch
    {
        ItemMaterial.Steel => (0.72f, 0.82f, 0.98f),
        ItemMaterial.Silver => (1f, 1f, 1f),
        ItemMaterial.ScaledSteel => (0.95f, 0.8f, 0.48f),
        ItemMaterial.Obsidian => (0.36f, 0.46f, 0.4f),
        ItemMaterial.DragonBone => (1f, 0.94f, 0.8f),
        ItemMaterial.EternalIce => (0.55f, 0.96f, 0.95f),
        ItemMaterial.Abyssal => (0.62f, 0.14f, 0.16f),
        _ => (0.64f, 0.66f, 0.7f),
    };

    /// <summary>Периодический блик (серебро — белый, обсидиан — зелёный отлив, лёд — искра).</summary>
    public static bool HasGlint(ItemMaterial material) =>
        material is ItemMaterial.Silver or ItemMaterial.Obsidian or ItemMaterial.EternalIce;

    /// <summary>Дым вокруг (Бездонное).</summary>
    public static bool HasSmoke(ItemMaterial material) => material == ItemMaterial.Abyssal;

    // ═════════════ аура брони ═════════════

    /// <summary>
    /// Аура по надетой броне (голова, тело, руки, ноги): класс — самый частый (при равенстве латы → лёгкая → одеяния),
    /// материал — у части самой высокой ступени этого класса, сила — 0,4 + 0,15 за каждую часть (до 1).
    /// </summary>
    public static ArmorAura AuraFor(IEnumerable<(ArmorClass Class, int Tier, ItemMaterial Material)> pieces)
    {
        var list = pieces.Where(p => p.Class != ArmorClass.None).ToList();
        if (list.Count == 0) return ArmorAura.None;
        var cls = list.GroupBy(p => p.Class)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => ClassOrder(g.Key))
            .First().Key;
        var best = list.Where(p => p.Class == cls).OrderByDescending(p => p.Tier).First();
        var strength = Math.Min(1f, 0.4f + 0.15f * list.Count);
        return new ArmorAura(cls, best.Material == ItemMaterial.None ? MaterialFor(best.Tier, false) : best.Material, strength);
    }

    private static int ClassOrder(ArmorClass cls) => cls switch
    {
        ArmorClass.Heavy => 0,
        ArmorClass.Light => 1,
        _ => 2,
    };

    public static AuraStyle StyleFor(ArmorClass cls) => cls switch
    {
        ArmorClass.Heavy => AuraStyle.Steady,
        ArmorClass.Light => AuraStyle.Flicker,
        ArmorClass.Robe => AuraStyle.Shimmer,
        _ => AuraStyle.None,
    };

    /// <summary>Непрозрачность ауры на полной силе: латы ярче, одеяния тоньше.</summary>
    public static float AuraAlpha(AuraStyle style) => style switch
    {
        AuraStyle.Steady => 0.42f,
        AuraStyle.Flicker => 0.32f,
        AuraStyle.Shimmer => 0.36f,
        _ => 0f,
    };

    /// <summary>Период пульса ауры, секунды: латы — медленно, лёгкая — быстро мерцает, одеяния — средне.</summary>
    public static float AuraPulseSeconds(AuraStyle style) => style switch
    {
        AuraStyle.Steady => 2.4f,
        AuraStyle.Flicker => 0.7f,
        AuraStyle.Shimmer => 1.5f,
        _ => 0f,
    };

    // ═════════════ оружие в руках ═════════════

    /// <summary>Спрайт оружия правой руки / предмета левой руки (images/ui/doll/{key}.png + {key}_metal.png); null — пусто.</summary>
    public static string? WeaponArt(WeaponKind kind) => kind switch
    {
        WeaponKind.Dagger => "dagger",
        WeaponKind.Sword => "sword",
        WeaponKind.Axe => "axe",
        WeaponKind.Mace => "mace",
        WeaponKind.Spear => "spear",
        WeaponKind.TwoHanded => "greatsword",
        WeaponKind.Bow => "bow",
        WeaponKind.Crossbow => "crossbow",
        WeaponKind.Staff => "staff",
        WeaponKind.Shield => "shield",
        _ => null,
    };

    /// <summary>Левая рука занята двуручным оружием, луком или арбалетом — её предмет не рисуется.</summary>
    public static bool BlocksOffHand(WeaponKind mainHand) =>
        mainHand is WeaponKind.TwoHanded or WeaponKind.Bow or WeaponKind.Crossbow;

    /// <summary>
    /// Рисовать ли оружие на модели: свой спрайт формы (вампир, оборотень — <see cref="HeroLookRules.HasOwnSprite"/>) держит
    /// своё оружие, в Звериной форме оружия нет. Аура брони видна всегда.
    /// </summary>
    public static bool ShowsWeapons(bool ownSpriteLook, bool beastForm) => !beastForm && !ownSpriteLook;

    /// <summary>
    /// Точка хвата в долях рамки Bounds от её левого верхнего угла. Герой смотрит вправо: правая рука с оружием — впереди
    /// (поверх тела), щит и посох — у дальней руки, за телом.
    /// </summary>
    public static (float X, float Y) HandAnchor(bool mainHand) => mainHand ? (0.8f, 0.56f) : (0.3f, 0.54f);

    /// <summary>Наклон оружия в покое (радианы): клинок вверх-вперёд; посох и копьё почти вертикально; щит ровно.</summary>
    public static float RestAngle(WeaponKind kind) => kind switch
    {
        WeaponKind.Staff or WeaponKind.Spear => -0.12f,
        WeaponKind.Shield => 0f,
        WeaponKind.Bow or WeaponKind.Crossbow => 0.1f,
        _ => 0.35f,
    };

    /// <summary>Холст спрайтов куклы (tools/art/monsters/doll.py: DOLL_W×DOLL_H).</summary>
    public const int CanvasWidth = 140;
    public const int CanvasHeight = 260;

    /// <summary>Точка хвата на холсте, пиксели: рукоять (70, 190); щит, лук и арбалет — за середину (70, 130).</summary>
    public static (int X, int Y) Pivot(WeaponKind kind) => kind switch
    {
        WeaponKind.Shield or WeaponKind.Bow or WeaponKind.Crossbow => (70, 130),
        _ => (70, 190),
    };

    /// <summary>Размер спрайта на модели: двуручное и копьё крупнее, кинжал мельче.</summary>
    public static float WeaponScale(WeaponKind kind) => kind switch
    {
        WeaponKind.Dagger => 0.62f,
        WeaponKind.TwoHanded or WeaponKind.Spear or WeaponKind.Staff => 0.95f,
        WeaponKind.Shield => 0.8f,
        _ => 0.8f,
    };
}
