using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace CampfireTrade.Patches;

/// <summary>
/// Lets gifted gold (the shop "Give Gold" feature) optionally skip the recipient's
/// gain-gold relic effects (e.g. Dragon Fruit's +Max HP).
///
/// <see cref="GoldGiftSynchronizer"/> sets <see cref="IsGiftedGold"/> immediately
/// before each <c>PlayerCmd.GainGold</c> call it makes for a gift. This prefix consumes
/// the flag:
///   • not a gift            → let the original GainGold run (unchanged).
///   • gift + toggle ON       → let the original GainGold run (Dragon Fruit fires).
///   • gift + toggle OFF      → plain credit (gold + SFX + history), skip AfterGoldGained.
///
/// Because the flag is set on EVERY machine that applies the gift (sender and observers
/// alike), the outcome is identical on all clients — no Max HP desync.
///
/// (The toggle idea is borrowed from Jzcse's STS2Trade fork; this is an independent
/// implementation. See NOTICE.md.)
/// </summary>
[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainGold))]
public static class GainGoldPatches
{
    /// <summary>Set by GoldGiftSynchronizer right before a gift's GainGold call.</summary>
    public static bool IsGiftedGold;

    [HarmonyPrefix]
    public static bool Prefix(decimal amount, Player player, bool wasStolenBack = false)
    {
        // Consume the flag regardless of the branch taken, so it can never leak into
        // an unrelated, later GainGold call.
        bool gifted = IsGiftedGold;
        IsGiftedGold = false;

        if (!gifted)
            return true; // normal gold gain — unchanged
        if (TradeConfig.GiftedGoldTriggersGainEffects)
            return true; // gifts fire gain-gold effects — run the original GainGold

        // Gifts suppressed: replicate GainGold's plain credit (no gold-gain modifiers,
        // no AfterGoldGained relic hook). Mirrors PlayerCmd.GainGold's body.
        if (amount <= 0m)
            return false;

        IRunState runState = player.RunState;
        if (player == LocalContext.GetMe(runState))
        {
            string sfx = amount >= 100m ? "event:/sfx/ui/gold/gold_3"
                : amount > 30m ? "event:/sfx/ui/gold/gold_2"
                : "event:/sfx/ui/gold/gold_1";
            SfxCmd.Play(sfx);
        }

        var entry = runState.CurrentMapPointHistoryEntry?.GetEntry(player.NetId);
        if (entry != null)
        {
            if (wasStolenBack)
                entry.GoldStolen -= (int)amount;
            else
                entry.GoldGained += (int)amount;
        }

        player.Gold += (int)amount;
        return false; // skip the original GainGold (and its AfterGoldGained hook)
    }
}
