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
                __result.RemoveAll(u => !State.IsPickAllowed(u.Type));
            bool stuck = assetGroupIndex != 0 && __result.Count == 0;
            // Inject a Ferry-typed placeholder so the upgrade panel still has one
            // button to render — the picker is never visually empty. The button is
            // relabeled "Skip" by NewAssetScreen_BuildAssets_Patch and its click is
            // intercepted by NewAssetScreen_OnAsset_Patch (calls SkipUpgrade instead
            // of granting Ferry, which doesn't exist in the live asset pool).
            // Ferry's assetIconGeos slot is null in this build, so the resulting
            // NewAssetButton renders as a plain dark circle with no icon overlay.
            if (stuck)
            {
                __result.Add(new UpgradeDefinition(AssetType.Ferry, 1));
                stuck = false;
            }
            IsPickerStuck = stuck;
        }
    }

    /// <summary>
    /// After the upgrade panel is built, walk its buttons; if any is the Ferry
    /// placeholder (from the GetAssets postfix above), swap its descriptor labels
    /// so the player sees "Skip" instead of the localized Ferry strings.
    /// </summary>
    [HarmonyPatch(typeof(NewAssetScreen), "BuildAssetsPanel")]
    internal static class NewAssetScreen_BuildAssets_Patch
    {
        private static readonly System.Reflection.FieldInfo F_NewAssetPanel =
            AccessTools.Field(typeof(NewAssetScreen), "newAssetPanel");
        private static readonly System.Reflection.FieldInfo F_PanelButtons =
            AccessTools.Field(typeof(NewAssetPanel), "buttons");
        private static readonly System.Reflection.FieldInfo F_ButtonDescriptor =
            AccessTools.Field(typeof(NewAssetButton), "descriptor");
        private static readonly System.Reflection.FieldInfo F_DescTitle =
            AccessTools.Field(typeof(AssetDescriptor), "titleLabel");
        private static readonly System.Reflection.FieldInfo F_DescDescription =
            AccessTools.Field(typeof(AssetDescriptor), "descriptionLabel");

        static void Postfix(NewAssetScreen __instance)
        {
            if (!(F_NewAssetPanel.GetValue(__instance) is NewAssetPanel panel)) return;
            if (!(F_PanelButtons.GetValue(panel) is System.Collections.Generic.List<NewAssetButton> buttons)) return;
            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                if (b == null || b.Type != AssetType.Ferry) continue;
                RelabelAsSkip(b);
            }
        }

        private static void RelabelAsSkip(NewAssetButton button)
        {
            if (!(F_ButtonDescriptor.GetValue(button) is AssetDescriptor desc)) return;
            var locale = LocaleDatabase.Instance.CurrentLocale;
            if (F_DescTitle.GetValue(desc) is UI.Label title)
                title.SetText(new LocalizedString(locale, "Skip"));
            if (F_DescDescription.GetValue(desc) is UI.Label description)
                description.SetText(new LocalizedString(locale, ""));
        }
    }

    /// <summary>
    /// Click handler interception. The Ferry placeholder isn't a real asset —
    /// when the player clicks it, route to <see cref="State.SkipUpgrade"/> and
    /// suppress the vanilla pick flow (which would try to grant a Ferry and
    /// fail/no-op).
    /// </summary>
    [HarmonyPatch(typeof(NewAssetScreen), "OnAsset")]
    internal static class NewAssetScreen_OnAsset_Patch
    {
        static bool Prefix(NewAssetButton button)
        {
            if (button == null || button.Type != AssetType.Ferry) return true;
            State.SkipUpgrade();
            return false;
        }
    }

    /// <summary>
    /// Reset <see cref="IsPickerStuck"/> on every entry into the picker screen.
    /// Without this, a stuck flag set during the previous run's upgrade pick can leak
    /// into a fresh screen (e.g. the locomotive grant on week start) before either
    /// the locomotive prefix below or the <c>GetAssets</c> postfix has had a chance
    /// to recompute it.
    /// </summary>
    [HarmonyPatch(typeof(NewAssetScreen), nameof(NewAssetScreen.HandleTransitionIn))]
    internal static class NewAssetScreen_TransitionIn_Patch
    {
        static void Prefix() => IsPickerStuck = false;
    }

    /// <summary>
    /// The locomotive panel never needs Skip — locomotive grants must be claimed.
    /// The fixed-choice path of <c>BuildLocomotivePanel</c> bypasses <c>GetAssets</c>,
    /// so we force the flag off whenever a locomotive panel is built, regardless of
    /// which path the screen took to get there.
    /// </summary>
    [HarmonyPatch(typeof(NewAssetScreen), "BuildLocomotivePanel")]
    internal static class NewAssetScreen_BuildLocomotive_Patch
    {
        static void Prefix() => IsPickerStuck = false;
    }
}
