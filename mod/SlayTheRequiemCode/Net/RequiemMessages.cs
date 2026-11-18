using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Acts;
using SlayTheRequiem.SlayTheRequiemCode.Rollback;
using SlayTheRequiem.SlayTheRequiemCode.Shops;

namespace SlayTheRequiem.SlayTheRequiemCode.Net;

/// <summary>Versioned, owner-authenticated request. Location buffering is still provided by BaseLib.</summary>
public sealed class ShopActionMessage : ICustomTargetedMessage
{
    public RunLocation location;
    public ShopRequest request = new("", "", 0, "", 0, 0, 0, "");
    private int _protocol = ShopOperations.Protocol;
    public RunLocation Location => location;
    public bool IsRewardMessage => false;
    public bool ShouldBroadcast => true;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(location);
        writer.WriteInt(ShopOperations.Protocol, 8);
        writer.WriteString(request.Run); writer.WriteString(request.Id); writer.WriteInt(request.Revision);
        writer.WriteString(request.Before); writer.WriteInt((int)request.Kind, 8);
        writer.WriteInt(request.A); writer.WriteInt(request.B); writer.WriteString(request.S);
    }
    public void Deserialize(PacketReader reader)
    {
        location = reader.Read<RunLocation>();
        _protocol = reader.ReadInt(8);
        var run = reader.ReadString(); var id = reader.ReadString(); var revision = reader.ReadInt();
        var before = reader.ReadString(); var kind = (ShopAction)reader.ReadInt(8);
        request = new(run, id, revision, before, kind, reader.ReadInt(), reader.ReadInt(), reader.ReadString());
    }
    public void HandleMessage(ulong senderId)
    {
        var player = RequiemNet.PlayerOf(senderId); // Never accept a player ID supplied in the request body.
        if (player == null || senderId == RequiemNet.LocalNetId) return;
        if (_protocol != ShopOperations.Protocol)
        { MainFile.Logger.Error($"[ShopOperation] Unsupported protocol {_protocol} from {senderId}"); return; }
        TaskHelper.RunSafely(ShopOperations.Apply(player, request, location));
    }
}

/// <summary>Each peer reports its actual result/digest. Mismatch or a missing peer blocks subsequent operations.</summary>
public sealed class ShopActionReceiptMessage : ICustomTargetedMessage
{
    public RunLocation location;
    public ulong playerId;
    public string operationId = "";
    public int status;
    public int gold;
    public string digest = "";
    public RunLocation Location => location;
    public bool IsRewardMessage => false;
    public bool ShouldBroadcast => true;
    public void Serialize(PacketWriter writer)
    {
        writer.Write(location); writer.WriteULong(playerId); writer.WriteString(operationId);
        writer.WriteInt(status, 8); writer.WriteInt(gold); writer.WriteString(digest);
    }
    public void Deserialize(PacketReader reader)
    {
        location = reader.Read<RunLocation>(); playerId = reader.ReadULong(); operationId = reader.ReadString();
        status = reader.ReadInt(8); gold = reader.ReadInt(); digest = reader.ReadString();
    }
    public void HandleMessage(ulong senderId)
    {
        if (RequiemNet.PlayerOf(playerId) is { } player)
            ShopOperations.OnReceipt(player, operationId, senderId, status, gold, digest);
    }
}

/// <summary>Фаза кооп-протокола Камня Памяти (docs/design/15 §5).</summary>
public enum StonePhase
{
    /// <summary>Хост → все: «вернуться к Камню?» (value = акт, text = свечи).</summary>
    Proposal = 1,
    /// <summary>Клиент → хост: голос (value 1 = за, 0 = против).</summary>
    Vote = 2,
    /// <summary>Хост → все: итог (value 1 = возврат, 0 = нет).</summary>
    Decision = 3,
    /// <summary>Клиент → хост: выбранный Шрам (value = <see cref="Core.Scar"/>).</summary>
    Scar = 4,
    /// <summary>Хост → все: снимок записан, хост открывает лобби загрузки — переподключайтесь.</summary>
    Go = 5,
    /// <summary>Хост → все: возврат сорвался (ошибка снимка и т. п.).</summary>
    Abort = 6,
}

/// <summary>Сообщение экрана поражения в коопе: голосование и Шрамы. Забег уже окончен — без привязки к месту.</summary>
public sealed class MemoryStoneMessage : ICustomMessage
{
    public int phase;
    public int round;
    public int value;
    public int extra;

    public bool ShouldBroadcast => false;
    public bool ShouldBuffer => false;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt(phase, 8);
        writer.WriteInt(round);
        writer.WriteInt(value);
        writer.WriteInt(extra);
    }

    public void Deserialize(PacketReader reader)
    {
        phase = reader.ReadInt(8);
        round = reader.ReadInt();
        value = reader.ReadInt();
        extra = reader.ReadInt();
    }

    public void HandleMessage(ulong senderId) => CoopMemoryStone.OnMessage((StonePhase)phase, round, value, extra, senderId);
}

/// <summary>Фаза голосования «двух дверей» (docs/design/13 §2).</summary>
public enum DoorPhase
{
    /// <summary>Игрок → все (через хост): голос (value = дверь 0/1, −1 — «судьба»).</summary>
    Vote = 1,
    /// <summary>Хост → все: итог (value = дверь).</summary>
    Decision = 2,
}

/// <summary>
/// Голос и итог «двух дверей». Без привязки к месту: экран открыт на награде за босса, а итог нужен до перехода в акт.
/// Хост пересылает голоса клиентов остальным (<see cref="ShouldBroadcast"/>), чтобы у всех были видны отметки.
/// </summary>
public sealed class ActDoorMessage : ICustomMessage
{
    public int phase;
    public int act;
    public int value;

    public bool ShouldBroadcast => true;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt(phase, 8);
        writer.WriteInt(act, 8);
        writer.WriteInt(value);
    }

    public void Deserialize(PacketReader reader)
    {
        phase = reader.ReadInt(8);
        act = reader.ReadInt(8);
        value = reader.ReadInt();
    }

    public void HandleMessage(ulong senderId) => ActDoors.OnMessage((DoorPhase)phase, act, value, senderId);
}

/// <summary>
/// Хост → клиенты в лобби: снимок игровых настроек забега (ревью F-03, <see cref="Config.RunSettings"/>). Отправляется до
/// <c>LobbyBeginRunMessage</c> и обрабатывается сразу (без буфера): клиент должен знать правила до генерации актов.
/// </summary>
public sealed class RunSettingsMessage : ICustomMessage
{
    public string data = string.Empty;

    public bool ShouldBroadcast => false;
    public bool ShouldBuffer => false;

    public void Serialize(PacketWriter writer) => writer.WriteString(data ?? string.Empty);

    public void Deserialize(PacketReader reader) => data = reader.ReadString();

    public void HandleMessage(ulong senderId) => Config.RunSettings.OnHostSettings(data, senderId);
}

/// <summary>Клиент → хост в лобби: личный выбор (происхождения трёх героев и предыстория) — при нажатии «Готов».</summary>
public sealed class PersonalChoicesMessage : ICustomMessage
{
    public int platebearer;
    public int silent;
    public int regent;
    public int background;

    public bool ShouldBroadcast => false;
    public bool ShouldBuffer => false;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt(platebearer, 8);
        writer.WriteInt(silent, 8);
        writer.WriteInt(regent, 8);
        writer.WriteInt(background, 8);
    }

    public void Deserialize(PacketReader reader)
    {
        platebearer = reader.ReadInt(8);
        silent = reader.ReadInt(8);
        regent = reader.ReadInt(8);
        background = reader.ReadInt(8);
    }

    public void HandleMessage(ulong senderId) =>
        Config.RunSettings.OnPersonalChoices(senderId, new Core.PersonalChoices(platebearer, silent, regent, background));
}


/// <summary>Only the authenticated current host can request rejoining its restored checkpoint timeline.</summary>
public sealed class InventoryRecoveryMessage : ICustomMessage
{
    public string timeline = "";
    public string seed = "";
    public bool ShouldBroadcast => false;
    public bool ShouldBuffer => false;
    public void Serialize(PacketWriter writer) { writer.WriteString(timeline); writer.WriteString(seed); }
    public void Deserialize(PacketReader reader) { timeline = reader.ReadString(); seed = reader.ReadString(); }
    public void HandleMessage(ulong senderId) => Rules.OperationRecovery.OnHostRestore(timeline, seed, senderId);
}
