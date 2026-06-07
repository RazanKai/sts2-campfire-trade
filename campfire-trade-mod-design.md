# Campfire Trade — Mod Design Document

**Mod ID:** `com.modder.campfire-trade`
**Target:** Slay the Spire 2 (Early Access, **v0.107.0** — commit `23d60b98`, 2026-06-04)
**Stack:** C# / **.NET 9** (Godot.NET.Sdk/4.5.1 project), game-bundled Harmony 2.4.2 (`0Harmony.dll`) + MonoMod, assembly publicizer, **BaseLib** (hard dependency), STS2 modding API; co-op networking via `INetMessage`/`NetService` (Steamworks transport).

> **Scope note (this revision):** the mod is **multiplayer-only**. The earlier singleplayer "Exchange with the pool" feature has been **removed**. What remains is **player-to-player trading** at rest sites: **cards** trade under a **rarity point-balance** rule; **potions and relics** trade freely under **slot caps** (no points). See `REVIEW.md` for the API audit and `PRIOR-ART-COMPARISON.md` for the two shipping mods this design now borrows from (`chaendizzle/STS2Trade`, `sirposh777/campfire-trading-update`).
>
> **Key de-risking:** the one make-or-break unknown in prior revisions — whether a mod-defined `INetMessage` subtype round-trips across co-op clients — is **resolved YES** by the shipping `chaendizzle/STS2Trade` mod. No networking spike is required.

---

## Overview

Campfire Trade adds one new option to every co-op rest site: **Trade with Player**. Any player at the rest site can propose a direct trade with another. A single trade can include cards, potions, and relics. The trade requires mutual confirmation and is applied **deterministically and identically on every client** (STS2 co-op has no separate server).

The constraint that makes it fair: **the card portion of both sides must have equal total trade value** (a rarity point system). Potions and relics are not point-valued; they are limited only by per-side slot caps and a few non-tradeable rules. Trading does **not** consume the campfire action — Rest and Forge remain available — so co-op players never have to choose between cooperating and healing.

This fills a gap nothing else in the run touches: a social negotiation layer between co-op players, plus a way to move a card to the teammate whose deck actually wants it.

---

## What can be traded

| Item type | Tradeable? | Balancing rule |
|---|---|---|
| **Cards** — Common / Uncommon / Rare | Yes | **Point-balanced** (see below) |
| Cards — Basic (starters), Ancient, Event, Token, Status, Quest, Curse | **No** | — |
| **Potions** | Yes | Slot cap only |
| **Relics** | Yes | Slot cap only; quest relics & on-obtain-hook relics blockable by config |

### Card trade value (point system)

| Rarity | Value |
|---|---|
| Common | 1 |
| Uncommon | 2 |
| Rare | 4 |

Both sides' **card subtotals must be equal and greater than zero** for the Confirm button to enable. Upgraded cards carry the same value as their base version — `+` is preserved on the traded card but does not change its point value. Potions and relics in the same trade do **not** count toward the card subtotal; they are governed separately by slot caps. A valid trade may therefore be "2 Commons + 1 potion" for "1 Uncommon + 1 relic" (cards: 2 = 2 ✓; potion/relic within caps).

Example balanced card combinations:

| One side | Other side |
|---|---|
| 1 Rare (4) | 1 Rare (4) · or 2 Uncommons · or 1 Uncommon + 2 Commons · or 4 Commons |
| 1 Uncommon (2) | 1 Uncommon (2) · or 2 Commons |
| 1 Common (1) | 1 Common (1) |

A trade with **no cards on either side** (potions/relics only) is allowed as long as it stays within slot caps — the card-balance rule only fires when cards are present.

### Slot caps & non-tradeable rules (config-driven)

| Option | Range | Default | Effect |
|---|---|---|---|
| `MaxCardSlots` | 1–5 | 3 | Max cards per side |
| `MaxPotionSlots` | 1–3 | 3 | Max potions per side |
| `MaxRelicSlots` | 1–3 | 1 | Max relics per side |
| `UnlimitedTrades` | on/off | off | Allow >1 completed trade per player per rest site |
| `BlockObtainHookRelics` | on/off | on | Disallow trading relics with meaningful `AfterObtained` hooks |
| `BlockQuestCards` | on/off | on | Disallow trading quest cards |

Config is a `BaseLib.Config.SimpleModConfig` (free in-game settings UI + persistence). The **host broadcasts its config** to all clients at rest-site start so everyone validates against identical rules — essential for determinism.

---

## Trade flow (symmetric mutual-build + confirm gate)

This adopts the proven flow from `chaendizzle/STS2Trade` rather than an asymmetric offer→counter flow. Because only the *card subtotal* must balance and potions/relics are free, balance is enforced as a **Confirm gate**, not by driving the UI — both players build their halves live, and Confirm stays disabled until the rule is met.

```
[ REST SITE — co-op ]
  [Rest]  [Forge]  [Trade with Player]   (+ relic-unlocked options)

        |  Player A clicks Trade
        v

[ PICK A PARTNER ]
  A targets another player's character (game's targeting system).
  If the target can't trade (already traded, or already used their
  action and options were cleared), a thought bubble says so; no cost.

        |  A targets B, and B targets A  → mutual match
        v

[ SHARED TRADE SCREEN  (open on both A and B) ]
  Player A's half            Player B's half
  ─────────────────         ─────────────────
  Cards:  [ ... ]            Cards:  [ ... ]
  Potions:[ ... ]            Potions:[ ... ]
  Relics: [ ... ]            Relics: [ ... ]
  Card value: 2             Card value: 2   ✓ balanced
  [ Confirm ]                [ Confirm ]
  • Each player adds items from their own deck/belt/relics, live.
  • Non-tradeable items are dimmed/unselectable (curses, starters,
    quest cards, blocked relics).
  • Live per-side card-value counter. Confirm DISABLED until:
      – card subtotals equal and > 0 (or no cards on either side), AND
      – each side within MaxCard/Potion/Relic slot caps, AND
      – resulting potion counts fit each player's potion slots.
  • Changing your offer after confirming auto-revokes your confirm
    AND your partner's confirm (no stale agreements).

        |  both Confirm
        v

[ TRADE APPLIED ]
  Swap runs identically on every client (deterministic).
  Trade option consumed for both players (unless UnlimitedTrades).
  Rest and Forge remain available — trade does NOT cost the action.
```

### Cancellation & timeouts

| Moment | Who | Outcome |
|---|---|---|
| Before targeting / while waiting for match | A | Esc / right-click cancels; no cost; option stays |
| Partner never matches | — | 2-minute wait timeout; clean cancel; no cost |
| Shared trade screen | either player | Cancel sends `TradeCancelMessage`; both close; no cost |
| Both confirm | — | Trade executes; option consumed for both |

A player who cancels or declines keeps their trade slot — they can still initiate or accept a different trade at the same rest site.

### Once per campfire

By default each player may complete **one** trade per rest site. The option is added for **all** players every time (to keep option indices identical across machines) but is **grayed out** via `IsEnabled` when that player can no longer trade. `UnlimitedTrades` removes the limit. In 3+ player runs, one trade pair resolves at a time; uninvolved machines still apply the swap as **observers** so all clients stay in sync.

---

## Networking model

**No separate server.** STS2 co-op applies synchronized choices on every client; each machine independently runs the same logic from the same synced inputs (the `EventSynchronizer` pattern). So the design is **deterministic synchronized application**, not client/server.

Messages are mod-defined `INetMessage` **structs** (confirmed to round-trip):

| Message | Carries | Purpose |
|---|---|---|
| `TradeTargetMessage` | `hasTarget`, `targetPlayerId` | who I want to trade with (mutual-match handshake) |
| `TradeOfferMessage` | `int[] cardDeckIndices`, `int[] potionSlotIndices`, `int[] relicIndices` | my current offer |
| `TradeConfirmMessage` | `confirmed` | my confirm state |
| `TradeCancelMessage` | — | abort |
| `TradeConfigMessage` | all config fields | host → clients rule sync |

**Offers travel as indices, not serialized cards.** Both clients hold the fully-replicated run state, so a deck/slot/relic index unambiguously identifies the item on every machine. This avoids serializing/reconstructing card objects over the wire entirely. Identity is `Player.NetId` (`ulong`); the sender's NetId arrives as the second handler argument. Card/relic objects are only cloned/serialized **locally at execution time**, never sent.

To keep the co-op checksum aligned, the Trade option reserves and syncs a choice id (`PlayerChoiceSynchronizer.ReserveChoiceId` / `SyncLocalChoice` / `WaitForRemoteChoice`), mirroring `MendRestSiteOption`.

---

## Trade execution (deterministic swap)

Runs identically on each client when both players have confirmed. Three phases (from the shipping mod, proven against the async Cmd API):

1. **Phase 0 — snapshot & clone, before any mutation.**
   - Cards: resolve offered deck indices to `CardModel`s, then `card.ClonePreservingMutability()` (preserves `+`/enchantment; keeps the clone valid after the original is removed). Defensively skip out-of-range indices.
   - Potions: save their `Id`s (fresh instances are created on the receiver).
   - Relics: `relic.ToSerializable()` (preserves saved counters/flags).
2. **Phase 1 — remove all, from both players.** `CardPileCmd.RemoveFromDeck(card, showPreview:false)`; `PotionCmd.Discard(potion)`; `RelicCmd.Remove(relic)` (handle `PotionBelt` by manually adjusting `MaxPotionCount`). Removals first so potion slots free up before additions.
3. **Phase 2 — add all, to destination players.** Cards: `clone.Owner = null` → `runState.AddCard(clone, target)` → `await CardPileCmd.Add(clone, PileType.Deck)`, then `target.Deck.InvokeCardAddFinished()` (the deck counter UI listens for this; nothing else fires it without a fly animation). Potions: `ModelDb.GetById<PotionModel>(id).ToMutable()` → `PotionCmd.TryToProcure(fresh, target)`. Relics: **deferred** — collected into a pending list and obtained via `RelicCmd.Obtain` **after the trade screen closes**, in canonical (lower-NetId-first) order, so interactive `AfterObtained` hooks can show UI without desyncing; pre-expand potion slots for an incoming `PotionBelt`.

**Atomicity model:** there is no transaction object and the Cmd API is fully async, so "atomic" means **snapshot-then-remove-all-then-add-all with defensive index bounds and deterministic ordering on every client** — not "no awaits." Re-validate slot/potion-capacity at confirm time; the Confirm gate is the primary guard.

---

## Invariants — never break these

- **Curses are never tradeable** (by `CardRarity.Curse` or `CardType.Curse`).
- **Only the card subtotal is point-balanced**; potions/relics are slot-capped, not valued.
- **Card subtotals must be equal and > 0 when any cards are present**; an all-potion/relic trade is allowed within caps.
- **Trading does not consume the campfire action** (Rest/Forge remain).
- **Cards keep their upgrade/enchantment state across a trade** (`ClonePreservingMutability`).
- **Cancelling or declining does not consume a trade slot.**
- **Host config is authoritative**; clients validate against the broadcast rules.
- **The swap is deterministic on every client** (participants and observers alike).

---

## File structure

```
CampfireTrade/
├── CampfireTrade.json                  # manifest (<ModId>.json; has_pck/has_dll/dependencies)
├── CampfireTrade.csproj
├── project.godot · export_presets.cfg  # Godot.NET.Sdk build + MegaDot .pck export
├── src/
│   ├── MainFile.cs                     # [ModInitializer]; register config; Harmony.PatchAll()
│   ├── TradeConfig.cs                  # SimpleModConfig (slot caps + block flags)
│   ├── TradeRestSiteOption.cs          # RestSiteOption subclass (targeting, wait, open screen)
│   ├── TradeSynchronizer.cs            # net handlers, match detection, execute (active + observed)
│   ├── TradeState.cs                   # TradePhase, TradeOffer, TradeSession (+ confirm validation)
│   ├── TradeValidator.cs               # CARD point balance (pure, unit-tested)
│   ├── Patches/
│   │   ├── AddTradeOptionPatch.cs      # postfix RestSiteOption.Generate
│   │   ├── PreventDisableAfterTradePatch.cs   # prefix Hook.ShouldDisableRemainingRestSiteOptions
│   │   ├── InitTradeSyncPatch.cs       # prefix RestSiteSynchronizer.BeginRestSite
│   │   ├── SkipTradeConfirmationPatch.cs
│   │   └── AddNotificationManagerPatch.cs
│   ├── Messages/                       # 5 INetMessage structs (index-based)
│   └── UI/                             # NTradeScreen, NTradeSlot, NTradeItemPicker, NTradeTooltip, notifications
└── CampfireTrade/                      # packed assets: localization/eng/rest_site_ui.json, option_trade.png
```

This mirrors `chaendizzle/STS2Trade` closely on purpose — that layout is proven. The only net-new logic versus the shipping mod is `TradeValidator` (the card point system) and the Confirm gate wiring.

---

## Balance notes

**Why a point system for cards but not potions/relics:** cards are the deck's identity and the obvious imbalance/grief vector (dumping a Rare-for-Common). Points keep card trades fair while still allowing flexible multi-card combinations. Potions are consumable and self-limiting; relics are slot-capped and the truly griefable ones (quest, on-obtain) are blockable. Adding a point economy on top of those would be friction for little gain.

**Why a Confirm gate, not an asymmetric counter flow:** with potions/relics free and only card subtotals constrained, a shared live-build screen plus a balance check is simpler to build and to understand than a turn-based offer/counter, and it reuses the proven shipping-mod flow almost unchanged.

**Why trading doesn't cost the action:** the cost of trading is the cards/items themselves. Stacking the campfire action on top would punish co-op, which is the whole point of the feature.

**Griefing surface:** curses non-tradeable; the shared preview removes surprise; quest/on-obtain relics blockable; the per-player option grays out after a completed trade. Card balance prevents lopsided card swaps.

---

## Known risks & mitigations

- **API churn (Early Access).** Symbols below were taken from a *working* mod built against a nearby build; re-confirm `RestSiteOption.Generate`'s signature, `ClonePreservingMutability`, `InvokeCardAddFinished`, and the manifest schema against your decompiled `sts2.dll` (v0.107.0) via the `sts2-modding` MCP before relying on them. They are concrete symbols to verify, not guesses.
- **3+ player desync.** Handled by the observer-session pattern + canonical ordering + choice-id sync — do not skip these; they are the parts most likely to desync if omitted.
- **Deck-counter not updating after add.** Call `InvokeCardAddFinished()`; the counter won't move otherwise.
- **`UnlimitedTrades` + non-consume interaction.** Only mark a player in `JustCompletedTrade` when the trade will actually consume the option (i.e. not unlimited); otherwise the non-consume prefix lingers and wrongly suppresses the next Rest. (This was a real bug found in the shipping mod.)

---

## Summary

A multiplayer-only rest-site trade. Cards trade under a rarity point balance (Common 1 / Uncommon 2 / Rare 4, both sides' card totals equal); potions and relics trade freely within slot caps with curses/quest/on-obtain items excluded. The flow is a symmetric shared-build screen with a Confirm gate, applied deterministically on every client. The networking is confirmed to work, the swap/sync patterns are taken from a shipping mod, and the only bespoke logic is the card point system layered on top.

---

*References: `CLAUDE.md` (build guide) · `REVIEW.md` (API audit) · `PRIOR-ART-COMPARISON.md` · [chaendizzle/STS2Trade](https://github.com/chaendizzle/STS2Trade) · [sirposh777/campfire-trading-update](https://github.com/sirposh777/campfire-trading-update) · [STS2 modding MCP](https://github.com/elliotttate/sts2-modding-mcp).*
