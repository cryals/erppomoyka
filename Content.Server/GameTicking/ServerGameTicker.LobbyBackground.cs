using Content.Shared.GameTicking.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.GameTicking;

public sealed partial class ServerGameTicker
{
    [ViewVariables]
    public ProtoId<LobbyBackgroundPrototype>? LobbyBackground { get; private set; }

    [ViewVariables]
    private List<ProtoId<LobbyBackgroundPrototype>>? _lobbyBackgrounds;

    private static readonly string[] WhitelistedBackgroundExtensions = {"png", "jpg", "jpeg", "webp", "gif"}; // Arcane-Edit: static formats > include GIF

    private void InitializeLobbyBackground()
    {
        var allprotos = ProtoMan.EnumeratePrototypes<LobbyBackgroundPrototype>().ToList();
        _lobbyBackgrounds ??= new List<ProtoId<LobbyBackgroundPrototype>>();

        //create protoids from them
        foreach (var proto in allprotos)
        {
            var ext = proto.Background.Extension;
            if (!WhitelistedBackgroundExtensions.Contains(ext.ToLowerInvariant())) // Arcane-Edit: case-sensitive > case-insensitive extension
                continue;

            //create a protoid and add it to the list
            _lobbyBackgrounds.Add(new ProtoId<LobbyBackgroundPrototype>(proto.ID));
        }

        RandomizeLobbyBackground();
    }

    private void RandomizeLobbyBackground()
    {
        if (_lobbyBackgrounds != null && _lobbyBackgrounds.Count != 0)
            LobbyBackground = Random.Pick(_lobbyBackgrounds);
        else
            LobbyBackground = null;
    }

    // Arcane-Start: owner API for permission-checked administrative callers.
    public IReadOnlyList<ProtoId<LobbyBackgroundPrototype>> AvailableLobbyBackgrounds =>
        _lobbyBackgrounds ?? new List<ProtoId<LobbyBackgroundPrototype>>();

    public bool SetLobbyBackground(ProtoId<LobbyBackgroundPrototype>? background)
    {
        if (background != null && (_lobbyBackgrounds == null || !_lobbyBackgrounds.Contains(background.Value))) return false;
        if (background == null) RandomizeLobbyBackground();
        else LobbyBackground = background;
        SendStatusToAll();
        return true;
    }
    // Arcane-End
}
