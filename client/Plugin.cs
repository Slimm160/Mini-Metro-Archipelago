using BepInEx;
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

    /// <summary>
    /// Hidden dev fallback. The native main-menu connection panel (see <c>client.MenuUi</c>) is
    /// the primary UI; flip this to re-enable the legacy IMGUI connect box if the native panel
    /// ever fails to render (e.g. an unexpected resolution/locale).
    /// </summary>
    public static bool ShowLegacyConnectUI;

    private void Awake()
    {
        BepinLogger = Logger;
        ArchipelagoClient = new ArchipelagoClient();
        ArchipelagoConsole.Awake();
        Patcher.ScheduleApply();
        DevPanel.Awake();

        // See OnQuitting — clean shutdown to avoid the native crash on exit.
        Application.quitting += OnQuitting;

        ArchipelagoConsole.LogMessage($"{ModDisplayInfo} loaded!");
    }

    private void Update()
    {
        MainThreadDispatcher.Drain();
    }

    private static bool _quitting;

    /// <summary>
    /// Clean shutdown to avoid the Unity crash reporter firing on exit.
    ///
    /// The Archipelago MultiClient keeps background <c>Task.Delay</c> loops alive (keepalive /
    /// reconnect). When Mono unloads the app domain on exit it injects a <c>ThreadAbortException</c>
    /// into those continuations, which crashes natively — this is the "Crash!!!" we see right after
    /// "Input System polling thread exited". Disconnecting alone doesn't help (the timers persist
    /// even after the socket drops). <see cref="Application.quitting"/> fires AFTER every
    /// <c>OnApplicationQuit</c> save pass, so by here the game has already persisted its state; we
    /// disconnect for tidiness and then terminate the process before the crashing teardown runs.
    /// </summary>
    private void OnQuitting()
    {
        if (_quitting) return;
        _quitting = true;

        try
        {
            if (ArchipelagoClient != null && Archipelago.ArchipelagoClient.Authenticated)
                ArchipelagoClient.DisconnectFromServer();
        }
        catch (System.Exception e)
        {
            BepinLogger?.LogError($"Error during Archipelago shutdown: {e}");
        }

        try { System.Diagnostics.Process.GetCurrentProcess().Kill(); }
        catch { /* nothing useful to do if even Kill fails */ }
    }

    private void OnGUI()
    {
        if (GameApi.State.IsInGame && GameApi.State.Game != null &&
            GameApi.State.Game.Screen == GameScreen.NewAsset)
            return;
        GUI.Label(new Rect(16, 16, 300, 20), ModDisplayInfo);
        ArchipelagoConsole.OnGUI();

        // The native main-menu panel (client.MenuUi) is the primary connection UI; the IMGUI box
        // below is a hidden dev fallback.
        if (!ShowLegacyConnectUI)
        {
            GameApi.OnGUI();
            return;
        }

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
                ArchipelagoClient.Connect();
            }
        }
        // DevPanel.OnGUI();
        GameApi.OnGUI();
    }
}