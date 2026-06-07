# Prior-Art Comparison — Campfire Exchange vs. shipping trade mods

Compares two existing Slay the Spire 2 trade mods against `campfire-trade-mod-design.md`, `CLAUDE.md`, and `REVIEW.md`, and flags where to **adopt** their approach vs. **keep** ours.

- **`chaendizzle/STS2Trade`** ("Campfire Trading", v1.0) — the canonical, working source. Built on **BaseLib**. Trades cards + potions + relics at rest sites in co-op. This is the primary reference; all source citations below are from it.
- **`sirposh777/campfire-trading-update`** (v1.1b, May 22 2026) — a *decompiled* rebuild of chaendizzle's mod ported to a newer beta branch (committed `bin/obj`, `--y__InlineArray3.cs`, `IgnoresAccessChecksToAttribute`, namespaces split into `STS2Trade.Messages/.Patches/.UI`). Useful only as a signal of what drifts across game versions — not a clean source to copy from.

---

## Headline: the make-or-break spike is already answered — YES

Our design (`CLAUDE.md` Step 7, `REVIEW.md` "the one genuine blocker") treats one question as spike-or-die: *does a mod-defined `INetMessage` subtype round-trip across co-op clients through the `[GenerateSubtypes]` / `NetMessageBus` dispatch?*

chaendizzle's mod proves it does. It defines five custom messages — `TradeTargetMessage`, `TradeOfferMessage`, `TradeConfirmMessage`, `TradeCancelMessage`, `TradeConfigMessage` — registers them with `_netService.RegisterMessageHandler<T>(...)`, and sends them with `_netService.SendMessage(...)`. They cross the wire and dispatch to handlers on the other client. The `[GenerateSubtypes]` concern was unfounded; mod `INetMessage` subtypes work.

**Consequence: the trade is no longer gated on a risky spike.** You can skip the Step-0 networking spike entirely and build directly. This is the single most valuable thing these repos give you.

Two corrections to our message assumptions fall out of the real implementation:

- **`INetMessage` is implemented as a `struct`, not a class**, and the real interface members are `ShouldBroadcast`, `Mode` (`NetTransferMode`), `LogLevel` — there is **no `ShouldBuffer`** member (our `TradeMessages.cs` sketch invented it). Serialization is `Serialize(PacketWriter)` / `Deserialize(PacketReader)` with **bit-width-aware** writes: `writer.WriteInt(count, 8)` then per-element `writer.WriteInt(idx)`.
- **They send deck/slot/relic *indices* (`int[]`), not serialized cards.** Because both clients hold the fully-replicated run state, an index unambiguously identifies the card on every machine. This sidesteps our entire `CardSerializable` / `card.ToSerializable()` / reconstruct-via-`CloneCard` plan for the wire. **Adopt this** — it is dramatically simpler and avoids a class of reconstruction bugs. (Cards are still cloned locally at execution time; see deck mutation below.)

---

## Point-by-point: design vs. shipping mod

Legend: **ADOPT** = switch our design to theirs · **KEEP** = our design is better/intentionally different · **RECONCILE** = a genuine conflict to resolve before coding.

| Area | Our design (`CLAUDE.md` / design doc) | chaendizzle (shipping, working) | Verdict |
|---|---|---|---|
| Manifest file | `mod_manifest.json` with `pck_name` | `STS2Trade.json` (named `<ModId>.json`) with `id`, `name`, `author`, `version`, `has_pck`, `has_dll`, `dependencies`, `affects_gameplay` | **ADOPT** their format |
| Publicizer | `Krafs.Publicizer` | `<Publicize>true</Publicize>` on the `sts2` ref + `BepInEx.AssemblyPublicizer.MSBuild` | **ADOPT** theirs (proven) |
| Base library | "optional… RitsuLib or BaseLib" | **Hard dependency on BaseLib** (config, logger, analyzers) | **RECONCILE** — see below |
| Option injection | Override `Hook.ModifyRestSiteOptions` — "no Harmony needed" | **Harmony postfix on `RestSiteOption.Generate`** (returns `List<RestSiteOption>`; they `__result.Add(...)`) | **RECONCILE** — empirically they patch, not hook |
| Don't-consume-action | Override `Hook.ShouldDisableRemainingRestSiteOptions` | **Harmony prefix** on that hook + a `JustCompletedTrade` set (one-shot, per-NetId) | **ADOPT** the mechanism |
| Once-per-campfire state | "engine handles it per-player; no custom state" | Explicit `PlayersWhoTraded` / `JustCompletedTrade` sets in a synchronizer | **ADOPT** theirs (the engine does *not* fully handle it for a non-consuming option) |
| Co-op choice-id sync | not mentioned | `PlayerChoiceSynchronizer.ReserveChoiceId` / `SyncLocalChoice` / `WaitForRemoteChoice` (MendRestSiteOption pattern) | **ADOPT** — this is mandatory to avoid checksum desync |
| Wire payload | `List<CardSerializable>` via `ToSerializable()` | `int[]` indices into deck/potions/relics | **ADOPT** indices |
| 3+ player handling | one paragraph ("C waits") | full **observer-session** pattern: every machine runs `ExecuteObservedTrade` for trades it isn't in | **ADOPT** — required for >2 players |
| Trade matchmaking | asymmetric: A offers → B counter-offers → preview | **symmetric mutual-target handshake**: both target each other → shared live screen → both build offers → both confirm | **RECONCILE** — theirs is simpler & proven |
| Trade economy | **rarity point balance** (C=1/U=2/R=4, both sides equal) | **none** — slot-count caps only (max N cards/potions/relics), any items | **KEEP** ours (it's our core design idea) — but see risk |
| Confirm re-arm | 15s timeout | re-confirm auto-resets if partner changes offer after you confirmed (`PartnerOfferWhenLocalConfirmed`) | **ADOPT** their re-arm; KEEP a timeout if wanted |
| Deck mutation | `runState.CreateCard`/`CloneCard` + `CardPileCmd.Add` | `card.ClonePreservingMutability()` → `clone.Owner=null` → `runState.AddCard(clone, target)` → `await CardPileCmd.Add(clone, PileType.Deck)`; remove via `CardPileCmd.RemoveFromDeck(card, showPreview:false)` | **ADOPT** their exact calls |
| Deck-counter UI | not mentioned | must call `playerB.Deck.InvokeCardAddFinished()` after adds (no fly-vfx fires it) | **ADOPT** — non-obvious, will bite you |
| Atomicity | "existence-check, then no `await`/yield mid-swap" | fully awaited Cmd sequence; bounds indices defensively; relies on synced determinism + clone-before-remove | **RECONCILE** — our no-await invariant is impractical |
| Scope | cards only | cards + potions + relics (incl. PotionBelt slot math, deferred relic `AfterObtained` hooks) | **KEEP** cards-only for v1 (their relic path is the hardest code in the repo) |
| Curse / non-tradeable | filter by `CardRarity` + `CardType` | `BlockQuestCards`, `BlockObtainHookRelics` config flags | **KEEP** ours; optionally add their config flags |
| Singleplayer Exchange | core feature | **does not exist** in either repo | **KEEP** — this is entirely ours |

---

## The conflicts worth resolving before you write code

**1. Harmony patch vs. `Hook.ModifyRestSiteOptions`.** This is the sharpest contradiction. `REVIEW.md` audited `Hook.ModifyRestSiteOptions(IRunState, Player, ICollection<RestSiteOption>)` as "real, dedicated hook" and made "no Harmony needed" the spine of the plan. But the *working* mod ignores that hook and Harmony-postfixes `RestSiteOption.Generate(Player) → List<RestSiteOption>` instead, and Harmony-prefixes `Hook.ShouldDisableRemainingRestSiteOptions` rather than overriding it. Possible explanations: the hook-override registration path isn't actually reachable by mods on the shipping build, the two builds differ, or the author simply found patching more reliable. Either way, the empirically-proven path is **Harmony on `Generate`**. Recommendation: verify whether the hook-override mechanism is real and invokable on your build with the `sts2-modding` MCP; if there's any doubt, do what ships — Harmony-patch `Generate`. Don't bet the feature on an unexercised hook when a 2-line postfix is known to work.

**2. BaseLib as a hard dependency.** Their `TradeConfig : SimpleModConfig` gets a free, persisted, in-game settings UI from `BaseLib.Config`, their logger is `BaseLib`-adjacent, and they pull `Alchyr.Sts2.ModAnalyzers`. Our design lists BaseLib as optional. Given the working mod leans on it for config + loader plumbing, **take the BaseLib dependency** unless you have a reason not to — it's the path the ecosystem and this proven mod actually use, and it removes a pile of hand-rolled infrastructure.

**3. Atomic swap invariant.** Our `CLAUDE.md` and design doc make "no `await`/yield between existence-check and the last mutation" a hard invariant. The shipping mod cannot honor that — every `CardPileCmd.Add/RemoveFromDeck`, `PotionCmd`, `RelicCmd` call is awaited, with work between them. It achieves correctness differently: snapshot/clone everything up front (Phase 0), remove all (Phase 1), add all (Phase 2), bound indices defensively, and rely on each machine executing the *same* synchronized inputs deterministically. **Rewrite the invariant** to "snapshot-then-remove-all-then-add-all, deterministic on every client" rather than "no awaits." The no-await rule is not achievable against the real async Cmd API.

**4. Rarity point system — keep it, but know the cost.** Neither repo has any value-balance logic; they cap slot counts and let players trade freely (this is the well-known griefing/imbalance surface — "give me your Rare for my Curse"… except curses are blocked, but "1 Common for your Rare" is allowed). Our point system is a genuine design improvement and the main reason to build our own trade rather than just fork theirs. Keep it — but it forces the **asymmetric offer→counter flow** (B must be shown which cards balance A's value), whereas their **symmetric mutual-build** flow is simpler and proven. You're choosing: keep our richer economy + accept building the harder UI/flow, or adopt their flow + drop the point system. They are somewhat coupled.

---

## What our design has that theirs doesn't

- **Singleplayer Exchange** — the pool-swap feature. Absent from both repos. This is unique to us and buildable independently of all the multiplayer machinery; ship it first regardless of what you decide about trade.
- **Rarity value economy** for trades (above).
- **Mod-to-mod extension events** (`OnBeforeExchange`/`OnExchangeComplete`/etc.). Their mod exposes none.

---

## Revised build order (folding in the prior art)

1. **Scaffold against the proven toolchain**, not our assumed one: `Godot.NET.Sdk/4.5.1`, `net9.0`, `<ModId>.json` manifest, `BepInEx.AssemblyPublicizer.MSBuild` (or the `<Publicize>` ref), BaseLib dependency, MegaDot `.pck` export target. Copy chaendizzle's `.csproj` as the starting template.
2. **Singleplayer Exchange** — entirely ours, no networking, no spike. (Use Harmony-postfix on `RestSiteOption.Generate` to inject the option, per the proven path.)
3. **`TradeValidator`** (the point system) + unit tests — still pure, still worth doing first for the trade.
4. **~~Co-op spike~~ — skip it.** Already proven. Go straight to trade.
5. **Trade**, reusing chaendizzle's resolved patterns wholesale: index-based messages, `RegisterMessageHandler<T>`, the `PlayerChoiceSynchronizer` choice-id dance, the `JustCompletedTrade` non-consume mechanism, the observer-session pattern for 3+ players, the Phase 0/1/2 clone-remove-add swap, and `InvokeCardAddFinished()`. Layer our rarity-balance validation and (if kept) asymmetric flow on top.

---

## Caveats

These mods target the game build *they* were written against (chaendizzle ~v1.0; sirposh's port dated May 21 2026). Our project pins **v0.107.0 (commit `23d60b98`, 2026-06-04)**. API names like `ClonePreservingMutability`, `InvokeCardAddFinished`, `RestSiteOption.Generate`'s return type, and the manifest schema should be re-confirmed against *your* decompiled `sts2.dll` via the `sts2-modding` MCP before relying on them — but they are now concrete, working symbols to verify rather than guesses to invent.

---

*Sources: [chaendizzle/STS2Trade](https://github.com/chaendizzle/STS2Trade) · [sirposh777/campfire-trading-update](https://github.com/sirposh777/campfire-trading-update). Compared against `campfire-trade-mod-design.md`, `CLAUDE.md`, `REVIEW.md`.*
