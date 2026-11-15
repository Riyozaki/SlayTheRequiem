using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Pantheon;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Transformations;

/// <summary>
/// События трансформаций (docs/design/06 §1): подставляются Знаком веры вместо «?» с шансом 12 %
/// (<see cref="TransformationRules.UnknownRoomEvent"/>), каждое — один раз за забег. Принять форму можно, только если
/// она совместима (06 §3), иначе вариант заблокирован с пояснением.
/// </summary>
public abstract class FormEvent : PantheonEvent
{
    public abstract FormKind Form { get; }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        if (Faith.SigilOf(Owner) is { } sigil) sigil.MarkFormEventSeen(Form);
        var options = new List<EventOption>();
        if (Requirement() is { } locked) options.Add(Locked(locked));
        else if (!Forms.CanTake(Owner!, Form)) options.Add(Locked("INCOMPATIBLE"));
        else options.Add(Choice(Accept, "ACCEPT"));
        options.AddRange(ExtraOptions());
        options.Add(Choice(Leave, "LEAVE"));
        return options;
    }

    /// <summary>Особое условие (ключ заблокированного варианта) или null.</summary>
    protected virtual string? Requirement() => null;

    protected virtual IEnumerable<EventOption> ExtraOptions() => [];

    private async Task Accept()
    {
        if (await Forms.Grant(Owner!, Form)) Finish("ACCEPTED");
        else Finish("LEAVE");
    }

    public static EventModel? For(FormKind kind) => kind switch
    {
        FormKind.Werewolf => ModelDb.Event<BeastBloodEvent>(),
        FormKind.DarkPact => ModelDb.Event<UrnWhisperEvent>(),
        FormKind.Righteous => ModelDb.Event<VigilEvent>(),
        FormKind.Clockwork => ModelDb.Event<DeepHeartEvent>(),
        FormKind.Vampire => ModelDb.Event<CryptFeastEvent>(),
        FormKind.Lich => ModelDb.Event<PhylacteryEvent>(),
        _ => null,
    };
}

/// <summary>«Кровь зверя» (акты 1–2): укус раненого оборотня — Носитель Зверя.</summary>
public sealed class BeastBloodEvent : FormEvent
{
    public override FormKind Form => FormKind.Werewolf;
}

/// <summary>«Шёпот из урны» (акты 1–2): Тёмный договор.</summary>
public sealed class UrnWhisperEvent : FormEvent
{
    public override FormKind Form => FormKind.DarkPact;
}

/// <summary>«Бдение» (акты 2–3): ночь молитвы у алтаря — Праведник (только праведным).</summary>
public sealed class VigilEvent : FormEvent
{
    public override FormKind Form => FormKind.Righteous;
    protected override string? Requirement() => Faith.Effective(Owner) == Piety.Righteous ? null : "NOT_RIGHTEOUS";
}

/// <summary>«Сердце Глубин» (акты 2–3): заводное ядро вместо сердца.</summary>
public sealed class DeepHeartEvent : FormEvent
{
    public override FormKind Form => FormKind.Clockwork;
}

/// <summary>«Пир в склепе» (акты 2–4): Дитя Ночи.</summary>
public sealed class CryptFeastEvent : FormEvent
{
    public override FormKind Form => FormKind.Vampire;
}

/// <summary>«Филактерия» (поздние акты): Вестник с 10 победами становится Личем.</summary>
public sealed class PhylacteryEvent : FormEvent
{
    public override FormKind Form => FormKind.Lich;

    protected override string? Requirement() => Forms.Of<HeraldForm>(Owner) is { CanBecomeLich: true } ? null : "NOT_HERALD";
}
