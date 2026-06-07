using System.Collections.Generic;

namespace client.Archipelago;

/// <summary>
/// Persistent record of which <see cref="AssetType"/> values the player has unlocked
/// via AP item drops. Mirrored into the live <c>GameApi.AllowedPicks</c> set on every
/// <see cref="GameApi.Events.GameStarted"/> — needed because <c>SetGame</c> wipes
/// <c>AllowedPicks</c> on each map open and the dev <c>LimitPicksTo</c> API is
/// per-run, not per-session.
///
/// Baseline: Locomotive + Tram (so the locomotive panel works on every city). Real
/// upgrade-panel types (Line, Carriage, Interchange, Crossing, Bridge, Shinkansen)
/// stay out until their corresponding "<c>... - Unlock</c>" item arrives from AP.
/// </summary>
public static class UnlockState
{
    private static readonly AssetType[] Baseline = { AssetType.Locomotive, AssetType.Tram };

    /// <summary>
    /// Persistent unlocked types and their per-type ownership cap (-1 = unlimited).
    /// Re-seeded into <c>GameApi.AllowedPicks</c> on every GameStarted, so the cap
    /// must live here — anything stored only in <c>AllowedPicks</c> is wiped by SetGame.
    /// </summary>
    public static readonly Dictionary<AssetType, int> Unlocked = new();

    /// <summary>
    /// Total "New Line - Unlock" items received this session. Authoritative cap is
    /// recomputed at each GameStarted as <c>city's starting line count + LineUnlockCount</c>
    /// — starting lines are kept via <c>KeepInitialTypes</c> and would otherwise eat
    /// the player's unlocks.
    /// </summary>
    public static int LineUnlockCount;

    static UnlockState() => Reset();

    /// <summary>Drop all unlocks and return to the baseline set. Called on (re)connect.</summary>
    public static void Reset()
    {
        Unlocked.Clear();
        foreach (var t in Baseline) Unlocked[t] = -1;
        LineUnlockCount = 0;
    }

    /// <summary>
    /// Add or update the per-type cap (-1 = unlimited). Last write wins, so repeat
    /// "New Line - Unlock" items can bump the cap upward.
    /// </summary>
    public static void Add(AssetType type, int maxCount = -1) => Unlocked[type] = maxCount;

    public static bool Contains(AssetType type) => Unlocked.ContainsKey(type);
}
