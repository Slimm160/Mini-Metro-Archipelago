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

    /// <summary>The full set the upgrade picker should be filtered against this run.</summary>
    public static readonly HashSet<AssetType> Unlocked = new(Baseline);

    /// <summary>Drop all unlocks and return to the baseline set. Called on (re)connect.</summary>
    public static void Reset()
    {
        Unlocked.Clear();
        foreach (var t in Baseline) Unlocked.Add(t);
    }

    /// <summary>Add an asset type to the persistent unlocked set. Returns true if it wasn't already there.</summary>
    public static bool Add(AssetType type) => Unlocked.Add(type);

    public static bool Contains(AssetType type) => Unlocked.Contains(type);
}
