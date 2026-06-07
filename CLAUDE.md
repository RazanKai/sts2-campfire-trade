# Campfire Trade — STS2 Mod (build guide)

You are building **Campfire Trade**, a C# mod for Slay the Spire 2 (Early Access, **v0.107.0** — commit `23d60b98`, 2026-06-04).

Read `campfire-trade-mod-design.md` (the design), `REVIEW.md` (the API audit), and `PRIOR-ART-COMPARISON.md` (the two shipping mods this build borrows from) before writing code.

> **Scope:** multiplayer-only **player-to-player trading** at rest sites. **Cards** trade under a rarity point-balance rule (Common 1 / Uncommon 2 / Rare 4, both sides' card subtotals equal). **Potions and relics** trade freely under slot caps (no points). There is **no singleplayer feature** — the old "Exchange with the pool" is removed.
>
> **De-risked:** a mod-defined `INetMessage` subtype **does round-trip across co-op clients** — proven by `chaendizzle/STS2Trade`. **Skip the old networking spike.** Most of this build adapts that shipping mod's proven patterns; the only net-new logic is `TradeValidator` (the card point system) + the Confirm gate.

---

## What this mod does

Adds one rest-site option, **Trade with Player** (co-op only):

- A player targets another player; when both target each other, a shared trade screen opens on both clients.
- Each player adds cards / potions / relics from their own deck/belt/relics. Non-tradeable items are dimmed.
- **Confirm is gated:** card subtotals must be equal and > 0 (or no cards on either side), within slot caps, and resulting potion counts must fit. Curses/starters/quest cards and (by config) on-obtain relics are never tradeable.
- On mutual confirm the swap applies **deterministically on every client**. Trading does **not** consume the campfire action. One completed trade per player per rest site (unless `UnlimitedTrades`).

---

## Tech stack (proven — matches the shipping mod)

- **Language:** C# / **.NET 9** (`net9.0`).
- **Project type:** a **Godot project** (`Godot.NET.Sdk/4.5.1`, with `project.godot` + `export_presets.cfg`); the `.pck` is exported via **MegaDot** (`--headless --export-pack`). Game logic is in `sts2.dll`, not GDScript.
- **Mod loader:** STS2 `ModManager`; entry via `[ModInitializer(nameof(Initialize))]` on a `partial class : Node`. **Manifest is `<ModId>.json`** (e.g. `CampfireTrade.json`) — *not* `mod_manifest.json`.
- **Patching:** Harmony (game-bundled `0Harmony.dll`, not the NuGet). The shipping mod uses **Harmony patches** for option injection and the consume-action behavior — see Steps below. Use the `Hook` surface only if you confirm a given hook is actually invokable on your build.
- **Publicizer:** publicize `sts2` via `<Publicize>true</Publicize>` on the reference **plus** `BepInEx.AssemblyPublicizer.MSBuild` (this is what ships; not `Krafs.Publicizer`).
- **Base library:** **BaseLib** (`Alchyr.Sts2.BaseLib`) is a **hard dependency** — it provides the config UI/persistence (`SimpleModConfig`), logging, and the analyzers. Reference `mods/BaseLib/BaseLib.dll` and declare it in the manifest `dependencies`.
- **Co-op networking:** `INetMessage : IPacketSerializable` (structs), sent via `RunManager.Instance.NetService.SendMessage`, received via `NetService.RegisterMessageHandler<T>`. Identity is `Player.NetId` (ulong). **Offers are sent as indices, not serialized cards.**
- **Reverse engineering:** the `sts2-modding` MCP (`get_entity_source` / `search_game_code` / `get_hook_signature` / `browse_namespace`) to confirm symbols; the `bridge_*` tools to playtest.

---

## .csproj (copy from `chaendizzle/STS2Trade`)

Use the shipping mod's `.csproj` as the template — it auto-detects the Steam path per-OS, publicizes `sts2`, references BaseLib, copies the DLL+manifest to the mods folder post-build, and exports the `.pck` via MegaDot on publish. Essentials:

```xml
<Project Sdk="Godot.NET.Sdk/4.5.1">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>true</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <!-- $(Sts2Path)/$(Sts2DataDir) resolved per-OS as in the shipping csproj -->
  <ItemGroup>
    <Reference Include="0Harmony"><HintPath>$(Sts2DataDir)/0Harmony.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="sts2"><HintPath>$(Sts2DataDir)/sts2.dll</HintPath><Private>false</Private><Publicize>true</Publicize></Reference>
    <Reference Include="BaseLib"><HintPath>$(Sts2Path)/mods/BaseLib/BaseLib.dll</HintPath><Private>false</Private></Reference>
    <PackageReference Include="Alchyr.Sts2.ModAnalyzers" Version="*" />
    <AdditionalFiles Include="CampfireTrade/localization/**/*.json"/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="BepInEx.AssemblyPublicizer.MSBuild" Version="0.4.3">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <Target Name="CopyToModsFolderOnBuild" AfterTargets="PostBuildEvent"> … copy $(TargetPath) + $(MSBuildProjectName).json … </Target>
  <Target Name="GodotPublish" AfterTargets="Publish" Condition="…"> … MegaDot --export-pack to mods/$(MSBuildProjectName)/$(MSBuildProjectName).pck … </Target>
</Project>
```

## Manifest — `CampfireTrade.json`

```json
{
  "id": "CampfireTrade",
  "name": "Campfire Trade",
  "author": "your-name",
  "description": "Player-to-player card, potion, and relic trading at campfires in multiplayer. Cards are rarity-point-balanced.",
  "version": "1.0.0",
  "has_pck": true,
  "has_dll": true,
  "dependencies": ["BaseLib"],
  "affects_gameplay": true
}
```

---

## Implementation order

### Step 1 — Verify the API surface on your build

These symbols are taken from the working `chaendizzle/STS2Trade` (built against a nearby version). Re-confirm against your decompiled `sts2.dll` (v0.107.0) with the `sts2-modding` MCP before relying on them:

- `RestSiteOption` (abstract) in `MegaCrit.Sts2.Core.Entities.RestSite`; `RestSiteOption.Generate(Player) → List<RestSiteOption>` (Harmony-postfix target); members `OptionId`, `OnSelect() → Task<bool>`, ctor `(Player owner)`, `IsEnabled`, localized `Title`/`Description` via `LocString("rest_site_ui", "OPTION_TRADE.name"/".description")`, icon `ui/rest_site/option_trade.png`.
- `Hook.ShouldDisableRemainingRestSiteOptions(IRunState, Player) → bool` (Harmony-prefix target).
- `RestSiteSynchronizer` (`RunManager.Instance.RestSiteSynchronizer`): `BeginRestSite`, `GetOptionsForPlayer(netId)`, `GetChosenOptionIndex(netId)`.
- Networking: `INetMessage : IPacketSerializable` (members `ShouldBroadcast`, `Mode`/`NetTransferMode`, `LogLevel` — **no `ShouldBuffer`**); `PacketWriter.WriteInt(value, bits)` / `PacketReader.ReadInt(bits)`; `NetService.RegisterMessageHandler<T>` / `UnregisterMessageHandler<T>` / `SendMessage`; `INetHostGameService` (host check); `LocalContext.NetId` / `LocalContext.IsMe(player)`.
- Choice sync: `PlayerChoiceSynchronizer.ReserveChoiceId(player)` / `SyncLocalChoice(player, id, PlayerChoiceResult.FromPlayerId(netId?))` / `WaitForRemoteChoice(player, id)`.
- Deck/potion/relic Cmds: `CardPileCmd.RemoveFromDeck(card, showPreview:false)` / `CardPileCmd.Add(card, PileType.Deck)`; `card.ClonePreservingMutability()`; `runState.AddCard(clone, target)`; `target.Deck.InvokeCardAddFinished()`; `PotionCmd.Discard` / `PotionCmd.TryToProcure(fresh, target)`; `ModelDb.GetById<PotionModel>(id).ToMutable()`; `RelicCmd.Remove` / `RelicCmd.Obtain(fresh, target)`; `relic.ToSerializable()` / `RelicModel.FromSerializable(...)`; `PlayerCmd.GainMaxPotionCount` / `LoseMaxPotionCount` (for `PotionBelt`).
- Cards/rarity: `CardModel.Rarity` (`CardRarity` enum: `None,Basic,Common,Uncommon,Rare,Ancient,Event,Token,Status,Curse,Quest`), `.Type` (`CardType` incl. `Curse`/`Status`/`Quest`), `.IsRemovable`; `player.Deck.Cards`.
- UI: `NTargetManager.StartTargeting(TargetType.AnyPlayer, …)` / `SelectionFinished()`; `NRestSiteRoom.Instance` (`GetButtonForOption`, `GetCharacterForPlayer`, `...GetRestSiteOptionAnchor()`); `NThoughtBubbleVfx.Create(...)`.

### Step 2 — Entry point + config (`MainFile.cs`, `TradeConfig.cs`)

```csharp
[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    private const string ModId = "CampfireTrade";
    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        ModConfigRegistry.Register(ModId, new TradeConfig());   // BaseLib config UI
        new Harmony(ModId).PatchAll();
        Logger.Info("CampfireTrade initialized.");
    }
}
```

`TradeConfig : BaseLib.Config.SimpleModConfig` with static props: `MaxCardSlots` (1–5, default 3), `MaxPotionSlots` (1–3, default 3), `MaxRelicSlots` (1–3, default 1), `UnlimitedTrades` (default false), `BlockObtainHookRelics` (default true), `BlockQuestCards` (default true). Include a `HasObtainHook(RelicModel)` reflection helper (checks whether `AfterObtained` is overridden). Verify the mod loads (mod name in the game log) before going further.

### Step 3 — Inject the Trade option (`Patches/AddTradeOptionPatch.cs`)

Harmony-postfix `RestSiteOption.Generate`. Add the option for **all** players (so option indices stay identical across machines); `IsEnabled` grays it out when the player can't trade.

```csharp
[HarmonyPatch(typeof(RestSiteOption), nameof(RestSiteOption.Generate))]
public static class AddTradeOptionPatch
{
    [HarmonyPostfix]
    public static void Postfix(Player player, List<RestSiteOption> __result)
    {
        if (player.RunState.Players.Count <= 1) return;   // co-op only
        __result.Add(new TradeRestSiteOption(player));
    }
}
```

> If you confirm a real, mod-invokable `Hook.ModifyRestSiteOptions(IRunState, Player, ICollection<RestSiteOption>)` exists on v0.107.0, you may use it instead — but the postfix above is the empirically proven path. Don't gate the feature on an unexercised hook.

### Step 4 — Don't consume the campfire action (`Patches/PreventDisableAfterTradePatch.cs`)

Harmony-prefix `Hook.ShouldDisableRemainingRestSiteOptions`; force `false` once for players flagged in `TradeSynchronizer.JustCompletedTrade` (one-shot, per-NetId).

```csharp
[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldDisableRemainingRestSiteOptions))]
public static class PreventDisableAfterTradePatch
{
    [HarmonyPrefix]
    public static bool Prefix(IRunState runState, Player player, ref bool __result)
    {
        var sync = TradeSynchronizer.Instance;
        if (sync != null && sync.JustCompletedTrade.Remove(player.NetId)) { __result = false; return false; }
        return true;
    }
}
```

**Critical:** only add a player to `JustCompletedTrade` when the trade will actually consume the option (i.e. **not** in `UnlimitedTrades`). Otherwise the flag lingers and wrongly makes the player's next Rest free. (Real bug in the shipping mod.) Also add `SkipTradeConfirmationPatch` (prefix `NRestSiteCharacter.ShowSelectedRestSiteOption`, return false for `TradeRestSiteOption`) so trade doesn't stamp the permanent "selected" icon, and `AddNotificationManagerPatch` (postfix `NRestSiteRoom._Ready`) to attach the incoming-trade notifier.

### Step 5 — Synchronizer lifecycle (`Patches/InitTradeSyncPatch.cs`)

Harmony-prefix `RestSiteSynchronizer.BeginRestSite`: if a `TradeSynchronizer` exists and uses the current `NetService`, `ResetForNewRestSite()`; if it's stale (different service), `Dispose()` and recreate; create a new one with `RunManager.Instance.NetService`, the run state, and `LocalContext.NetId`. Skip when `Players.Count <= 1`.

### Step 6 — Card point validation (`TradeValidator.cs`) — the only net-new logic

Pure, unit-test first. **Cards only.**

```csharp
public static class TradeValidator
{
    private static readonly Dictionary<CardRarity,int> Points = new()
        { {CardRarity.Common,1}, {CardRarity.Uncommon,2}, {CardRarity.Rare,4} };

    public static bool IsCardTradeable(CardModel c) =>
        Points.ContainsKey(c.Rarity)
        && c.Type is not (CardType.Curse or CardType.Status or CardType.Quest)
        && c.IsRemovable
        && !(TradeConfig.BlockQuestCards && c.Type == CardType.Quest);

    public static int CardValue(IEnumerable<CardModel> cards) =>
        cards.Sum(c => Points.GetValueOrDefault(c.Rarity, 0));

    // Card subtotals must match. Empty-on-both-sides is allowed (potion/relic-only trade).
    public static bool CardsBalanced(IReadOnlyList<CardModel> a, IReadOnlyList<CardModel> b)
    {
        if (a.Count == 0 && b.Count == 0) return true;
        return CardValue(a) == CardValue(b) && CardValue(a) > 0;
    }
}
```

The Confirm gate = `CardsBalanced(...)` **AND** within `MaxCard/Potion/Relic` slot caps **AND** `TradeSession.GetValidationError(...)` returns null (potion-slot capacity, incl. `PotionBelt` math). Curses must never even be selectable in the picker.

### Step 7 — Messages (`Messages/*.cs`) — index-based structs

Five `INetMessage` structs (see design table). Example:

```csharp
public struct TradeOfferMessage : INetMessage, IPacketSerializable
{
    public int cardCount;   public int[] cardDeckIndices;
    public int potionCount; public int[] potionSlotIndices;
    public int relicCount;  public int[] relicIndices;

    public bool ShouldBroadcast => true;
    public NetTransferMode Mode => NetTransferMode.Reliable;
    public LogLevel LogLevel => LogLevel.Debug;

    public void Serialize(PacketWriter w) {
        w.WriteInt(cardCount, 8);   for (int i=0;i<cardCount;i++)   w.WriteInt(cardDeckIndices[i]);
        w.WriteInt(potionCount, 8); for (int i=0;i<potionCount;i++) w.WriteInt(potionSlotIndices[i], 8);
        w.WriteInt(relicCount, 8);  for (int i=0;i<relicCount;i++)  w.WriteInt(relicIndices[i]);
    }
    public void Deserialize(PacketReader r) {
        cardCount   = r.ReadInt(8); cardDeckIndices   = new int[cardCount];   for (int i=0;i<cardCount;i++)   cardDeckIndices[i]   = r.ReadInt();
        potionCount = r.ReadInt(8); potionSlotIndices = new int[potionCount]; for (int i=0;i<potionCount;i++) potionSlotIndices[i] = r.ReadInt(8);
        relicCount  = r.ReadInt(8); relicIndices      = new int[relicCount];  for (int i=0;i<relicCount;i++)  relicIndices[i]      = r.ReadInt();
    }
}
```

Also `TradeTargetMessage` (`hasTarget`, `targetPlayerId`), `TradeConfirmMessage` (`confirmed`), `TradeCancelMessage` (empty), `TradeConfigMessage` (all config fields). Never put cards on the wire — indices only.

### Step 8 — `TradeSynchronizer.cs` (adapt the shipping mod almost verbatim)

`IDisposable`, static `Instance`. Register/unregister the five handlers. State: `PendingRequests` (NetId→targetNetId), `PlayersWhoTraded`, `JustCompletedTrade`, `ActiveSession` (this machine participates), `_observedSession` (this machine observes a trade between two others). Events: `TradeStarted/OfferUpdated/ConfirmChanged/TradeCancelled/TradeCompleted(a,b)/TradeStateChanged`.

- **Match detection (`CheckForMatch`):** mutual target (`PendingRequests[A]==B && PendingRequests[B]==A`). For the local player → `BeginTradeSession`; for two other players → `BeginObservedSession` (canonical: lower NetId = "Local" side).
- **Offer/confirm routing:** changing an offer revokes both confirms (track `PartnerOfferWhenLocalConfirmed`); `BothConfirmed` → `ExecuteTrade`.
- **Config sync:** host `BroadcastConfig()` on creation; clients apply in `HandleTradeConfig`.
- **Execution:** `ExecuteTrade` (participant) and `ExecuteObservedTrade` (observer) both call the Phase 0/1/2 `ExecuteTradeCoreAsync`; defer relic `Obtain` until after `TradeCompleted` fires (so the screen closes first); clear `ActiveSession` **after** `TradeCompleted` but before any new trade.

### Step 9 — `TradeRestSiteOption.cs`

`OptionId => "TRADE"`; `IsEnabled` = co-op AND `sync.CanTrade(owner.NetId)`; `Description` returns a disabled-variant loc when not enabled. `OnSelect`:

1. `ReserveChoiceId(Owner)`.
2. `LocalContext.IsMe(Owner)` → `OnSelectLocal` else `OnSelectRemote`.
3. **Local:** target a partner via `NTargetManager.StartTargeting(TargetType.AnyPlayer, …)`; validate `CanTargetTrade` (not already traded; still has options); `SyncLocalChoice(target?.NetId)`; `SelectTarget`; if not yet matched, show a waiting bubble + `WaitingInputHandler` (Esc/right-click cancels) and await match; then `RunTradeUI()` (open `NTradeScreen`). Return `true` to consume the option (unless `UnlimitedTrades` → return `false`).
4. **Remote:** `WaitForRemoteChoice`; if a target was chosen, await `TradeCompleted`/`TradeCancelled` for *this* player (filter by NetId; 3-min timeout). Return accordingly (false in unlimited mode).

Always `SyncLocalChoice` even on cancel, so choice counters stay aligned across machines.

### Step 10 — Trade UI (`UI/*.cs`)

Custom Godot UI (reuse the shipping mod's `NTradeScreen` / `NTradeSlot` / `NTradeItemPicker` / `NTradeTooltip` / notification manager as the starting point). Add to ours:

- A **live per-side card-value counter** and a **Confirm button gated** on `TradeValidator.CardsBalanced` + slot caps + `GetValidationError`.
- **Dim/disable** non-tradeable items in the picker: curses (rarity or type), starters/Ancient/Event/Token/Status/Quest cards, blocked relics, over-cap selections.

Parent the screen to `NRun.Instance.GlobalUi` (fallback `NRestSiteRoom.Instance`), `ZIndex = 100`, free it in a `finally`.

### Step 11 — Assets

Ship in the `.pck`: localization table `rest_site_ui` with `OPTION_TRADE.name` / `.description` / `.descriptionDisabled`, and icon `ui/rest_site/option_trade.png`.

---

## Invariants — never break these

- **Curses never tradeable** (rarity or `CardType`); never selectable in the picker.
- **Only card subtotals are point-balanced**; potions/relics are slot-capped, not valued.
- **Card subtotals equal and > 0 whenever cards are present**; potion/relic-only trades allowed within caps.
- **Trade does not consume the campfire action**; Rest/Forge remain.
- **Cards keep upgrade/enchantment state** across a trade (`ClonePreservingMutability`).
- **Cancel/decline never consumes a trade slot.**
- **Host config is authoritative.**
- **Swap is deterministic on every client** (participant + observer); snapshot → remove-all → add-all; defensive index bounds; canonical ordering.
- **Only flag `JustCompletedTrade` when the trade actually consumes the option** (not in unlimited mode).
- **Call `Deck.InvokeCardAddFinished()` after adds** or the deck counter won't update.

---

## Testing checklist (in-game, co-op)

- [ ] Trade option appears only in a 2+ player run; absent in singleplayer
- [ ] Option grays out (`IsEnabled`) for a player who already traded (non-unlimited)
- [ ] Mutual targeting opens the shared screen on both clients
- [ ] Card value counter updates live per side
- [ ] Confirm disabled until card subtotals are equal (>0) and within slot caps
- [ ] Potion/relic-only trade (no cards) is allowed within caps
- [ ] Curses / starters / quest cards / blocked relics are dimmed & unselectable
- [ ] Over-cap selection is prevented (MaxCard/Potion/Relic)
- [ ] Potion-slot capacity validation blocks confirm when it would overflow (incl. PotionBelt)
- [ ] After a successful trade both players have the correct cards/potions/relics, with `+` preserved
- [ ] Deck counter updates for both players
- [ ] Trade does NOT consume the action — Rest/Forge still available; next Rest still consumes normally
- [ ] Trade option grays out for both after a completed trade (non-unlimited)
- [ ] Cancel at any stage returns both players to the rest site with slots intact
- [ ] Waiting-for-partner: Esc/right-click cancels cleanly; partner timeout cancels cleanly
- [ ] 3+ players: a trade between two players applies correctly on the uninvolved (observer) client; no desync/checksum error
- [ ] `UnlimitedTrades` on: option stays after a trade; Rest still consumes the action correctly
- [ ] Host config changes propagate to clients
- [ ] No errors in game log on mod load

---

## Dev tooling (CLI)

Tools that pay off on this C# / Godot / .NET codebase. Prefer these over `cat`+`sed`/`grep` where they apply.

Dev env: **CachyOS** (Arch-based; install with `sudo pacman -S <pkg>`).

- **`ast-grep`** — *most useful here; adopted.* Structural (AST-aware) search & replace for C#. Use it to study the cloned shipping mods under `refs/` and the decompiled game source, and to refactor our own `src/` safely. Examples: find every call site — `ast-grep -p 'CardPileCmd.Add($$$)' refs/ src/`; find option subclasses — `ast-grep -p 'class $N : RestSiteOption { $$$ }'`; rename an API across the tree with `ast-grep -p '…' -r '…'`. Beats regex for anything that spans tokens or nests. Install: `sudo pacman -S ast-grep`. **Invoke as `ast-grep`, not `sg`** — on Linux `sg` is shadow-utils' set-group command, not this tool.
- **`delta`** — *adopted.* Syntax-highlighted, side-by-side `git diff`/`git log` output for reviewing changes to these docs or the mod source. Install: `sudo pacman -S git-delta` (package is `git-delta`; binary is `delta`). Wire it up as the git pager once: set `[core] pager = delta` and `[interactive] diffFilter = delta --color-only` in `~/.gitconfig`, then `git diff` uses it automatically.
- **`jq` / `yq`** — *installed.* Validate and edit the `<ModId>.json` manifest and the `localization/eng/rest_site_ui.json` tables programmatically instead of hand-editing: `jq . CampfireTrade/localization/eng/rest_site_ui.json` (validate), `yq` handles the same plus XML/properties if you ever need to poke `.csproj`/`project.godot`.
- **`watchexec`** — terminal build-on-save loop: `watchexec -e cs -- dotnet build`. *Optional:* the `sts2-modding` MCP's `watch_project` already does build + deploy + in-game hot-reload, which is the better loop for iterating against the running game; reach for `watchexec` only for a pure terminal rebuild without the game attached. Install if wanted: `sudo pacman -S watchexec`.
- **`shellcheck`** — only relevant if you add build/deploy shell scripts (we mostly drive `dotnet`/MSBuild + MegaDot); lint any such script before committing. Install if needed: `sudo pacman -S shellcheck`.

## Key references

- Design: `campfire-trade-mod-design.md` · API audit: `REVIEW.md` · Prior-art comparison: `PRIOR-ART-COMPARISON.md`
- **Primary code reference (proven patterns):** [chaendizzle/STS2Trade](https://github.com/chaendizzle/STS2Trade) (cloned under `refs/`) · newer-build port [sirposh777/campfire-trading-update](https://github.com/sirposh777/campfire-trading-update)
- BaseLib: [Alchyr's BaseLib-StS2](https://github.com/Alchyr/BaseLib-StS2) · mod template: [ModTemplate-StS2](https://github.com/Alchyr/ModTemplate-StS2)
- **STS2 modding MCP** (decompiles + indexes the real API — verify every symbol here): https://github.com/elliotttate/sts2-modding-mcp
- Modding tutorial / API inventory: https://github.com/fresh-milkshake/Modding-Tutorial
