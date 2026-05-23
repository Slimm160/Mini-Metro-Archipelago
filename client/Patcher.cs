using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace client;

/// <summary>
/// Two-stage Harmony patching to dodge a font-init race:
///
///   Stage 1 (<see cref="ScheduleApply"/>): in <c>Plugin.Awake</c>, install ONLY a
///   postfix on <c>FontDatabase</c>'s instance constructor. Touching NewAssetScreen
///   (or any class whose static initializers read <c>FontDatabase.Helvetica*</c>)
///   before that constructor runs captures null font references into the static
///   <c>DeviceVariable&lt;Font&gt;</c> fields — once cached, the title label renders
///   with a null font and the "Week N" header degrades to "ee".
///
///   Stage 2 (<see cref="Apply"/>): the postfix fires after FontDatabase's ctor
///   completes (Main.Begin builds it during the game's own Awake), at which point
///   all <c>Helvetica*</c> font fields are non-null. Now it's safe to patch
///   NewAssetScreen and friends, since the static initializers will capture real
///   fonts.
/// </summary>
public static class Patcher
{
    private static Harmony _harmony;
    private static bool _scheduled;
    private static bool _applied;

    /// <summary>Install only the bootstrap hook. Real patches apply later.</summary>
    public static void ScheduleApply()
    {
        if (_scheduled) return;
        _scheduled = true;

        _harmony = new Harmony(Plugin.PluginGUID);

        var ctor = AccessTools.Constructor(typeof(FontDatabase));
        if (ctor == null)
        {
            Plugin.BepinLogger.LogError("Patcher: FontDatabase constructor not found — patches won't apply.");
            return;
        }

        var postfix = new HarmonyMethod(typeof(Patcher).GetMethod(
            nameof(FontDatabaseCtorPostfix),
            BindingFlags.Static | BindingFlags.NonPublic));

        _harmony.Patch(ctor, postfix: postfix);
        Plugin.BepinLogger.LogInfo("Patcher: bootstrap hook installed on FontDatabase ctor; real patches deferred.");
    }

    /// <summary>Fires once FontDatabase's instance constructor finishes — all Helvetica* fonts are now non-null.</summary>
    private static void FontDatabaseCtorPostfix()
    {
        if (_applied) return;
        Apply();
    }

    public static void Apply()
    {
        if (_applied) return;
        _applied = true;

        if (_harmony == null) _harmony = new Harmony(Plugin.PluginGUID);
        var patchTypes = new[]
        {
            typeof(GameApi.GameController_Ctor_Patch),
            typeof(GameApi.GameController_Release_Patch),
            typeof(GameApi.Game_IncreaseScore_Patch),
            typeof(GameApi.Game_StartWeek_Patch),
            typeof(GameApi.Game_OverflowStart_Patch),
            typeof(GameApi.Game_Update_Patch),
            typeof(GameApi.City_PeepSpawnScale_Patch),
            typeof(GameApi.LocomotiveDefinition_Speed_Patch),
            typeof(GameApi.NewAssetScreen_GetAssets_Patch),
            typeof(GameApi.NewAssetScreen_BuildAssets_Patch),
            typeof(GameApi.NewAssetScreen_OnAsset_Patch),
            typeof(GameApi.NewAssetScreen_TransitionIn_Patch),
            typeof(GameApi.NewAssetScreen_BuildLocomotive_Patch),
            typeof(MapApi.IsUnlockedPatch),
            typeof(SuppressApi.Game_SubmitScore_Patch),
            typeof(SuppressApi.Profile_CompleteAchievement_Patch),
            typeof(SuppressApi.Profile_SaveToCloud_Patch),
        };

        int applied = 0;
        foreach (var t in patchTypes)
        {
            try
            {
                _harmony.CreateClassProcessor(t).Patch();
                applied++;
            }
            catch (System.Exception e)
            {
                Plugin.BepinLogger.LogError($"Failed to apply patch {t.FullName}: {e}");
            }
        }
        Plugin.BepinLogger.LogInfo($"Applied {applied}/{patchTypes.Length} Harmony patch classes ({_harmony.GetPatchedMethods().Count()} methods total) after FontDatabase init.");
    }

    public static void Unapply()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
        _scheduled = false;
        _applied = false;
    }
}
