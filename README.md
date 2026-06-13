# Campfire Trade

A **Slay the Spire 2** mod that adds **player-to-player trading** at rest sites in co-op. Move a card to the teammate whose deck actually wants it, swap spare potions, or hand off a relic. It also adds an optional **Give Gold** button at merchant shops so teammates can share gold.

>  Targets STS2 Early Access **v0.107.0**. Built in C# / .NET 9 on the Godot SDK, using Harmony patches and the game's `INetMessage` co-op networking. Requires **[BaseLib](https://github.com/Alchyr/BaseLib-StS2)**.

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

This whole system can be turned off with **Enable point balance** (default on). With it off, cards trade freely with no value-matching requirement — slot caps and the non-tradeable rules still apply.

## Give Gold at shops

At a merchant (real or event), a **Give Gold** button appears under each teammate's character. Click to send 50 gold; hold for an accelerating repeat to transfer larger amounts. Transfers are deterministic and synced across all clients — including the recipient's gain-gold relic effects (e.g. Dragon Fruit's +Max HP), which fire identically on every machine. The **Gifted gold triggers gain effects** toggle (default on) controls whether those effects fire at all; turn it off to prevent two Dragon Fruit owners from farming Max HP by bouncing gold. The whole feature is on by default and can be turned off in config.

## Configuration

Configured in-game via BaseLib's settings UI. All settings are **host-authoritative** — the host's values are synced to clients:

- **Max card / potion / relic slots** per side
- **Enable point balance** (default on — require matching card values; off = free card trades)
- **Unlimited trades** (default off — one trade per player per rest site)
- **Allow starter cards** (default off — let Basic Strikes/Defends trade, valued 1 each)
- **Enable gold gifting** (default on — show Give Gold buttons at shops)
- **Gifted gold triggers gain effects** (default on — gifted gold fires gain-gold relics like Dragon Fruit; turn off to stop Max HP farming)
- **Block on-obtain-hook relics** (default on)
- **Block quest cards** (default on)

## Building

```sh
dotnet build       # compiles and copies the DLL + manifest to the game's mods folder
dotnet publish     # additionally exports the .pck asset pack via MegaDot
```

The `.csproj` auto-detects the Steam install per-OS, publicizes `sts2.dll`, and references BaseLib. NuGet packages restore into a local `packages/` cache (gitignored).

## Credits

This mod is **based on [chaendizzle/STS2Trade](https://github.com/chaendizzle/STS2Trade)** ("Campfire Trading", on [Nexus Mods](https://www.nexusmods.com/slaythespire2/mods/107)). The networking, synchronizer, rest-site option, trade UI, and the Give Gold shop feature are taken largely from that mod. The changes on top are: a rarity **point-balance system for cards** (`TradeValidator.cs`, with an on/off toggle); making trade **consume the campfire action** (deferring to the game's native Miniature Tent handling); a fix for a co-op **choice-ID desync** on duplicate selections; an asset-import build step so the trade **icon resolves**; and config toggles for **starter cards** and **gold gifting**. The newer-build port [sirposh777/campfire-trading-update](https://github.com/sirposh777/campfire-trading-update) was also referenced.

The original author's Nexus permissions allow modifying and re-uploading the mod with credit, and prohibit use in mods sold for money — this project is free and credits the author accordingly. The trade icon ("card exchange") is by Delapouite via [game-icons.net](https://game-icons.net/1x1/delapouite/card-exchange.html) (CC BY 3.0). The "gifted gold triggers gain effects" toggle was inspired by a fix in [Jzcse/STS2Trade](https://github.com/Jzcse/STS2Trade) (independently reimplemented here). Config and infrastructure via [Alchyr's BaseLib](https://github.com/Alchyr/BaseLib-StS2) (MIT).
