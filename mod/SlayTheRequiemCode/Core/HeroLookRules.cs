namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// Облик героя в бою (docs/design/06 §5, M6 п. 3): своя модель формы вместо ванильной модели героя.
/// Вампир и оборотень — свои спрайты (images/monsters/hero_*.png), Лич — модель Некробиндер, Заводное сердце — модель Дефекта.
/// </summary>
public enum HeroLook
{
    Hero,
    Vampire,
    Werewolf,
    Lich,
    Clockwork,
}

/// <summary>Чистые правила обликов форм и Знаков Путей (зеркало — tools/sim/requiem_rules.py, раздел M6 п. 3).</summary>
public static class HeroLookRules
{
    /// <summary>Мировоззрение важнее Заводного сердца (сердце совместимо только с Праведником, у которого облика нет).</summary>
    public static HeroLook LookFor(FormKind worldview, bool clockwork) => worldview switch
    {
        FormKind.Lich => HeroLook.Lich,
        FormKind.Vampire => HeroLook.Vampire,
        FormKind.Werewolf => HeroLook.Werewolf,
        _ => clockwork ? HeroLook.Clockwork : HeroLook.Hero,
    };

    /// <summary>Облик на ванильной модели другого героя (Некробиндер, Дефект).</summary>
    public static bool BorrowsModel(HeroLook look) => look is HeroLook.Lich or HeroLook.Clockwork;

    /// <summary>Облик — свой статичный спрайт.</summary>
    public static bool HasOwnSprite(HeroLook look) => SpriteKey(look) != null;

    /// <summary>Файл спрайта без расширения в images/monsters/ (null — не спрайт).</summary>
    public static string? SpriteKey(HeroLook look) => look switch
    {
        HeroLook.Vampire => "hero_vampire",
        HeroLook.Werewolf => "hero_werewolf",
        _ => null,
    };

    /// <summary>Вариант картинки: оборотень в Звериной форме — «beast», вампир в Стойке Кровавого лорда — «lord».</summary>
    public static string? SpriteVariant(HeroLook look, bool beastForm, bool bloodLordStance) => look switch
    {
        HeroLook.Werewolf when beastForm => "beast",
        HeroLook.Vampire when bloodLordStance => "lord",
        _ => null,
    };

    /// <summary>Перекраска модели (hsv + множитель цвета) — только на родной модели героя; у своих обликов цвет уже свой.</summary>
    public static bool KeepsTint(HeroLook look) => look == HeroLook.Hero;

    /// <summary>Оборотень без своего спрайта крупнее в Звериной форме; спрайт «beast» уже нарисован крупнее.</summary>
    public const float BeastScale = 1.15f;

    public static float BeastScaleFor(HeroLook look) => HasOwnSprite(look) ? 1f : BeastScale;

    // ═════════════ Знаки Путей — знамя за спиной ═════════════

    /// <summary>Файл знамени в images/ui/path_signs/ (snake_case Пути); у «Без пути» и Возрождённого знака нет.</summary>
    public static string? PathSignKey(HeroPath path) => path switch
    {
        HeroPath.None or HeroPath.Reborn => null,
        _ => Snake(path.ToString()),
    };

    /// <summary>Знамя стоит за спиной (слева от героя, смотрящего вправо): доля ширины и высоты рамки Bounds от её левого верхнего угла.</summary>
    public const float SignAnchorX = 0.08f;
    public const float SignAnchorY = 0.22f;
    public const float SignScale = 0.85f;

    private static string Snake(string name)
    {
        var chars = new List<char>(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0) chars.Add('_');
            chars.Add(char.ToLowerInvariant(c));
        }
        return new string(chars.ToArray());
    }

    // ═════════════ Сферы стихий как сферы Дефекта ═════════════

    /// <summary>Ванильная сфера, чей спрайт рисует сферу стихии: Пар — плазма, Искра — молния, Иней — лёд.</summary>
    public static string OrbArt(Element element) => element switch
    {
        Element.Fire => "PlasmaOrb",
        Element.Shock => "LightningOrb",
        _ => "FrostOrb",
    };

    /// <summary>Сферы на дуге по порядку: Пар, Искра, Иней; не больше <see cref="TransformationRules.MaxOrbs"/>.</summary>
    public static IReadOnlyList<Element> OrbSequence(int steam, int spark, int rime)
    {
        var list = new List<Element>();
        for (var i = 0; i < Math.Max(0, steam); i++) list.Add(Element.Fire);
        for (var i = 0; i < Math.Max(0, spark); i++) list.Add(Element.Shock);
        for (var i = 0; i < Math.Max(0, rime); i++) list.Add(Element.Frost);
        return list.Count > TransformationRules.MaxOrbs ? list.GetRange(0, TransformationRules.MaxOrbs) : list;
    }

    /// <summary>
    /// Смещение слота сферы от точки OrbPos — та же дуга над головой, что у ванильного NOrbManager.TweenLayout:
    /// радиус Lerp(225, 300, (ёмкость − 3) / 7), углы от −150° до −25°; чужой герой (сетевая игра) — радиус ×0,75.
    /// </summary>
    public static (float X, float Y) OrbSlot(int index, int capacity, bool isLocal)
    {
        capacity = Math.Max(1, capacity);
        index = Math.Clamp(index, 0, capacity - 1);
        var radius = 225f + (300f - 225f) * ((capacity - 3f) / 7f);
        if (!isLocal) radius *= 0.75f;
        var sweep = capacity == 1 ? 0f : 125f - index * (125f / (capacity - 1));
        var angle = (-25f - sweep) * MathF.PI / 180f;
        return (-MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
    }
}
