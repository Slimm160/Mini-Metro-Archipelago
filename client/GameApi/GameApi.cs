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
    /// If non-empty, every upgrade picker that opens for this run is forced to present
    /// only these <see cref="AssetType"/> options (set per-picker via <c>Game.ForcedAssets</c>
    /// in <c>NewAssetScreen.HandleTransitionIn</c>).
    /// </summary>
    internal static readonly System.Collections.Generic.HashSet<AssetType> AllowedPicks = new();

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
        if (g != null) Events.RaiseGameEnded(g);
    }
}
