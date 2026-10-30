namespace Content.Arcane.Client.Arcade;

/// <summary>The cabinet owns the UI lifetime; DOOM itself runs entirely on this client.</summary>
public sealed class DoomArcadeBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    protected override void Open()
    {
        base.Open();
        this.CreateWindow<DoomArcadeWindow>();
    }
}
