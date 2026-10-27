// Arcane-Start
using System.Text;
using Content.Client.Changelog;
using Content.Client.Voting;
using Content.Client.Voting.UI;
using Content.Shared.CCVar;
using Robust.Shared.Timing;
using Robust.Shared.Asynchronous;

namespace Content.Client.UserInterface.Systems.EscapeMenu;

public sealed partial class EscapeUIController
{
    [Dependency] private ChangelogManager _changelogManager = default!;
    [Dependency] private IVoteManager _voteManager = default!;
    [Dependency] private ITaskManager _taskManager = default!;
    private VoteCallMenu? _voteCallMenu;
    private readonly System.Collections.Concurrent.ConcurrentQueue<bool> _menuStateChanges = new();
    private int _menuRows = 13;
    private int _menuDividers = 3;
    private bool _fallbackRendered;
    private bool _menuUpdateQueued;

    private sealed record MenuAction(string Id, string Label, string Icon, bool Visible = true,
        bool Enabled = true, string? Tooltip = null, string Tone = "normal", string? Badge = null);
    private sealed record MenuGroup(string Id, bool SeparatorBefore, MenuAction[] Actions);
    private sealed record MenuState(string Title, string NewLabel, string PendingLabel, string ErrorLabel, string RetryLabel, MenuGroup[] Groups);

    private void InitializeWebUiMenu()
    {
        _escapeWindow!.NativeActionRequested += OnNativeMenuAction;
        _escapeWindow.OnOpen += MarkStateDirty;
        _cfg.OnValueChanged(CCVars.SeeOwnNotes, OnSeeOwnNotesChanged, true);
        _cfg.OnValueChanged(CCVars.InfoLinksGithub, OnLinkChanged);
        _cfg.OnValueChanged(CCVars.InfoLinksWiki, OnLinkChanged);
        _cfg.OnValueChanged(CCVars.InfoLinksBugReport, OnLinkChanged);
        _changelogManager.NewChangelogEntriesChanged += MarkStateDirty;
        _voteManager.CanCallVoteChanged += OnCanCallVoteChanged;
    }

    private void ShutdownWebUiMenu()
    {
        if (_escapeWindow != null) _escapeWindow.NativeActionRequested -= OnNativeMenuAction;
        if (_escapeWindow != null) _escapeWindow.OnOpen -= MarkStateDirty;
        _fallbackRendered = false;
        _cfg.UnsubValueChanged(CCVars.SeeOwnNotes, OnSeeOwnNotesChanged);
        _cfg.UnsubValueChanged(CCVars.InfoLinksGithub, OnLinkChanged);
        _cfg.UnsubValueChanged(CCVars.InfoLinksWiki, OnLinkChanged);
        _cfg.UnsubValueChanged(CCVars.InfoLinksBugReport, OnLinkChanged);
        _changelogManager.NewChangelogEntriesChanged -= MarkStateDirty;
        _voteManager.CanCallVoteChanged -= OnCanCallVoteChanged;
        _voteCallMenu?.Close();
        _voteCallMenu = null;
    }

    private void OnSeeOwnNotesChanged(bool _) => MarkStateDirty();
    private void OnLinkChanged(string _) => MarkStateDirty();
    private void OnCanCallVoteChanged(bool _) => MarkStateDirty();
    private void MarkStateDirty() => _menuStateChanges.Enqueue(true);
    public void NotifyAccountStateChanged() => MarkStateDirty();

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var window = _escapeWindow;
        if (window == null || !window.IsOpen) return;
        if (_menuUpdateQueued) return;
        _menuUpdateQueued = true;
        // UI controllers run after layout. Opening native windows here can draw
        // unarranged controls. Dispatch on the next main-thread input/update pass.
        _taskManager.RunOnMainThread(() =>
        {
            _menuUpdateQueued = false;
            if (_escapeWindow != window || !window.IsOpen) return;
            UpdateMenu(window, args.DeltaSeconds);
        });
    }

    private void UpdateMenu(Options.UI.EscapeMenu window, float deltaSeconds)
    {
        window.UpdateBrowser(deltaSeconds);
        window.FitToViewport(UIManager.WindowRoot.Size, _menuRows, _menuDividers);
        while (window.Bridge is { } bridge && bridge.TryDequeue(out var request))
            HandleWebUiRequest(request);
        var dirty = _menuStateChanges.TryDequeue(out _);
        while (_menuStateChanges.TryDequeue(out _)) { }
        if (window.State == Options.UI.EscapeMenu.BrowserState.Failed)
        {
            if (!_fallbackRendered || dirty) UpdateNativeMenu();
            _fallbackRendered = true;
        }
        else
        {
            _fallbackRendered = false;
            if (dirty && window.State == Options.UI.EscapeMenu.BrowserState.Ready)
                window.Bridge?.PushEvent("escape-menu-state", SerializeState(GetMenuState()));
        }
    }

    private void UpdateNativeMenu()
    {
        var state = GetMenuState();
        var actions = new List<(string Id, string Label, bool Enabled, string? Tooltip, bool Separator)>();
        foreach (var group in state.Groups)
        {
            var separator = group.SeparatorBefore && actions.Count > 0;
            foreach (var action in group.Actions)
            {
                if (!action.Visible) continue;
                var label = action.Badge == "new" ? $"{action.Label} ({state.NewLabel})" : action.Label;
                actions.Add((action.Id, label, action.Enabled, action.Tooltip, separator));
                separator = false;
            }
        }
        _escapeWindow?.SetFallbackActions(actions);
    }

    private void OnNativeMenuAction(string action)
    {
        if (_escapeWindow is not { IsOpen: true, State: Options.UI.EscapeMenu.BrowserState.Failed } window ||
            !TryGetAction(action, out var selected) || !selected.Visible || !selected.Enabled) return;
        try
        {
            if (ExecuteMenuAction(action)) CloseEscapeWindow();
            MarkStateDirty();
        }
        catch (Exception exception)
        {
            Log.Error($"Native Escape Menu action {action} failed: {exception}");
            window.ShowActionError();
        }
    }

    private void HandleWebUiRequest(Options.UI.WebUiRequest request)
    {
        var window = _escapeWindow;
        if (window == null || !window.IsOpen) return;
        var bridge = window.Bridge;
        if (bridge == null) return;
        if (!bridge.TryReassemble(request, out request)) return;
        if (bridge.HandleNativeKey(request)) return;
        if (request.Data == null)
        {
            bridge.SendError(request.RequestId, "invalid-payload");
            return;
        }
        switch (request.Event)
        {
            case "page-ready":
                if (Options.UI.WebUiBridge.PropertyCount(request.Data) is not (0 or 2) ||
                    Options.UI.WebUiBridge.PropertyCount(request.Data) == 2 &&
                    (!Options.UI.WebUiBridge.TryGetString(request.Data, "initialWidth", out var width) ||
                     !int.TryParse(width, out var parsedWidth) || parsedWidth is < 1 or > 4000 ||
                     !Options.UI.WebUiBridge.TryGetString(request.Data, "initialHeight", out var height) ||
                     !int.TryParse(height, out var parsedHeight) || parsedHeight is < 1 or > 4000))
                {
                    bridge.SendError(request.RequestId, "invalid-payload");
                    break;
                }
                bridge.SendSuccess(request.RequestId, "{\"ready\":true}");
                break;
            case "escape-menu-ready":
                if (Options.UI.WebUiBridge.PropertyCount(request.Data) != 0)
                {
                    bridge.SendError(request.RequestId, "invalid-payload");
                    break;
                }
                window.MarkReady();
                bridge.SendSuccess(request.RequestId, "{\"ready\":true}");
                MarkStateDirty();
                break;
            case "escape-menu-get-state":
                if (Options.UI.WebUiBridge.PropertyCount(request.Data) != 0)
                {
                    bridge.SendError(request.RequestId, "invalid-payload");
                    break;
                }
                bridge.SendSuccess(request.RequestId, SerializeState(GetMenuState()));
                break;
            case "escape-menu-close":
                if (Options.UI.WebUiBridge.PropertyCount(request.Data) != 0)
                {
                    bridge.SendError(request.RequestId, "invalid-payload");
                    break;
                }
                bridge.SendSuccess(request.RequestId, "{\"success\":true}");
                CloseEscapeWindow();
                break;
            case "escape-menu-action":
                if (window.State != Options.UI.EscapeMenu.BrowserState.Ready)
                {
                    bridge.SendError(request.RequestId, "page-not-ready");
                    break;
                }
                if (Options.UI.WebUiBridge.PropertyCount(request.Data) != 1 ||
                    !Options.UI.WebUiBridge.TryGetString(request.Data, "action", out var action))
                {
                    bridge.SendError(request.RequestId, "invalid-payload");
                    break;
                }
                if (!TryGetAction(action, out var selected))
                {
                    bridge.SendError(request.RequestId, "unknown-action");
                    break;
                }
                if (!selected.Visible || !selected.Enabled)
                {
                    bridge.SendError(request.RequestId, "action-unavailable");
                    break;
                }
                try
                {
                    var closeMenu = ExecuteMenuAction(action);
                    if (window.IsOpen)
                        bridge.SendSuccess(request.RequestId, closeMenu
                            ? "{\"success\":true,\"closeMenu\":true}"
                            : "{\"success\":true,\"closeMenu\":false}");
                    MarkStateDirty();
                }
                catch (Exception exception)
                {
                    Log.Error($"Escape Menu action {action} failed: {exception}");
                    if (window.IsOpen) bridge.SendError(request.RequestId, "action-failed");
                }
                break;
            case "invalid-payload":
                bridge.SendError(request.RequestId, "invalid-payload");
                break;
            default:
                bridge.SendError(request.RequestId, "unknown-action");
                break;
        }
    }

    private bool TryGetAction(string id, out MenuAction action)
    {
        foreach (var group in GetMenuState().Groups)
            foreach (var candidate in group.Actions)
                if (candidate.Id == id) { action = candidate; return true; }
        action = default!;
        return false;
    }

    private static string SerializeState(MenuState state)
    {
        var json = new StringBuilder("{\"title\":").Append(Options.UI.WebUiBridge.Quote(state.Title))
            .Append(",\"newLabel\":").Append(Options.UI.WebUiBridge.Quote(state.NewLabel))
            .Append(",\"pendingLabel\":").Append(Options.UI.WebUiBridge.Quote(state.PendingLabel))
            .Append(",\"errorLabel\":").Append(Options.UI.WebUiBridge.Quote(state.ErrorLabel))
            .Append(",\"retryLabel\":").Append(Options.UI.WebUiBridge.Quote(state.RetryLabel))
            .Append(",\"groups\":[");
        for (var groupIndex = 0; groupIndex < state.Groups.Length; groupIndex++)
        {
            if (groupIndex != 0) json.Append(',');
            var group = state.Groups[groupIndex];
            json.Append("{\"id\":").Append(Options.UI.WebUiBridge.Quote(group.Id))
                .Append(",\"separatorBefore\":").Append(group.SeparatorBefore ? "true" : "false")
                .Append(",\"actions\":[");
            for (var actionIndex = 0; actionIndex < group.Actions.Length; actionIndex++)
            {
                if (actionIndex != 0) json.Append(',');
                var action = group.Actions[actionIndex];
                json.Append("{\"id\":").Append(Options.UI.WebUiBridge.Quote(action.Id))
                    .Append(",\"label\":").Append(Options.UI.WebUiBridge.Quote(action.Label))
                    .Append(",\"icon\":").Append(Options.UI.WebUiBridge.Quote(action.Icon))
                    .Append(",\"visible\":").Append(action.Visible ? "true" : "false")
                    .Append(",\"enabled\":").Append(action.Enabled ? "true" : "false")
                    .Append(",\"tooltip\":").Append(action.Tooltip == null ? "null" : Options.UI.WebUiBridge.Quote(action.Tooltip))
                    .Append(",\"tone\":").Append(Options.UI.WebUiBridge.Quote(action.Tone))
                    .Append(",\"badge\":").Append(action.Badge == null ? "null" : Options.UI.WebUiBridge.Quote(action.Badge))
                    .Append('}');
            }
            json.Append("]}");
        }
        return json.Append("]}").ToString();
    }

    private MenuState GetMenuState()
    {
        var unread = _changelogManager.NewChangelogEntries;
        var seeNotes = _cfg.GetCVar(CCVars.SeeOwnNotes);
        var state = new MenuState(
            Loc.GetString("ui-escape-title"),
            Loc.GetString("ui-webui-new"),
            Loc.GetString("ui-webui-opening"),
            Loc.GetString("ui-webui-action-failed"),
            Loc.GetString("ui-webui-retry"),
            [
                new MenuGroup("top", false, [
                    new MenuAction("patron-perks", Loc.GetString("rmc-ui-patron-perks"), "crown", Visible: CanViewPatronPerks()),
                    new MenuAction("changelog", Loc.GetString("changelog-button"), "scroll", Badge: unread ? "new" : null),
                    new MenuAction("source-code", Loc.GetString("ui-escape-source-code"), "code-branch", Visible: HasSourceCodeUrl()),
                    new MenuAction("call-vote", Loc.GetString("ui-vote-menu-button"), "check-to-slot", Enabled: _voteManager.CanCallVote,
                        Tooltip: _voteManager.CanCallVote ? null : Loc.GetString("ui-webui-vote-disabled")),
                ]),
                new MenuGroup("information", true, [
                    new MenuAction("rules", Loc.GetString("ui-escape-rules"), "scale-balanced"),
                    new MenuAction("guidebook", Loc.GetString("ui-escape-guidebook"), "book-open"),
                    new MenuAction("wiki", Loc.GetString("ui-escape-wiki"), "globe", Visible: !string.IsNullOrWhiteSpace(_cfg.GetCVar(CCVars.InfoLinksWiki))),
                    new MenuAction("feedback", Loc.GetString("ui-escape-feedback"), "comment-dots"),
                    new MenuAction("bug-report", Loc.GetString("ui-escape-bug-report"), "bug", Visible: !string.IsNullOrWhiteSpace(_cfg.GetCVar(CCVars.InfoLinksBugReport))),
                    new MenuAction("admin-remarks", Loc.GetString("ui-escape-remarks"), "clipboard-list", Enabled: seeNotes,
                        Tooltip: seeNotes ? null : Loc.GetString("ui-escape-remarks-button-disabled")),
                ]),
                new MenuGroup("configuration", true, [
                    new MenuAction("options", Loc.GetString("ui-escape-options"), "gear"),
                    new MenuAction("character", Loc.GetString("ui-escape-character"), "user-pen"),
                ]),
                new MenuGroup("session", true, [
                    new MenuAction("disconnect", Loc.GetString("ui-escape-disconnect"), "plug-circle-xmark", Tone: "danger"),
                    new MenuAction("quit", Loc.GetString("ui-escape-quit"), "right-from-bracket", Tone: "danger"),
                ]),
            ]);
        _menuRows = 0;
        _menuDividers = 0;
        foreach (var group in state.Groups)
        {
            var count = 0;
            foreach (var action in group.Actions)
                if (action.Visible) count++;
            if (count == 0) continue;
            if (_menuRows > 0 && group.SeparatorBefore) _menuDividers++;
            _menuRows += count;
        }
        return state;
    }

    private bool ExecuteMenuAction(string action)
    {
        switch (action)
        {
            case "patron-perks": OpenPatronPerks(); return true;
            case "changelog": _changelog.OpenWindow(); return true;
            case "source-code": OpenSourceCode(); break;
            case "call-vote": ToggleVoteCallMenu(); break;
            case "rules": _info.OpenWindow(); return true;
            case "guidebook": _guidebook.ToggleGuidebook(); break;
            case "wiki": _uri.OpenUri(_cfg.GetCVar(CCVars.InfoLinksWiki)); break;
            case "feedback": _feedback.ToggleWindow(); return true;
            case "bug-report": _uri.OpenUri(_cfg.GetCVar(CCVars.InfoLinksBugReport)); break;
            case "admin-remarks": _console.ExecuteCommand("adminremarks"); return true;
            case "options": _options.OpenWindow(); return true;
            case "character": OpenCharacterSetup(); return true;
            case "disconnect": CloseEscapeWindow(); _console.ExecuteCommand("disconnect"); break;
            case "quit": CloseEscapeWindow(); _console.ExecuteCommand("quit"); break;
        }
        return false;
    }

    private void ToggleVoteCallMenu()
    {
        if (_voteCallMenu is { IsOpen: true }) { _voteCallMenu.Close(); return; }
        _voteCallMenu = new VoteCallMenu();
        _voteCallMenu.OpenCentered();
    }
}
// Arcane-End
