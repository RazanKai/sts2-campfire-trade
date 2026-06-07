using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace CampfireTrade.Messages;

/// <summary>
/// Sent by the host at rest site start to synchronize trade config settings.
/// Clients apply the host's config so all machines use the same rules.
/// </summary>
public struct TradeConfigMessage : INetMessage, IPacketSerializable
{
    public bool UnlimitedTrades;
    public bool BlockObtainHookRelics;
    public bool BlockQuestCards;
    public bool AllowStarterCards;
    public bool EnableGoldGifting;
    public int MaxCardSlots;
    public int MaxPotionSlots;
    public int MaxRelicSlots;

    public bool ShouldBroadcast => true;
    public NetTransferMode Mode => NetTransferMode.Reliable;
    public LogLevel LogLevel => LogLevel.Debug;
    public bool ShouldBuffer => false;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteBool(UnlimitedTrades);
        writer.WriteBool(BlockObtainHookRelics);
        writer.WriteBool(BlockQuestCards);
        writer.WriteBool(AllowStarterCards);
        writer.WriteBool(EnableGoldGifting);
        writer.WriteInt(MaxCardSlots);
        writer.WriteInt(MaxPotionSlots);
        writer.WriteInt(MaxRelicSlots);
    }

    public void Deserialize(PacketReader reader)
    {
        UnlimitedTrades = reader.ReadBool();
        BlockObtainHookRelics = reader.ReadBool();
        BlockQuestCards = reader.ReadBool();
        AllowStarterCards = reader.ReadBool();
        EnableGoldGifting = reader.ReadBool();
        MaxCardSlots = reader.ReadInt();
        MaxPotionSlots = reader.ReadInt();
        MaxRelicSlots = reader.ReadInt();
    }

    public override string ToString()
    {
        return $"TradeConfigMessage(UnlimitedTrades={UnlimitedTrades}, BlockObtainHookRelics={BlockObtainHookRelics}, BlockQuestCards={BlockQuestCards}, AllowStarterCards={AllowStarterCards}, EnableGoldGifting={EnableGoldGifting}, Cards={MaxCardSlots}, Potions={MaxPotionSlots}, Relics={MaxRelicSlots})";
    }
}
