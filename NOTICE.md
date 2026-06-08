# Notice & attribution

**Campfire Trade** is a derivative of **[chaendizzle/STS2Trade](https://github.com/chaendizzle/STS2Trade)** ("Campfire Trading", on [Nexus Mods](https://www.nexusmods.com/slaythespire2/mods/107)). The networking, trade synchronizer, rest-site option, trade UI, and the Give Gold shop feature originate from that mod.

Changes here: a rarity **point-balance system for cards** (`TradeValidator.cs`, with an on/off toggle); making a trade **consume the campfire action** (deferring to the game's native Miniature Tent handling); a co-op **choice-ID desync** fix; an asset-import build step so the trade **icon resolves**; updates for game **v0.107.0** (`INetMessage.ShouldBuffer`, `RestSiteOption.IsEnabled`); and config toggles for **starter cards** and **gold gifting**.

## Permissions

The original author's Nexus Mods page grants, among its permissions:

- **Modification** — "You are allowed to modify my files and release bug fixes or improve on the features without permission from or credit to me."
- **Upload to other sites** — "You can upload this file to other sites but you must credit me as the creator of the file."
- **Asset use is not permitted in mods/files that are sold for money.**

This project is free and open-source and credits the original author accordingly; it is not sold.

## Third-party credits

- Trade option icon ("card exchange") by **Delapouite** via [game-icons.net](https://game-icons.net/1x1/delapouite/card-exchange.html), licensed **CC BY 3.0**.
- Config/infrastructure via [Alchyr/BaseLib-StS2](https://github.com/Alchyr/BaseLib-StS2) (MIT).
- Newer-build port [sirposh777/campfire-trading-update](https://github.com/sirposh777/campfire-trading-update) was also referenced.
