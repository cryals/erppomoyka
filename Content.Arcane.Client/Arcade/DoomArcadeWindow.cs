using Content.Client.Options.UI;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.WebView;
using Robust.Shared.Asynchronous;
using Robust.Shared.ContentPack;
using Robust.Shared.Timing;

namespace Content.Arcane.Client.Arcade;

/// <summary>Native window chrome and a local-origin browser for the arcade game.</summary>
public sealed class DoomArcadeWindow : DefaultWindow
{
    private static readonly IReadOnlyDictionary<string, string> Assets = new Dictionary<string, string>
    {
        ["/assets/doom/websockets-doom.js"] = "/Doom/_Arcane/websockets-doom.js",
        ["/assets/doom/websockets-doom.wasm"] = "/Doom/_Arcane/websockets-doom.wasm",
        ["/assets/doom/doom1.wad"] = "/Doom/_Arcane/doom1.wad",
        ["/assets/doom/default.cfg"] = "/Doom/_Arcane/default.cfg",
    };

    private readonly ITaskManager _tasks = IoCManager.Resolve<ITaskManager>();
    private readonly LayoutContainer _host = new();
    private readonly Label _fallback;
    private readonly Button _retry;
    private WebViewControl? _browser;
    private WebUiBridge? _bridge;
    private bool _updateQueued;
    private bool _failed;
    private bool _ready;
    private bool _scaleChanged;
    private float _loadTime;
    private string? _game;
    private FireRedSaveStore? _fireRedSaveStore;
    private Vector2 _contentSize = new(840, 700);

    public void ConfigureGame(string game)
    {
        if (game is not ("asteroids" or "flappy_bird" or "pacman" or "prince_of_persia" or "snake_game" or "space_Invaders" or "nu_pogodi" or "firered"))
            throw new ArgumentOutOfRangeException(nameof(game));
        _game = game;
        Title = Loc.GetString("arcane-arcade-" + game + "-title");
        _contentSize = game switch
        {
            "nu_pogodi" => new Vector2(936, 607),
            "firered" => new Vector2(752, 590),
            "flappy_bird" => new Vector2(400, 650),
            "pacman" => new Vector2(520, 720),
            "prince_of_persia" => new Vector2(840, 640),
            "asteroids" => new Vector2(840, 660),
            _ => new Vector2(800, 660),
        };
        FitWindow();
    }

    public DoomArcadeWindow()
    {
        Title = Loc.GetString("arcane-doom-arcade-title");
        MinSize = new Vector2(100, 100);
        Resizable = false;
        Contents.Margin = new Thickness(0);
        SetSize = _contentSize + new Vector2(0, 25);
        Contents.AddChild(_host);
        _host.HorizontalExpand = true;
        _host.VerticalExpand = true;

        var fallbackPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 10,
        };
        _fallback = new Label { Text = Loc.GetString("arcane-doom-arcade-loading") };
        _retry = new Button { Text = Loc.GetString("arcane-doom-arcade-retry"), Visible = false, MinHeight = 40 };
        _retry.OnPressed += _ => ResetBrowser();
        fallbackPanel.AddChild(_fallback);
        fallbackPanel.AddChild(_retry);
        _host.AddChild(fallbackPanel);
        LayoutContainer.SetAnchorPreset(fallbackPanel, LayoutContainer.LayoutPreset.Wide);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!IsOpen || Disposed) return;
        if (_bridge != null && !_ready && !_failed) _loadTime += args.DeltaSeconds;
        if (_updateQueued) return;
        _updateQueued = true;
        // Bridge actions can close the BUI or replace its browser. Never mutate
        // the control tree during DoFrameUpdateRecursive's child enumeration.
        _tasks.RunOnMainThread(UpdateBrowser);
    }

    private void UpdateBrowser()
    {
        _updateQueued = false;
        if (Disposed || !IsOpen || !IsInsideTree) return;
        FitWindow();
        if (_scaleChanged)
        {
            _scaleChanged = false;
            ResetBrowser();
        }
        if (_browser == null && !_failed) CreateBrowser();
        var bridge = _bridge;
        if (bridge == null) return;
        for (var i = 0; i < 16 && !Disposed && IsOpen && _bridge == bridge &&
             bridge.TryDequeue(out var request); i++)
            HandleRequest(request);
        if (!Disposed && IsOpen && _bridge == bridge && !_ready && !_failed && _loadTime > 15)
            FailBrowser();
    }

    private void CreateBrowser()
    {
        try
        {
            _fireRedSaveStore = _game == "firered"
                ? new FireRedSaveStore(IoCManager.Resolve<IResourceManager>())
                : null;
            _loadTime = 0;
            _ready = false;
            _failed = false;
            _fallback.Text = Loc.GetString("arcane-doom-arcade-loading");
            _retry.Visible = false;
            _browser = new WebViewControl { HorizontalExpand = true, VerticalExpand = true };
            _bridge = _game == null
                ? new WebUiBridge(_browser, "doom-arcade", Assets)
                : new WebUiBridge(_browser, "arcade", game: _game);
            _host.AddChild(_browser);
            LayoutContainer.SetAnchorPreset(_browser, LayoutContainer.LayoutPreset.Wide);
        }
        catch (Exception e)
        {
            Logger.Error($"DOOM arcade browser failed: {e}");
            FailBrowser();
        }
    }

    private void FitWindow()
    {
        if (Parent == null || Parent.Size.X <= 0 || Parent.Size.Y <= 0) return;
        // Measure native chrome separately: 936x607 refers to the game surface,
        // not the title bar. Work in logical UI units, without applying UI scale twice.
        var chrome = Contents.Size.Y > 0 ? Vector2.Max(Size - Contents.Size, Vector2.Zero) : new Vector2(0, 25);
        var available = Vector2.Max(Parent.Size - chrome - new Vector2(32), Vector2.One);
        var scale = MathF.Min(1, MathF.Min(available.X / _contentSize.X, available.Y / _contentSize.Y));
        var size = _contentSize * scale + chrome;
        if (Vector2.DistanceSquared(SetSize, size) > 0.01f) SetSize = size;
    }

    private void HandleRequest(WebUiRequest request)
    {
        var bridge = _bridge;
        if (bridge == null || !bridge.TryReassemble(request, out request)) return;
        if (_game == "firered" && request.Event == "firered-save-load")
        {
            if (WebUiBridge.PropertyCount(request.Data) != 0)
            {
                bridge.SendError(request.RequestId, "invalid-payload");
                return;
            }

            try
            {
                var save = _fireRedSaveStore?.Load();
                if (save == null)
                {
                    Logger.Info("FireRed save: no local save found; starting a new game.");
                    bridge.SendSuccess(request.RequestId, "{\"save\":null}");
                    return;
                }

                Logger.Info($"FireRed save: loaded local SRAM ({save.Length} bytes).");
                bridge.SendSuccess(request.RequestId, "{\"save\":" + WebUiBridge.Quote(Convert.ToBase64String(save)) + "}");
            }
            catch (Exception e)
            {
                Logger.Error($"FireRed save: failed to load local SRAM: {e}");
                bridge.SendError(request.RequestId, "load-failed");
            }
            return;
        }

        if (_game == "firered" && request.Event == "firered-save-write")
        {
            if (WebUiBridge.PropertyCount(request.Data) != 1 ||
                !WebUiBridge.TryGetString(request.Data, "save", out var encodedSave) ||
                encodedSave.Length is 0 or > (FireRedSaveStore.MaxSaveBytes * 4 / 3 + 4))
            {
                bridge.SendError(request.RequestId, "invalid-payload");
                return;
            }

            try
            {
                var store = _fireRedSaveStore ?? throw new InvalidOperationException("Save store is not initialized.");
                var save = Convert.FromBase64String(encodedSave);
                store.Save(save);
                Logger.Info($"FireRed save: local SRAM saved ({save.Length} bytes).");
                bridge.SendSuccess(request.RequestId, "{\"success\":true}");
            }
            catch (Exception e)
            {
                Logger.Error($"FireRed save: failed to persist local SRAM: {e}");
                bridge.SendError(request.RequestId, "save-failed");
            }
            return;
        }

        if (request.Event == "doom-runtime-error")
        {
            if (WebUiBridge.PropertyCount(request.Data) != 1 ||
                !WebUiBridge.TryGetString(request.Data, "message", out var message) || message.Length > 1000)
            {
                bridge.SendError(request.RequestId, "invalid-payload");
                return;
            }
            Logger.Warning($"DOOM arcade runtime: {message.Replace('\r', ' ').Replace('\n', ' ')}");
            bridge.SendSuccess(request.RequestId, "{}");
            return;
        }
        if (_game == "firered" && request.Event == "firered-save-load-status")
        {
            if (WebUiBridge.PropertyCount(request.Data) != 2 ||
                !WebUiBridge.TryGetString(request.Data, "loaded", out var loadedText) ||
                !WebUiBridge.TryGetString(request.Data, "bytes", out var bytesText) ||
                !int.TryParse(bytesText, out var bytes) || bytes is < 0 or > FireRedSaveStore.MaxSaveBytes ||
                loadedText is not ("true" or "false") || (loadedText == "true") != (bytes > 0))
            {
                bridge.SendError(request.RequestId, "invalid-payload");
                return;
            }

            if (loadedText == "true")
                Logger.Info($"FireRed save: emulator applied local SRAM ({bytes} bytes).");
            else
                Logger.Info("FireRed save: no existing SRAM; emulator started a new game.");
            bridge.SendSuccess(request.RequestId, "{}");
            return;
        }
        if (WebUiBridge.PropertyCount(request.Data) != 0)
        {
            bridge.SendError(request.RequestId, "invalid-payload");
            return;
        }
        switch (request.Event)
        {
            case "page-ready":
                bridge.SendSuccess(request.RequestId, "{}");
                break;
            case "doom-get-state":
                bridge.SendSuccess(request.RequestId, BuildState());
                _ready = true;
                _fallback.Visible = false;
                _browser?.GrabKeyboardFocus();
                break;
            case "doom-close":
                bridge.SendSuccess(request.RequestId, "{}");
                Close();
                break;
            default:
                bridge.SendError(request.RequestId, "unknown-action");
                break;
        }
    }

    private string BuildState()
    {
        static string Label(string key) => WebUiBridge.Quote(Loc.GetString(key));
        var gameState = _game == null ? "" : "\"game\":" + WebUiBridge.Quote(_game) +
            ",\"title\":" + Label("arcane-arcade-" + _game + "-title") + ",";
        return "{" + gameState + "\"start\":" + Label("arcane-doom-arcade-start") +
            ",\"close\":" + Label("arcane-doom-arcade-close") +
            ",\"loading\":" + Label("arcane-doom-arcade-loading") +
            ",\"ready\":" + Label("arcane-doom-arcade-ready") +
            ",\"failed\":" + Label(_game == null ? "arcane-doom-arcade-failed" : "arcane-arcade-failed") +
            ",\"retry\":" + Label("arcane-doom-arcade-retry") +
            ",\"controls\":" + Label(_game == null ? "arcane-doom-arcade-controls" : "arcane-arcade-" + _game + "-controls") +
            ",\"audio\":" + Label("arcane-doom-arcade-audio") +
            ",\"muted\":" + Label("arcane-doom-arcade-muted") + "}";
    }

    private void ResetBrowser()
    {
        _bridge?.Dispose();
        _bridge = null;
        if (_browser?.Parent != null) _browser.Parent.RemoveChild(_browser);
        _browser = null;
        _failed = false;
        _ready = false;
        _fallback.Visible = true;
        _fallback.Text = Loc.GetString("arcane-doom-arcade-loading");
        _retry.Visible = false;
    }

    private void FailBrowser()
    {
        ResetBrowser();
        _failed = true;
        _fallback.Text = Loc.GetString(_game == null ? "arcane-doom-arcade-failed" : "arcane-arcade-failed");
        _retry.Visible = true;
        _retry.GrabKeyboardFocus();
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        if (!_ready || _browser == null) return;
        // Recreate at the new scale outside tree traversal, as in the lobby host.
        _browser.Visible = false;
        _scaleChanged = true;
    }

    protected override void ExitedTree()
    {
        _bridge?.Dispose();
        _bridge = null;
        base.ExitedTree();
    }

    [Obsolete]
    protected override void Dispose(bool disposing)
    {
        if (disposing) _bridge?.Dispose();
        base.Dispose(disposing);
    }
}
