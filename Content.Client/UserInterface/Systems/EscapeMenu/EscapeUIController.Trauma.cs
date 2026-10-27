using Content.Shared.CCVar;
using Content.Trauma.Common.LinkAccount;
using Robust.Shared;

namespace Content.Client.UserInterface.Systems.EscapeMenu;

public sealed partial class EscapeUIController
{
    [Dependency] private ILinkAccountManager _linkAccount = default!;

    public Action? OnTogglePatronPerksWindow;
    public Options.UI.EscapeMenu? Window => _escapeWindow;

    // Arcane-Edit-Start
    private bool CanViewPatronPerks() => _linkAccount.CanViewPatronPerks();

    private bool HasSourceCodeUrl() => !string.IsNullOrWhiteSpace(_cfg.GetCVar(CCVars.InfoLinksGithub));

    private void OpenSourceCode()
    {
        var repo = _cfg.GetCVar(CCVars.InfoLinksGithub);
        var commit = _cfg.GetCVar(CVars.BuildVersion);
        if (commit == "") commit = "master";
        _uri.OpenUri($"{repo}/tree/{commit}");
    }

    private void OpenPatronPerks()
    {
        OnTogglePatronPerksWindow?.Invoke();
    }
    // Arcane-Edit-End
}
