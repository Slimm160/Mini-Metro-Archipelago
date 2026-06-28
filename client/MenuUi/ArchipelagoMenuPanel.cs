using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UI;
using UnityEngine;
using client.Archipelago;

namespace client.MenuUi;

/// <summary>
/// Builds and drives the native Archipelago connection UI on a <c>HomeScreen</c>:
///   • an "Archipelago" entry injected into the main-menu button column, and
///   • a side panel (Host / Player Name / Password + Connect, swapped for a status + Disconnect
///     view once connected) built entirely from the game's own Futile widgets.
///
/// A new instance is created per <c>HomeScreen</c> construction (see
/// <see cref="MenuUiPatches"/>); positions and styling intentionally mirror the game's own
/// screens (<c>ProfileCreateEditScreen</c> is the closest reference).
/// </summary>
public class ArchipelagoMenuPanel
{
    private const float PanelWidth    = 340f;
    private const float IconGutter    = 48f;
    private const float FieldTextW    = 250f;
    private const float FieldPad      = 6f;
    private const float FieldCenterX  = IconGutter + (FieldTextW + 2f * FieldPad) * 0.5f;
    private const string DefaultHost  = "archipelago.gg:38281";
    private const float PanelRightFrac = 3.00f;
    private const float MenuPanFrac    = 2.00f;
    public const float MenuButtonsYOffset = 24f;
    private static readonly FieldInfo  F_ButtonPanel = AccessTools.Field(typeof(HomeScreen), "buttonPanel");
    private static readonly FieldInfo  F_Buttons     = AccessTools.Field(typeof(HomeScreen), "buttons");
    private static readonly MethodInfo M_Reposition  = AccessTools.Method(typeof(HomeScreen), "RepositionButtons");
    private static readonly FieldInfo  F_Menu        = AccessTools.Field(typeof(MenuScreen), "menu");

    private readonly Theme theme;
    private readonly Menu menu;
    private readonly Frame frame;
    private readonly Locale locale;

    private LabelButton entryButton;
    private Panel formPanel;
    private Panel statusPanel;
    private Label statusLine1;
    private Label statusLine2;

    private readonly Field[] fields = new Field[3];
    private int focusedIndex = -1;
    private bool visible;
    private bool lastAuth;

    // The menu text stays fixed; instead we pan the MenuCity (map/player view) sideways. We apply
    // the pan as an incremental OFFSET on top of whatever position the game owns — never an
    // absolute value — so when the offset is zero we don't touch city.Position at all and the
    // game's own screen transitions are left untouched. appliedOffset tracks what we've added.
    private float panX;
    private float appliedOffset;

    private class Field
    {
        public TextField Tf;
        public string Buffer = "";
        public bool IsPassword;
    }

    public ArchipelagoMenuPanel(HomeScreen home)
    {
        theme  = home.Theme;
        menu   = F_Menu.GetValue(home) as Menu;
        frame  = home.GlobalFrame; 
        locale = LocaleDatabase.Instance.CurrentLocale;

        CustomStrings.EnsureInjected();
        BuildMenuEntry(home);
        BuildPanels();

        lastAuth = ArchipelagoClient.Authenticated;
        Refresh();
    }

    private void BuildMenuEntry(HomeScreen home)
    {
        // Mobile / exhibition layouts may not have the desktop button panel — bail gracefully.
        if (F_ButtonPanel.GetValue(home) is not Panel buttonPanel) return;
        if (F_Buttons.GetValue(home) is not List<LabelButton> buttons) return;

        Font font = buttonPanel.GetButtonFont(ButtonFlags.XLARGE);
        // Use the custom logo if present; otherwise fall back to the vanilla arrow so the menu
        // never breaks when the asset is absent.
        bool hasLogo = MenuAssets.EnsureLoaded(MenuAssets.ApLogo, "ap_menu_logo.png");
        Icon icon = hasLogo ? null : new Icon(theme, CardinalDirection.EAST, UI.Constants.BLACK, -1f, 0);
        entryButton = new LabelButton(theme, CustomStrings.Archipelago, font,
            UI.Constants.PERSISTENT_WHITE, UI.Constants.BLUE, icon, 0f, 0.5f, buttonPanel.ButtonContext, 0);

        buttonPanel.Body.AddChild(entryButton);
        entryButton.SignalRelease += OnEntryClicked;
        if (hasLogo) AttachMenuLogo(entryButton);

        // Slot it just below Play (index 0). Re-running the screen's own layout pass spaces the
        // whole column evenly, including our new entry.
        int insertAt = Mathf.Min(1, buttons.Count);
        buttons.Insert(insertAt, entryButton);
        M_Reposition?.Invoke(home, null);
    }

    /// <summary>Place the custom logo sprite just left of the button's pill, where the arrow sits
    /// on the other menu entries. It's a child of the button, so it moves/positions with it.</summary>
    private void AttachMenuLogo(LabelButton button)
    {
        Rect dim = button.Dimensions;
        float size = dim.height * 0.95f;
        var sprite = new FSprite(MenuAssets.ApLogo, Main.Instance.Scene.MenuDetailPartitionId, 1)
        {
            width = size,
            height = size,
            x = dim.x - size * 0.5f - 6f,        // just left of the pill
            y = dim.y + dim.height * 0.5f,        // vertically centred on the button
        };
        button.AddChild(sprite);
    }

    private void BuildPanels()
    {
        float ax = Main.Instance.AspectScale;
        panX = Futile.screen.halfWidth * MenuPanFrac / ax;
        float px = Futile.screen.halfWidth * PanelRightFrac / ax;
        var pos = new Vector2(px, 0f);
        formPanel = new Panel(theme, PanelWidth);
        formPanel.AddWhitespace(18f);
        BuildHeader(formPanel);
        formPanel.AddWhitespace(14f);
        AddField(formPanel, "Host",        UI.Constants.ERROR_RED,         Icon.Type.GLOBE,       0, false);
        AddField(formPanel, "Player Name", UI.Constants.ACHIEVEMENT_YELLOW, Icon.Type.PERSON_HEAD, 1, false);
        AddField(formPanel, "Password",    UI.Constants.GREEN,             Icon.Type.PADLOCK,     2, true);
        formPanel.AddWhitespace(18f);
        var connectIcon = new Icon(theme, CardinalDirection.EAST, UI.Constants.PERSISTENT_WHITE, -1f, 1);
        var connect = formPanel.AddButton(CustomStrings.Connect, ButtonFlags.MEDIUM, connectIcon, OnConnectClicked,
            0.5f, KeyCode.Return, 1);
        connect.MinWidth = FieldTextW + 2f * FieldPad; // span the box width like the mockup
        connect.x = FieldCenterX;                       // AddButton pins x=0; centre under the boxes
        formPanel.Build();
        frame.AddPanel(formPanel, pos);
        AddBackButton(formPanel);
        statusPanel = new Panel(theme, PanelWidth);
        statusPanel.AddWhitespace(18f);
        BuildHeader(statusPanel);
        statusPanel.AddWhitespace(14f);
        statusLine1 = statusPanel.AddParagraph(new LocalizedString(locale, "Connected"),
            FontDatabase.Helvetica22, TextJustification.Center, 4f, 2f);
        statusLine1.anchorX = 0.5f; statusLine1.x = PanelWidth * 0.5f; statusLine1.color = UI.Constants.GREEN;
        statusLine2 = statusPanel.AddParagraph(new LocalizedString(locale, ""),
            FontDatabase.Helvetica18, TextJustification.Center, 2f, 8f);
        statusLine2.anchorX = 0.5f; statusLine2.x = PanelWidth * 0.5f;
        statusPanel.AddWhitespace(18f);
        var disconnectIcon = new Icon(theme, CardinalDirection.SOUTH, UI.Constants.PERSISTENT_WHITE, -1f, 1);
        var disconnect = statusPanel.AddButton(CustomStrings.Disconnect, ButtonFlags.MEDIUM, disconnectIcon,
            OnDisconnectClicked, 0.5f, KeyCode.None, 1);
        disconnect.MinWidth = FieldTextW + 2f * FieldPad;
        disconnect.x = FieldCenterX;
        statusPanel.Build();
        frame.AddPanel(statusPanel, pos);
        AddBackButton(statusPanel);
    }

    private void BuildHeader(Panel p)
    {
        var title = p.AddTitle(new LocalizedString(locale, "Archipelago"), FontDatabase.Helvetica45Bold,
            TextHeight.LINE, 6f, 2f);
        title.anchorX = 0.5f; title.x = FieldCenterX;
        var sub = p.AddParagraph(new LocalizedString(locale, "Multiworld Connection"), FontDatabase.Helvetica22,
            TextJustification.DontBreak, 0f, 6f);
        sub.anchorX = 0.5f; sub.x = FieldCenterX; sub.color = UI.Constants.BLUE;
    }

    private void AddField(Panel p, string label, CircadianColor circleColor, Icon.Type glyph, int idx, bool isPw)
    {
        var lbl = p.AddParagraph(new LocalizedString(locale, label), FontDatabase.Helvetica18,
            TextJustification.DontBreak, 10f, 2f);
        lbl.x = IconGutter;
        float fh = FontDatabase.Helvetica22.GetFFont(locale).lineHeight + 2f * FieldPad;
        float centerY = -(p.Cursor + fh * 0.5f);
        var tf = new TextField(theme, menu, FontDatabase.Helvetica22, FieldTextW, FieldPad, MakeOpenDelegate(idx),
            StringId.NONE, 1);
        p.Body.AddChild(tf);
        tf.SetPosition(FieldCenterX, centerY);
        FContainer fieldIcon = MakeFieldIcon(circleColor, glyph, fh * 0.95f);
        p.Body.AddChild(fieldIcon);
        fieldIcon.SetPosition(IconGutter * 0.5f, centerY);
        p.Cursor += fh + 14f;
        var f = new Field { Tf = tf, IsPassword = isPw };
        string seed = idx switch
        {
            0 => string.IsNullOrEmpty(ArchipelagoClient.ServerData.Uri) ? DefaultHost : ArchipelagoClient.ServerData.Uri,
            1 => ArchipelagoClient.ServerData.SlotName,
            _ => ArchipelagoClient.ServerData.Password,
        };
        f.Buffer = seed ?? "";
        fields[idx] = f;
        tf.SetValue(Display(f));
    }

    /// <summary>A coloured circle with a white glyph centred on it (Host/Name/Password icons).</summary>
    private FContainer MakeFieldIcon(CircadianColor circleColor, Icon.Type glyph, float size)
    {
        var c = new FContainer();
        var bg = new FInstancedGeoSprite(GeoFactory.Instance.UnitCircleSmall, Main.Instance.Scene.MenuDetailPartitionId, 1)
        {
            color = circleColor.GetColor(theme),
            scale = size * 0.5f,
        };
        c.AddChild(bg);
        c.AddChild(new Icon(theme, glyph, UI.Constants.PERSISTENT_WHITE, false, size * 0.6f, 1));
        return c;
    }

    public void Tick()
    {
        if (menu != null && menu.Option != MenuOption.Home)
        {
            if (visible) { visible = false; Refresh(); }
            return;
        }
        UpdateCityPan();
        bool auth = ArchipelagoClient.Authenticated;
        if (auth != lastAuth)
        {
            lastAuth = auth;
            focusedIndex = -1;
            Refresh();
        }
        if (visible && !auth && focusedIndex >= 0)
            HandleInput();
        if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            focusedIndex = -1;
    }

    private void HandleInput()
    {
        Field f = fields[focusedIndex];
        if (f == null) return;
        string typed = UnityEngine.Input.inputString;
        if (string.IsNullOrEmpty(typed)) return;
        bool changed = false;
        foreach (char c in typed)
        {
            if (c == '\b')
            {
                if (f.Buffer.Length > 0) { f.Buffer = f.Buffer.Substring(0, f.Buffer.Length - 1); changed = true; }
            }
            else if (c == '\n' || c == '\r')
            {
                focusedIndex = (focusedIndex + 1) % fields.Length; // Enter/Tab → next field
                return;
            }
            else if (!char.IsControl(c))
            {
                f.Buffer += c;
                changed = true;
            }
        }
        if (changed)
        {
            f.Tf.SetValue(Display(f));
            WriteServerData();
        }
    }

    private static string Display(Field f) => f.IsPassword ? new string('*', f.Buffer.Length) : f.Buffer;

    private void WriteServerData()
    {
        ArchipelagoClient.ServerData.Uri      = fields[0].Buffer;
        ArchipelagoClient.ServerData.SlotName = fields[1].Buffer;
        ArchipelagoClient.ServerData.Password = fields[2].Buffer;
    }

    private Button.TouchEventDelegate MakeOpenDelegate(int idx) => (_, _) => focusedIndex = idx;

    private void OnEntryClicked(Button button, FTouch touch)
    {
        visible = !visible;
        focusedIndex = -1;
        Refresh();
    }

    private void AddBackButton(Panel p)
    {
        var arrow = new Icon(theme, CardinalDirection.WEST, UI.Constants.BLUE, 28f, 1);
        var back = new IconButton(arrow, 10f, p.ButtonContext);
        back.SignalRelease += OnBackClicked;
        p.AddElement(back, new Vector2(18f, -24f));
    }

    private void OnBackClicked(Button button, FTouch touch)
    {
        if (!visible) return;
        visible = false;
        focusedIndex = -1;
        Refresh();
    }

    /// <summary>
    /// Pan only the MenuCity (map/player view) by an incremental offset — the menu text is
    /// untouched. We ease <see cref="appliedOffset"/> toward the target and apply only the delta
    /// to <c>city.Position</c>, so once settled (offset 0 when closed) we stop touching it and the
    /// game's own screen transitions run uncorrupted.
    /// </summary>
    private void UpdateCityPan()
    {
        if (menu?.City == null) return;

        float desired = visible ? panX : 0f;
        float t = 1f - Mathf.Exp(-12f * UnityEngine.Time.deltaTime);
        float newOffset = Mathf.Lerp(appliedOffset, desired, t);
        if (Mathf.Abs(desired - newOffset) < 0.05f) newOffset = desired; // snap when basically there

        float delta = newOffset - appliedOffset;
        if (delta != 0f)
        {
            var p = menu.City.Position;
            menu.City.Position = new Vector2(p.x + delta, p.y);
            appliedOffset = newOffset;
        }
    }

    /// <summary>
    /// Remove any pan offset we've added, restoring the game's own baseline. Called before every
    /// screen transition (via the <c>Menu.SetOption</c> prefix) so transitions never inherit our
    /// displacement and land off-centre.
    /// </summary>
    public void ResetCityPan()
    {
        if (appliedOffset != 0f && menu?.City != null)
        {
            var p = menu.City.Position;
            menu.City.Position = new Vector2(p.x - appliedOffset, p.y);
        }
        appliedOffset = 0f;
        if (visible) { visible = false; Refresh(); }
    }

    private void OnConnectClicked(Button button, FTouch touch)
    {
        focusedIndex = -1;
        WriteServerData();
        if (!string.IsNullOrWhiteSpace(fields[1].Buffer))
            Plugin.ArchipelagoClient.Connect();
    }

    private void OnDisconnectClicked(Button button, FTouch touch)
    {
        Plugin.ArchipelagoClient.DisconnectFromServer();
    }

    private void Refresh()
    {
        bool auth = ArchipelagoClient.Authenticated;
        if (formPanel != null)   formPanel.isVisible   = visible && !auth;
        if (statusPanel != null) statusPanel.isVisible = visible && auth;

        if (auth && statusLine1 != null)
        {
            statusLine1.SetText(new LocalizedString(locale, "Connected as: " + (ArchipelagoClient.ServerData.SlotName ?? "")));
            statusLine2.SetText(new LocalizedString(locale, "Server: " + (ArchipelagoClient.ServerData.Uri ?? "")));
        }
    }

    public void HandleThemeChanged(Theme newTheme)
    {
        formPanel?.HandleThemeChanged(newTheme);
        statusPanel?.HandleThemeChanged(newTheme);
    }
}
