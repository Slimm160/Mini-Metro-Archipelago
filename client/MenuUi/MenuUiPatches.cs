using HarmonyLib;

namespace client.MenuUi;

/// <summary>
/// Harmony hooks that graft the native Archipelago connection UI onto the game's main menu.
/// Registered in <see cref="Patcher.Apply"/>.
/// </summary>
public static class MenuUiPatches
{
    /// <summary>
    /// After a <c>HomeScreen</c> is built, attach a fresh <see cref="ArchipelagoMenuPanel"/>
    /// (menu entry + connection panel). Runs again on every menu rebuild (e.g. resolution
    /// change), so the UI self-heals; the previous panel dies with its released screen.
    /// </summary>
    [HarmonyPatch(typeof(HomeScreen), MethodType.Constructor, new[] { typeof(Menu) })]
    internal static class HomeScreen_Ctor_Patch
    {
        static void Postfix(HomeScreen __instance)
        {
            try
            {
                MenuUi.Current = new ArchipelagoMenuPanel(__instance);
            }
            catch (System.Exception e)
            {
                Plugin.BepinLogger.LogError($"Failed to build Archipelago menu panel: {e}");
                MenuUi.Current = null;
            }
        }
    }

    /// <summary>Per-frame pump for text input and connection-state polling (main thread).</summary>
    [HarmonyPatch(typeof(Menu), nameof(Menu.Update))]
    internal static class Menu_Update_Patch
    {
        static void Postfix()
        {
            try { MenuUi.Tick(); }
            catch (System.Exception e) { Plugin.BepinLogger.LogError($"Archipelago menu tick error: {e}"); }
        }
    }

    /// <summary>
    /// Before any menu screen change, undo our MenuCity pan offset so the game's own transition
    /// starts from a clean baseline — otherwise the displacement bakes in and the menu returns
    /// off-centre.
    /// </summary>
    [HarmonyPatch(typeof(Menu), nameof(Menu.SetOption))]
    internal static class Menu_SetOption_Patch
    {
        static void Prefix()
        {
            MenuUi.Current?.ResetCityPan();
        }
    }

    /// <summary>
    /// Nudge the whole Home button column down a touch after the game lays it out. Runs on every
    /// relayout (entry, screen return, locale change), so the offset persists and never accumulates
    /// (positions are recomputed from scratch each time before we shift them).
    /// </summary>
    [HarmonyPatch(typeof(HomeScreen), "RepositionButtons")]
    internal static class HomeScreen_RepositionButtons_Patch
    {
        private static readonly System.Reflection.FieldInfo F_Buttons =
            AccessTools.Field(typeof(HomeScreen), "buttons");

        static void Postfix(HomeScreen __instance)
        {
            float dy = ArchipelagoMenuPanel.MenuButtonsYOffset;
            if (dy == 0f) return;
            if (F_Buttons.GetValue(__instance) is not System.Collections.Generic.List<UI.LabelButton> buttons) return;
            foreach (var b in buttons)
            {
                if (b == null) continue;
                UnityEngine.Vector2 p = b.GetPosition();
                b.SetPosition(new UnityEngine.Vector2(p.x, p.y - dy));
            }
        }
    }

    /// <summary>Forward day/night theme changes to our panels so they tween with the rest of the menu.</summary>
    [HarmonyPatch(typeof(HomeScreen), nameof(HomeScreen.HandleThemeChanged))]
    internal static class HomeScreen_HandleThemeChanged_Patch
    {
        static void Postfix(Theme newTheme)
        {
            MenuUi.Current?.HandleThemeChanged(newTheme);
        }
    }
}
