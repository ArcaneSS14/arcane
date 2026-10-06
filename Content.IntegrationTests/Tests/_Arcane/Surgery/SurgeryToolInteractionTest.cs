// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Arcane.Surgery;
using Content.Shared._Shitcode.Heretic.Components;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Arcane.Surgery;

[TestFixture]
[TestOf(typeof(SharedSurgeryToolSystem))]
public sealed class SurgeryToolInteractionTest : InteractionTest
{
    private const string Patient = "MobHuman";
    private static readonly EntProtoId CloseIncisionChest = "SurgeryCloseIncisionChest";
    private static readonly EntProtoId StopBloodOutput = "SurgeryStopBloodOutput";
    private static readonly EntProtoId OpenIncision = "SurgeryOpenIncision";
    private static readonly EntProtoId InsertItem = "SurgeryInsertItem";
    private static readonly EntProtoId TendWoundsBrute = "SurgeryTendWoundsBrute";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    [Test]
    public async Task ScalpelThenRetractorOpensChest()
    {
        var chest = await SpawnPatient(lying: true);

        await InteractUsing("Scalpel");
        Assert.That(SEntMan.HasComponent<IncisionOpenComponent>(chest));

        await InteractUsing("Retractor");
        Assert.That(SEntMan.HasComponent<SkinRetractedComponent>(chest));
    }

    [Test]
    public async Task StandingPatientIsNotOperated()
    {
        var chest = await SpawnPatient(lying: false);

        await InteractUsing("Scalpel", awaitDoAfters: false);
        await RunTicks(5);

        Assert.Multiple(() =>
        {
            Assert.That(ActiveDoAfters, Is.Empty);
            Assert.That(SEntMan.HasComponent<IncisionOpenComponent>(chest), Is.False);
        });
    }

    [Test]
    public async Task CombatModeDoesNotOperate()
    {
        var chest = await SpawnPatient(lying: true);
        await SetCombatMode(true);

        await InteractUsing("Scalpel", awaitDoAfters: false);
        await RunTicks(5);

        Assert.Multiple(() =>
        {
            Assert.That(ActiveDoAfters, Is.Empty);
            Assert.That(SEntMan.HasComponent<IncisionOpenComponent>(chest), Is.False);
        });
    }

    [Test]
    public async Task CauteryClosesWhatTendSealWouldSeal()
    {
        var chest = await SpawnPatient(lying: true);
        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<IncisionOpenComponent>(chest);
            SEntMan.EnsureComponent<SkinRetractedComponent>(chest);
        });

        var cautery = ToServer(await PlaceInHands("Cautery"));
        var toolSystem = SEntMan.System<SharedSurgeryToolSystem>();

        // Tend-wound surgeries are valid on any opened part, but closing the incision also completes their seal step.
        await Server.WaitAssertion(() =>
        {
            var options = toolSystem.GetToolOptions(STarget!.Value, chest, SPlayer, cautery);
            Assert.That(options, Has.Count.EqualTo(1));
            Assert.That(options[0].Surgery, Is.EqualTo(CloseIncisionChest));
            Assert.That(options[0].Step.Id, Is.EqualTo("SurgeryStepCloseIncision"));
        });

        await Interact();

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<IncisionOpenComponent>(chest), Is.False);
            Assert.That(SEntMan.HasComponent<SkinRetractedComponent>(chest), Is.False);
        });
    }

    [Test]
    public async Task CauteryDoesNotSealOverSawedBones()
    {
        var chest = await SpawnPatient(lying: true);
        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<IncisionOpenComponent>(chest);
            SEntMan.EnsureComponent<SkinRetractedComponent>(chest);
            SEntMan.EnsureComponent<BonesSawedComponent>(chest);
        });

        var cautery = ToServer(await PlaceInHands("Cautery"));
        var toolSystem = SEntMan.System<SharedSurgeryToolSystem>();

        // The bones have to be sealed before the incision is closed.
        await Server.WaitAssertion(() =>
            Assert.That(toolSystem.GetToolOptions(STarget!.Value, chest, SPlayer, cautery), Is.Empty));
    }

    [Test]
    public async Task ScalpelOnWoundedPartIsOneChoice()
    {
        var chest = await SpawnPatient(lying: true);
        var scalpel = ToServer(await PlaceInHands("Scalpel"));
        var toolSystem = SEntMan.System<SharedSurgeryToolSystem>();
        var surgerySystem = SEntMan.System<SharedSurgerySystem>();

        await Server.WaitAssertion(() =>
        {
            var damage = new DamageSpecifier(ProtoMan.Index(Blunt), 30);
            SEntMan.System<DamageableSystem>().TryChangeDamage(STarget!.Value, damage, true, targetPart: TargetBodyPart.Chest);
            Assert.That(surgerySystem.GetValidSurgeries(STarget!.Value, chest), Does.Contain(TendWoundsBrute));

            var options = toolSystem.GetToolOptions(STarget!.Value, chest, SPlayer, scalpel);
            Assert.That(options, Has.Count.EqualTo(1));
            Assert.That(options[0].Target, Is.Null);
        });

        await Interact();

        Assert.That(SEntMan.HasComponent<IncisionOpenComponent>(chest));
    }

    [Test]
    public async Task AnyOrderToolDoesFirstOpenStep()
    {
        var chest = await SpawnPatient(lying: true);
        var scalpel = ToServer(await PlaceInHands("Scalpel"));
        var toolSystem = SEntMan.System<SharedSurgeryToolSystem>();

        await Server.WaitAssertion(() =>
        {
            // Flesh surgery lets every step be done in any order while it is held.
            SEntMan.EnsureComponent<FleshSurgeryComponent>(scalpel);

            var options = toolSystem.GetToolOptions(STarget!.Value, chest, SPlayer, scalpel);
            Assert.That(options, Has.Some.Matches<SurgeryToolOption>(o =>
                o.Surgery == OpenIncision && o.Step.Id == "SurgeryStepOpenIncisionScalpel"));
        });
    }

    [Test]
    public async Task EmptyHandKeepsCavityInsertPlan()
    {
        var chest = await SpawnPatient(lying: true);
        var toolSystem = SEntMan.System<SharedSurgeryToolSystem>();

        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<IncisionOpenComponent>(chest);
            SEntMan.EnsureComponent<SkinRetractedComponent>(chest);
            SEntMan.EnsureComponent<BonesSawedComponent>(chest);
            SEntMan.EnsureComponent<BonesOpenComponent>(chest);
            toolSystem.SetPlan(SPlayer, chest, InsertItem);
        });

        await Interact(awaitDoAfters: false);
        await RunTicks(5);

        Assert.Multiple(() =>
        {
            Assert.That(ActiveDoAfters, Is.Empty);
            Assert.That(SEntMan.GetComponent<SurgeryPlanComponent>(SPlayer).Surgery, Is.EqualTo(InsertItem));
        });
    }

    [Test]
    public async Task ClampedBleedersStillNeedStitching()
    {
        var chest = await SpawnPatient(lying: true);
        var surgerySystem = SEntMan.System<SharedSurgerySystem>();

        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<IncisionOpenComponent>(chest);
            SEntMan.EnsureComponent<SkinRetractedComponent>(chest);
            Assert.That(surgerySystem.GetValidSurgeries(STarget!.Value, chest), Does.Not.Contain(StopBloodOutput));

            // The last hemostat clamp stops every bleed and leaves the bleeders clamped.
            SEntMan.EnsureComponent<BleedersClampedComponent>(chest);
            Assert.That(surgerySystem.GetValidSurgeries(STarget!.Value, chest), Does.Contain(StopBloodOutput));

            var surgery = surgerySystem.GetSingleton(StopBloodOutput)!.Value;
            var next = surgerySystem.GetNextStep(STarget!.Value, chest, surgery, SPlayer);
            Assert.That(next, Is.Not.Null);
            Assert.That(next!.Value.Surgery.Comp.Steps[next.Value.Step].Id, Is.EqualTo("SurgeryStepCloseBloodOutputs"));
        });
    }

    [Test]
    public async Task SeveredLimbSlotIsTargeted()
    {
        await SpawnPatient(lying: true);
        var body = STarget!.Value;
        var bodySystem = SEntMan.System<SharedBodySystem>();
        var toolSystem = SEntMan.System<SharedSurgeryToolSystem>();

        await Server.WaitAssertion(() =>
        {
            var leg = bodySystem.GetBodyChildrenOfType(body, BodyPartType.Leg, symmetry: BodyPartSymmetry.Left).Single().Id;
            var groin = bodySystem.GetParentPartAndSlotOrNull(leg)!.Value.Parent;
            SEntMan.System<WoundSystem>().AmputateWoundableSafely(groin, leg);

            var targeting = SEntMan.EnsureComponent<TargetingComponent>(SPlayer);
            targeting.Target = TargetBodyPart.LeftLeg;

            // The empty slot is found through the parent, which holds the surgeries that attach a part there.
            Assert.That(toolSystem.TryGetTargetPart(body, SPlayer, out var part, out var missing));
            Assert.That(part, Is.EqualTo(groin));
            Assert.That(missing, Is.EqualTo((BodyPartType.Leg, BodyPartSymmetry.Left)));
        });
    }

    [TestCase(TargetBodyPart.LeftArm)]
    [TestCase(TargetBodyPart.Chest)]
    public async Task ToolClicksReattachLeftArm(TargetBodyPart target)
    {
        var chest = await SpawnPatient(lying: true);
        var bodySystem = SEntMan.System<SharedBodySystem>();
        EntityUid arm = default;
        var slotCount = 0;

        await Server.WaitAssertion(() =>
        {
            arm = bodySystem.GetBodyChildrenOfType(STarget!.Value, BodyPartType.Arm, symmetry: BodyPartSymmetry.Left).Single().Id;
            slotCount = SEntMan.GetComponent<BodyPartComponent>(chest).Children.Count;
            Assert.That(bodySystem.DetachPart(chest, "left arm", arm));

            SEntMan.EnsureComponent<TargetingComponent>(SPlayer).Target = target;
            SEntMan.Dirty(SPlayer, SEntMan.GetComponent<TargetingComponent>(SPlayer));

            // Losing a limb stands a patient who is not buckled back up.
            Assert.That(SEntMan.System<StandingStateSystem>().Down(STarget!.Value, playSound: false, dropHeldItems: false, force: true));
        });
        await RunTicks(5);

        await InteractUsing("Scalpel");
        await InteractUsing("Retractor");
        Assert.That(SEntMan.HasComponent<SkinRetractedComponent>(chest));

        await Pickup(SEntMan.GetNetEntity(arm));
        await Interact();
        Assert.That(bodySystem.GetParentPartAndSlotOrNull(arm), Is.EqualTo((chest, "left arm")));

        // Getting the arm back stands the patient up as well.
        await Server.WaitPost(() =>
            SEntMan.System<StandingStateSystem>().Down(STarget!.Value, playSound: false, dropHeldItems: false, force: true));
        await RunTicks(5);

        await InteractUsing("Cautery");

        Assert.Multiple(() =>
        {
            Assert.That(bodySystem.GetParentPartAndSlotOrNull(arm), Is.EqualTo((chest, "left arm")));
            Assert.That(SEntMan.HasComponent<BodyPartReattachedComponent>(arm), Is.False);
            Assert.That(SEntMan.GetComponent<BodyPartComponent>(chest).Children, Has.Count.EqualTo(slotCount));
        });
    }

    private async Task<EntityUid> SpawnPatient(bool lying)
    {
        await SpawnTarget(Patient);
        var body = STarget!.Value;
        EntityUid? chest = null;

        await Server.WaitAssertion(() =>
        {
            if (lying)
                Assert.That(SEntMan.System<StandingStateSystem>().Down(body, playSound: false, dropHeldItems: false, force: true));

            chest = SEntMan.System<SharedBodySystem>().GetBodyChildrenOfType(body, BodyPartType.Chest).Single().Id;
        });

        await RunTicks(1);
        return chest!.Value;
    }
}
