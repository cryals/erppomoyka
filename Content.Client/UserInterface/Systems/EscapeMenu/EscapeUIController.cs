using Content.Client.FeedbackPopup;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Client.UserInterface.Systems.Info;
using Content.Shared.CCVar;
using JetBrains.Annotations;
using Robust.Client.Console;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BaseButton;
// Arcane-Start
using Content.Client.Lobby;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Client.UserInterface.CustomControls;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Content.Client.Guidebook;
using Content.Client.Lobby.UI;
using Content.Client.Players.PlayTimeTracking;
// Arcane-End

namespace Content.Client.UserInterface.Systems.EscapeMenu;

[UsedImplicitly]
public sealed partial class EscapeUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private IClientConsoleHost _console = default!;
    [Dependency] private IUriOpener _uri = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ChangelogUIController _changelog = default!;
    [Dependency] private InfoUIController _info = default!;
    [Dependency] private OptionsUIController _options = default!;
    [Dependency] private GuidebookUIController _guidebook = default!;
    [Dependency] private FeedbackPopupUIController _feedback = null!;
    [Dependency] private ILocalizationManager _loc = default!;
    // Arcane-Start
    [Dependency] private IClientPreferencesManager _preferencesManager = default!;
    [Dependency] private IFileDialogManager _dialogManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private JobRequirementsManager _requirements = default!;
    [Dependency] private MarkingManager _markings = default!;
    [UISystemDependency] private readonly GuidebookSystem? _guide = default!;
    // Arcane-End

    private Options.UI.EscapeMenu? _escapeWindow;
    // Arcane-Start
    private DefaultWindow? _characterWindow;
    private CharacterSetupGui? _characterSetup;
    private HumanoidProfileEditor? _profileEditor;
    // Arcane-End

    private MenuButton? EscapeButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.EscapeButton;

    public void UnloadButton()
    {
        if (EscapeButton == null)
        {
            return;
        }

        EscapeButton.Pressed = false;
        EscapeButton.OnPressed -= EscapeButtonOnOnPressed;
    }

    public void LoadButton()
    {
        if (EscapeButton == null)
        {
            return;
        }

        EscapeButton.OnPressed += EscapeButtonOnOnPressed;
    }

    private void ActivateButton() => EscapeButton?.SetClickPressed(true); // Arcane-Edit: EscapeButton! > EscapeButton?
    private void DeactivateButton() => EscapeButton?.SetClickPressed(false); // Arcane-Edit: EscapeButton! > EscapeButton?

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_escapeWindow == null);

        _escapeWindow = UIManager.CreateWindow<Options.UI.EscapeMenu>();
        // Arcane-Edit-Start
        _escapeWindow.OnClose += DeactivateButton;
        _escapeWindow.OnOpen += ActivateButton;
        InitializeWebUiMenu();
        // Arcane-Edit-End

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.EscapeMenu,
                InputCmdHandler.FromDelegate(_ => ToggleWindow()))
            .Register<EscapeUIController>();
    }

    public void OnStateExited(GameplayState state)
    {
        // Arcane-Edit-Start
        ShutdownWebUiMenu();
        // Arcane-Edit-End

        if (_escapeWindow != null)
        {
            _escapeWindow.Dispose();
            _escapeWindow = null;
        }

        CloseCharacterSetup(); // Arcane

        CommandBinds.Unregister<EscapeUIController>();
    }

    // Arcane-Edit: OnSeeOwnNotesChanged updated the XAML button > WebUI state updates in EscapeUIController.WebUI.cs
    private void EscapeButtonOnOnPressed(ButtonEventArgs obj)
    {
        ToggleWindow();
    }

    private void CloseEscapeWindow()
    {
        _escapeWindow?.Close();
    }

    /// <summary>
    /// Toggles the game menu.
    /// </summary>
    public void ToggleWindow()
    {
        if (_escapeWindow == null)
            return;

        if (_escapeWindow.IsOpen)
        {
            CloseEscapeWindow();
            if (EscapeButton != null) EscapeButton.Pressed = false; // Arcane-Edit: EscapeButton!.Pressed = false > null-safe HUD update
        }
        else
        {
            // Arcane-Start
            GetMenuState();
            _escapeWindow.PrepareBrowser();
            _escapeWindow.FitToViewport(UIManager.WindowRoot.Size, _menuRows, _menuDividers);
            // Arcane-End
            _escapeWindow.OpenCentered();
            if (EscapeButton != null) EscapeButton.Pressed = true; // Arcane-Edit: EscapeButton!.Pressed = true > null-safe HUD update
        }
    }
    // Arcane-Start
    private void OpenCharacterSetup()
    {
        if (_characterWindow is { IsOpen: true })
        {
            _characterWindow.MoveToFront();
            return;
        }

        _profileEditor = new HumanoidProfileEditor(
            _preferencesManager,
            _cfg,
            EntityManager,
            _dialogManager,
            LogManager,
            _playerManager,
            _prototypeManager,
            _resourceCache,
            _requirements,
            _markings);

        if (_guide != null)
            _profileEditor.OnOpenGuidebook += _guide.OpenHelp;

        LobbyUIController.RaiseProfileEditorCreated(_profileEditor);

        _profileEditor.Save += SaveCharacterProfile;

        _characterSetup = new CharacterSetupGui(_profileEditor);

        _characterSetup.CloseButton.OnPressed += _ =>
        {
            if (_profileEditor.Profile != null && _profileEditor.IsDirty)
            {
                OpenCharacterSavePanel(CloseCharacterSetup);
                return;
            }

            CloseCharacterSetup();
        };

        _characterSetup.SelectCharacter += slot =>
        {
            if (_profileEditor.Profile != null && _profileEditor.IsDirty && _profileEditor.CharacterSlot != slot)
            {
                OpenCharacterSavePanel(
                    () =>
                    {
                        _preferencesManager.SelectCharacter(slot);
                        ReloadCharacterSetup();
                    },
                    _characterSetup.ReloadCharacterPickers);
                return;
            }

            _preferencesManager.SelectCharacter(slot);
            ReloadCharacterSetup();
        };

        _characterSetup.DeleteCharacter += slot =>
        {
            if (_profileEditor.Profile != null && _profileEditor.IsDirty && _profileEditor.CharacterSlot == slot)
            {
                OpenCharacterSavePanel(
                    () =>
                    {
                        _preferencesManager.DeleteCharacter(slot);
                        ReloadCharacterSetup();
                    },
                    _characterSetup.ReloadCharacterPickers);
                return;
            }

            _preferencesManager.DeleteCharacter(slot);

            if (_profileEditor.CharacterSlot == slot)
                ReloadCharacterSetup();
            else
                _characterSetup.ReloadCharacterPickers();
        };

        _characterWindow = new DefaultWindow
        {
            Title = Loc.GetString("ui-escape-character"),
            Resizable = true,
            MinWidth = 1050,
            MinHeight = 550,
            SetWidth = 1050,
            SetHeight = 700
        };

        _characterWindow.Contents.AddChild(_characterSetup);

        if (_preferencesManager.ServerDataLoaded)
        {
            _characterSetup.ReloadCharacterPickers();
            _profileEditor.SetProfile(
                (HumanoidCharacterProfile?) _preferencesManager.Preferences?.SelectedCharacter,
                _preferencesManager.Preferences?.SelectedCharacterIndex);
        }

        _characterWindow.OpenCentered();
    }

    private void CloseCharacterSetup()
    {
        _characterWindow?.Dispose();
        _characterWindow = null;
        _characterSetup = null;
        _profileEditor = null;
    }

    private void SaveCharacterProfile()
    {
        if (_profileEditor?.Profile == null || _profileEditor.CharacterSlot == null)
            return;

        _preferencesManager.UpdateCharacter(_profileEditor.Profile, _profileEditor.CharacterSlot.Value);
        ReloadCharacterSetup();
    }

    private void ReloadCharacterSetup()
    {
        _characterSetup?.ReloadCharacterPickers();
        _profileEditor?.SetProfile(
            (HumanoidCharacterProfile?) _preferencesManager.Preferences?.SelectedCharacter,
            _preferencesManager.Preferences?.SelectedCharacterIndex);
    }

    private void OpenCharacterSavePanel(Action? onConfirm = null, Action? onCancel = null)
    {
        var savePanel = new CharacterSetupGuiSavePanel();

        savePanel.SaveButton.OnPressed += _ =>
        {
            SaveCharacterProfile();
            savePanel.Close();
            onConfirm?.Invoke();
        };

        savePanel.NoSaveButton.OnPressed += _ =>
        {
            savePanel.Close();
            onConfirm?.Invoke();
        };

        if (onCancel != null)
            savePanel.CancelButton.OnPressed += _ => onCancel();

        savePanel.OpenCentered();
    }
    // Arcane-End
}
