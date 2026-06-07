# Campfire Trade

A **Slay the Spire 2** mod that adds **player-to-player trading** at rest sites in co-op. Move a card to the teammate whose deck actually wants it, swap spare potions, or hand off a relic — all without giving up your Rest or Forge.

> **Multiplayer only.** Targets STS2 Early Access **v0.107.0**. Built in C# / .NET 9 on the Godot SDK, using Harmony patches and the game's `INetMessage` co-op networking. Requires **[BaseLib](https://github.com/Alchyr/BaseLib-StS2)**.

## How it works

At any co-op rest site, a new **Trade with Player** option appears. Target a teammate; when you both target each other, a shared trade screen opens on both clients. Each side adds items from their own deck, potion belt, and relics, then confirms. On mutual confirm the swap applies deterministically and identically on every client.

Trading **does not consume the campfire action** — Rest and Forge stay available.

## What can be traded

| Item type | Tradeable? | Rule |
|---|---|---|
| Cards — Common / Uncommon / Rare | Yes | Rarity **point-balanced** (see below) |
| Cards — starters, Ancient, Event, Token, Status, Quest, Curse | No | — |
| Potions | Yes | Slot cap only |
| Relics | Yes | Slot cap only (quest & on-obtain-hook relics blockable by config) |

### Card point balance

| Rarity | Value |
|---|---|
| Common | 1 |
| Uncommon | 2 |
| Rare | 4 |

Both sides' **card subtotals must be equal and greater than zero** for Confirm to enable. Upgrades and enchantments are preserved on the traded card but don't change its point value. Potions and relics aren't point-valued — they're governed only by slot caps. So "2 Commons + 1 potion" for "1 Uncommon + 1 relic" is valid (cards: 2 = 2).

## Configuration

Configured in-game via BaseLib's settings UI:

- **Max card / potion / relic slots** per side
- **Unlimited trades** (default off — one trade per player per rest site)
- **Block on-obtain-hook relics** (default on)
- **Block quest cards** (default on)

## Building

```sh
dotnet build       # compiles and copies the DLL + manifest to the game's mods folder
dotnet publish     # additionally exports the .pck asset pack via MegaDot
```

The `.csproj` auto-detects the Steam install per-OS, publicizes `sts2.dll`, and references BaseLib. NuGet packages restore into a local `packages/` cache (gitignored).

## Project layout

```
MainFile.cs              Mod entry point ([ModInitializer])
TradeConfig.cs           BaseLib config (slot caps, toggles)
TradeValidator.cs        Card point-balance rules
TradeSynchronizer.cs     Co-op state machine + trade execution
TradeRestSiteOption.cs   The "Trade with Player" rest-site option
TradeState.cs            Trade session state
Messages/                Index-based INetMessage structs (offer/target/confirm/cancel/config)
Patches/                 Harmony patches (option injection, action consumption, save)
UI/                      Custom trade screen, slots, picker, tooltip, notification
CampfireTrade/           Localization + mod image (packed into the .pck)
images/                  Rest-site option icon
docs/                    Design doc, API audit, prior-art comparison
```

## Documentation

- [`docs/campfire-trade-mod-design.md`](docs/campfire-trade-mod-design.md) — full design
- [`docs/REVIEW.md`](docs/REVIEW.md) — API audit against v0.107.0
- [`docs/PRIOR-ART-COMPARISON.md`](docs/PRIOR-ART-COMPARISON.md) — comparison with the two shipping trade mods this build borrows from
- [`CLAUDE.md`](CLAUDE.md) — build guide / implementation notes

## Credits

This mod is a **fork of [chaendizzle/STS2Trade](https://github.com/chaendizzle/STS2Trade)** ("Campfire Trading"). The networking, synchronizer, rest-site option, and trade UI are taken largely verbatim from that mod. The changes on top are: a new rarity **point-balance system for cards** (`TradeValidator.cs`), the removal of the gold-gift and shop features, and assorted edits to the trade state and screen. The newer-build port [sirposh777/campfire-trading-update](https://github.com/sirposh777/campfire-trading-update) was also referenced.

Config and infrastructure via [Alchyr's BaseLib](https://github.com/Alchyr/BaseLib-StS2).
