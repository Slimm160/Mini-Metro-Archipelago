namespace client;

/// <summary>
/// Gameplay-side AP surface — split across files:
///   <c>GameApi.State</c>   — query run state
///   <c>GameApi.Events</c>  — observe game events
///   <c>GameApi.Grant</c>   — give the player items/upgrades
///   <c>GameApi.Take</c>    — trap effects (overflow, train/line confiscation, peep clears)
///
/// Internal state below is the shared live <see cref="Game"/> reference set by
/// the Harmony patches in <c>Patches.cs</c>.
/// </summary>
public static partial class GameApi
{
    /// <summary>The live <see cref="Game"/> instance, or null when in the menu.</summary>
    internal static Game CurrentGame { get; private set; }

    /// <summary>Last-observed <c>Game.Week</c>; used by the Game.Update postfix to detect changes.</summary>
    internal static int LastSeenWeek = -1;

    /// <summary>
    /// If non‑empty, every upgrade picker that opens for this run is forced to present only the asset
    /// types present (the dictionary keys), respecting the maximum number of that asset the player
    /// may own (the dictionary value). Use <c>-1</c> to mean "no limit".
    /// (Set per‑picker via <c>Game.ForcedAssetPicks</c> in <c>NewAssetScreen.HandleTransitionIn</c>.)
    /// </summary>
    internal static readonly System.Collections.Generic.Dictionary<AssetType, int> AllowedPicks = new();

    /// <summary>
    /// Set by the <c>NewAssetScreen.GetAssets</c> postfix when the panel about to be
    /// rendered has zero options (typically because <see cref="AllowedPicks"/> filtered
    /// out every choice). Drives the in-game "Skip" rescue button — without it the
    /// player would be locked out of an empty picker.
    /// </summary>
    internal static bool IsPickerStuck;

    /// TODO: remove / unimplemented
    /// <summary>
    /// Scales the live <c>City.PeepSpawnScale</c> via a Harmony postfix. 1.0 = vanilla;
    /// higher values produce proportionally more peeps per station per hour. Resets to
    /// 1.0 when a run ends so dev tweaks don't leak across games.
    /// </summary>
    internal static float PeepSpawnMultiplier = 1f;

    /// TODO: remove / unimplemented
    /// <summary>
    /// Scales the live <c>LocomotiveDefinition.Speed</c> getter via a Harmony postfix.
    /// 1.0 = vanilla. Affects every locomotive type (Locomotive/Tram/Shinkansen)
    /// uniformly. Resets to 1.0 on game end.
    /// </summary>
    internal static float TrainSpeedMultiplier = 1f;

    /// <summary>
    /// IMGUI rescue button — appears below the picker when the live upgrade panel has
    /// no buttons to click. Calls <c>State.SkipUpgrade</c>, which advances to the next
    /// pick or closes the picker if this was the last. Positioned at ~75% screen
    /// height so it doesn't overlap the title (top) or the asset panel (centre).
    /// </summary>
    public static void OnGUI()
    {
        if (!IsPickerStuck) return;
        var g = CurrentGame;
        if (g == null || g.Screen != GameScreen.NewAsset) return;

        const float w = 200f, h = 40f;
        var rect = new UnityEngine.Rect(
            (UnityEngine.Screen.width - w) * 0.5f,
            UnityEngine.Screen.height * 0.75f,
            w, h);
        if (UnityEngine.GUI.Button(rect, "Skip")) State.SkipUpgrade();
    }

    internal static void SetGame(Game game)
    {
        CurrentGame = game;
        LastSeenWeek = game.Week;
        AllowedPicks.Clear();
        Events.RaiseGameStarted(game);
    }

    internal static void ClearGame()
    {
        var g = CurrentGame;
        CurrentGame = null;
        LastSeenWeek = -1;
        AllowedPicks.Clear();
        PeepSpawnMultiplier = 1f;
        TrainSpeedMultiplier = 1f;
        if (g != null) Events.RaiseGameEnded(g);
    }
}
