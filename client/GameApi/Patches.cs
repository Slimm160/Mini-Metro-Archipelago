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
    /// <summary>Scales the per-tick peep spawn rate by <see cref="PeepSpawnMultiplier"/>.</summary>
    [HarmonyPatch(typeof(City), nameof(City.PeepSpawnScale), MethodType.Getter)]
    internal static class City_PeepSpawnScale_Patch
    {
        static void Postfix(ref float __result) => __result *= PeepSpawnMultiplier;
    }

    /// <summary>Scales every train's max-speed cap by <see cref="TrainSpeedMultiplier"/>.</summary>
    [HarmonyPatch(typeof(LocomotiveDefinition), nameof(LocomotiveDefinition.Speed), MethodType.Getter)]
    internal static class LocomotiveDefinition_Speed_Patch
    {
        static void Postfix(ref float __result) => __result *= TrainSpeedMultiplier;
    }

    [HarmonyPatch(typeof(NewAssetScreen), "GetAssets")]
    internal static class NewAssetScreen_GetAssets_Patch
    {
        static void Postfix(int assetGroupIndex, ref System.Collections.Generic.List<UpgradeDefinition> __result)
        {
            if (__result == null) { IsPickerStuck = false; return; }
            if (AllowedPicks.Count > 0)
                __result.RemoveAll(u => !AllowedPicks.Contains(u.Type));
            // Locomotive panel (group 0) never offers Skip — locomotive grants
            // are gated separately and must be claimed.
            IsPickerStuck = assetGroupIndex != 0 && __result.Count == 0;
        }
    }
}
