namespace Content.Arcane.Client.Arcade;

/// <summary>Each cabinet fixes its game; the browser cannot select another cabinet's game.</summary>
public abstract class ClassicArcadeBoundUserInterface(EntityUid owner, Enum uiKey, string game)
    : BoundUserInterface(owner, uiKey)
{
    protected override void Open()
    {
        base.Open();
        this.CreateWindow<DoomArcadeWindow>().ConfigureGame(game);
    }
}

public sealed class AsteroidsArcadeBoundUserInterface(EntityUid owner, Enum uiKey)
    : ClassicArcadeBoundUserInterface(owner, uiKey, "asteroids");
public sealed class FlappyBirdArcadeBoundUserInterface(EntityUid owner, Enum uiKey)
    : ClassicArcadeBoundUserInterface(owner, uiKey, "flappy_bird");
public sealed class PacmanArcadeBoundUserInterface(EntityUid owner, Enum uiKey)
    : ClassicArcadeBoundUserInterface(owner, uiKey, "pacman");
public sealed class PrinceArcadeBoundUserInterface(EntityUid owner, Enum uiKey)
    : ClassicArcadeBoundUserInterface(owner, uiKey, "prince_of_persia");
public sealed class SnakeArcadeBoundUserInterface(EntityUid owner, Enum uiKey)
    : ClassicArcadeBoundUserInterface(owner, uiKey, "snake_game");
public sealed class InvadersArcadeBoundUserInterface(EntityUid owner, Enum uiKey)
    : ClassicArcadeBoundUserInterface(owner, uiKey, "space_Invaders");
public sealed class NuPogodiArcadeBoundUserInterface(EntityUid owner, Enum uiKey)
    : ClassicArcadeBoundUserInterface(owner, uiKey, "nu_pogodi");
public sealed class FireRedArcadeBoundUserInterface(EntityUid owner, Enum uiKey)
    : ClassicArcadeBoundUserInterface(owner, uiKey, "firered");
