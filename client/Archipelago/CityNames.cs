using System.Collections.Generic;

namespace client.Archipelago;

/// <summary>
/// Two-way bridge between the game's internal city ids (e.g. <c>"nyc"</c>, the lowercase keys
/// in <c>CityDatabase</c>) and the apworld's display names (e.g. <c>"New York City"</c>, the
/// keys in items/locations JSON). Lookup tables are derived from
/// <c>decompiled/CityDatabase.cs</c>'s playable-cities list cross-referenced with
/// <c>apworld/MiniMetro.py</c>'s <c>MAPS</c> dict — 34 entries total.
/// </summary>
public static class CityNames
{
    /// <summary>Internal id → apworld display name.</summary>
    public static readonly IReadOnlyDictionary<string, string> InternalToDisplay = new Dictionary<string, string>
    {
        ["london"]       = "London",
        ["paris"]        = "Paris",
        ["nyc"]          = "New York City",
        ["warsaw"]       = "Warsaw",
        ["lisbon"]       = "Lisbon",
        ["tokyo"]        = "Tokyo",
        ["chicago"]      = "Chicago",
        ["budapest"]     = "Budapest",
        ["berlin"]       = "Berlin",
        ["melbourne"]    = "Melbourne",
        ["hongkong"]     = "Hong Kong",
        ["barcelona"]    = "Barcelona",
        ["osaka"]        = "Osaka",
        ["stockholm"]    = "Stockholm",
        ["stpetersburg"] = "Saint Petersburg",
        ["boston"]       = "Boston",
        ["montreal"]     = "Montreal",
        ["sanfrancisco"] = "San Francisco",
        ["saopaulo"]     = "Sao Paulo",
        ["seoul"]        = "Seoul",
        ["santiago"]     = "Santiago",
        ["washingtondc"] = "Washington, D.C.",
        ["tashkent"]     = "Tashkent",
        ["singapore"]    = "Singapore",
        ["cairo"]        = "Cairo",
        ["istanbul"]     = "Istanbul",
        ["shanghai"]     = "Shanghai",
        ["guangzhou"]    = "Guangzhou",
        ["nanjing"]      = "Nanjing",
        ["chongqing"]    = "Chongqing",
        ["mumbai"]       = "Mumbai",
        ["addisababa"]   = "Addis Ababa",
        ["lagos"]        = "Lagos",
        ["auckland"]     = "Auckland",
    };

    /// <summary>Apworld display name → internal id. Built lazily from the forward map.</summary>
    public static readonly IReadOnlyDictionary<string, string> DisplayToInternal = BuildReverse();

    private static IReadOnlyDictionary<string, string> BuildReverse()
    {
        var d = new Dictionary<string, string>(InternalToDisplay.Count);
        foreach (var kv in InternalToDisplay) d[kv.Value] = kv.Key;
        return d;
    }

    public static string ToDisplay(string internalId) =>
        internalId != null && InternalToDisplay.TryGetValue(internalId, out var name) ? name : null;

    public static string ToInternal(string displayName) =>
        displayName != null && DisplayToInternal.TryGetValue(displayName, out var id) ? id : null;
}
