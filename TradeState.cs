using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace CampfireTrade;

public enum TradePhase
{
    Idle,
    SelectingPartner,
    WaitingForPartner,
    Trading,
    Completed
}

public class TradeOffer
{
    public List<int> CardDeckIndices { get; } = new();
    public List<int> PotionSlotIndices { get; } = new();
    public List<int> RelicIndices { get; } = new();

    public void Clear()
    {
        CardDeckIndices.Clear();
        PotionSlotIndices.Clear();
        RelicIndices.Clear();
    }

    public bool IsEmpty => CardDeckIndices.Count == 0 && PotionSlotIndices.Count == 0 && RelicIndices.Count == 0;

    public bool Equals(TradeOffer? other)
    {
        if (other == null) return false;
        if (CardDeckIndices.Count != other.CardDeckIndices.Count) return false;
        if (PotionSlotIndices.Count != other.PotionSlotIndices.Count) return false;
        if (RelicIndices.Count != other.RelicIndices.Count) return false;
        for (int i = 0; i < CardDeckIndices.Count; i++)
            if (CardDeckIndices[i] != other.CardDeckIndices[i]) return false;
        for (int i = 0; i < PotionSlotIndices.Count; i++)
            if (PotionSlotIndices[i] != other.PotionSlotIndices[i]) return false;
        for (int i = 0; i < RelicIndices.Count; i++)
            if (RelicIndices[i] != other.RelicIndices[i]) return false;
        return true;
    }

    public TradeOffer Clone()
    {
        var clone = new TradeOffer();
        clone.CardDeckIndices.AddRange(CardDeckIndices);
        clone.PotionSlotIndices.AddRange(PotionSlotIndices);
        clone.RelicIndices.AddRange(RelicIndices);
        return clone;
    }
}

public class TradeSession
{
    public ulong LocalPlayerId { get; set; }
    public ulong PartnerPlayerId { get; set; }
    public TradeOffer LocalOffer { get; } = new();
    public TradeOffer PartnerOffer { get; } = new();
    public TradeOffer? PartnerOfferWhenLocalConfirmed { get; set; }
    public bool LocalConfirmed { get; set; }
    public bool PartnerConfirmed { get; set; }

    public bool BothConfirmed => LocalConfirmed && PartnerConfirmed;

    public void Reset()
    {
        LocalOffer.Clear();
        PartnerOffer.Clear();
        PartnerOfferWhenLocalConfirmed = null;
        LocalConfirmed = false;
        PartnerConfirmed = false;
    }

    public bool ValidateCanConfirm(Player localPlayer, Player partnerPlayer)
    {
        return GetValidationError(localPlayer, partnerPlayer) == null;
    }

    /// <summary>
    /// Returns a human-readable error string if the trade would be invalid, or null if valid.
    /// Accounts for Potion Belt being traded (changes effective max potion count).
    /// </summary>
    /// <summary>Resolves a side's offered card deck-indices into CardModels (bounds-checked).</summary>
    public static List<CardModel> GetOfferedCards(TradeOffer offer, Player player)
    {
        var result = new List<CardModel>();
        var cards = player.Deck?.Cards;
        if (cards == null) return result;
        foreach (int idx in offer.CardDeckIndices)
            if (idx >= 0 && idx < cards.Count)
                result.Add(cards[idx]);
        return result;
    }

    public string? GetValidationError(Player localPlayer, Player partnerPlayer)
    {
        // --- Net-new: slot caps + card rarity-point balance ---
        if (LocalOffer.CardDeckIndices.Count > TradeConfig.MaxCardSlotsInt
            || PartnerOffer.CardDeckIndices.Count > TradeConfig.MaxCardSlotsInt)
            return $"Cards exceed the limit of {TradeConfig.MaxCardSlotsInt} per side.";
        if (LocalOffer.PotionSlotIndices.Count > TradeConfig.MaxPotionSlotsInt
            || PartnerOffer.PotionSlotIndices.Count > TradeConfig.MaxPotionSlotsInt)
            return $"Potions exceed the limit of {TradeConfig.MaxPotionSlotsInt} per side.";
        if (LocalOffer.RelicIndices.Count > TradeConfig.MaxRelicSlotsInt
            || PartnerOffer.RelicIndices.Count > TradeConfig.MaxRelicSlotsInt)
            return $"Relics exceed the limit of {TradeConfig.MaxRelicSlotsInt} per side.";

        var localCards = GetOfferedCards(LocalOffer, localPlayer);
        var partnerCards = GetOfferedCards(PartnerOffer, partnerPlayer);
        if (!TradeValidator.CardsBalanced(localCards, partnerCards))
        {
            int lv = TradeValidator.CardValue(localCards);
            int pv = TradeValidator.CardValue(partnerCards);
            return $"Card values must match and be above zero ({lv} vs {pv}).";
        }

        // An empty trade (nothing on either side) is not a real trade.
        if (LocalOffer.IsEmpty && PartnerOffer.IsEmpty)
            return "Add at least one item to trade.";

        // Calculate Potion Belt adjustments
        int localMaxAdj = 0;
        int partnerMaxAdj = 0;

        foreach (int idx in LocalOffer.RelicIndices)
        {
            if (idx >= 0 && idx < localPlayer.Relics.Count && localPlayer.Relics[idx] is PotionBelt belt)
            {
                int slots = belt.DynamicVars["PotionSlots"].IntValue;
                localMaxAdj -= slots;    // Local loses potion slots
                partnerMaxAdj += slots;  // Partner gains potion slots
            }
        }

        foreach (int idx in PartnerOffer.RelicIndices)
        {
            if (idx >= 0 && idx < partnerPlayer.Relics.Count && partnerPlayer.Relics[idx] is PotionBelt belt)
            {
                int slots = belt.DynamicVars["PotionSlots"].IntValue;
                partnerMaxAdj -= slots;  // Partner loses potion slots
                localMaxAdj += slots;    // Local gains potion slots
            }
        }

        int localEffectiveMax = localPlayer.MaxPotionCount + localMaxAdj;
        int partnerEffectiveMax = partnerPlayer.MaxPotionCount + partnerMaxAdj;

        // Count potions each player keeps (non-null potions not being traded away)
        int localPotionsAfter = 0;
        for (int i = 0; i < localPlayer.PotionSlots.Count; i++)
        {
            if (localPlayer.PotionSlots[i] != null && !LocalOffer.PotionSlotIndices.Contains(i))
                localPotionsAfter++;
        }
        localPotionsAfter += PartnerOffer.PotionSlotIndices.Count;

        int partnerPotionsAfter = 0;
        for (int i = 0; i < partnerPlayer.PotionSlots.Count; i++)
        {
            if (partnerPlayer.PotionSlots[i] != null && !PartnerOffer.PotionSlotIndices.Contains(i))
                partnerPotionsAfter++;
        }
        partnerPotionsAfter += LocalOffer.PotionSlotIndices.Count;

        if (localPotionsAfter > localEffectiveMax)
            return $"You would have {localPotionsAfter} potions but only {localEffectiveMax} slots.";

        if (partnerPotionsAfter > partnerEffectiveMax)
            return $"Your partner would have {partnerPotionsAfter} potions but only {partnerEffectiveMax} slots.";

        return null;
    }
}
