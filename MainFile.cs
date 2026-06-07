using BaseLib.Config;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace CampfireTrade;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    private const string ModId = "CampfireTrade";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        Logger.Info("CampfireTrade: Campfire Trading mod initializing...");

        ModConfigRegistry.Register(ModId, new TradeConfig());

        Harmony harmony = new(ModId);
        harmony.PatchAll();

        Logger.Info("CampfireTrade: Harmony patches applied.");
    }
}
