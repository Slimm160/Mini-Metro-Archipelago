using HarmonyLib;

namespace client;

public static partial class MapApi
{
    /// <summary>
    /// Routes CityDefinition.IsUnlocked through <see cref="State"/> when active.
    /// When inactive, vanilla logic (profile achievement checks) runs normally.
    /// </summary>
    [HarmonyPatch(typeof(CityDefinition), nameof(CityDefinition.IsUnlocked))]
    internal static class IsUnlockedPatch
    {
        static bool Prefix(CityDefinition __instance, GameMode mode, ref bool __result)
        {
            if (!State.IsActive) return true;
            __result = State.IsUnlocked(__instance.Id, mode);
            return false;
        }
    }
}
