using System.Reflection;
using BaseLib.Config;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace CampfireTrade;

public enum CardSlots { One = 1, Two = 2, Three = 3, Four = 4, Five = 5 }
public enum PotionSlots { One = 1, Two = 2, Three = 3 }
public enum RelicSlots { One = 1, Two = 2, Three = 3 }

public class TradeConfig : SimpleModConfig
{
    public static CardSlots MaxCardSlots { get; set; } = CardSlots.Three;
    public static PotionSlots MaxPotionSlots { get; set; } = PotionSlots.Three;
    public static RelicSlots MaxRelicSlots { get; set; } = RelicSlots.One;
    public static bool UnlimitedTrades { get; set; } = false;
    public static bool BlockObtainHookRelics { get; set; } = true;
    public static bool BlockQuestCards { get; set; } = true;

    /// <summary>
    /// When true (default), card trades are rarity point-balanced — both sides' card
    /// subtotals (Common 1 / Uncommon 2 / Rare 4) must match. When false, cards trade
    /// freely with no value-matching requirement (slot caps and non-tradeable rules
    /// still apply). Host-authoritative: synced to clients via TradeConfigMessage.
    /// </summary>
    public static bool EnablePointBalance { get; set; } = true;

    /// <summary>
    /// When true, Basic-rarity cards (starter Strikes/Defends) become tradeable and
    /// are valued at 1 point each (same as Common). Off by default — starters are
    /// normally excluded. Host-authoritative: synced to clients via TradeConfigMessage.
    /// </summary>
    public static bool AllowStarterCards { get; set; } = false;

    /// <summary>
    /// When true, "Give Gold" buttons appear under other players at merchant shops,
    /// letting players gift gold to each other. On by default. Host-authoritative:
    /// synced to clients via TradeConfigMessage.
    /// </summary>
    public static bool EnableGoldGifting { get; set; } = true;

    /// <summary>
    /// When true (default), gold given via the shop Give Gold button triggers the
    /// recipient's gain-gold relic effects (e.g. Dragon Fruit's +Max HP) — matching
    /// normal earned gold. When false, gifted gold is a plain transfer that fires no
    /// gain-gold effects (prevents two Dragon Fruit owners farming Max HP by bouncing
    /// gold). Applied identically on every client, so it never desyncs. Host-authoritative.
    /// </summary>
    public static bool GiftedGoldTriggersGainEffects { get; set; } = true;

    /// <summary>
    /// Local-only (NOT synced over the network): when true, emit detailed per-phase
    /// trade/sync logs to godot.log. Off by default so normal runs stay quiet and the
    /// game's LOCAL-vs-REMOTE desync state-diff dumps aren't buried under trade chatter.
    /// Errors always log regardless of this setting.
    /// </summary>
    public static bool VerboseLogging { get; set; } = false;

    public TradeConfig() { }

    public override void SetupConfigUI(Control optionContainer)
    {
        foreach (var child in optionContainer.GetChildren())
            child.Free();

        base.SetupConfigUI(optionContainer);
    }

    public static int MaxCardSlotsInt => (int)MaxCardSlots;
    public static int MaxPotionSlotsInt => (int)MaxPotionSlots;
    public static int MaxRelicSlotsInt => (int)MaxRelicSlots;

    /// <summary>
    /// Returns true if the given relic has an AfterObtained hook that does
    /// something beyond the base no-op (i.e., the method is overridden).
    /// </summary>
    public static bool HasObtainHook(RelicModel relic)
    {
        var method = relic.GetType().GetMethod("AfterObtained",
            BindingFlags.Instance | BindingFlags.Public);
        return method != null && method.DeclaringType != typeof(RelicModel);
    }
}
