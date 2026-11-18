namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Тип урона (docs/design/02 §4.5, 03).</summary>
public enum DamageType
{
    Physical,
    Fire,
    Frost,
    Shock,
    Poison,
    Blight,
    Magic,
    Light,
    /// <summary>Игнорирует Латы/Раскол и сопротивления, но не обычный Блок, cap и особую неуязвимость.</summary>
    True,
}

/// <summary>Класс удара для цен выносливости (docs/design/02 §1).</summary>
public enum StrikeClass
{
    Light,
    Normal,
    Heavy,
}

/// <summary>Состояние выносливости в момент атаки.</summary>
public enum StaminaState
{
    /// <summary>Бодр: ◆ ≥ половины пула — полный урон.</summary>
    Rested,
    /// <summary>Устал: ◆ меньше половины пула — −25 % урона атак.</summary>
    Tired,
    /// <summary>Изнурён: не хватает ◆ на атаку — −50 % урона и надрыв (потеря ♥).</summary>
    Exhausted,
}
