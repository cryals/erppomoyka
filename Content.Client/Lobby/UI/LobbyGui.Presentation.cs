// Arcane-Start
using Content.Client.Info;
using Content.Client.Voting.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class LobbyGui
{
    // A supported presentation adapter; the original controls remain the native fallback.
    public LinkBanner Links => LinkBanner;
    public VoteCallMenuButton VoteButton => CallVoteButton;
    public Control NativeContent => MainContainer;
    public bool IsEditingCharacter => CharacterSetupState.Visible;
    public event Action? PresentationChanged;

    public void SetNativePresentationVisible(bool visible)
    {
        MainContainer.Visible = visible;
        Background.Visible = visible;
    }
}
// Arcane-End
