using System;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using CampfireTrade.Messages;
using CampfireTrade.Patches;

namespace CampfireTrade;

public class GoldGiftSynchronizer : IDisposable
{
    private static GoldGiftSynchronizer? _instance;
    public static GoldGiftSynchronizer? Instance => _instance;

    private readonly INetGameService _netService;
    private readonly IPlayerCollection _playerCollection;
    private readonly ulong _localPlayerId;

    public bool IsUsingService(INetGameService service) => ReferenceEquals(_netService, service);

    public GoldGiftSynchronizer(INetGameService netService, IPlayerCollection playerCollection, ulong localPlayerId)
    {
        _netService = netService;
        _playerCollection = playerCollection;
        _localPlayerId = localPlayerId;
        _netService.RegisterMessageHandler<GiveGoldMessage>(HandleGiveGold);
        _netService.RegisterMessageHandler<GoldConfigMessage>(HandleGoldConfig);
        _instance = this;

        // Host pushes the gold-gift rules so every client resolves gifts identically.
        if (_netService is INetHostGameService)
            BroadcastGoldConfig();

        MainFile.Logger.Info("GoldGiftSynchronizer initialized");
    }

    public void Dispose()
    {
        try
        {
            _netService.UnregisterMessageHandler<GiveGoldMessage>(HandleGiveGold);
            _netService.UnregisterMessageHandler<GoldConfigMessage>(HandleGoldConfig);
        }
        catch { /* net service may already be torn down */ }
        if (_instance == this) _instance = null;
        MainFile.Logger.Info("GoldGiftSynchronizer disposed");
    }

    /// <summary>Host → clients: broadcast the current gold-gift rules. Host-authoritative.</summary>
    public void BroadcastGoldConfig()
    {
        if (_netService is not INetHostGameService) return;
        _netService.SendMessage(new GoldConfigMessage
        {
            giftedGoldTriggersGainEffects = TradeConfig.GiftedGoldTriggersGainEffects
        });
        MainFile.Logger.Info($"BroadcastGoldConfig: GiftedGoldTriggersGainEffects={TradeConfig.GiftedGoldTriggersGainEffects}");
    }

    private void HandleGoldConfig(GoldConfigMessage message, ulong senderId)
    {
        if (_netService is INetHostGameService) return; // host ignores its own broadcast
        TradeConfig.GiftedGoldTriggersGainEffects = message.giftedGoldTriggersGainEffects;
        MainFile.Logger.Info($"HandleGoldConfig: GiftedGoldTriggersGainEffects={message.giftedGoldTriggersGainEffects}");
    }

    /// <summary>
    /// Transfers gold from the local player to the target player.
    /// Deducts gold locally BEFORE broadcasting to prevent race conditions
    /// on rapid button holds.
    /// Returns the actual amount transferred (0 if no gold available).
    /// </summary>
    public int SendGold(ulong targetPlayerId, int requestedAmount)
    {
        var localPlayer = _playerCollection.GetPlayer(_localPlayerId);
        if (localPlayer == null || localPlayer.Gold <= 0) return 0;

        int actual = Math.Min(requestedAmount, localPlayer.Gold);
        if (actual <= 0) return 0;

        // Deduct locally FIRST — prevents overspend from rapid holds
        // LoseGold plays gold_1 SFX (sender feedback)
        PlayerCmd.LoseGold(actual, localPlayer);

        // Credit target via the SAME path used on every other machine (PlayerCmd.GainGold)
        // so gain-gold relic effects (e.g. Dragon Fruit's +Max HP) fire identically on all
        // clients — otherwise the recipient's Max HP would desync. GainGold only plays SFX
        // for the local player, and here the target isn't local, so there's no double SFX.
        var targetPlayer = _playerCollection.GetPlayer(targetPlayerId);
        if (targetPlayer != null)
        {
            GainGoldPatches.IsGiftedGold = true;
            TaskHelper.RunSafely(PlayerCmd.GainGold(actual, targetPlayer));
        }

        // Broadcast so all other machines apply the same change
        _netService.SendMessage(new GiveGoldMessage
        {
            targetPlayerId = targetPlayerId,
            amount = actual
        });

        MainFile.Logger.Info($"SendGold: {actual}g to {targetPlayerId}");
        return actual;
    }

    private void HandleGiveGold(GiveGoldMessage message, ulong senderId)
    {
        // Sender already applied locally in SendGold — skip to avoid double-apply
        if (senderId == _localPlayerId) return;

        var sender = _playerCollection.GetPlayer(senderId);
        var target = _playerCollection.GetPlayer(message.targetPlayerId);
        if (sender == null || target == null) return;

        // Clamp to sender's actual gold (defensive against desync)
        int actual = Math.Min(message.amount, sender.Gold);
        if (actual <= 0) return;

        // Deduct from sender — direct set to avoid SFX on receiver for remote player's loss
        sender.Gold = Math.Max(0, sender.Gold - actual);

        // Credit target — use PlayerCmd so local player gets SFX + gain-gold hooks.
        // Flag it as a gift so GainGoldPatches can honor the GiftedGoldTriggersGainEffects toggle.
        GainGoldPatches.IsGiftedGold = true;
        TaskHelper.RunSafely(PlayerCmd.GainGold(actual, target));

        MainFile.Logger.Info($"HandleGiveGold: {senderId} sent {actual}g to {message.targetPlayerId}");
    }
}
