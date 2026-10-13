// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Stylesheets;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Actions.UI
{
    /// <summary>
    /// Tooltip for actions or alerts because they are very similar.
    /// </summary>
    public sealed class ActionAlertTooltip : PanelContainer
    {
        private const float TooltipTextMaxWidth = 350;

        private readonly RichTextLabel _cooldownLabel;
        private readonly IGameTiming _gameTiming;

        // Arcane-Start
        private int _lastCooldownDuration = -1;
        private int _lastCooldownTimeLeft = -1;
        // Arcane-End

        /// <summary>
        /// Current cooldown displayed in this tooltip. Set to null to show no cooldown.
        /// </summary>
        public (TimeSpan Start, TimeSpan End)? Cooldown { get; set; }

        public ActionAlertTooltip(FormattedMessage name, FormattedMessage? desc, string? requires = null)
        {
            Stylesheet = IoCManager.Resolve<IStylesheetManager>().SheetSystem;
            _gameTiming = IoCManager.Resolve<IGameTiming>();

            SetOnlyStyleClass(StyleClass.TooltipPanel);

            BoxContainer vbox;
            AddChild(vbox = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                RectClipContent = true
            });
            var nameLabel = new RichTextLabel
            {
                MaxWidth = TooltipTextMaxWidth,
                StyleClasses = { StyleClass.TooltipTitle }
            };
            nameLabel.SetMessage(name);
            vbox.AddChild(nameLabel);

            if (desc != null && !string.IsNullOrWhiteSpace(desc.ToString()))
            {
                var description = new RichTextLabel
                {
                    MaxWidth = TooltipTextMaxWidth,
                    StyleClasses = { StyleClass.TooltipDesc }
                };
                description.SetMessage(desc);
                vbox.AddChild(description);
            }

            vbox.AddChild(_cooldownLabel = new RichTextLabel
            {
                MaxWidth = TooltipTextMaxWidth,
                StyleClasses = { StyleClass.TooltipDesc },
                Visible = false
            });

            if (!string.IsNullOrWhiteSpace(requires))
            {
                var requiresLabel = new RichTextLabel
                {
                    MaxWidth = TooltipTextMaxWidth,
                    StyleClasses = { StyleClass.TooltipDesc }
                };

                if (!FormattedMessage.TryFromMarkup("[color=#635c5c]" + requires + "[/color]", out var markup))
                    return;

                requiresLabel.SetMessage(markup);

                vbox.AddChild(requiresLabel);
            }
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            if (!Cooldown.HasValue)
            {
                HideCooldown(); // Arcane-Edit
                return;
            }

            var timeLeft = Cooldown.Value.End - _gameTiming.CurTime;
            if (timeLeft > TimeSpan.Zero)
            {
                var duration = Cooldown.Value.End - Cooldown.Value.Start;
                // Arcane-Start
                var durationSeconds = (int) duration.TotalSeconds;
                var timeLeftSeconds = (int) timeLeft.TotalSeconds + 1;

                if (_lastCooldownDuration == durationSeconds && _lastCooldownTimeLeft == timeLeftSeconds)
                    return;

                _lastCooldownDuration = durationSeconds;
                _lastCooldownTimeLeft = timeLeftSeconds;
                // Arcane-End

                if (!FormattedMessage.TryFromMarkup(Loc.GetString("ui-actionslot-duration", ("duration", durationSeconds), ("timeLeft", timeLeftSeconds)), out var markup)) // Arcane-End
                    return;

                _cooldownLabel.SetMessage(markup);
                _cooldownLabel.Visible = true;
            }
            else
            {
            // Arcane-Start
                HideCooldown();
            }
        }

        private void HideCooldown()
        {
            _lastCooldownDuration = -1;
            _lastCooldownTimeLeft = -1;
            // Arcane-End
            _cooldownLabel.Visible = false;
        }
    }
}
