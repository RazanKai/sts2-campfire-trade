# Review — Campfire Exchange design & CLAUDE.md

Reviewer: Opus. Scope: `CLAUDE.md` + `campfire-trade-mod-design.md`.

**This revision is an API audit.** The earlier review reasoned from the community tutorial inventory and two shipping mods, so most concrete symbols were marked "unverified / probably invented." This pass resolves every one of them against the **actual decompiled game** via the `sts2-modding` MCP. The headline: the design is in much better shape than the prior review feared — the rest-site system has a *dedicated, co-op-aware modding hook*, and almost all of the "invented" types are real. A handful of names were genuinely wrong and are corrected below.

No mod source code exists yet, so this still reviews the *plan*, but now against ground truth.

## How the API claims were verified

The `sts2-modding` MCP decompiles the shipping `sts2.dll` and indexes it (Roslyn). Setup status confirms: game found, source decompiled (3,422 `.cs` files), all tools ready. Every symbol below was checked with `get_entity_source` / `search_game_code` / `get_hook_signature` / `browse_namespace` against that decompiled tree, not against a tutorial.

### Version correction (important)

- **The installed game is `v0.107.0`** (commit `23d60b98`, dated 2026-06-04), **not `v0.99.1`.** Both docs are pinned to a stale version. Engine: **Godot 4.5.1, C#/.NET 9**. Bundled modding libs: **Harmony 2.4.2 + MonoMod**. The `net9.0` / `Godot.NET.Sdk/4.5.1` toolchain in CLAUDE.md is correct; the version string in both docs is not.

### Confirmed REAL (the prior review thought several of these were invented)

| Symbol | Status | Where |
|---|---|---|
| `RestSiteRoom : AbstractRoom` | **real** | `MegaCrit.Sts2.Core.Rooms.RestSiteRoom` |
| `RestSiteOption` (abstract class) | **real** | `MegaCrit.Sts2.Core.Entities.RestSite.RestSiteOption` |
| `Hook.ModifyRestSiteOptions(IRunState, Player, ICollection<RestSiteOption>)` | **real, dedicated hook** | hook surface |
| `Hook.ShouldDisableRemainingRestSiteOptions(IRunState, Player) → bool` | **real** | hook surface |
| `CardRarity` enum | **real** | `MegaCrit.Sts2.Core.Entities.Cards.CardRarity` |
| `INetMessage : IPacketSerializable` | **real** | `MegaCrit.Sts2.Core.Multiplayer.Serialization` |
| `RunManager.Instance.NetService` + `RegisterMessageHandler<T>` / `SendMessage` | **real** | networking guide + `NetMessageBus` |
| `Player.NetId` (ulong) | **real** | players |
| `CardPileCmd.Add` / `RemoveFromDeck` | **real** | `MegaCrit.Sts2.Core.Commands.CardPileCmd` |
| `RunState.CreateCard(model, owner)` / `CloneCard(card)` | **real** | run state |
| `CardFactory.CreateForReward(player, count, CardCreationOptions)` | **real** | `MegaCrit.Sts2.Core.Factories.CardFactory` |
| `CardSelectCmd.FromDeckGeneric / FromChooseACardScreen / FromSimpleGridForRewards` | **real** | `CardSelectCmd` |
| `RunRngSet` / `Rng` / `RunRngType` | **real** | RNG guide |
| `ModHelper.ConcatModelsFromMods`, `ModelDb`, `PlayerChoiceContext`, `IRunState`, `AbstractRoom` | **real** | as before |

### Genuinely wrong names (corrected in both docs)

- `RestSiteRoom.GetOptions()` — **does not exist.** Options come from a `RestSiteSynchronizer` (`RunManager.Instance.RestSiteSynchronizer`), exposed as `RestSiteRoom.Options`. The whole Harmony-postfix-on-`GetOptions` approach is replaced by the real hook (see below).
- `CardPool.All()` — **does not exist.** Use `CardFactory.CreateForReward` with a `CardCreationOptions` built from `player.Character.CardPool` (a `CardPoolModel`).
- `RunState.BeginTransaction()` — **does not exist** (correctly flagged before; confirmed).
- `CardModel.CreateInstance(upgraded:)` — **does not exist.** Real creation is `RunState.CreateCard(model, owner)` (fresh, unupgraded) or `RunState.CloneCard(card)` (preserves upgrade/enchantment state).
- `Deck.Remove` / `Deck.Add` — **not the mutation path.** Use `CardPileCmd.RemoveFromDeck(card)` and `CardPileCmd.Add(card, PileType.Deck)`, and a card must be registered to the RunState (`CreateCard`/`CloneCard`) *before* `Add` to a deck — `Add` throws otherwise (`"… must be added to a RunState before adding it to your deck."`).

## The single biggest finding: rest-site modding is a solved, co-op-aware path

The prior review and both docs assumed "there is no campfire-options hook" and led with a guessed Harmony patch. **That is false.** The real flow:

```csharp
// RestSiteOption.Generate(player) builds Heal + Smith (+ Mend if Players.Count > 1), THEN:
Hook.ModifyRestSiteOptions(player.RunState, player, list);   // ← the injection point
```

So a mod injects by overriding `Hook.ModifyRestSiteOptions` and `list.Add(new MyOption(player))`. No Harmony at all. Three consequences the docs must absorb:

1. **Options are generated and stored *per player*** (`RestSiteSynchronizer` keeps a `PlayerRestSite` per player slot) and **selection is already co-op-synchronized** (`OptionIndexChosenMessage` over the net). The elaborate `ExchangeState` / `ConditionalWeakTable` design for "per-room vs per-player once-per-campfire" is **largely unnecessary** — the engine removes a chosen option from that player's list, so "once per campfire per player" is automatic.

2. **"Does it consume the campfire action?" is exactly `Hook.ShouldDisableRemainingRestSiteOptions(runState, player)`.** After any successful option, the synchronizer either clears *all* remaining options (hook returns `true`, default — this is what "consuming the campfire action" means) or just removes the one chosen (`false`). So:
   - **Singleplayer Exchange** → consumes the action: leave the default (`true`).
   - **Trade** → must *not* consume the action: the mod's hook impl returns `false` when the just-chosen option was the trade. The chosen index is available (`RestSiteSynchronizer.GetChosenOptionIndex` / the `AfterPlayerOptionChosen` event) at the time the hook runs, so this is implementable — but it's a real subtlety the design's "just don't consume it" hand-wave never accounted for.

3. **A `RestSiteOption` is an abstract class, not an object initializer.** The `new RestSiteOption { Key=, Label=, Tooltip=, OnSelect= }` snippet in both docs is invalid. Real subclasses (`HealRestSiteOption`, `SmithRestSiteOption`, `CloneRestSiteOption`, `Cook/Dig/Hatch/Kindle/Lift/Mend`) implement: `OptionId`, `OnSelect() → Task<bool>`, ctor `(Player owner)`, localized `Title`/`Description` via `LocString("rest_site_ui", "OPTION_<ID>.name")`, and an **icon PNG** at `ui/rest_site/option_<id>.png`. The mod must therefore ship loc entries and an icon asset in its `.pck`.

## Singleplayer Exchange — buildable today, and simpler than written

Every piece exists; `CloneRestSiteOption` and `SmithRestSiteOption` are near-exact templates. The corrected flow:

- **Inject** via `Hook.ModifyRestSiteOptions` (no Harmony).
- **Pick a card to give up:** `CardSelectCmd.FromDeckGeneric(player, prefs, filter)` (or `FromDeckForRemoval`) — shows `NDeckCardSelectScreen` and is *already co-op-synced* via `PlayerChoiceSynchronizer`.
- **Generate 3 candidates of the same rarity from the player's class pool:**

  ```csharp
  var rng = player.RunState.RunRngSet.GetRng(RunRngType.CardReward);
  var opts = CardCreationOptions
      .ForNonCombatWithUniformOdds(
          new[] { player.Character.CardPool },
          c => c.Rarity == selected.Rarity)
      .WithFlags(CardCreationFlags.NoRarityModification)
      .WithRngOverride(rng);
  var candidates = CardFactory.CreateForReward(player, 3, opts).ToList(); // List<CardCreationResult>
  ```

  This replaces the invented `CandidateGenerator` + `CardPool.All()` + `WeightedSample` + `System.Random`. It is deterministic (engine RNG), includes modded cards (pool-based), and is automatically in-class — which **dissolves the "30% out-of-class candidates" problem** (just don't add a cross-class pool).

- **Let the player pick one:** `CardSelectCmd.FromChooseACardScreen(context, candidates.Select(c => c.Card).ToList(), player, canSkip: true)` — note this throws if given **more than 3** cards, which is exactly the 3-candidate cap. Back-out maps to `canSkip`.
- **Swap:** `await CardPileCmd.RemoveFromDeck(selected);` then add the chosen candidate (already a RunState-registered `CardModel` from the factory) via `await CardPileCmd.Add(chosen, PileType.Deck);`. Unupgraded-on-arrival is the natural state of a freshly created card.

The only real new work is the option subclass + its icon/loc and the candidate-rarity wiring. No Harmony, no custom RNG, no custom state table.

## Multiplayer Trade — networking foundation is real; one precise blocker remains

The prior review oscillated ("Severity 1" → "downgraded"). The audit settles it: **the networking primitives are real and richer than assumed.**

- `INetMessage : IPacketSerializable` with `ShouldBroadcast`, `Mode` (`NetTransferMode`), `LogLevel`, `ShouldBuffer`. Implement with `PacketWriter`/`PacketReader`.
- Send: `RunManager.Instance.NetService.SendMessage(msg)`. Receive: `NetService.RegisterMessageHandler<T>(handler)`. Identity: `Player.NetId`.
- Card state crosses the wire via the real `card.ToSerializable()` (used throughout `CardPileCmd`), which is what the invented `CardSnapshot` should map onto. The receiver reconstructs into its own RunState via `CreateCard`/`CloneCard` preserving upgrade/enchantment.
- There is **no separate "server"**: STS2 co-op applies synchronized choices on every client (the rest-site synchronizer runs `ChooseOption` locally on each client when the index message arrives). So "server-authoritative" should be reframed as **deterministic synchronized application** — `TradeServer.cs`/`TradeClient.cs` as separate roles is the wrong mental model. There is also an action path (`INetAction`, `ActionEnqueuedMessage`, `RequestEnqueueActionMessage`) layered on the same bus.

**The one genuine blocker, now precisely located:** `INetMessage` carries `[GenerateSubtypes]`, and `NetMessageBus` deserializes by `Activator.CreateInstance(type)` from a type id sent on the wire. If `[GenerateSubtypes]` builds a *closed* subtype→id map at the game's compile time, a **mod-defined `INetMessage` subtype has no wire id** and won't round-trip across clients — even though `RegisterMessageHandler<T>` accepts it locally. **This is the entire Step-0 spike:** send a mod-defined `INetMessage` from one co-op client and successfully deserialize+handle it on another. If it works, the trade is mostly UI + validation. If it doesn't, options are (a) ride an existing synced primitive (e.g. piggyback on `PlayerChoiceSynchronizer`/`INetAction` if those are open to mods), or (b) ship singleplayer Exchange only. The same `[GenerateSubtypes]`/`Activator.CreateInstance` question applies to `INetAction`.

## Carry-over correctness bugs (still valid, still flagged)

These were right in the prior review and remain true against the real API:

- **Trade slot must be per-player, not per-room** — though note the engine's per-player option list now makes this mostly moot for the *option visibility*; the only per-player state worth keeping is whatever the trade handshake needs.
- **Existence-check every offered card before mutating** — `RemoveFromDeck` *throws* if a card isn't in the deck (`"You cannot remove a card that is not in the deck."`), so the trade execution must verify presence first or guard the exception, and must not partially apply.
- **No await/yield mid-swap** — still the way to keep the mutation observably atomic, since there's no transaction object.
- **Curses non-tradeable / cards are tied to `CardType` too** — `CardRarity` has `Curse`, but `CardType.Curse` also exists and the engine filters on type in places (`c.Type != CardType.Curse`). The non-tradeable predicate should check both rarity and type. Tradeable rarities are `Common`/`Uncommon`/`Rare`; `Basic` (starters), `Ancient`, `Event`, `Token`, `Status`, `Quest`, `Curse` are not tradeable as designed.

## Design choices worth reconsidering (unchanged)

- **Curse → random Curse** is pure downside; also low value given `CurseCardPool` content. Reconsider supporting it.
- **30% out-of-class candidates** — resolved structurally by using `player.Character.CardPool` (in-class only). Drop the rule.
- **Veto event multicast trap** (`OnBeforeExchange` returning only the last subscriber's bool) — iterate the invocation list.
- **`TradeProposal` is referenced but never defined** — define it before the trade code.

## What's solid

The singleplayer Exchange is well-scoped and now demonstrably buildable on confirmed API with *less* custom code than the design assumed (no Harmony, no custom RNG, no custom state table — the engine's rest-site synchronizer does the heavy lifting). `TradeValidator` (the point system) is clean, pure, and unit-testable first. The balance reasoning is coherent. The docs' instinct to reuse existing card screens is correct and the real `CardSelectCmd` family makes it true. The honesty about uncertainty was warranted — and the fix is to act on it: the rest-site path is now resolved, and the trade has exactly one spike-or-die question.

## Suggested build order

1. Scaffold from Alchyr's `ModTemplate-StS2`; get the mod loading and confirm in the game log. Adopt RitsuLib or BaseLib for patcher/logger/settings/loc.
2. `TradeValidator` + unit tests (pure C#, no game API).
3. **Singleplayer Exchange** via `Hook.ModifyRestSiteOptions`, a `RestSiteOption` subclass, `CardSelectCmd` + `CardFactory.CreateForReward`, and the `CardPileCmd` swap path. Ship this independently.
4. **Co-op spike (make-or-break):** can a mod-defined `INetMessage` (or `INetAction`) subtype round-trip between two co-op clients through the `[GenerateSubtypes]`/`NetMessageBus` dispatch?
5. **Multiplayer Trade** only if the spike passes — built as synchronized application over the real net bus, with per-handshake state, execution-time existence checks, and the no-yield atomic swap. Not a bespoke client/server.

---
*All symbols above verified against the decompiled `sts2.dll` for `v0.107.0` (commit `23d60b98`) via the `sts2-modding` MCP on 2026-06-06.*
