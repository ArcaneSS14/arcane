using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;

namespace Content.Client._Arcane.Administration.UI.Bwoink;

public sealed class BwoinkRoundSeparatorTag : IMarkupTagHandler
{
    public string Name => "bwoinkround";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        if (node.Closing || !node.Value.TryGetLong(out var roundId) || roundId is < 1 or > int.MaxValue)
        {
            control = null;
            return false;
        }

        control = new RoundSeparatorControl((int) roundId.Value);
        return true;
    }

    private sealed class RoundSeparatorControl : Container
    {
        private readonly RichTextLabel _label;

        public RoundSeparatorControl(int roundId)
        {
            _label = new RichTextLabel
            {
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0, 4)
            };
            _label.SetMessage(Loc.GetString("bwoink-history-round-separator", ("round", roundId)), Color.Gray);
            AddChild(_label);
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            _label.Measure(availableSize);
            return new Vector2(availableSize.X, _label.DesiredSize.Y);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            _label.Arrange(UIBox2.FromDimensions(Vector2.Zero, finalSize));
            return finalSize;
        }
    }
}
