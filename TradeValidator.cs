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
        { CardRarity.Common, 1 },
        { CardRarity.Uncommon, 2 },
        { CardRarity.Rare, 4 },
    };

    /// <summary>
    /// A card may be offered only if it has a point value (Common/Uncommon/Rare),
    /// is not a Curse/Status/Quest by type, and is removable from the deck.
    /// Curses, starters, tokens, event, and ancient cards are never tradeable.
    /// </summary>
    public static bool IsCardTradeable(CardModel c)
    {
        if (!Points.ContainsKey(c.Rarity))
            return false;
        if (c.Type is CardType.Curse or CardType.Status or CardType.Quest)
            return false;
        if (!c.IsRemovable)
            return false;
        if (TradeConfig.BlockQuestCards && c.Type == CardType.Quest)
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
        int va = CardValue(a);
        int vb = CardValue(b);
        return va == vb && va > 0;
    }
}
