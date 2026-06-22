using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using client.Archipelago;
using client.Utils;
using UnityEngine;

namespace client;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGUID = "com.Slimm.MiniMetroArchipelagoClient";
    public const string PluginName = "client";
    public const string PluginVersion = "1.0.0";

    public const string ModDisplayInfo = $"{PluginName} v{PluginVersion}";
    private const string APDisplayInfo = $"Archipelago v{ArchipelagoClient.APVersion}";
    public static ManualLogSource BepinLogger;
    public static ArchipelagoClient ArchipelagoClient;

    // Persisted connection info, written to BepInEx/config/<GUID>.cfg. Seeded into
    // ServerData on launch and saved back when the player clicks Connect, so host,
    // slot name, and password survive a restart. Password is stored in plaintext.
    private ConfigEntry<string> _cfgHost;
    private ConfigEntry<string> _cfgSlotName;
    private ConfigEntry<string> _cfgPassword;

    private void Awake()
    {
        BepinLogger = Logger;
        ArchipelagoClient = new ArchipelagoClient();

        BindConnectionConfig();

        ArchipelagoConsole.Awake();
        Patcher.ScheduleApply();
        DevPanel.Awake();

        ArchipelagoConsole.LogMessage($"{ModDisplayInfo} loaded!");
    }

    /// <summary>
    /// Bind the persisted connection fields and pre-fill <see cref="ArchipelagoClient.ServerData"/>
    /// with the last-used values (defaults match the vanilla <c>localhost</c>/<c>Player1</c>).
    /// </summary>
    private void BindConnectionConfig()
    {
        _cfgHost = Config.Bind("Connection", "Host", "localhost",
            "Archipelago server host (and port) to pre-fill in the connect overlay.");
        _cfgSlotName = Config.Bind("Connection", "SlotName", "Player1",
            "Slot/player name to pre-fill in the connect overlay.");
        _cfgPassword = Config.Bind("Connection", "Password", "",
            "Room password to pre-fill in the connect overlay (stored in plaintext).");

        ArchipelagoClient.ServerData.Uri = _cfgHost.Value;
        ArchipelagoClient.ServerData.SlotName = _cfgSlotName.Value;
        ArchipelagoClient.ServerData.Password = _cfgPassword.Value;
    }

    /// <summary>
    /// Persist whatever the player last entered in the connect overlay. Setting a
    /// <see cref="ConfigEntry{T}.Value"/> flushes the .cfg by default, so the next launch
    /// pre-fills these via <see cref="BindConnectionConfig"/>.
    /// </summary>
    private void SaveConnectionConfig()
    {
        _cfgHost.Value = ArchipelagoClient.ServerData.Uri;
        _cfgSlotName.Value = ArchipelagoClient.ServerData.SlotName;
        _cfgPassword.Value = ArchipelagoClient.ServerData.Password;
    }

    private void Update()
    {
        MainThreadDispatcher.Drain();
    }

    private void OnGUI()
    {
        if (GameApi.State.IsInGame && GameApi.State.Game != null &&
            GameApi.State.Game.Screen == GameScreen.NewAsset)
            return;
        GUI.Label(new Rect(16, 16, 300, 20), ModDisplayInfo);
        ArchipelagoConsole.OnGUI();
        string statusMessage;
        // show the Archipelago Version and whether we're connected or not
        if (ArchipelagoClient.Authenticated)
        {
            // if your game doesn't usually show the cursor this line may be necessary
            // Cursor.visible = false;

            statusMessage = " Status: Connected";
            GUI.Label(new Rect(16, 50, 300, 20), APDisplayInfo + statusMessage);
        }
        else
        {
            // if your game doesn't usually show the cursor this line may be necessary
            // Cursor.visible = true;

            statusMessage = " Status: Disconnected";
            GUI.Label(new Rect(16, 50, 300, 20), APDisplayInfo + statusMessage);
            GUI.Label(new Rect(16, 70, 150, 20), "Host: ");
            GUI.Label(new Rect(16, 90, 150, 20), "Player Name: ");
            GUI.Label(new Rect(16, 110, 150, 20), "Password: ");

            ArchipelagoClient.ServerData.Uri = GUI.TextField(new Rect(150, 70, 150, 20),
                ArchipelagoClient.ServerData.Uri);
            ArchipelagoClient.ServerData.SlotName = GUI.TextField(new Rect(150, 90, 150, 20),
                ArchipelagoClient.ServerData.SlotName);
            ArchipelagoClient.ServerData.Password = GUI.TextField(new Rect(150, 110, 150, 20),
                ArchipelagoClient.ServerData.Password);
            if (GUI.Button(new Rect(16, 130, 100, 20), "Connect") &&
                !ArchipelagoClient.ServerData.SlotName.IsNullOrWhiteSpace())
            {
                SaveConnectionConfig();
                ArchipelagoClient.Connect();
            }
        }
        // DevPanel.OnGUI();
        GameApi.OnGUI();
    }
}