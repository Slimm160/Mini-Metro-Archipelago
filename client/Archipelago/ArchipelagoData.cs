using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace client.Archipelago;

public class ArchipelagoData
{
    public string Uri;
    public string SlotName;
    public string Password;
    public int Index;

    public List<long> CheckedLocations;

    /// <summary>
    /// seed for this archipelago data. Can be used when loading a file to verify the session the player is trying to
    /// load is valid to the room it's connecting to.
    /// </summary>
    private string seed;

    private Dictionary<string, object> slotData;

    public bool NeedSlotData => slotData == null;

    /// <summary>True if the apworld's slot data sets the standard <c>death_link</c> flag.</summary>
    public bool DeathLink => GetBool("death_link");

    /// <summary>Apworld display names (e.g. "London") of cities the player starts with unlocked.</summary>
    public IReadOnlyList<string> StartingMaps => GetStringList("starting_maps");

    /// <summary>Apworld display names of cities locked behind shard items.</summary>
    public IReadOnlyList<string> UnlockableMaps => GetStringList("unlockable_maps");

    /// <summary>Number of distinct cities the player must clear at <see cref="TargetWeek"/> to win.</summary>
    public int MapsToComplete => GetInt("maps_to_complete", 20);

    /// <summary>The week number that, when reached on a map, counts toward the victory tally.</summary>
    public int TargetWeek => GetInt("target_week", 9);

    /// <summary>Per-map week ceiling — the apworld emits one location per week 1..MaxWeeks.</summary>
    public int MaxWeeks => GetInt("max_weeks", 9);

    /// <summary>
    /// Victory condition: <c>0</c> = Complete Maps (clear <see cref="TargetWeek"/> on
    /// <see cref="MapsToComplete"/> distinct cities), <c>1</c> = Final Check (clear
    /// <see cref="TargetWeek"/> on the single <see cref="GoalMap"/>).
    /// </summary>
    public int Goal => GetInt("goal", 0);

    /// <summary>Apworld display name of the map whose Target Week wins under the Final Check goal.</summary>
    public string GoalMap => GetString("goal_map", "");

    /// <summary>
    /// Game mode the apworld pinned this seed to ("classic" or "extreme"). The client maps
    /// this to a single <see cref="GameMode"/> below; only that mode is added to the
    /// <see cref="MapApi"/> unlock set on connect.
    /// </summary>
    public string GameModeRaw => GetString("game_mode", "classic");

    /// <summary>Parsed counterpart to <see cref="GameModeRaw"/>.</summary>
    public global::GameMode Mode =>
        string.Equals(GameModeRaw, "extreme", StringComparison.OrdinalIgnoreCase)
            ? global::GameMode.EXTREME
            : global::GameMode.CLASSIC;

    /// <summary>
    /// Number of <c>&lt;City&gt; - Shard</c> items the player must hold before the city
    /// unlocks. Prefer the apworld-resolved <c>shards_per_map</c> slot value (honors
    /// the Map Shard Count Mode — Static vs Custom). Falls back to the original
    /// Static-mode formula for compatibility with older apworld zips that don't
    /// send this key.
    /// </summary>
    public int ShardsPerMap
    {
        get
        {
            int fromSlot = GetInt("shards_per_map", -1);
            if (fromSlot > 0) return fromSlot;
            int mw = MaxWeeks;
            int v = mw < 5 ? mw - 1 : mw - 2;
            return v < 1 ? 1 : v;
        }
    }

    public ArchipelagoData()
    {
        Uri = "localhost";
        SlotName = "Player1";
        CheckedLocations = new();
    }

    public ArchipelagoData(string uri, string slotName, string password)
    {
        Uri = uri;
        SlotName = slotName;
        Password = password;
        CheckedLocations = new();
    }

    /// <summary>
    /// assigns the slot data and seed to our data handler. any necessary setup using this data can be done here.
    /// </summary>
    /// <param name="roomSlotData">slot data of your slot from the room</param>
    /// <param name="roomSeed">seed name of this session</param>
    public void SetupSession(Dictionary<string, object> roomSlotData, string roomSeed)
    {
        slotData = roomSlotData;
        seed = roomSeed;
    }

    /// <summary>
    /// Drop the cached slot data so the next connect re-requests it. Without this a
    /// reconnect in the same process leaves <see cref="NeedSlotData"/> false, so the
    /// client logs in with <c>requestSlotData: false</c> and <see cref="SetupSession"/>
    /// then overwrites the good data with the login result's empty SlotData —
    /// emptying <see cref="StartingMaps"/>/<see cref="UnlockableMaps"/> and locking
    /// every map. Call this on disconnect/teardown.
    /// </summary>
    public void ClearSession()
    {
        slotData = null;
        seed = null;
    }

    /// <summary>
    /// returns the object as a json string to be written to a file which you can then load
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }

    // --- slot_data extraction helpers --------------------------------------
    // The MultiClient SDK deserializes slot_data into Dictionary<string, object>
    // with JSON-native scalar types — but lists arrive as Newtonsoft JArray and
    // numbers can be int/long/JValue depending on size. Normalize everything here.

    private bool GetBool(string key)
    {
        if (slotData == null || !slotData.TryGetValue(key, out var v) || v == null) return false;
        try { return Convert.ToInt32(v) != 0; }
        catch { return false; }
    }

    private int GetInt(string key, int fallback)
    {
        if (slotData == null || !slotData.TryGetValue(key, out var v) || v == null) return fallback;
        try { return Convert.ToInt32(v); }
        catch { return fallback; }
    }

    private string GetString(string key, string fallback)
    {
        if (slotData == null || !slotData.TryGetValue(key, out var v) || v == null) return fallback;
        return v.ToString();
    }

    private IReadOnlyList<string> GetStringList(string key)
    {
        if (slotData == null || !slotData.TryGetValue(key, out var v) || v == null)
            return Array.Empty<string>();
        // Newtonsoft hands us a JArray for JSON arrays.
        if (v is Newtonsoft.Json.Linq.JArray arr)
            return arr.Select(t => t?.ToString()).Where(s => s != null).ToList();
        if (v is IEnumerable<object> list)
            return list.Select(o => o?.ToString()).Where(s => s != null).ToList();
        return Array.Empty<string>();
    }
}
