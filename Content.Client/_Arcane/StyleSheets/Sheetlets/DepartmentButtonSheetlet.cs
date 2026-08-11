// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

/// <summary>
/// Department-colored buttons used by the ghost teleport menu and access level controls.
/// The style class name is driven by <see cref="DepartmentPrototype.ButtonStyle"/>.
/// </summary>
[CommonSheetlet]
public sealed class DepartmentButtonSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet
{
    private static readonly Color CentralCommand = Color.FromHex("#185e1f");
    private static readonly Color Command = Color.FromHex("#1d4776");
    private static readonly Color Security = Color.FromHex("#991d1d");
    private static readonly Color Medical = Color.FromHex("#3e86ad");
    private static readonly Color Engineering = Color.FromHex("#998011");
    private static readonly Color Cargo = Color.FromHex("#A46106");
    private static readonly Color Science = Color.FromHex("#84287a");
    private static readonly Color Silicon = Color.FromHex("#08766b");
    private static readonly Color Civilian = Color.FromHex("#508264");
    private static readonly Color Justice = Color.FromHex("#9f2342");
    private static readonly Color Legal = Color.FromHex("#c22b51");
    private static readonly Color Specific = Color.FromHex("#767676");
    private static readonly Color Antagonist = Color.FromHex("#6f3a3a");

    public override StyleRule[] GetRules(T sheet, object config)
    {
        return
        [
            // CentralCommand
            E<Button>().Class("ButtonColorCentralCommandDepartment").Modulate(CentralCommand),
            E<Button>().Class("ButtonColorCentralCommandDepartment").PseudoNormal().Modulate(CentralCommand),

            // Command
            E<Button>().Class("ButtonColorCommandDepartment").Modulate(Command),
            E<Button>().Class("ButtonColorCommandDepartment").PseudoNormal().Modulate(Command),

            // Security
            E<Button>().Class("ButtonColorSecurityDepartment").Modulate(Security),
            E<Button>().Class("ButtonColorSecurityDepartment").PseudoNormal().Modulate(Security),

            // Medical
            E<Button>().Class("ButtonColorMedicalDepartment").Modulate(Medical),
            E<Button>().Class("ButtonColorMedicalDepartment").PseudoNormal().Modulate(Medical),

            // Engineering
            E<Button>().Class("ButtonColorEngineeringDepartment").Modulate(Engineering),
            E<Button>().Class("ButtonColorEngineeringDepartment").PseudoNormal().Modulate(Engineering),

            // Science
            E<Button>().Class("ButtonColorScienceDepartment").Modulate(Science),
            E<Button>().Class("ButtonColorScienceDepartment").PseudoNormal().Modulate(Science),

            // Silicon
            E<Button>().Class("ButtonColorSiliconDepartment").Modulate(Silicon),
            E<Button>().Class("ButtonColorSiliconDepartment").PseudoNormal().Modulate(Silicon),

            // Civilian
            E<Button>().Class("ButtonColorCivilianDepartment").Modulate(Civilian),
            E<Button>().Class("ButtonColorCivilianDepartment").PseudoNormal().Modulate(Civilian),

            // Cargo
            E<Button>().Class("ButtonColorCargoDepartment").Modulate(Cargo),
            E<Button>().Class("ButtonColorCargoDepartment").PseudoNormal().Modulate(Cargo),

            // Justice
            E<Button>().Class("ButtonColorJusticeDepartment").Modulate(Justice),
            E<Button>().Class("ButtonColorJusticeDepartment").PseudoNormal().Modulate(Justice),

            // Legal
            E<Button>().Class("ButtonColorLegalDepartment").Modulate(Legal),
            E<Button>().Class("ButtonColorLegalDepartment").PseudoNormal().Modulate(Legal),

            // Specific
            E<Button>().Class("ButtonColorSpecificDepartment").Modulate(Specific),
            E<Button>().Class("ButtonColorSpecificDepartment").PseudoNormal().Modulate(Specific),

            // Antagonist
            E<Button>().Class("ButtonColorAntagonistDepartment").Modulate(Antagonist),
            E<Button>().Class("ButtonColorAntagonistDepartment").PseudoNormal().Modulate(Antagonist),
        ];
    }
}
