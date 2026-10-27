using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Audio;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.GameTicking.Prototypes;
using Robust.Shared.Console;
using Robust.Shared.Random;

namespace Content.Arcane.Server.Lobby;

/// <summary>Whitelisted media selection; commands never read arbitrary paths or remote URLs.</summary>
public abstract partial class LobbyMediaCommand : LocalizedEntityCommands
{
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IAdminLogManager _audit = default!;
    [Dependency] private IRobustRandom _random = default!;
    protected abstract string[] Pool();
    protected abstract string? Current();
    protected abstract bool Select(string id);
    protected abstract void Reset();

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is { } player && !_admins.HasAdminFlag(player, AdminFlags.Fun))
        {
            shell.WriteError(Loc.GetString("arcane-lobby-command-denied"));
            return;
        }
        if (args.Length is < 1 or > 2 || (args[0] == "set" ? args.Length != 2 : args.Length != 1))
        { shell.WriteError(Help); return; }
        var pool = Pool();
        if (args[0] == "list")
        {
            shell.WriteLine(Loc.GetString("arcane-lobby-command-list", ("count", pool.Length)));
            foreach (var id in pool) shell.WriteLine(id);
            return;
        }
        var previous = Current() ?? "none";
        switch (args[0])
        {
            case "reset": Reset(); break;
            case "random":
                if (pool.Length == 0) { shell.WriteError(Loc.GetString("arcane-lobby-command-empty")); return; }
                var alternatives = pool.Where(id => id != previous).ToArray();
                if (!Select(_random.Pick(alternatives.Length > 0 ? alternatives : pool)))
                { shell.WriteError(Loc.GetString("arcane-lobby-command-invalid")); return; }
                break;
            case "set":
                if (!pool.Contains(args[1]) || !Select(args[1]))
                { shell.WriteError(Loc.GetString("arcane-lobby-command-invalid")); return; }
                break;
            default: shell.WriteError(Help); return;
        }
        var selected = Current() ?? "none";
        _audit.Add(LogType.AdminCommands, LogImpact.Low,
            $"{shell.Player?.Name ?? "server console"} ({shell.Player?.UserId}) used {Command} {args[0]}: {previous} -> {selected}");
        shell.WriteLine(Loc.GetString("arcane-lobby-command-selected", ("id", selected)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (shell.Player is { } player && !_admins.HasAdminFlag(player, AdminFlags.Fun)) return CompletionResult.Empty;
        if (args.Length == 1) return CompletionResult.FromOptions(new[] { "list", "set", "random", "reset" });
        if (args.Length == 2 && args[0] == "set") return CompletionResult.FromOptions(Pool());
        return CompletionResult.Empty;
    }
}

[AdminCommand(AdminFlags.Fun)]
public sealed partial class LobbyBackgroundCommand : LobbyMediaCommand
{
    public override string Command => "lobbybackground";
    private ServerGameTicker Ticker => EntityManager.System<ServerGameTicker>();
    protected override string[] Pool() => Ticker.AvailableLobbyBackgrounds.Select(id => id.ToString()).Order().ToArray();
    protected override string? Current() => Ticker.LobbyBackground?.ToString();
    protected override bool Select(string id) => Ticker.SetLobbyBackground(new ProtoId<LobbyBackgroundPrototype>(id));
    protected override void Reset() => Ticker.SetLobbyBackground(null);
}

[AdminCommand(AdminFlags.Fun)]
public sealed partial class LobbyMusicCommand : LobbyMediaCommand
{
    public override string Command => "lobbymusic";
    private ContentAudioSystem Audio => EntityManager.System<ContentAudioSystem>();
    protected override string[] Pool() => Audio.AvailableLobbyTracks;
    protected override string? Current() => Audio.SelectedLobbyTrack;
    protected override bool Select(string id) => Audio.SetLobbyTrack(id);
    protected override void Reset() => Audio.ResetLobbyPlaylist();
}
