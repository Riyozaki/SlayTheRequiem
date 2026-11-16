namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// «Две двери» (docs/design/13 §2, M5 п. 2, ревью R-12): перед актами 2 и 4 полного забега игрок выбирает вариант акта.
/// В коопе голосуют все; решает большинство, при ничьей — жребий от сида забега (одинаковый у всех). Воздержавшийся
/// (настройка «Случайно» или кнопка «Довериться судьбе») не голосует; если воздержались все — остаётся вариант,
/// выпавший при создании забега (прежнее поведение).
/// </summary>
public static class ActDoorRules
{
    /// <summary>Голос «довериться судьбе».</summary>
    public const int Abstain = -1;

    /// <summary>Дверей всегда две: 0 — левая, 1 — правая.</summary>
    public const int Doors = 2;

    /// <summary>Перед этим актом (индекс в пятиактном забеге) стоят две двери.</summary>
    public static bool HasDoors(int actIndex, int actCount) =>
        actCount >= ActRules.FullRunActs && (actIndex == 1 || actIndex == 3);

    /// <summary>Варианты акта: левая и правая дверь.</summary>
    public static ActKind[] Options(int actIndex) => actIndex switch
    {
        1 => [ActKind.FrozenBarrows, ActKind.ClockworkDepths],
        3 => [ActKind.BloodCourt, ActKind.StarTower],
        _ => [],
    };

    /// <summary>Номер двери варианта или −1, если вариант не из этой пары.</summary>
    public static int DoorOf(int actIndex, ActKind kind) => Array.IndexOf(Options(actIndex), kind);

    /// <summary>
    /// Итог голосования: дверь с большинством голосов; ничья — <paramref name="tieBreak"/> (жребий от сида);
    /// никто не выбрал — <paramref name="current"/> (вариант, выпавший при создании забега).
    /// </summary>
    public static int Resolve(IEnumerable<int> votes, int current, int tieBreak)
    {
        var counts = new int[Doors];
        foreach (var vote in votes)
            if (vote is >= 0 and < Doors) counts[vote]++;
        if (counts[0] == 0 && counts[1] == 0) return Math.Clamp(current, 0, Doors - 1);
        if (counts[0] == counts[1]) return Math.Abs(tieBreak) % Doors;
        return counts[0] > counts[1] ? 0 : 1;
    }

    /// <summary>Жребий ничьей: детерминированно из сида и индекса акта (FNV-1a), без генераторов игры.</summary>
    public static int TieBreak(string seed, int actIndex)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in $"{seed}/doors/{actIndex}")
            {
                hash ^= ch;
                hash *= 16777619u;
            }
            return (int)(hash % Doors);
        }
    }

    /// <summary>Хост ждёт голоса всех: решение принимается, когда голосов не меньше, чем игроков.</summary>
    public static bool AllVoted(int votes, int players) => players > 0 && votes >= players;
}
