using MegaCrit.Sts2.Core.Entities.Players;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Spells;

/// <summary>Владение школами (docs/design/08 §8.1): уровень в Гримуаре + бонусы реликвий/сил, 0–5.</summary>
public static class Mastery
{
    public static readonly SpellSchool[] Schools = Enum.GetValues<SpellSchool>();

    public static Grimoire? GrimoireOf(Player? player) => player?.Relics.OfType<Grimoire>().FirstOrDefault();

    public static int Of(Player? player, SpellSchool school)
    {
        if (player == null) return 0;
        var grimoire = GrimoireOf(player);
        var level = grimoire?.Level(school) ?? SpellAffinity.StartingMastery(player.Character.GetType().Name, school);
        var bonus = player.Relics.OfType<IMasterySource>().Sum(r => r.MasteryBonus(school))
                  + player.Creature.Powers.OfType<IMasterySource>().Sum(p => p.MasteryBonus(school));
        return Math.Clamp(level + bonus, 0, SpellAffinity.MaxMastery);
    }

    public static int Total(Player? player) => Schools.Sum(s => Of(player, s));
}

/// <summary>Источник бонуса владения (Знамение Ока, Хранитель Свитков, снаряжение школы).</summary>
public interface IMasterySource
{
    int MasteryBonus(SpellSchool school);
}
