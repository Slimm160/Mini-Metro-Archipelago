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
/// <see cref="GameApi.Events.GameStarted"/> to fire the AP "Week 1" check the
/// moment the map opens, and <see cref="GameApi.Events.WeekChanged"/> for every
/// subsequent week transition.
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

    /// <summary>
    /// Map just opened (HUD shows "Week 1", <c>Game.Week == 0</c>) — fire the
    /// AP "Week 1" check immediately so the first check no longer requires the
    /// player to finish a full week first.
    /// </summary>
    private void OnGameStarted(Game g) => TrySend(g, 1);

    /// <summary>
    /// The HUD displays "Week N" where <c>N = Clock.ClosestWeek + 1</c>, and
    /// <c>Game.Week</c> mirrors the 0-based <c>Clock.Week</c>. So when
    /// <c>Game.Week</c> transitions 1 → 2 (HUD "Week 2" → "Week 3"), we want
    /// to fire the AP "<c>Week 3</c>" check — i.e., <c>week + 1</c>. The Week 1
    /// check is sent separately by <see cref="OnGameStarted"/>.
    /// </summary>
    private void OnWeekChanged(Game g, int week) => TrySend(g, week + 1);

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
    /// Evaluate the active victory condition and, if met, send the goal packet.
    /// Iterates the local checked-list, not the server's snapshot, since the server
    /// one isn't guaranteed up-to-date the instant we send a check.
    /// </summary>
    private void CheckVictory()
    {
        if (victorySent) return;

        // The goal location only exists for weeks 1..MaxWeeks; mirror the apworld's clamp.
        int target = Math.Min(data.TargetWeek, data.MaxWeeks);

        if (data.Goal == 1)
        {
            CheckFinalCheckVictory(target);
            return;
        }

        CheckCompleteMapsVictory(target);
    }

    /// <summary>Final Check goal: win the instant the chosen map's Target Week is checked.</summary>
    private void CheckFinalCheckVictory(int target)
    {
        string goalMap = data.GoalMap;
        if (string.IsNullOrEmpty(goalMap)) return;

        string goalLoc = $"{goalMap} - Week {target}";
        foreach (long id in data.CheckedLocations)
        {
            string name;
            try { name = session.Locations.GetLocationNameFromId(id); }
            catch { continue; }
            if (name == goalLoc)
            {
                SendGoal($"Victory! Cleared {goalLoc}.");
                return;
            }
        }
    }

    /// <summary>
    /// Complete Maps goal: count distinct cities at-or-past TargetWeek; once we hit
    /// MapsToComplete, win.
    /// </summary>
    private void CheckCompleteMapsVictory(int target)
    {
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

        SendGoal($"Victory! {citiesAtTarget.Count} cities cleared at Week {target}+ (goal: {needed}).");
    }

    /// <summary>Send the one-shot ClientGoal status update, latching <see cref="victorySent"/>.</summary>
    private void SendGoal(string message)
    {
        victorySent = true;
        try
        {
            session.Socket.SendPacketAsync(new StatusUpdatePacket
            {
                Status = ArchipelagoClientState.ClientGoal,
            });
            Plugin.BepinLogger.LogMessage(message);
        }
        catch (Exception e)
        {
            victorySent = false; // let next check retry
            Plugin.BepinLogger.LogError($"Failed to send ClientGoal: {e}");
        }
    }
}
