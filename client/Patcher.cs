using System.Linq;
using HarmonyLib;

namespace client;

public static class Patcher
{
    private static Harmony _harmony;

    public static void Apply()
    {
        if (_harmony != null) return;

        _harmony = new Harmony(Plugin.PluginGUID);
        _harmony.PatchAll(typeof(Patcher).Assembly);
        Plugin.BepinLogger.LogInfo($"Applied {_harmony.GetPatchedMethods().Count()} Harmony patches.");
    }

    public static void Unapply()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
    }
}
