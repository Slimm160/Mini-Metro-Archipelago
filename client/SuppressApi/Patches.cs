using HarmonyLib;

namespace client;

public static partial class SuppressApi
{
    /// <summary>Skip Steam leaderboard submission when <c>State.Leaderboard</c> is true.</summary>
    [HarmonyPatch(typeof(Game), "SubmitScore")]
    internal static class Game_SubmitScore_Patch
    {
        static bool Prefix()
        {
            if (!State.Leaderboard) return true;
            Plugin.BepinLogger.LogDebug("SuppressApi: SubmitScore skipped.");
            return false;
        }
    }

    /// <summary>Skip vanilla achievement persistence when <c>State.Achievements</c> is true.</summary>
    [HarmonyPatch(typeof(Profile), nameof(Profile.CompleteAchievement))]
    internal static class Profile_CompleteAchievement_Patch
    {
        static bool Prefix(string achievementId)
        {
            if (!State.Achievements) return true;
            Plugin.BepinLogger.LogDebug($"SuppressApi: CompleteAchievement({achievementId}) skipped.");
            return false;
        }
    }

    /// <summary>Skip profile cloud writes when <c>State.CloudSaves</c> is true.</summary>
    [HarmonyPatch(typeof(Profile), "SaveToCloud")]
    internal static class Profile_SaveToCloud_Patch
    {
        static bool Prefix(ref bool __result)
        {
            if (!State.CloudSaves) return true;
            __result = true;
            Plugin.BepinLogger.LogDebug("SuppressApi: SaveToCloud skipped.");
            return false;
        }
    }
}
