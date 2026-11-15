using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Rollback;
using SlayTheRequiem.SlayTheRequiemCode.Shops;

namespace SlayTheRequiem.SlayTheRequiemCode.Net;

/// <summary>
/// Действие в лавке (продажа, доверие, услуги, Заказ, провал кражи). Отправитель уже выполнил его у себя;
/// получатели повторяют его для игрока-отправителя (<see cref="ShopActions.Apply"/>).
/// Хост пересылает сообщение клиента остальным (<see cref="ShouldBroadcast"/>).
/// </summary>
public sealed class ShopActionMessage : ICustomTargetedMessage
{
    public RunLocation location;
    public int kind;
    public int a;
    public int b;
    public string s = string.Empty;

    public RunLocation Location => location;
    public bool IsRewardMessage => false;
    public bool ShouldBroadcast => true;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(location);
        writer.WriteInt(kind, 8);
        writer.WriteInt(a);
        writer.WriteInt(b);
        writer.WriteString(s ?? string.Empty);
    }

    public void Deserialize(PacketReader reader)
    {
        location = reader.Read<RunLocation>();
        kind = reader.ReadInt(8);
        a = reader.ReadInt();
        b = reader.ReadInt();
        s = reader.ReadString();
    }

    public void HandleMessage(ulong senderId)
    {
        var player = RequiemNet.PlayerOf(senderId);
        if (player == null || senderId == RequiemNet.LocalNetId) return;
        TaskHelper.RunSafely(ShopActions.Apply(player, (ShopAction)kind, a, b, s));
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
