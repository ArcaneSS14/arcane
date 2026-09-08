using Content.Shared.Hands.Components;
using Content.Shared.Stunnable;

namespace Content.Shared.Hands.EntitySystems;

/// <summary>
/// This is for events that don't affect normal hand functions but do care about hands.
/// </summary>
public abstract partial class SharedHandsSystem
{
    private void InitializeEventListeners()
    {
        SubscribeLocalEvent<HandsComponent, GetStandUpTimeEvent>(OnStandupArgs);
        SubscribeLocalEvent<HandsComponent, KnockedDownRefreshEvent>(OnKnockedDownRefresh);
    }

    /// <summary>
    /// Reduces the time it takes to stand up based on the number of hands we have available.
    /// </summary>
    private void OnStandupArgs(Entity<HandsComponent> ent, ref GetStandUpTimeEvent time)
    {
        if (!HasComp<KnockedDownComponent>(ent))
            return;

        var hands = GetEmptyHandCount(ent.Owner);

        if (hands == 0)
            return;

        time.DoAfterTime *= (float)ent.Comp.Count / (hands + ent.Comp.Count);
    }

    private void OnKnockedDownRefresh(Entity<HandsComponent> ent, ref KnockedDownRefreshEvent args)
    {
        var freeHands = CountFreeHands(ent.AsNullable());
        var totalHands = GetHandCount(ent.AsNullable());

        // Arcane-Edit-Start
        if (totalHands == 0 || freeHands == totalHands)
            return;

        if (totalHands <= 2)
        {
            args.SpeedModifier *= totalHands == 1 || freeHands == 1 ? 0.85f : 0.5f;
            return;
        }

        if (freeHands <= 1)
            args.SpeedModifier *= 1f - 0.5f * (totalHands - freeHands) / totalHands;
        // Arcane-Edit-End
    }
}
