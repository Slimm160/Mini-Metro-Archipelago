using System;
using System.Collections.Generic;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Packets;

namespace client.Archipelago;

/// <summary>
/// Forwards in-game week progression to the AP server as location checks.
///
/// One AP location per (city, week) pair, named <c>"&lt;Display&gt; - Week N"</c>. We hook
/// <see cref="GameApi.Events.GameStarted"/> to catch Week 1 (which the
/// <c>WeekChanged</c> poll skips — see <c>Game_Update_Patch</c>) and
/// <see cref="GameApi.Events.WeekChanged"/> for the 2→3→4… transitions.
///
/// Victory: once the player has reached <c>TargetWeek</c> on <c>MapsToComplete</c>
/// distinct cities, send a <c>ClientGoal</c> status update. The check runs after
/// every successful location send, but the goal packet only ever fires once per
/// session (re-sending isn't harmful but the log noise is).
/// </summary>
public class LocationHandler : IDisposable
{
    private const string GameName = "Mini Metro";

    private readonly ArchipelagoSession session;
    private readonly ArchipelagoData data;
    private readonly HashSet<long> sentThisSession = new();
    private bool victorySent;

    public LocationHandler(ArchipelagoSession session, ArchipelagoData data)
    {
        this.session = session;
        this.data = data;

        // Seed the session-local dedupe set with whatever the server already knows
        // we've cleared — prevents re-sending on reconnect, but lets the victory
        // check still run against the full history.
        foreach (long id in data.CheckedLocations) sentThisSession.Add(id);

        GameApi.Events.GameStarted += OnGameStarted;
        GameApi.Events.WeekChanged += OnWeekChanged;
    }

    public void Dispose()
    {
        GameApi.Events.GameStarted -= OnGameStarted;
        GameApi.Events.WeekChanged -= OnWeekChanged;
    }

    /// <summary>Game start → fire the Week 1 check (the WeekChanged poll never raises for it).</summary>
    private void OnGameStarted(Game g) => TrySend(g, 1);

    /// <summary>Each subsequent week transition → fire the check for the new week.</summary>
    private void OnWeekChanged(Game g, int week) => TrySend(g, week);

    private void TrySend(Game g, int week)
    {
        if (g?.City?.Definition?.Id == null) return;
        if (week < 1 || week > data.MaxWeeks) return;

        string display = CityNames.ToDisplay(g.City.Definition.Id);
        if (display == null) return; // unknown city (UGC, menu, tutorial, alt variant)

        string locName = $"{display} - Week {week}";
        long id;
        try
        {
            id = session.Locations.GetLocationIdFromName(GameName, locName);
        }
        catch (Exception e)
        {
            Plugin.BepinLogger.LogError($"AP location lookup failed for '{locName}': {e}");
            return;
        }
        if (id <= 0) return; // not an apworld location (e.g., max_weeks below this week)

        if (!sentThisSession.Add(id)) return; // already sent this session

        try
        {
            session.Locations.CompleteLocationChecksAsync(id);
            if (!data.CheckedLocations.Contains(id)) data.CheckedLocations.Add(id);
            Plugin.BepinLogger.LogInfo($"AP check: {locName} (id={id}).");
        }
        catch (Exception e)
        {
            sentThisSession.Remove(id); // allow retry next time
            Plugin.BepinLogger.LogError($"AP check send failed for '{locName}': {e}");
            return;
        }

        CheckVictory();
    }

    /// <summary>
    /// Count distinct cities at-or-past TargetWeek; once we hit MapsToComplete, send
    /// the goal packet. Iterates the local checked-list, not the server's snapshot,
    /// since the server one isn't guaranteed up-to-date the instant we send a check.
    /// </summary>
    private void CheckVictory()
    {
        if (victorySent) return;

        int target = data.TargetWeek;
        int needed = data.MapsToComplete;
        if (needed <= 0) return;

        var citiesAtTarget = new HashSet<string>();
        foreach (long id in data.CheckedLocations)
        {
            string name;
            try { name = session.Locations.GetLocationNameFromId(id); }
            catch { continue; }
            if (string.IsNullOrEmpty(name)) continue;

            int sep = name.LastIndexOf(" - Week ", StringComparison.Ordinal);
            if (sep <= 0) continue;
            if (!int.TryParse(name.Substring(sep + " - Week ".Length), out int w)) continue;
            if (w < target) continue;

            citiesAtTarget.Add(name.Substring(0, sep));
        }

        if (citiesAtTarget.Count < needed) return;

        victorySent = true;
        try
        {
            session.Socket.SendPacketAsync(new StatusUpdatePacket
            {
                Status = ArchipelagoClientState.ClientGoal,
            });
            Plugin.BepinLogger.LogMessage(
                $"Victory! {citiesAtTarget.Count} cities cleared at Week {target}+ (goal: {needed}).");
        }
        catch (Exception e)
        {
            victorySent = false; // let next check retry
            Plugin.BepinLogger.LogError($"Failed to send ClientGoal: {e}");
        }
    }
}
