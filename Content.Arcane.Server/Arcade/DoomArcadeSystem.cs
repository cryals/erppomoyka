using Content.Arcane.Shared.Arcade;
using Content.Shared.Power;
using Robust.Server.GameObjects;

namespace Content.Arcane.Server.Arcade;

/// <summary>Closes every local DOOM session when its cabinet loses power.</summary>
public sealed partial class DoomArcadeSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DoomArcadeComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnPowerChanged(EntityUid uid, DoomArcadeComponent component, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            _ui.CloseUi(uid, DoomArcadeUiKey.Key);
    }
}
