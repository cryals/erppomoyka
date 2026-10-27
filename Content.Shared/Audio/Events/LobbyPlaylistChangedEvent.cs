using Robust.Shared.Serialization;

namespace Content.Shared.Audio.Events;

/// <summary>
/// Event of changing lobby music playlist (on server).
/// </summary>
[Serializable, NetSerializable]
public sealed class LobbyPlaylistChangedEvent : EntityEventArgs
{
    /// <inheritdoc />
    public LobbyPlaylistChangedEvent(string[] playlist)
    {
        Playlist = playlist;
    }

    /// <summary>
    /// List of soundtrack filenames for lobby playlist.
    /// </summary>
    public string[] Playlist;
    // Arcane-Start
    public int Revision;
    public bool LobbyOnly;
    // Arcane-End
}

/// <summary>
/// Event of stopping lobby music.
/// </summary>
[Serializable, NetSerializable]
public sealed class LobbyMusicStopEvent : EntityEventArgs
{
}
