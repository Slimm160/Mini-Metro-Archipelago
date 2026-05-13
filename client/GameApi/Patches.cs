using HarmonyLib;

namespace client;

public static partial class GameApi
{
    [HarmonyPatch(typeof(GameController), MethodType.Constructor, new[] { typeof(Game) })]
    internal static class GameController_Ctor_Patch
    {
        static void Postfix(Game game)
        {
            Plugin.BepinLogger.LogInfo($"Game started: {game.City.Definition.Id} / {game.Mode}");
            SetGame(game);
        }
    }

    [HarmonyPatch(typeof(GameController), nameof(GameController.Release))]
    internal static class GameController_Release_Patch
    {
        static void Postfix()
        {
            Plugin.BepinLogger.LogInfo("Game released.");
            ClearGame();
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.IncreaseScore))]
    internal static class Game_IncreaseScore_Patch
    {
        static void Postfix(Game __instance) => Events.RaiseScoreIncreased(__instance, __instance.Score);
    }

    [HarmonyPatch(typeof(Game), nameof(Game.StartWeek))]
    internal static class Game_StartWeek_Patch
    {
        static void Postfix(Game __instance) => Events.RaiseWeekStarted(__instance, __instance.Week);
    }

    [HarmonyPatch(typeof(Game), nameof(Game.HandleStationOverflowStart))]
    internal static class Game_OverflowStart_Patch
    {
        static void Postfix(Game __instance, Station station) => Events.RaiseOverflowStarted(__instance, station);
    }

    /// <summary>Polls <c>Game.Week</c> every frame and raises <see cref="Events.WeekChanged"/> on increment.</summary>
    [HarmonyPatch(typeof(Game), nameof(Game.Update))]
    internal static class Game_Update_Patch
    {
        static void Postfix(Game __instance)
        {
            int w = __instance.Week;
            if (w == LastSeenWeek) return;
            int prev = LastSeenWeek;
            LastSeenWeek = w;
            if (prev != -1) Events.RaiseWeekChanged(__instance, w);
        }
    }

    /// <summary>
    /// Filters the random upgrade pool through <see cref="AllowedPicks"/>. <c>GetAssets</c>
    /// is the private method <c>NewAssetScreen</c> calls to build the choices that show in
    /// the picker; trimming its output is the cleanest way to enforce a persistent limit.
    /// (<c>Game.ForcedAssets</c> wouldn't work here — it's only consumed by the screen
    /// after the panels are already built, so it affects the next pick, not this one.)
    /// </summary>
    [HarmonyPatch(typeof(NewAssetScreen), "GetAssets")]
    internal static class NewAssetScreen_GetAssets_Patch
    {
        static void Postfix(ref System.Collections.Generic.List<UpgradeDefinition> __result)
        {
            if (AllowedPicks.Count == 0 || __result == null) return;
            __result.RemoveAll(u => !AllowedPicks.Contains(u.Type));
        }
    }
}
