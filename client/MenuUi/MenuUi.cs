using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UI;

namespace client.MenuUi;

/// <summary>
/// Static coordinator for the native Archipelago connection UI. Holds the panel built for
/// the current <c>HomeScreen</c> instance and is pumped once per frame from
/// <see cref="MenuUiPatches.Menu_Update_Patch"/>. A fresh <see cref="ArchipelagoMenuPanel"/>
/// is created every time <c>HomeScreen</c> is constructed (incl. resolution changes), so the
/// reference here is simply replaced — the old panel dies with its released screen.
/// </summary>
public static class MenuUi
{
    public static ArchipelagoMenuPanel Current;

    /// <summary>Per-frame pump: drives text input and connection-state polling.</summary>
    public static void Tick()
    {
        Current?.Tick();
    }
}

/// <summary>
/// The game's UI text comes from <c>StringId</c> enum keys resolved through the active
/// <see cref="Locale"/>'s private <c>stringTable</c>. <c>LabelButton</c> re-resolves its text
/// from its key on every <c>RecalculateSize</c>, so we can't just poke an <c>FLabel</c> — the
/// text would revert. Instead we register our own strings under <c>StringId</c> values cast
/// from unused integers (no real enum member uses these, so there's zero collision) by
/// injecting directly into the locale table. Labels that aren't buttons use the
/// <see cref="LocalizedString"/> literal overloads and don't need this.
/// </summary>
internal static class CustomStrings
{
    // Far above any real StringId member; cast to the enum gives us private keys.
    private const int Base = 0x7AC10000;
    public static readonly StringId Archipelago = (StringId)(Base + 1);
    public static readonly StringId Connect     = (StringId)(Base + 2);
    public static readonly StringId Disconnect  = (StringId)(Base + 3);
    public static readonly StringId Connecting  = (StringId)(Base + 4);

    private static readonly Dictionary<StringId, string> Values = new()
    {
        [Archipelago] = "Archipelago",
        [Connect]     = "Connect",
        [Disconnect]  = "Disconnect",
        [Connecting]  = "Connecting…",
    };

    private static readonly FieldInfo F_StringTable = AccessTools.Field(typeof(Locale), "stringTable");

    /// <summary>Idempotently inject our strings into the current and fallback locales.</summary>
    public static void EnsureInjected()
    {
        var db = LocaleDatabase.Instance;
        if (db == null) return;
        Inject(db.CurrentLocale);
        Inject(db.FallbackLocale);
    }

    private static void Inject(Locale locale)
    {
        if (locale == null || F_StringTable == null) return;
        if (F_StringTable.GetValue(locale) is not Dictionary<StringId, List<string>> table) return;
        foreach (var kv in Values)
            table[kv.Key] = new List<string> { kv.Value };
    }
}
