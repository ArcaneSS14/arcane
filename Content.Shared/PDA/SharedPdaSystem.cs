// SPDX-License-Identifier: MIT

using Content.Shared.Access.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared.PDA
{
    public abstract class SharedPdaSystem : EntitySystem
    {
        [Dependency] protected readonly ItemSlotsSystem ItemSlotsSystem = default!;
        [Dependency] protected readonly SharedAppearanceSystem Appearance = default!;

        // Orion-Start
        private static readonly SpriteSpecifier.Rsi FallbackScreenSprite = new(new ResPath("_Orion/Objects/Devices/pda.rsi"), "pda_screen_borders");
        // Orion-End

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<PdaComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<PdaComponent, ComponentRemove>(OnComponentRemove);

            SubscribeLocalEvent<PdaComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
            SubscribeLocalEvent<PdaComponent, EntRemovedFromContainerMessage>(OnItemRemoved);

            SubscribeLocalEvent<PdaComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);
        }
        protected virtual void OnComponentInit(EntityUid uid, PdaComponent pda, ComponentInit args)
        {
            if (pda.IdCard != null)
                pda.IdSlot.StartingItem = pda.IdCard;

            ItemSlotsSystem.AddItemSlot(uid, PdaComponent.PdaIdSlotId, pda.IdSlot);
            ItemSlotsSystem.AddItemSlot(uid, PdaComponent.PdaPenSlotId, pda.PenSlot);
            ItemSlotsSystem.AddItemSlot(uid, PdaComponent.PdaPaiSlotId, pda.PaiSlot);
            ItemSlotsSystem.AddItemSlot(uid, PdaComponent.PdaPaperSlotId, pda.PaperSlot); // Arcane

            UpdatePdaAppearance(uid, pda);
        }

        private void OnComponentRemove(EntityUid uid, PdaComponent pda, ComponentRemove args)
        {
            ItemSlotsSystem.RemoveItemSlot(uid, pda.IdSlot);
            ItemSlotsSystem.RemoveItemSlot(uid, pda.PenSlot);
            ItemSlotsSystem.RemoveItemSlot(uid, pda.PaiSlot);
            ItemSlotsSystem.RemoveItemSlot(uid, pda.PaperSlot); // Arcane
        }

        protected virtual void OnItemInserted(EntityUid uid, PdaComponent pda, EntInsertedIntoContainerMessage args)
        {
            if (args.Container.ID == PdaComponent.PdaIdSlotId)
                pda.ContainedId = args.Entity;
            //goob addition for pen
            if (args.Container.ID == PdaComponent.PdaPenSlotId)
                pda.ContainedPen = args.Entity;
            // Arcane-Start
            if (args.Container.ID == PdaComponent.PdaPaperSlotId)
                pda.ContainedPaper = args.Entity;
            // Arcane-End
            UpdatePdaAppearance(uid, pda);
        }

        protected virtual void OnItemRemoved(EntityUid uid, PdaComponent pda, EntRemovedFromContainerMessage args)
        {
            if (args.Container.ID == pda.IdSlot.ID)
                pda.ContainedId = null;
            //goob addition for pen
            if (args.Container.ID == pda.PenSlot.ID)
                pda.ContainedPen = null;
            // Arcane-Start
            if (args.Container.ID == pda.PaperSlot.ID)
                pda.ContainedPaper = null;
            // Arcane-End
            UpdatePdaAppearance(uid, pda);
        }

        private void OnGetAdditionalAccess(EntityUid uid, PdaComponent component, ref GetAdditionalAccessEvent args)
        {
            if (component.ContainedId is { } id)
                args.Entities.Add(id);
        }

        private void UpdatePdaAppearance(EntityUid uid, PdaComponent pda)
        {
            Appearance.SetData(uid, PdaVisuals.IdCardInserted, pda.ContainedId != null);
            Appearance.SetData(uid, PdaVisuals.ScreenState, GetScreenState(uid)); // Orion
            //goob addition for pen
            Appearance.SetData(uid, PdaVisuals.PenInserted, pda.ContainedPen != null);
            Appearance.SetData(uid, PdaVisuals.PaperInserted, pda.ContainedPaper != null); // Arcane
        }

        // Orion-Start
        protected SpriteSpecifier GetScreenState(EntityUid uid)
        {
            if (!TryComp(uid, out CartridgeLoaderComponent? loader) || !loader.ActiveProgram.HasValue || !TryComp(loader.ActiveProgram.Value, out CartridgeComponent? cartridge) || cartridge.ScreenState == null)
                return FallbackScreenSprite;

            return cartridge.ScreenState;
        }
        // Orion-End

        public virtual void UpdatePdaUi(EntityUid uid, PdaComponent? pda = null)
        {
            // This does nothing yet while I finish up PDA prediction
            // Overriden by the server
        }
    }
}
