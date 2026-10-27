// <Trauma>
using Content.Trauma.Common.LinkAccount;
// </Trauma>
using Content.Client.Changelog;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;

namespace Content.Client.Info
{
    public sealed partial class LinkBanner : BoxContainer
    {
        // <Trauma>
        public static Action<LinkBanner>? OnLinkBanner;
        public Action? OnToggleWindow;
        // </Trauma>

        [Dependency] private IConfigurationManager _cfg = default!;

        private ValueList<(CVarDef<string> cVar, Button button)> _infoLinks;

        // Arcane-Start: both presentations use these same action delegates.
        public readonly List<(string Id, string Icon, Button Button, Action Execute)> Actions = new();

        private void BindAction(string id, string icon, Button button, Action execute)
        {
            button.OnPressed += _ => execute();
            Actions.Add((id, icon, button, execute));
        }

        public void RefreshLinks()
        {
            foreach (var (cVar, link) in _infoLinks)
                link.Visible = !string.IsNullOrWhiteSpace(_cfg.GetCVar(cVar));
        }
        // Arcane-End

        public LinkBanner()
        {
            // <Trauma>
            IoCManager.InjectDependencies(this);
            OnLinkBanner?.Invoke(this);
            // </Trauma>
            var buttons = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal
            };
            AddChild(buttons);

            var uriOpener = IoCManager.Resolve<IUriOpener>();

            var rulesButton = new Button() {Text = Loc.GetString("server-info-rules-button")};
            BindAction("rules", "scale-balanced", rulesButton, () => new RulesAndInfoWindow().Open()); // Arcane-Edit: direct callback > shared action
            buttons.AddChild(rulesButton);

            AddInfoButton("server-info-discord-button", CCVars.InfoLinksDiscord);
            AddInfoButton("server-info-website-button", CCVars.InfoLinksWebsite);
            AddInfoButton("server-info-wiki-button", CCVars.InfoLinksWiki);
            AddInfoButton("server-info-forum-button", CCVars.InfoLinksForum);
            AddInfoButton("server-info-telegram-button", CCVars.InfoLinksTelegram);
            AddInfoButton("rmc-ui-patreon", CCVars.InfoLinksPatreon);

            var linkAccountButton = new Button
            {
                Text = Loc.GetString("rmc-ui-link-discord-account"),
            };
            BindAction("link-account", "link", linkAccountButton, () => OnToggleWindow?.Invoke()); // Arcane-Edit: direct callback > shared action
            buttons.AddChild(linkAccountButton);

            var guidebookController = UserInterfaceManager.GetUIController<GuidebookUIController>();
            var guidebookButton = new Button() { Text = Loc.GetString("server-info-guidebook-button") };
            // Arcane-Edit-Start
            BindAction("guidebook", "book-open", guidebookButton, guidebookController.ToggleGuidebook);
            // Arcane-Edit-End
            buttons.AddChild(guidebookButton);

            var changelogButton = new ChangelogButton();
            BindAction("changelog", "scroll", changelogButton, () => UserInterfaceManager.GetUIController<ChangelogUIController>().ToggleWindow()); // Arcane-Edit: direct callback > shared action
            buttons.AddChild(changelogButton);

            void AddInfoButton(string loc, CVarDef<string> cVar)
            {
                var button = new Button { Text = Loc.GetString(loc) };
                BindAction(loc, "globe", button, () => uriOpener.OpenUri(_cfg.GetCVar(cVar))); // Arcane-Edit: direct callback > shared action
                buttons.AddChild(button);
                _infoLinks.Add((cVar, button));
            }
        }

        protected override void EnteredTree()
        {
            // LinkBanner is constructed before the client even connects to the server due to UI refactor stuff.
            // We need to update these buttons when the UI is shown.

            base.EnteredTree();

            foreach (var (cVar, link) in _infoLinks)
            {
                link.Visible = _cfg.GetCVar(cVar) != "";
            }
        }
    }
}
