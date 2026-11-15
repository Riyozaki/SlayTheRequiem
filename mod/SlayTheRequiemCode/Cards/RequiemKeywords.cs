using BaseLib.Patches.Content;
using MegaCrit.Sts2.Core.Entities.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Cards;

/// <summary>
/// Ключевые слова-подсказки мода (completion-plan M1 п. 9, art spec 4.6). Текст карты они не меняют
/// (<see cref="AutoKeywordPosition.None"/>): подсказки к картам добавляет UI.CardHoverTipsPatch по свойствам карты
/// (платит ◆, тип урона, Оружейная атака, Клич, Подлый…). Тексты — localization/*/card_keywords.json,
/// ключ = «SLAYTHEREQUIEM-» + имя поля в верхнем регистре (генератор tools/loc/loc_ui.py).
/// </summary>
public static class RequiemKeywords
{
    // ───── ресурсы и груз ─────
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Stamina;
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Mana;
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Burden;
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Weight;

    // ───── состояние ◆ ─────
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Tired;
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Exhausted;
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Overstrain;

    // ───── свойства карт ─────
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Sneak;
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Shout;
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword WeaponAttack;

    // ───── типы урона (03) ─────
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Fire;
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Frost;
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Shock;
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Poison;
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Blight;
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Magic;
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword Light;
    [CustomEnum][KeywordProperties(AutoKeywordPosition.None)] public static CardKeyword TrueDamage;

    public static CardKeyword? ForDamageType(DamageType type) => type switch
    {
        DamageType.Fire => Fire,
        DamageType.Frost => Frost,
        DamageType.Shock => Shock,
        DamageType.Poison => Poison,
        DamageType.Blight => Blight,
        DamageType.Magic => Magic,
        DamageType.Light => Light,
        DamageType.True => TrueDamage,
        _ => null,
    };
}
