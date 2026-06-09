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

    /// <summary>
    /// Verbose log helper — only emits when TradeConfig.VerboseLogging is on. Use for
    /// per-phase / per-item trade and sync chatter so normal runs stay quiet. Errors
    /// should always use Logger.Error directly and are never gated.
    /// </summary>
    public static void LogVerbose(string message)
    {
        if (TradeConfig.VerboseLogging)
            Logger.Info(message);
    }

    public static void Initialize()
    {
        Logger.Info("CampfireTrade: Campfire Trading mod initializing...");

        ModConfigRegistry.Register(ModId, new TradeConfig());

        Harmony harmony = new(ModId);
        harmony.PatchAll();

        Logger.Info("CampfireTrade: Harmony patches applied.");
    }
}
