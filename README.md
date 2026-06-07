# Campfire Trade

A **Slay the Spire 2** mod that adds **player-to-player trading** at rest sites in co-op. Move a card to the teammate whose deck actually wants it, swap spare potions, or hand off a relic. It also adds an optional **Give Gold** button at merchant shops so teammates can share gold.

> **Multiplayer only.** Targets STS2 Early Access **v0.107.0**. Built in C# / .NET 9 on the Godot SDK, using Harmony patches and the game's `INetMessage` co-op networking. Requires **[BaseLib](https://github.com/Alchyr/BaseLib-StS2)**.

## How it works

At any co-op rest site, a new **Trade with Player** option appears. Target a teammate; when you both target each other, a shared trade screen opens on both clients. Each side adds items from their own deck, potion belt, and relics, then confirms. On mutual confirm the swap applies deterministically and identically on every client.

Trading **consumes the campfire action**, just like Rest or Forge — it's one action per rest site. A player who owns **Miniature Tent** can still take every option (the game's native behavior is respected). Enable **Unlimited trades** in config to trade repeatedly without consuming the action. Cancelling a trade never consumes anything.

## What can be traded

| Item type | Tradeable? | Rule |
|---|---|---|
| Cards — Common / Uncommon / Rare | Yes | Rarity **point-balanced** (see below) |
| Cards — starters (Basic) | Optional | Off by default; enable **Allow starter cards** (valued 1, like Common) |
| Cards — Ancient, Event, Token, Status, Quest, Curse | No | — |
| Potions | Yes | Slot cap only |
| Relics | Yes | Slot cap only (quest & on-obtain-hook relics blockable by config) |

### Card point balance

| Rarity | Value |
|---|---|
| Common | 1 |
| Uncommon | 2 |
| Rare | 4 |

Both sides' **card subtotals must be equal and greater than zero** for Confirm to enable. Upgrades and enchantments are preserved on the traded card but don't change its point value. Potions and relics aren't point-valued — they're governed only by slot caps. So "2 Commons + 1 potion" for "1 Uncommon + 1 relic" is valid (cards: 2 = 2).

## Give Gold at shops

At a merchant (real or event), a **Give Gold** button appears under each teammate's character. Click to send 50 gold; hold for an accelerating repeat to transfer larger amounts. Transfers are deterministic and synced across all clients. The feature is on by default and can be turned off in config.

## Configuration

Configured in-game via BaseLib's settings UI. All settings are **host-authoritative** — the host's values are synced to clients:

- **Max card / potion / relic slots** per side
- **Unlimited trades** (default off — one trade per player per rest site)
- **Allow starter cards** (default off — let Basic Strikes/Defends trade, valued 1 each)
- **Enable gold gifting** (default on — show Give Gold buttons at shops)
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
GoldGiftSynchronizer.cs  Co-op gold-transfer logic for the shop feature
Messages/                Index-based INetMessage structs (offer/target/confirm/cancel/config/give-gold)
Patches/                 Harmony patches (option injection, sync lifecycle, shop gold buttons)
UI/                      Custom trade screen, slots, picker, tooltip, notification, Give Gold button
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

This mod is a **fork of [chaendizzle/STS2Trade](https://github.com/chaendizzle/STS2Trade)** ("Campfire Trading"). The networking, synchronizer, rest-site option, trade UI, and the Give Gold shop feature are taken largely from that mod. The changes on top are: a new rarity **point-balance system for cards** (`TradeValidator.cs`); making trade **consume the campfire action** (deferring to the game's native Miniature Tent handling); a fix for a co-op **choice-ID desync** on duplicate selections; an asset-import build step so the trade **icon resolves**; and config toggles for **starter cards** and **gold gifting**. The newer-build port [sirposh777/campfire-trading-update](https://github.com/sirposh777/campfire-trading-update) was also referenced.

Config and infrastructure via [Alchyr's BaseLib](https://github.com/Alchyr/BaseLib-StS2).
