// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Shitcode.UserActions.Controls;

[Virtual]
public class IconButton : Button
{
    private readonly BoxContainer _mainContainer;

    public readonly TextureRect Icon;
    public readonly Label Label; // # Arcane-Edit

    public IconButton(string name)
    {
        // # Arcane-Edit-Start
        HorizontalExpand = true;
        MinSize = new Vector2(120, 32);
        MaxSize = new Vector2(float.PositiveInfinity, 32);
        // # Arcane-Edit-End
        Margin = new Thickness(1);
        HorizontalAlignment = HAlignment.Left;

        _mainContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            // # Arcane-Edit-Start
            HorizontalExpand = true,
            MinSize = new Vector2(120, 32),
            MaxSize = new Vector2(float.PositiveInfinity, 32),
            // # Arcane-Edit-End
            Margin = new Thickness(0)
        };
        AddChild(_mainContainer);

        Icon = new TextureRect
        {
            HorizontalExpand = false,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Center,
            Stretch = TextureRect.StretchMode.Scale,
            Margin = new Thickness(4, 0, 4, 0),
            TextureScale = new Vector2(1, 1),
            MinSize = new Vector2(20, 20),
            MaxSize = new Vector2(20, 20),
            Visible = true
        };
        _mainContainer.AddChild(Icon);

        // # Arcane-Edit-Start
        Label = new Label
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Text = name,
            Visible = true
        };
        // # Arcane-Edit-End
        _mainContainer.AddChild(Label);
    }
}
