using System.Collections.Generic;
using UnityEngine;

namespace client;

/// <summary>
/// Developer testing overlay. Renders a side panel with buttons for every
/// <see cref="GameApi"/> and <see cref="MapApi"/> entry point so changes can
/// be exercised without a live AP session.
///
/// <see cref="SuppressApi"/> is intentionally not exposed — its toggles are
/// disabled on first show, so dev testing always observes vanilla side effects.
/// </summary>
public static class DevPanel
{
    private static bool isOpen;
    private static bool initialized;
    private static Vector2 scrollPos;

    // Map API inputs
    private static string mapCityInput = "london";
    private static int mapModeIndex;
    private static readonly GameMode[] Modes = { GameMode.CLASSIC, GameMode.ZEN, GameMode.EXTREME, GameMode.SANDBOX };
    private static readonly string[] ModeNames = { "Normal", "Endless", "Extreme", "Creative" };

    // LimitPicksTo toggle state
    private static readonly Dictionary<AssetType, bool> LimitToggles = new()
    {
        [AssetType.Locomotive] = false,
        [AssetType.Carriage] = false,
        [AssetType.Line] = false,
        [AssetType.Interchange] = false,
        [AssetType.Crossing] = false,
        [AssetType.Tram] = false,
        [AssetType.Shinkansen] = false,
    };

    public static void Awake()
    {
        if (initialized) return;
        SuppressApi.State.DisableAll();
        initialized = true;
    }

    public static void OnGUI()
    {
        int x = Screen.width - 260;

        if (GUI.Button(new Rect(x, 16, 80, 24), isOpen ? "Hide Dev" : "Dev Panel"))
            isOpen = !isOpen;

        if (!isOpen) return;

        GUILayout.BeginArea(new Rect(x, 48, 240, Mathf.Min(Screen.height - 80, 720)), GUI.skin.box);
        scrollPos = GUILayout.BeginScrollView(scrollPos);
        Draw();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static void Draw()
    {
        // --- State ---------------------------------------------------------
        GUILayout.Label("=== State ===");
        GUILayout.Label($"InGame: {GameApi.State.IsInGame}   Over: {GameApi.State.IsOver}");
        GUILayout.Label($"Score: {GameApi.State.Score}   Wk: {GameApi.State.Week}   Day: {GameApi.State.Day}");
        GUILayout.Label($"City: {GameApi.State.CityId}   Mode: {GameApi.State.Mode}");
        GUILayout.Label($"Lines: {GameApi.State.LineCount}/{GameApi.State.MaxLineCount}   Stations: {GameApi.State.StationCount}");
        GUILayout.Label($"Peeps wait: {GameApi.State.WaitingPeepCount}   imp: {GameApi.State.ImpatientPeepCount}");

        if (GUILayout.Button(GameApi.State.IsPaused ? "Resume" : "Pause"))
            GameApi.State.IsPaused = !GameApi.State.IsPaused;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+1 wk")) GameApi.State.IncrementWeek();
        if (GUILayout.Button("+3 wk")) GameApi.State.IncrementWeek(3);
        if (GUILayout.Button("-1 wk")) GameApi.State.DecrementWeek();
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // --- Grant ---------------------------------------------------------
        GUILayout.Label("=== Grant ===");
        if (GUILayout.Button("Locomotive")) GameApi.Grant.Locomotive();
        if (GUILayout.Button("Carriage")) GameApi.Grant.Carriage();
        if (GUILayout.Button("Line")) GameApi.Grant.Line();
        if (GUILayout.Button("Interchange")) GameApi.Grant.Interchange();
        if (GUILayout.Button("Crossing")) GameApi.Grant.Crossing();
        if (GUILayout.Button("Tram")) GameApi.Grant.Tram();
        if (GUILayout.Button("Shinkansen")) GameApi.Grant.Shinkansen();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Pick +1")) GameApi.Grant.UpgradePick();
        if (GUILayout.Button("Pick +3")) GameApi.Grant.UpgradePick(3);
        GUILayout.EndHorizontal();

        GUILayout.Label("Limit pool:");
        foreach (var key in new List<AssetType>(LimitToggles.Keys))
            LimitToggles[key] = GUILayout.Toggle(LimitToggles[key], key.ToString());

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Limit"))
        {
            var allowed = new List<AssetType>();
            foreach (var kv in LimitToggles) if (kv.Value) allowed.Add(kv.Key);
            GameApi.Grant.LimitPicksTo(allowed.ToArray());
        }
        if (GUILayout.Button("Clear Limit")) GameApi.Grant.ClearPickLimit();
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Skip pick (close picker)")) GameApi.State.SkipUpgrade();

        GUILayout.Space(6);

        // --- Take ----------------------------------------------------------
        GUILayout.Label("=== Take ===");
        if (GUILayout.Button("Overflow (random)")) GameApi.Take.Overflow();
        if (GUILayout.Button("Remove a train")) GameApi.Take.Train();
        if (GUILayout.Button("Remove a line")) GameApi.Take.Line();
        if (GUILayout.Button("Delete a train (permanent)")) GameApi.Take.DeleteTrain();
        if (GUILayout.Button("Delete a line (permanent)")) GameApi.Take.DeleteLine();
        if (GUILayout.Button("Clear peeps (1 stn)")) GameApi.Take.Peeps();
        if (GUILayout.Button("Clear ALL peeps")) GameApi.Take.AllPeeps();
        if (GUILayout.Button("End game (force loss)")) GameApi.Take.EndGame();

        GUILayout.Space(6);

        // --- Tuning --------------------------------------------------------
        GUILayout.Label("=== Tuning ===");
        GUILayout.Label($"Spawn x{GameApi.PeepSpawnMultiplier:F2}   Speed x{GameApi.TrainSpeedMultiplier:F2}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn +25%")) GameApi.PeepSpawnMultiplier += 0.25f;
        if (GUILayout.Button("Speed +25%")) GameApi.TrainSpeedMultiplier += 0.25f;
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // --- Map API -------------------------------------------------------
        GUILayout.Label("=== MapApi ===");
        MapApi.State.IsActive = GUILayout.Toggle(MapApi.State.IsActive, "Override active");

        GUILayout.Label("City id:");
        mapCityInput = GUILayout.TextField(mapCityInput);

        mapModeIndex = GUILayout.SelectionGrid(mapModeIndex, ModeNames, 2);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Unlock")) MapApi.Grant.Unlock(mapCityInput, Modes[mapModeIndex]);
        if (GUILayout.Button("Lock")) MapApi.Take.Lock(mapCityInput, Modes[mapModeIndex]);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Unlock (all modes)")) MapApi.Grant.Unlock(mapCityInput);
        if (GUILayout.Button("Lock (all modes)")) MapApi.Take.Lock(mapCityInput);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Unlock ALL cities")) MapApi.Grant.UnlockAll();
        if (GUILayout.Button("Clear set")) MapApi.Take.Clear();
        GUILayout.EndHorizontal();

        GUILayout.Label($"Unlocked: {MapApi.State.All.Count}");
    }
}
