using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace CampfireTrade.Messages;

/// <summary>
/// Sent by the host (from GoldGiftSynchronizer) at shop time to sync the gold-gift
/// rules, so every client resolves gifted gold identically. Currently carries the
/// "gifted gold triggers gain-gold effects" toggle.
/// </summary>
public struct GoldConfigMessage : INetMessage, IPacketSerializable
{
    public bool giftedGoldTriggersGainEffects;

    public bool ShouldBroadcast => true;
    public NetTransferMode Mode => NetTransferMode.Reliable;
    public LogLevel LogLevel => LogLevel.Debug;
    public bool ShouldBuffer => false;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteBool(giftedGoldTriggersGainEffects);
    }

    public void Deserialize(PacketReader reader)
    {
        giftedGoldTriggersGainEffects = reader.ReadBool();
    }

    public override string ToString()
    {
        return $"GoldConfigMessage(giftedGoldTriggersGainEffects={giftedGoldTriggersGainEffects})";
    }
}
