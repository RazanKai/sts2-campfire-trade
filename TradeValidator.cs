using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace CampfireTrade;

/// <summary>
/// The only net-new logic in CampfireTrade: the card rarity point-balance rule.
///
/// Cards are valued Common 1 / Uncommon 2 / Rare 4. A trade is only confirmable
/// when both sides' card subtotals are equal and greater than zero (or there are
/// no cards on either side — a potion/relic-only trade). Potions and relics are
/// NOT valued here; they are slot-capped elsewhere.
///
/// Pure and deterministic so it can be unit-reasoned and runs identically on
/// every client.
/// </summary>
public static class TradeValidator
{
    private static readonly Dictionary<CardRarity, int> Points = new()
    {
        // Basic (starters) are valued 1 — only ever counted when AllowStarterCards
        // is on (IsCardTradeable gates whether they can be offered at all).
        { CardRarity.Basic, 1 },
        { CardRarity.Common, 1 },
        { CardRarity.Uncommon, 2 },
        { CardRarity.Rare, 4 },
    };

    /// <summary>
    /// A card may be offered only if it has a point value (Common/Uncommon/Rare, plus
    /// Basic when AllowStarterCards is on), is not a Curse/Status/Quest by type, and is
    /// removable from the deck. Curses, tokens, event, and ancient cards are never
    /// tradeable; starters (Basic) are tradeable only when the config toggle is enabled.
    /// </summary>
    public static bool IsCardTradeable(CardModel c)
    {
        if (!Points.ContainsKey(c.Rarity))
            return false;
        if (c.Rarity == CardRarity.Basic && !TradeConfig.AllowStarterCards)
            return false;
        if (c.Type is CardType.Curse or CardType.Status)
            return false;
        // Quest cards are gated by config. Previously Quest was ALSO excluded
        // unconditionally on the line above, which made BlockQuestCards dead code;
        // the toggle now actually controls Quest tradeability (default: blocked).
        if (TradeConfig.BlockQuestCards && c.Type == CardType.Quest)
            return false;
        if (!c.IsRemovable)
            return false;
        return true;
    }

    /// <summary>Point value of a single card (0 if it has no rarity value).</summary>
    public static int CardValue(CardModel c) => Points.GetValueOrDefault(c.Rarity, 0);

    /// <summary>Sum of point values for a set of cards.</summary>
    public static int CardValue(IEnumerable<CardModel> cards) => cards.Sum(CardValue);

    /// <summary>
    /// Card subtotals must match. Empty-on-both-sides is allowed (a potion/relic-only
    /// trade). Whenever any cards are present, both sides must be equal and &gt; 0.
    /// </summary>
    public static bool CardsBalanced(IReadOnlyList<CardModel> a, IReadOnlyList<CardModel> b)
    {
        if (a.Count == 0 && b.Count == 0)
            return true;
        // Point balance disabled: cards trade freely, any combination allowed.
        if (!TradeConfig.EnablePointBalance)
            return true;
        int va = CardValue(a);
        int vb = CardValue(b);
        return va == vb && va > 0;
    }
}
