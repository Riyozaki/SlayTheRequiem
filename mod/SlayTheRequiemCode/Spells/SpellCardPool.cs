using BaseLib.Abstracts;
using Godot;

namespace SlayTheRequiem.SlayTheRequiemCode.Spells;

/// <summary>
/// Общий пул заклинаний (не привязан к герою). В награды попадает через <see cref="SpellRewardRules"/>
/// по владению школами (docs/design/08 §8.4), в Чародейской лавке — через пул лавки (14).
/// </summary>
public sealed class SpellCardPool : CustomCardPoolModel
{
    public override string Title => "requiem_spells";
    public override bool IsShared => true;
    public override bool IsColorless => true;
    public override Color ShaderColor => new("6FA8FF");
    public override Color DeckEntryCardColor => new("6FA8FF");
}
