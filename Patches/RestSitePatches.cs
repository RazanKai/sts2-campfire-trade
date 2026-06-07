using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using CampfireTrade.UI;

namespace CampfireTrade.Patches;

[HarmonyPatch(typeof(RestSiteOption), nameof(RestSiteOption.Generate))]
public static class AddTradeOptionPatch
{
    [HarmonyPostfix]
    public static void Postfix(Player player, List<RestSiteOption> __result)
    {
        if (player.RunState.Players.Count <= 1)
            return;

        // Always add Trade for ALL players to keep option indices consistent across
        // machines, regardless of whether the player has already traded. The option's
        // UpdateIsEnabled() will disable it (gray it out) if CanTrade returns false.
        // This prevents desync when players have different UnlimitedTrades settings.
        __result.Add(new TradeRestSiteOption(player));
    }
}

// NOTE: Trade is a normal campfire action — it consumes the player's single rest-site
// action like Rest/Smith. We deliberately do NOT patch
// Hook.ShouldDisableRemainingRestSiteOptions: the game's native flow already clears the
// remaining options after a successful OnSelect (return true) unless the player owns
// Miniature Tent (whose relic override keeps options enabled). UnlimitedTrades works
// because OnSelect returns false in that mode, so ChooseOption never consumes the action.

[HarmonyPatch(typeof(RestSiteSynchronizer), nameof(RestSiteSynchronizer.BeginRestSite))]
public static class InitTradeSyncPatch
{
    [HarmonyPrefix]
    public static void Prefix(RestSiteSynchronizer __instance)
    {
        var netService = RunManager.Instance.NetService;
        if (netService == null) return;

        var existingSync = TradeSynchronizer.Instance;
        if (existingSync != null)
        {
            if (existingSync.IsUsingService(netService))
            {
                // Same session — just reset state for the new rest site
                existingSync.ResetForNewRestSite();
                return;
            }

            // Stale instance from a previous game session — dispose it so we
            // create a fresh one with the current (connected) net service.
            MainFile.Logger.Info("Disposing stale TradeSynchronizer from previous session");
            existingSync.Dispose();
        }

        var runState = RunManager.Instance.DebugOnlyGetState();
        if (runState == null || runState.Players.Count <= 1) return;

        var localId = LocalContext.NetId;
        if (!localId.HasValue) return;

        new TradeSynchronizer(netService, runState, localId.Value);
        MainFile.Logger.Info($"Created TradeSynchronizer for player {localId.Value}");
    }
}

// NOTE: We no longer suppress the "selected option" confirmation icon for trades.
// Trade now consumes the campfire action like Rest/Smith, so the game showing the
// selected trade icon (only when the option succeeds, i.e. a completed trade in
// non-unlimited mode) is the correct, consistent behavior.

[HarmonyPatch(typeof(NRestSiteRoom), nameof(NRestSiteRoom._Ready))]
public static class AddNotificationManagerPatch
{
    [HarmonyPostfix]
    public static void Postfix(NRestSiteRoom __instance)
    {
        if (__instance.Options.Count > 0)
        {
            var existing = __instance.GetNodeOrNull<TradeNotificationManager>("TradeNotificationManager");
            if (existing == null)
            {
                var manager = new TradeNotificationManager();
                manager.Name = "TradeNotificationManager";
                __instance.AddChild(manager);
            }
        }
    }
}
