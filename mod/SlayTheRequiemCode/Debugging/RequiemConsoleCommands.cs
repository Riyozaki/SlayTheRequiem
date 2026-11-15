using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Acts;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rollback;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Shops;
using SlayTheRequiem.SlayTheRequiemCode.Transformations;

namespace SlayTheRequiem.SlayTheRequiemCode.Debugging;

// Отладочные команды консоли (completion-plan M0 п. 8, technical-plan §8 «Смоук-сцены»). Игра находит их сама
// (DevConsole → ReflectionHelper.GetSubtypesInMods<AbstractConsoleCmd>), доступны только в отладочной консоли (DebugOnly).
// Идентификаторы принимаются в любом виде: «PathGarrote», «path_garrote», «PATH_GARROTE» или полный
// «SLAYTHEREQUIEM-PATH_GARROTE» — префикс мода добавляется сам. Ванильные id тоже работают (поиск без префикса — вторым).

/// <summary>Общая часть: разбор id мода, поиск модели, проверки забега и боя.</summary>
public abstract class RequiemConsoleCmd : AbstractConsoleCmd
{
    public const string Prefix = "SLAYTHEREQUIEM-";

    /// <summary>Вход id без префикса → Entry с префиксом мода.</summary>
    public static string ModEntry(string arg)
    {
        var a = arg.Trim();
        return a.Contains('-') ? a.ToUpperInvariant() : Prefix + StringHelper.Slugify(a);
    }

    /// <summary>Модель мода по id, затем ванильная с тем же именем.</summary>
    protected static T? Find<T>(string arg) where T : AbstractModel
    {
        var category = ModelId.SlugifyCategory<T>();
        return ModelDb.GetByIdOrNull<T>(new ModelId(category, ModEntry(arg)))
               ?? ModelDb.GetByIdOrNull<T>(new ModelId(category, StringHelper.Slugify(arg.Trim())));
    }

    /// <summary>Подсказки: все модели мода этого типа без префикса (в нижнем регистре — как печатают).</summary>
    protected CompletionResult CompleteModIds(IEnumerable<AbstractModel> models, string[] args)
    {
        if (args.Length > 1) return new CompletionResult { Type = CompletionType.Argument, ArgumentContext = CmdName };
        var ids = models.Select(m => m.Id.Entry).Where(e => e.StartsWith(Prefix)).Select(e => e[Prefix.Length..].ToLowerInvariant()).Distinct();
        return CompleteArgument(ids, Array.Empty<string>(), args.FirstOrDefault() ?? "");
    }

    protected static CmdResult Fail(string msg) => new(success: false, msg);
    protected static CmdResult Ok(string msg) => new(success: true, msg);
    protected static CmdResult Ok(Task task, string msg) => new(task, success: true, msg);

    protected static CmdResult? NeedRun(Player? player) =>
        player?.RunState == null || !RunManager.Instance.IsInProgress ? Fail("Нужен идущий забег.") : null;

    protected static CmdResult? NeedCombat(Player? player) =>
        NeedRun(player) ?? (player!.PlayerCombatState == null ? Fail("Команда работает только в бою.") : null);

    protected static bool TryInt(string[] args, out int value)
    {
        value = 0;
        return args.Length > 0 && int.TryParse(args[0], out value);
    }
}

/// <summary><c>str_fight &lt;encounter&gt;</c> — бой с энкаунтером мода (враги, элиты, боссы, супербоссы).</summary>
public sealed class StrFightCmd : RequiemConsoleCmd
{
    public override string CmdName => "str_fight";
    public override string Args => "<encounter:string>";
    public override string Description => "Slay the Requiem: jump to a mod encounter (prefix optional).";
    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (NeedRun(issuingPlayer) is { } fail) return fail;
        if (args.Length == 0) return Fail("Укажите энкаунтер.");
        return Fight(args[0]);
    }

    internal static CmdResult Fight(string arg)
    {
        var model = Find<EncounterModel>(arg);
        if (model == null) return Fail($"Энкаунтер «{ModEntry(arg)}» не найден.");
        var encounter = model.ToMutable();
        encounter.DebugRandomizeRng();
        var task = RunManager.Instance.EnterRoomDebug(encounter.RoomType, MapPointType.Unassigned, encounter);
        return Ok(task, $"Бой: {encounter.Id.Entry}");
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) =>
        CompleteModIds(ModelDb.AllEncounters, args);
}

/// <summary><c>str_superboss &lt;id&gt;</c> — бой с одним из 8 супербоссов; без аргумента — их список.</summary>
public sealed class StrSuperbossCmd : RequiemConsoleCmd
{
    public override string CmdName => "str_superboss";
    public override string Args => "[id:string]";
    public override string Description => "Slay the Requiem: fight a superboss (no args = list).";
    public override bool IsNetworked => true;

    private static IEnumerable<string> Names => typeof(SuperbossEncounter).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(SuperbossEncounter)))
        .Select(t => StringHelper.Slugify(t.Name).ToLowerInvariant()).OrderBy(n => n);

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length == 0) return Ok("Супербоссы: " + string.Join(", ", Names));
        if (NeedRun(issuingPlayer) is { } fail) return fail;
        return StrFightCmd.Fight(args[0]);
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) =>
        args.Length > 1 ? new CompletionResult { Type = CompletionType.Argument, ArgumentContext = CmdName }
            : CompleteArgument(Names, Array.Empty<string>(), args.FirstOrDefault() ?? "");
}

/// <summary><c>str_act &lt;n | id&gt;</c> — перейти к акту n или заменить текущий актом мода (barrows, depths, court, tower…).</summary>
public sealed class StrActCmd : RequiemConsoleCmd
{
    public override string CmdName => "str_act";
    public override string Args => "<index:int | act:string>";
    public override string Description => "Slay the Requiem: go to act N or replace the current act with a mod act.";
    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (NeedRun(issuingPlayer) is { } fail) return fail;
        if (args.Length != 1) return Fail("Нужен один аргумент: номер акта или id.");
        // Ванильная act умеет и номер, и замену по Entry — передаём ей Entry с префиксом мода.
        var arg = int.TryParse(args[0], out _) ? args[0] : Find<ActModel>(args[0])?.Id.Entry ?? ModEntry(args[0]);
        return new ActConsoleCmd().Process(issuingPlayer, [arg]);
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) =>
        CompleteModIds(ModelDb.Acts, args);
}

/// <summary><c>str_event &lt;id&gt;</c> — открыть событие мода (в том числе не входящее в пулы актов).</summary>
public sealed class StrEventCmd : RequiemConsoleCmd
{
    public override string CmdName => "str_event";
    public override string Args => "<event:string>";
    public override string Description => "Slay the Requiem: jump to a mod event.";
    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (NeedRun(issuingPlayer) is { } fail) return fail;
        if (args.Length == 0) return Fail("Укажите событие.");
        var model = Find<EventModel>(args[0]);
        if (model == null) return Fail($"Событие «{ModEntry(args[0])}» не найдено.");
        var point = model is AncientEventModel ? MapPointType.Ancient : MapPointType.Unknown;
        issuingPlayer!.RunState.AppendToMapPointHistory(point, RoomType.Event, model.Id);
        return Ok(RunManager.Instance.EnterRoom(new EventRoom(model)), $"Событие: {model.Id.Entry}");
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) =>
        CompleteModIds(ModelDb.AllEvents.Concat(ModelDb.AllAncients), args);
}

/// <summary><c>str_card &lt;id&gt; [pile]</c> — карта мода в руку (или в колоду: Deck), включая служебные жетоны.</summary>
public sealed class StrCardCmd : RequiemConsoleCmd
{
    public override string CmdName => "str_card";
    public override string Args => "<card:string> [pile:Hand|Deck|Draw|Discard]";
    public override string Description => "Slay the Requiem: add a mod card (tokens included).";
    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (NeedRun(issuingPlayer) is { } fail) return fail;
        if (args.Length == 0) return Fail("Укажите карту.");
        var pile = PileType.Hand;
        if (args.Length >= 2 && !TryParseEnum(args[1], out pile))
            return Fail($"Неизвестная стопка «{args[1]}»: {string.Join(", ", Enum.GetNames<PileType>())}.");
        if (pile.IsCombatPile() && issuingPlayer!.PlayerCombatState == null) return Fail("Стопка боя — только в бою; вне боя: Deck.");
        if (pile == PileType.Hand && PileType.Hand.GetPile(issuingPlayer!).Cards.Count >= CardPile.MaxCardsInHand) return Fail("Рука полна.");
        var model = Find<CardModel>(args[0]);
        if (model == null) return Fail($"Карта «{ModEntry(args[0])}» не найдена.");
        ICardScope scope = pile.IsCombatPile() ? CombatManagerScope() : RunManager.Instance.DebugOnlyGetState()!;
        var card = scope.CreateCard(model, issuingPlayer!);
        return Ok(CardPileCmd.Add(card, pile), $"Карта {model.Id.Entry} → {pile}");
    }

    private static ICardScope CombatManagerScope() => MegaCrit.Sts2.Core.Combat.CombatManager.Instance.DebugOnlyGetState()!;

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) =>
        CompleteModIds(ModelDb.AllCards, args);
}

/// <summary><c>str_relic &lt;id&gt;</c> — реликвия мода (снаряжение, фоны, формы, Камень Памяти…).</summary>
public sealed class StrRelicCmd : RequiemConsoleCmd
{
    public override string CmdName => "str_relic";
    public override string Args => "<relic:string>";
    public override string Description => "Slay the Requiem: obtain a mod relic (equipment, backgrounds…).";
    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (NeedRun(issuingPlayer) is { } fail) return fail;
        if (args.Length == 0) return Fail("Укажите реликвию.");
        var model = Find<RelicModel>(args[0]);
        if (model == null) return Fail($"Реликвия «{ModEntry(args[0])}» не найдена.");
        return Ok(RelicCmd.Obtain(model.ToMutable(), issuingPlayer!), $"Реликвия {model.Id.Entry}");
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) =>
        CompleteModIds(ModelDb.AllRelics, args);
}

/// <summary><c>str_stamina &lt;n&gt;</c> — установить ◆ (можно выше пула: проверка надрыва и наград).</summary>
public sealed class StrStaminaCmd : RequiemConsoleCmd
{
    public override string CmdName => "str_stamina";
    public override string Args => "<amount:int>";
    public override string Description => "Slay the Requiem: set Stamina in combat.";
    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (NeedCombat(issuingPlayer) is { } fail) return fail;
        if (!TryInt(args, out var n) || n < 0) return Fail("Нужно целое ≥ 0.");
        var stamina = RequiemResource.Of<StaminaResource>(issuingPlayer);
        if (stamina == null) return Fail("У героя нет ◆ (не в системе Реквиема).");
        stamina.Amount = n;
        return Ok($"◆ = {n} / {stamina.Pool}");
    }
}

/// <summary><c>str_mana &lt;n&gt;</c> — установить ✦ (звёзды игры).</summary>
public sealed class StrManaCmd : RequiemConsoleCmd
{
    public override string CmdName => "str_mana";
    public override string Args => "<amount:int>";
    public override string Description => "Slay the Requiem: set Mana (stars) in combat.";
    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (NeedCombat(issuingPlayer) is { } fail) return fail;
        if (!TryInt(args, out var n) || n < 0) return Fail("Нужно целое ≥ 0.");
        var delta = n - Mana.Amount(issuingPlayer);
        var task = delta >= 0 ? Mana.Gain(issuingPlayer, delta) : Mana.Lose(issuingPlayer, -delta);
        return Ok(task, $"✦ = {n} / {Mana.Pool(issuingPlayer)}");
    }
}

/// <summary><c>str_level &lt;n&gt;</c> — поднять Летопись героя до уровня n (Слава добавляется до порога).</summary>
public sealed class StrLevelCmd : RequiemConsoleCmd
{
    public override string CmdName => "str_level";
    public override string Args => "<level:int>";
    public override string Description => "Slay the Requiem: raise the Hero Record to level N.";
    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (NeedRun(issuingPlayer) is { } fail) return fail;
        if (!TryInt(args, out var n) || n < 1 || n > ProgressionRules.MaxLevel) return Fail($"Уровень 1–{ProgressionRules.MaxLevel}.");
        var hero = HeroRecord.Of(issuingPlayer);
        if (hero == null) return Fail("У героя нет Летописи.");
        if (n <= hero.Level) return Fail($"Уже уровень {hero.Level}; понижение не поддерживается.");
        hero.GainRenown(ProgressionRules.RenownToReach(n) - hero.Renown);
        return Ok($"Уровень {hero.Level}; очков: характеристик {hero.UnspentStatPoints}, навыков {hero.UnspentSkillPoints}.");
    }
}

/// <summary><c>str_form &lt;form&gt;</c> — принять форму (righteous, vampire, werewolf, darkpact, herald, lich, clockwork).</summary>
public sealed class StrFormCmd : RequiemConsoleCmd
{
    public override string CmdName => "str_form";
    public override string Args => "<form:Righteous|Vampire|Werewolf|DarkPact|Herald|Lich|Clockwork>";
    public override string Description => "Slay the Requiem: grant a transformation.";
    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (NeedRun(issuingPlayer) is { } fail) return fail;
        if (args.Length == 0 || !TryParseEnum<FormKind>(args[0].Replace("_", ""), out var kind) || kind == FormKind.None)
            return Fail("Формы: " + string.Join(", ", Enum.GetNames<FormKind>().Skip(1)));
        if (!Forms.CanTake(issuingPlayer!, kind)) return Fail($"Форму {kind} сейчас взять нельзя (несовместима с текущими).");
        return Ok(Forms.Grant(issuingPlayer!, kind), $"Форма: {kind}");
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) =>
        args.Length > 1 ? new CompletionResult { Type = CompletionType.Argument, ArgumentContext = CmdName }
            : CompleteArgument(Enum.GetNames<FormKind>().Skip(1), Array.Empty<string>(), args.FirstOrDefault() ?? "");
}

/// <summary><c>str_shop &lt;type&gt;</c> — войти в лавку заданного вида (wanderer, armorer, arcanist, herbalist).</summary>
public sealed class StrShopCmd : RequiemConsoleCmd
{
    public override string CmdName => "str_shop";
    public override string Args => "<type:Wanderer|Armorer|Arcanist|Herbalist>";
    public override string Description => "Slay the Requiem: enter a shop of the given type.";
    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (NeedRun(issuingPlayer) is { } fail) return fail;
        if (args.Length == 0 || !TryParseEnum<ShopType>(args[0], out var type))
            return Fail("Виды лавок: " + string.Join(", ", Enum.GetNames<ShopType>()));
        MerchantLedger.DebugNextShop = type;
        return Ok(RunManager.Instance.EnterRoomDebug(RoomType.Shop), $"Лавка: {type}");
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) =>
        args.Length > 1 ? new CompletionResult { Type = CompletionType.Argument, ArgumentContext = CmdName }
            : CompleteArgument(Enum.GetNames<ShopType>(), Array.Empty<string>(), args.FirstOrDefault() ?? "");
}

/// <summary><c>str_memorystone</c> — снять Отпечаток Камня Памяти сейчас (дальше — смерть через <c>die</c>).</summary>
public sealed class StrMemoryStoneCmd : RequiemConsoleCmd
{
    public override string CmdName => "str_memorystone";
    public override string Args => "";
    public override string Description => "Slay the Requiem: take a Memory Stone imprint now.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (NeedRun(issuingPlayer) is { } fail) return fail;
        if (!MemoryStone.Enabled) return Fail("Камень Памяти выключен в настройках мода.");
        MemoryStone.TakeImprint();
        var state = RunManager.Instance.DebugOnlyGetState();
        var candles = state == null ? 0 : MemoryStone.CandlesLeft(state);
        return Ok($"Отпечаток снят (подробности — в логе). Свечей: {(candles == MemoryStoneRules.Infinite ? "∞" : candles)}.");
    }
}

/// <summary><c>str_stat</c> — сводка героя: ◆ ✦ ⚖ Стойкость, уровень, формы, уровень Реквиема.</summary>
public sealed class StrStatCmd : RequiemConsoleCmd
{
    public override string CmdName => "str_stat";
    public override string Args => "";
    public override string Description => "Slay the Requiem: print hero stats.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (NeedRun(issuingPlayer) is { } fail) return fail;
        var p = issuingPlayer!;
        if (!RequiemMode.IsInSystem(p)) return Ok("Герой вне системы Реквиема (ванильные правила).");
        var stats = RequiemMode.StatsFor(p);
        var stamina = RequiemResource.Of<StaminaResource>(p);
        var hero = HeroRecord.Of(p);
        var forms = string.Join(", ", Forms.All(p).Select(f => f.Kind));
        var lines = new[]
        {
            stamina != null ? $"◆ {stamina.Amount}/{stamina.Pool} (+{stamina.Regen})" : $"◆ пул {stats.StaminaPool} (+{stats.StaminaRegen}) — вне боя",
            p.PlayerCombatState != null ? $"✦ {Mana.Amount(p)}/{Mana.Pool(p)} (+{Mana.Regen(p)})" : $"✦ пул {Mana.Pool(p)} (+{Mana.Regen(p)})",
            $"⚖ {Loadout.TotalWeight(p)}/{Loadout.Capacity(p)}; Стойкость {HeroStatRules.Poise(p)}",
            hero != null ? $"Уровень {hero.Level}, Слава {hero.Renown}" : "Летописи нет",
            $"Формы: {(forms.Length == 0 ? "—" : forms)}; уровень Реквиема {RequiemLevel.Of(p)}",
        };
        return Ok(string.Join("\n", lines));
    }
}
