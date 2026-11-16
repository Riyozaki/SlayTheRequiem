namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Какой ванильный эффект играет удар данного типа (UI/RequiemVfx подставляет сцену и цвет).</summary>
public enum HitFxKind
{
    /// <summary>Физический — только ванильный эффект самой атаки (разрез, тупой удар…).</summary>
    None,
    /// <summary>Огонь — ванильная вспышка пламени (NFireBurstVfx).</summary>
    FireBurst,
    /// <summary>Холод — облако инея (NGaseousImpactVfx в ледяном цвете).</summary>
    FrostMist,
    /// <summary>Разряд — сноп искр (NDaggerSprayImpactVfx в фиолетовом).</summary>
    SparkSpray,
    /// <summary>Яд — ванильное ядовитое облако (NPoisonImpactVfx).</summary>
    PoisonCloud,
    /// <summary>Хворь (мор) — споры (NSporeImpactVfx в болотном цвете).</summary>
    BlightSpores,
    /// <summary>Свет — звёздная вспышка (NStarryImpactVfx в золотом).</summary>
    LightStars,
    /// <summary>Магия — магический снаряд сверху (NSmallMagicMissileVfx в фиолетовом).</summary>
    MagicMissile,
    /// <summary>Чистый урон — тонкий белый разрез (NThinSliceVfx).</summary>
    TrueSlice,
}

/// <summary>Эффект удара: вид, цвет (hex без #) и масштаб.</summary>
public readonly record struct HitFx(HitFxKind Kind, string Tint, float Scale);

/// <summary>Вариант волны Клича — по типу карты (три вариации, art spec §5–6).</summary>
public enum ShoutFx
{
    /// <summary>Атакующий Клич — боевой, рыжий.</summary>
    War,
    /// <summary>Навык — защитный, золотой.</summary>
    Guard,
    /// <summary>Сила — ободряющий, светло-голубой.</summary>
    Rally,
}

/// <summary>
/// Боевые эффекты Реквиема (M7 п. 4, art spec §5): ванильные эффекты игры с перекраской — удары по 8 типам урона, надрыв,
/// Слом, Уворот, Кличи, призыв, откат Камня Памяти, Багровый портал. Здесь только решения (что, каким цветом, как долго) —
/// без Godot, чтобы их проверяли xUnit и зеркало tools/sim (M7VfxLooks).
/// </summary>
public static class VfxRules
{
    // ───── удары ─────

    /// <summary>Повтор одного и того же эффекта на одном существе не чаще, чем раз в столько мс (многоудары, тики статусов).</summary>
    public const int HitThrottleMs = 150;

    /// <summary>Масштаб эффекта для урона не-атакой (тики горения, яда, взрывы): мельче, чтобы не забивать экран.</summary>
    public const float TickScale = 0.6f;

    /// <summary>Цвет типа урона (тот же, что у числа урона и названия карты — RequiemUi.DamageTypeColor).</summary>
    public static string DamageTint(DamageType type) => type switch
    {
        DamageType.Frost => "9FD8FF",
        DamageType.Fire => "FF7A2E",
        DamageType.Shock => "C9A8FF",
        DamageType.Poison => "7BC043",
        DamageType.Blight => "5E6B3A",
        DamageType.Light => "FFE680",
        DamageType.Magic => "B565E8",
        DamageType.True => "E0E0E0",
        _ => "FFF6E2",
    };

    public static HitFxKind KindOf(DamageType type) => type switch
    {
        DamageType.Fire => HitFxKind.FireBurst,
        DamageType.Frost => HitFxKind.FrostMist,
        DamageType.Shock => HitFxKind.SparkSpray,
        DamageType.Poison => HitFxKind.PoisonCloud,
        DamageType.Blight => HitFxKind.BlightSpores,
        DamageType.Light => HitFxKind.LightStars,
        DamageType.Magic => HitFxKind.MagicMissile,
        DamageType.True => HitFxKind.TrueSlice,
        _ => HitFxKind.None,
    };

    /// <summary>
    /// Эффект поверх удара или null. Физический удар — без добавки (ванильный эффект атаки уже есть). Атака играет эффект
    /// всегда (даже в блок), урон не-атакой — только если что-то прошло, и мельче.
    /// Хворь рисуется светлее числа урона: тёмно-болотный цвет спор на тёмном фоне не виден.
    /// </summary>
    public static HitFx? For(DamageType type, bool poweredAttack, int totalDamage)
    {
        var kind = KindOf(type);
        if (kind == HitFxKind.None) return null;
        if (!poweredAttack && totalDamage <= 0) return null;
        var tint = type == DamageType.Blight ? "8C9A4E" : DamageTint(type);
        return new HitFx(kind, tint, poweredAttack ? 1f : TickScale);
    }

    /// <summary>Пропустить эффект: такой же уже играл на этом существе только что.</summary>
    public static bool Throttled(ulong lastMs, ulong nowMs) => lastMs != 0 && nowMs >= lastMs && nowMs - lastMs < HitThrottleMs;

    // ───── события ─────

    /// <summary>Длительности по art spec §5 (секунды).</summary>
    public const float OverstrainSeconds = 0.4f;
    public const float BrokenSeconds = 0.6f;
    public const float DodgeSeconds = 0.3f;
    public const float ShoutSeconds = 0.5f;
    public const float SummonSeconds = 0.6f;
    public const float RewindSeconds = 1.5f;
    public const float PortalEnterSeconds = 1.0f;
    public const float PortalPulseSeconds = 1.0f;

    /// <summary>Надрыв — кровавые шипы и красный отблеск.</summary>
    public const string OverstrainTint = "FF5555";

    /// <summary>Слом: на сколько враг «проседает» по высоте (доля) в середине эффекта.</summary>
    public const float BrokenSag = 0.9f;

    /// <summary>Уворот: на сколько пикселей герой отскакивает (назад, от врагов).</summary>
    public const float DodgeShift = 70f;

    /// <summary>Ключ надписи «Уворот!» (static_hover_tips, tools/loc/loc_m7vfx.py).</summary>
    public const string DodgeTextKey = "VFX.dodge";

    public static ShoutFx ShoutOf(bool attack, bool power) => attack ? ShoutFx.War : power ? ShoutFx.Rally : ShoutFx.Guard;

    public static string ShoutTint(ShoutFx fx) => fx switch
    {
        ShoutFx.War => "FF8A3D",
        ShoutFx.Rally => "9FD8FF",
        _ => "FFD36B",
    };

    /// <summary>Цвет круга призыва: стихия атронаха, зелень духов зверя, мертвенная зелень нежити, багрянец Бездны.</summary>
    public static string SummonTint(SummonKind kind) => kind switch
    {
        SummonKind.FlameAtronach => "FF7A2E",
        SummonKind.FrostAtronach => "9FD8FF",
        SummonKind.StormAtronach => "C9A8FF",
        SummonKind.SpiritWolf or SummonKind.BeastWolf or SummonKind.BeastSabertooth or SummonKind.BeastBear => "7FE0B0",
        SummonKind.RaisedDead or SummonKind.BoneThrall => "A8C27A",
        SummonKind.AbyssGuardian => "B3122E",
        SummonKind.SpectralKnight => "BFD8FF",
        _ => "FFE680",
    };

    /// <summary>Цвет Багрового портала (пульсация на карте и вход в бой).</summary>
    public const string PortalTint = "B3122E";

    /// <summary>Наибольшая непрозрачность красного затемнения при входе к супербоссу.</summary>
    public const float PortalEnterAlpha = 0.55f;

    /// <summary>Пульсация узла на карте — только у второго босса акта, если там супербосс (Багровый портал).</summary>
    public static bool PortalPulse(bool isSecondBossPoint, bool isSuperboss) => isSecondBossPoint && isSuperboss;

    // ───── откат Камня Памяти ─────

    public const int RewindCrackCount = 7;
    public const int RewindCrackSegments = 4;

    /// <summary>
    /// Трещины «экрана» при откате: ломаные из центра к краям в долях полуэкрана (−1…1). Детерминированы по сиду —
    /// ГПСЧ свой (LCG), чтобы зеркало на Python давало те же точки.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<(float X, float Y)>> RewindCracks(int seed, int count = RewindCrackCount,
        int segments = RewindCrackSegments)
    {
        var state = (uint)seed;
        float Next()
        {
            state = unchecked(state * 1664525u + 1013904223u);
            return (state >> 8) / 16777216f;
        }

        var cracks = new List<IReadOnlyList<(float X, float Y)>>();
        for (var i = 0; i < count; i++)
        {
            var angle = (i + 0.3f + 0.4f * Next()) / count * MathF.Tau;
            var points = new List<(float X, float Y)> { (0.04f * MathF.Cos(angle), 0.04f * MathF.Sin(angle)) };
            for (var s = 1; s <= segments; s++)
            {
                var radius = 0.04f + 0.96f * s / segments;
                var a = angle + (Next() - 0.5f) * 0.35f;
                points.Add((Math.Clamp(radius * MathF.Cos(a), -1f, 1f), Math.Clamp(radius * MathF.Sin(a), -1f, 1f)));
            }
            cracks.Add(points);
        }
        return cracks;
    }
}
