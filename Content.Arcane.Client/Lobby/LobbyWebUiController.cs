using System.Globalization;
using System.Linq;
using Content.Client.Changelog;
using Content.Client.Credits;
using Content.Client.GameTicking;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Options.UI;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.Bwoink;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.Shared.CCVar;
using Content.Shared.GameTicking.Prototypes;
using Robust.Client.Console;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.WebView;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Arcane.Client.Lobby;

/// <summary>Web lobby presentation; existing controllers remain responsible for game actions.</summary>
public sealed partial class LobbyWebUiController : UIController, IOnStateEntered<LobbyState>, IOnStateExited<LobbyState>
{
    [Dependency] private ITaskManager _tasks = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IClientConsoleHost _console = default!;
    [Dependency] private IUriOpener _uris = default!;
    [Dependency] private ChangelogManager _changelog = default!;

    private LobbyState? _state;
    private LobbyGui? _gui;
    private LobbySurface? _surface;
    private Button? _recovery;
    private readonly List<NativeSlot> _slots = new();
    private Vector2 _previewScale;
    private bool _previewPlaced;
    private bool _queued;
    private bool _ready;
    private bool _hidden;
    private bool _infoCollapsed;
    private bool _failed;
    private float _elapsed;
    private float _refresh;
    private int _revision;
    private string? _lastState;

    private sealed record ActionEntry(string Id, string Label, string Icon, bool Enabled, Action Execute,
        string Group = "main", bool Unread = false, bool Danger = false, string? Tooltip = null);

    // Preserve the original tree and sizing so fallback/editor restoration is lossless.
    private sealed record NativeSlot(Control Control, Control Parent, int Index, Vector2 Size,
        Vector2 MaxSize, Thickness Margin, Control.HAlignment Horizontal, Control.VAlignment Vertical);

    public void OnStateEntered(LobbyState state)
    {
        _state = state;
        _gui = state.Lobby ?? UIManager.ActiveScreen as LobbyGui;
        _failed = false;
        _hidden = false;
        _infoCollapsed = false;
        _revision = 0;
        _chatLimit = 200;
        _chatDraft = _gui?.Chat.ChatInput.Input.Text ?? "";
        _changelog.NewChangelogEntriesChanged += Invalidate;
        if (_gui != null) _gui.PresentationChanged += OnPresentationChanged;
    }

    public void OnStateExited(LobbyState state)
    {
        _changelog.NewChangelogEntriesChanged -= Invalidate;
        if (_gui != null) _gui.PresentationChanged -= OnPresentationChanged;
        RemoveSurface(false);
        _recovery?.Dispose();
        _recovery = null;
        _gui = null;
        _state = null;
        _hidden = false;
    }

    private void Invalidate() => _lastState = null;

    private void OnPresentationChanged()
    {
        if (_gui?.IsEditingCharacter == true) RemoveSurface();
        Invalidate();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_gui == null || _queued) return;
        var gui = _gui;
        _queued = true;
        _tasks.RunOnMainThread(() =>
        {
            _queued = false;
            if (_gui != gui || gui.Disposed || !gui.IsInsideTree) return;
            try { UpdateLobby(args.DeltaSeconds); }
            catch (Exception e)
            {
                Log.Error($"Lobby WebUI failed: {e}");
                Fail();
            }
        });
    }

    private void UpdateLobby(float seconds)
    {
        if (_gui == null || _gui.IsEditingCharacter || _failed) return;
        if (_surface?.NeedsReset == true) RemoveSurface();
        if (_surface == null) CreateSurface();
        if (_surface == null) return;
        _elapsed += seconds;
        var bridge = _surface.Bridge;
        // Bound work per update. Navigation/lifecycle can invalidate the surface during dispatch.
        for (var i = 0; i < 32 && _surface?.Bridge == bridge && bridge.TryDequeue(out var request); i++)
            HandleRequest(bridge, request);
        if (_surface == null) return;
        if (!_ready && _elapsed > 10) { Fail(); return; }
        if (!_ready) return;
        ApplyLayout();
        _refresh += seconds;
        if (_refresh < .25f) return;
        _refresh = 0;
        var state = BuildState();
        if (state == _lastState) return;
        _lastState = state;
        bridge.PushEvent("lobby-state", WithRevision(state));
    }

    private void CreateSurface()
    {
        var assets = new Dictionary<string, string> { ["/assets/lobby/logo"] = "/Textures/Logo/icon/icon-256x256.png" };
        foreach (var proto in _prototypes.EnumeratePrototypes<LobbyBackgroundPrototype>())
        {
            assets[AssetUrl(proto.ID)] = proto.Background.ToString();
            if (proto.Poster is { } poster) assets[AssetUrl(proto.ID, "poster")] = poster.ToString();
            if (proto.License is { } license) assets[AssetUrl(proto.ID, "license")] = license.ToString();
        }
        _surface = new LobbySurface(assets);
        _gui!.AddChild(_surface);
        LayoutContainer.SetAnchorPreset(_surface, LayoutContainer.LayoutPreset.Wide);
        _elapsed = 0;
        _ready = false;
        _previewPlaced = false;
        _lastState = null;
        _recovery?.Dispose();
        _recovery = null;
    }

    private static string AssetUrl(string id, string kind = "image") => "/assets/lobby/" + Uri.EscapeDataString(id) + "/" + kind;

    private void AttachNative()
    {
        if (_gui == null || _surface == null || _slots.Count > 0) return;
        _previewScale = _gui.CharacterPreview.ProfilePreviewSpriteView.Scale;
        MoveNative(_gui.CharacterPreview.ProfilePreviewSpriteView, _surface);
        _gui.CharacterPreview.ProfilePreviewSpriteView.Scale = new Vector2(2);
        UIManager.GetUIController<LobbyUIController>().RefreshLobbyPreview();
        MoveNative(_gui.VoteContainer, _surface);
        _gui.Chat.FocusOverride = FocusWebChat;
        _gui.SetNativePresentationVisible(false);
        ApplyLayout();
        if (_hidden) SetVisible(false);
    }

    private void MoveNative(Control control, Control target)
    {
        var parent = control.Parent!;
        _slots.Add(new NativeSlot(control, parent, control.GetPositionInParent(), control.SetSize,
            control.MaxSize, control.Margin, control.HorizontalAlignment, control.VerticalAlignment));
        parent.RemoveChild(control);
        control.Margin = new Thickness(0);
        control.HorizontalAlignment = Control.HAlignment.Stretch;
        control.VerticalAlignment = Control.VAlignment.Stretch;
        control.MaxSize = new Vector2(float.PositiveInfinity);
        target.AddChild(control);
    }

    private void RemoveSurface(bool refreshPreview = true)
    {
        var hadPreview = _slots.Count > 0;
        if (_gui != null) _gui.Chat.FocusOverride = null;
        foreach (var slot in _slots.AsEnumerable().Reverse())
        {
            slot.Control.Parent?.RemoveChild(slot.Control);
            slot.Control.SetSize = slot.Size;
            slot.Control.MaxSize = slot.MaxSize;
            slot.Control.Margin = slot.Margin;
            slot.Control.HorizontalAlignment = slot.Horizontal;
            slot.Control.VerticalAlignment = slot.Vertical;
            slot.Control.Visible = true;
            LayoutContainer.SetPosition(slot.Control, Vector2.Zero);
            slot.Parent.AddChild(slot.Control);
            slot.Control.SetPositionInParent(Math.Min(slot.Index, slot.Parent.ChildCount - 1));
        }
        _slots.Clear();
        if (hadPreview && _gui != null)
        {
            _gui.CharacterPreview.ProfilePreviewSpriteView.Scale = _previewScale;
            if (refreshPreview) UIManager.GetUIController<LobbyUIController>().RefreshLobbyPreview();
        }
        _surface?.Dispose();
        _surface = null;
        _ready = false;
        _gui?.SetNativePresentationVisible(true);
    }

    private float RightWidth => MathF.Min(MathF.Max(300, _gui!.Width * .32f), MathF.Min(640, _gui.Width * .5f));
    private float InfoHeight => _infoCollapsed ? 0 : MathF.Min(180, MathF.Max(80, _gui!.Height * .25f));

    private static void Place(Control control, float x, float y, float width, float height)
    {
        LayoutContainer.SetPosition(control, new Vector2(x, y));
        control.SetSize = new Vector2(MathF.Max(1, width), MathF.Max(1, height));
    }

    private void ApplyLayout()
    {
        if (_gui == null || _surface == null || !_ready) return;
        var left = _gui.Width - RightWidth;
        _gui.VoteContainer.Visible = !_hidden;
        Place(_gui.VoteContainer, 16, 16, MathF.Min(340, left - 280), 200);
        _gui.CharacterPreview.ProfilePreviewSpriteView.Visible = !_hidden && _previewPlaced && _gui.CharacterPreview.PreferencesLoaded;
    }

    private void SetVisible(bool visible)
    {
        _hidden = !visible;
        Invalidate();
        ApplyLayout();
        _recovery?.Dispose();
        _recovery = null;
        if (!visible && _gui != null)
        {
            // Native recovery remains available even if the browser stops responding.
            _recovery = new Button { Text = Loc.GetString("arcane-lobby-show-ui"), CanKeyboardFocus = true,
                MinHeight = 40, HorizontalAlignment = Control.HAlignment.Left, VerticalAlignment = Control.VAlignment.Bottom, Margin = new Thickness(16) };
            _recovery.OnPressed += _ => SetVisible(true);
            _gui.AddChild(_recovery);
            _recovery.GrabKeyboardFocus();
        }
    }

    private void Fail()
    {
        RemoveSurface();
        _failed = true;
        _hidden = false;
        _recovery?.Dispose();
        if (_gui == null) return;
        _recovery = new Button { Text = Loc.GetString("arcane-lobby-retry"), CanKeyboardFocus = true,
            HorizontalAlignment = Control.HAlignment.Left, VerticalAlignment = Control.VAlignment.Center, MinHeight = 40 };
        _recovery.OnPressed += _ => { _failed = false; Invalidate(); };
        _gui.AddChild(_recovery);
    }

    private void HandleRequest(WebUiBridge bridge, WebUiRequest request)
    {
        if (!bridge.TryReassemble(request, out request)) return;
        if (bridge.HandleNativeKey(request)) return;
        if (_ready && HandleChatRequest(bridge, request)) return;
        var count = WebUiBridge.PropertyCount(request.Data);
        if (count < 0) { bridge.SendError(request.RequestId, "invalid-payload"); return; }
        switch (request.Event)
        {
            case "page-ready" when count is 0 or 2:
                bridge.SendSuccess(request.RequestId, "{}");
                return;
            case "lobby-get-state" when count == 0:
                bridge.SendSuccess(request.RequestId, WithRevision(BuildState()));
                return;
            case "lobby-ready" when count == 0:
                _ready = true;
                _surface?.MarkReady();
                AttachNative();
                bridge.SendSuccess(request.RequestId, "{}");
                return;
            case "lobby-set-ui-visible" when _ready && count == 1 && WebUiBridge.TryGetString(request.Data, "visible", out var visible) && visible is "0" or "1":
                SetVisible(visible == "1");
                bridge.SendSuccess(request.RequestId, "{}");
                return;
            case "lobby-layout" when _ready && count == 4:
                var values = new int[4];
                var keys = new[] { "x", "y", "width", "height" };
                for (var i = 0; i < 4; i++)
                    if (!WebUiBridge.TryGetString(request.Data, keys[i], out var text) || !int.TryParse(text, out values[i]) || values[i] < 0)
                    { bridge.SendError(request.RequestId, "invalid-payload"); return; }
                if (values[2] is < 1 or > 160 || values[3] is < 1 or > 160 || values[0] + values[2] > _gui!.Width || values[1] + values[3] > _gui.Height)
                { bridge.SendError(request.RequestId, "invalid-payload"); return; }
                _gui.CharacterPreview.ProfilePreviewSpriteView.MaxSize = new Vector2(values[2], values[3]);
                Place(_gui.CharacterPreview.ProfilePreviewSpriteView, values[0], values[1], values[2], values[3]);
                _previewPlaced = true;
                bridge.SendSuccess(request.RequestId, "{}");
                return;
            case "lobby-action" when _ready && !_hidden && count == 1 && WebUiBridge.TryGetString(request.Data, "action", out var id):
                var action = Actions().FirstOrDefault(a => a.Id == id);
                if (action == null || !action.Enabled) { bridge.SendError(request.RequestId, "action-unavailable"); return; }
                try
                {
                    action.Execute();
                    bridge.SendSuccess(request.RequestId, "{}");
                    Invalidate();
                }
                catch (Exception e)
                {
                    Log.Error($"Lobby action {id} failed: {e}");
                    bridge.SendError(request.RequestId, "action-failed");
                }
                return;
            default:
                bridge.SendError(request.RequestId, "invalid-payload");
                return;
        }
    }

    private List<ActionEntry> Actions()
    {
        var gui = _gui!;
        var ticker = EntityManager.System<ClientGameTicker>();
        var entries = new List<ActionEntry>
        {
            new("ready", gui.ReadyButton.Text ?? "", ticker.IsGameStarted ? "gamepad" : "check", !ticker.IsGameStarted || !ticker.DisallowedLateJoin, () => _state!.ActivateReady()),
            new("observe", gui.ObserveButton.Text ?? "", "ghost", ticker.IsGameStarted && !gui.ObserveButton.Disabled && gui.ObserveButton.Visible, () => new ObserveWarningWindow().OpenCentered()),
            new("character", Loc.GetString("lobby-character-preview-panel-character-setup-button"), "user-pen", gui.CharacterPreview.PreferencesLoaded, () => _state!.OpenCharacterSetup()),
            new("ahelp", Loc.GetString("ui-lobby-ahelp-button"), "headset", true, () => UIManager.GetUIController<AHelpUIController>().ToggleWindow(), "right", gui.AHelpButton.HasStyleClass(StyleClass.Negative)),
            new("vote", Loc.GetString("ui-vote-menu-button"), "check-to-slot", !gui.VoteButton.Disabled, gui.VoteButton.ToggleMenu, "right", Tooltip: gui.VoteButton.Disabled ? Loc.GetString("ui-webui-vote-disabled") : null),
            new("options", Loc.GetString("ui-lobby-options-button"), "gear", true, () => UIManager.GetUIController<OptionsUIController>().ToggleWindow(), "right"),
            new("leave", Loc.GetString("ui-lobby-leave-button"), "right-from-bracket", true, () => _console.ExecuteCommand("disconnect"), "right", Danger: true),
            new("server-info", Loc.GetString("ui-lobby-server-info-block"), "circle-info", true, () => _infoCollapsed = !_infoCollapsed, "info"),
        };
        if (gui.CharacterPreview.CurrencyAvailable && gui.CharacterPreview.PreferencesLoaded)
            entries.Add(new("store", Loc.GetString("gs-balanceui-shop-label"), "coins", true, () => _console.ExecuteCommand("balanceui")));
        if (gui.CharacterPreview.PatronPerks.Visible)
            entries.Add(new("patron", Loc.GetString("rmc-ui-patron-perks"), "crown", true, () => _state!.OnTogglePatronPerksWindow?.Invoke()));
        gui.Links.RefreshLinks();
        foreach (var link in gui.Links.Actions)
            if (link.Button.Visible)
                entries.Add(new(link.Id, link.Button.Text ?? "", link.Icon, !link.Button.Disabled, link.Execute, "links", link.Id == "changelog" && _changelog.NewChangelogEntries));
        if (!string.IsNullOrWhiteSpace(_config.GetCVar(CCVars.InfoLinksBugReport)))
            entries.Add(new("bug-report", Loc.GetString("server-info-report-button"), "bug", true, () => _uris.OpenUri(_config.GetCVar(CCVars.InfoLinksBugReport)), "links"));
        entries.Add(new("credits", Loc.GetString("server-info-credits-button"), "heart", true, () => new CreditsWindow().Open(), "links"));
        return entries;
    }

    private static string Q(string value) => WebUiBridge.Quote(value);
    private static string B(bool value) => value ? "true" : "false";
    private static string N(float value) => value.ToString(CultureInfo.InvariantCulture);
    private string WithRevision(string state) => "{\"revision\":" + (++_revision) + "," + state[1..];

    private string BuildState()
    {
        var gui = _gui!;
        var ticker = EntityManager.System<ClientGameTicker>();
        var background = "null";
        if (_prototypes.TryIndex(ticker.LobbyBackground, out var proto))
            background = "{\"url\":" + Q(AssetUrl(proto.ID)) + ",\"poster\":" + (proto.Poster == null ? "null" : Q(AssetUrl(proto.ID, "poster"))) +
                ",\"license\":" + (proto.License == null ? "null" : Q(AssetUrl(proto.ID, "license"))) + ",\"x\":" + N(Math.Clamp(proto.FocusX, 0, 100)) + ",\"y\":" + N(Math.Clamp(proto.FocusY, 0, 100)) +
                ",\"credit\":" + Q(Loc.GetString("lobby-state-background-text", ("backgroundTitle", Loc.GetString(proto.Title)), ("backgroundArtist", Loc.GetString(proto.Artist)))) + "}";
        var actions = Actions().Select(a => "{\"id\":" + Q(a.Id) + ",\"label\":" + Q(a.Label) + ",\"icon\":" + Q(a.Icon) +
            ",\"group\":" + Q(a.Group) + ",\"enabled\":" + B(a.Enabled) + ",\"unread\":" + B(a.Unread) + ",\"danger\":" + B(a.Danger) + ",\"tooltip\":" + Q(a.Tooltip ?? "") + "}");
        return "{\"version\":1,\"title\":" + Q(gui.ServerName.Text ?? "") + ",\"character\":" + Q(gui.CharacterPreview.SummaryText) +
            ",\"balance\":" + Q(gui.Balance.Text ?? "") + ",\"visible\":" + B(!_hidden) + ",\"rightWidth\":" + N(RightWidth) +
            ",\"infoHeight\":" + N(InfoHeight) + ",\"round\":" + Q(ticker.IsGameStarted ? Loc.GetString("arcane-lobby-round-running") : gui.StartTime.Text ?? "") +
            ",\"roundTitle\":" + Q(Loc.GetString("arcane-lobby-round")) +
            ",\"players\":" + Q(Loc.GetString("arcane-lobby-players", ("count", ticker.TotalPlayerCount))) +
            ",\"lobbyPlayers\":" + Q(Loc.GetString("arcane-lobby-waiting", ("count", ticker.LobbyPlayerCount))) +
            ",\"readyPlayers\":" + Q(ticker.IsGameStarted ? "" : Loc.GetString("arcane-lobby-ready", ("count", ticker.LobbyReadyCount))) +
            ",\"hideLabel\":" + Q(Loc.GetString("arcane-lobby-hide-ui")) + ",\"errorLabel\":" + Q(Loc.GetString("ui-webui-action-failed")) +
            ",\"newLabel\":" + Q(Loc.GetString("ui-webui-new")) + ",\"licenseLabel\":" + Q(Loc.GetString("arcane-lobby-license")) +
            ",\"song\":" + Q(FormattedMessage.RemoveMarkupPermissive(gui.LobbySong.Text ?? "")) +
            ",\"info\":" + RichTextJson(gui.ServerInfo.Markup + "\n" + FormattedMessage.EscapeText(gui.StationTime.Text ?? "") +
                "\n" + gui.LobbyMessageLabel.Text + (gui.PlaytimeComment.Visible ? "\n" + gui.PlaytimeComment.Text : ""), Color.LightGray) +
            ",\"chat\":" + BuildChatState() +
            ",\"background\":" + background + ",\"actions\":[" + string.Join(',', actions) + "]}";
    }
}

internal sealed class LobbySurface : LayoutContainer
{
    public void FocusBrowser() => _browser.GrabKeyboardFocus();
    public readonly WebUiBridge Bridge;
    public bool NeedsReset { get; private set; }
    private readonly WebViewControl _browser;
    private bool _active;

    public void MarkReady() => _active = true;

    public LobbySurface(IReadOnlyDictionary<string, string> assets)
    {
        _browser = new WebViewControl { HorizontalExpand = true, VerticalExpand = true };
        AddChild(_browser);
        SetAnchorPreset(_browser, LayoutPreset.Wide);
        Bridge = new WebUiBridge(_browser, "lobby", assets);
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        // Initial attachment inherits the parent's scale. Only reset an already running surface.
        if (!_active) return;
        _browser.Visible = false;
        NeedsReset = true;
    }

    [Obsolete]
    protected override void Dispose(bool disposing)
    {
        if (disposing) Bridge.Dispose();
        base.Dispose(disposing);
    }
}
