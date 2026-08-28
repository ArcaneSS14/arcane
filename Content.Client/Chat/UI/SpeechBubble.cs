// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using System.Text;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Speech;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Chat.UI
{
    public abstract class SpeechBubble : Control
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IEyeManager _eyeManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] protected readonly IConfigurationManager ConfigManager = default!;
        private readonly SharedTransformSystem _transformSystem;

        public enum SpeechType : byte
        {
            Emote,
            Say,
            Whisper,
            Looc
        }

        /// <summary>
        ///     The total time a speech bubble stays on screen.
        /// </summary>
        private static readonly TimeSpan TotalTime = TimeSpan.FromSeconds(4);

        /// <summary>
        ///     The amount of time at the end of the bubble's life at which it starts fading.
        /// </summary>
        private static readonly TimeSpan FadeTime = TimeSpan.FromSeconds(0.25f);

        // Arcane-start
        private const float RevealRunesPerSecond = 15.5f;
        private const float SpaceRevealWeight = 2.25f;
        private static readonly TimeSpan MaxRevealTime = TimeSpan.FromSeconds(4);
        protected virtual float RevealSpeedMultiplier => 1f;
        // Arcane-end

        /// <summary>
        ///     The distance in world space to offset the speech bubble from the center of the entity.
        ///     i.e. greater -> higher above the mob's head.
        /// </summary>
        private const float EntityVerticalOffset = 0.5f;

        /// <summary>
        ///     The default maximum width for speech bubbles.
        /// </summary>
        public const float SpeechMaxWidth = 256;

        private readonly EntityUid _senderEntity;

        /// <summary>
        /// The time at which this bubble will die.
        /// </summary>
        private TimeSpan _deathTime;
        // Arcane-start
        private readonly TimeSpan _creationTime;
        private readonly TimeSpan _revealTime;
        private readonly float _maxRevealWeight;
        private readonly List<SpeechTextReveal> _textReveals = new();
        // Arcane-end

        public float VerticalOffset { get; set; }
        private float _verticalOffsetAchieved;

        public Vector2 ContentSize { get; private set; }

        // man down
        public event Action<EntityUid, SpeechBubble>? OnDied;

        public static SpeechBubble CreateSpeechBubble(SpeechType type, ChatMessage message, EntityUid senderEntity)
        {
            switch (type)
            {
                case SpeechType.Emote:
                    return new TextSpeechBubble(message, senderEntity, "emoteBox");

                case SpeechType.Say:
                    return new FancyTextSpeechBubble(message, senderEntity, "sayBox");

                case SpeechType.Whisper:
                    return new FancyTextSpeechBubble(message, senderEntity, "whisperBox");

                case SpeechType.Looc:
                    return new TextSpeechBubble(message, senderEntity, "emoteBox"); // Orion-Edit: Removed color

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public SpeechBubble(ChatMessage message, EntityUid senderEntity, string speechStyleClass, Color? fontColor = null)
        {
            IoCManager.InjectDependencies(this);
            _senderEntity = senderEntity;
            _transformSystem = _entityManager.System<SharedTransformSystem>();

            // Use text clipping so new messages don't overlap old ones being pushed up.
            RectClipContent = true;

            var bubble = BuildBubble(message, speechStyleClass, fontColor);
            // Arcane-start
            bubble.HorizontalAlignment = HAlignment.Center;
            bubble.VerticalAlignment = VAlignment.Bottom;
            // Arcane-end

            AddChild(bubble);

            ForceRunStyleUpdate();

            bubble.Measure(Vector2Helpers.Infinity);
            ContentSize = bubble.DesiredSize;
            // Arcane-start
            bubble.MinWidth = ContentSize.X;
            _creationTime = _timing.RealTime;
            _maxRevealWeight = GetMaxRevealWeight();
            _revealTime = GetRevealTime(_maxRevealWeight);
            _deathTime = _creationTime + TotalTime + _revealTime;
            UpdateTextReveal();
            _verticalOffsetAchieved = -ContentSize.Y;
            // Arcane-end
        }

        protected abstract Control BuildBubble(ChatMessage message, string speechStyleClass, Color? fontColor = null);

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            var timeLeft = (float)(_deathTime - _timing.RealTime).TotalSeconds;
            if (_entityManager.Deleted(_senderEntity) || timeLeft <= 0)
            {
                // Timer spawn to prevent concurrent modification exception.
                Timer.Spawn(0, Die);
                return;
            }

            // Arcane-start
            UpdateTextReveal();
            // Arcane-end

            // Lerp to our new vertical offset if it's been modified.
            if (MathHelper.CloseToPercent(_verticalOffsetAchieved - VerticalOffset, 0, 0.1))
            {
                _verticalOffsetAchieved = VerticalOffset;
            }
            else
            {
                _verticalOffsetAchieved = MathHelper.Lerp(_verticalOffsetAchieved, VerticalOffset, 10 * args.DeltaSeconds);
            }

            if (!_entityManager.TryGetComponent<TransformComponent>(_senderEntity, out var xform) || xform.MapID != _eyeManager.CurrentEye.Position.MapId)
            {
                Modulate = Color.White.WithAlpha(0);
                return;
            }

            if (timeLeft <= FadeTime.TotalSeconds)
            {
                // Update alpha if we're fading.
                Modulate = Color.White.WithAlpha(timeLeft / (float)FadeTime.TotalSeconds);
            }
            else
            {
                // Make opaque otherwise, because it might have been hidden before
                Modulate = Color.White;
            }

            var baseOffset = 0f;

            if (_entityManager.TryGetComponent<SpeechComponent>(_senderEntity, out var speech))
                baseOffset = speech.SpeechBubbleOffset;

            var offset = (-_eyeManager.CurrentEye.Rotation).ToWorldVec() * -(EntityVerticalOffset + baseOffset);
            var worldPos = _transformSystem.GetWorldPosition(xform) + offset;

            var lowerCenter = _eyeManager.WorldToScreen(worldPos) / UIScale;
            var screenPos = lowerCenter - new Vector2(ContentSize.X / 2, ContentSize.Y + _verticalOffsetAchieved);
            // Round to nearest 0.5
            screenPos = (screenPos * 2).Rounded() / 2;
            LayoutContainer.SetPosition(this, screenPos);

            var height = MathF.Ceiling(MathHelper.Clamp(lowerCenter.Y - screenPos.Y, 0, ContentSize.Y));
            SetHeight = height;
        }

        private void Die()
        {
            if (Disposed)
            {
                return;
            }

            OnDied?.Invoke(_senderEntity, this);
        }

        // Arcane-start
        protected void SetRevealedMessage(RichTextLabel label, FormattedMessage message)
        {
            label.SetMessage(message, tagsAllowed: null);

            var revealWeight = CountRevealWeight(message);
            if (revealWeight <= 0f)
                return;

            _textReveals.Add(new SpeechTextReveal(label, message, revealWeight));
        }

        private float GetMaxRevealWeight()
        {
            var revealWeight = 0f;

            foreach (var reveal in _textReveals)
            {
                revealWeight = Math.Max(revealWeight, reveal.RevealWeight);
            }

            return revealWeight;
        }

        private TimeSpan GetRevealTime(float revealWeight)
        {
            if (revealWeight <= 0f)
                return TimeSpan.Zero;

            var seconds = revealWeight / (RevealRunesPerSecond * RevealSpeedMultiplier);
            return TimeSpan.FromSeconds(MathF.Min(seconds, (float) MaxRevealTime.TotalSeconds));
        }

        private void UpdateTextReveal()
        {
            if (_textReveals.Count == 0)
                return;

            var progress = _revealTime <= TimeSpan.Zero
                ? 1f
                : MathHelper.Clamp((float) ((_timing.RealTime - _creationTime).TotalSeconds / _revealTime.TotalSeconds), 0f, 1f);

            var visibleWeight = _maxRevealWeight * progress;

            foreach (var reveal in _textReveals)
            {
                var visibleRunes = CountVisibleRunes(reveal.Message, Math.Min(visibleWeight, reveal.RevealWeight));
                if (visibleRunes == reveal.LastVisibleRunes)
                    continue;

                reveal.LastVisibleRunes = visibleRunes;
                reveal.Label.SetMessage(CreateRevealedMessage(reveal.Message, visibleRunes), tagsAllowed: null);
            }
        }

        private static float CountRevealWeight(FormattedMessage message)
        {
            var weight = 0f;

            foreach (var node in message.Nodes)
            {
                if (node.Name != null || node.Value.StringValue == null)
                    continue;

                foreach (var rune in node.Value.StringValue.EnumerateRunes())
                {
                    weight += GetRevealWeight(rune);
                }
            }

            return weight;
        }

        private static int CountVisibleRunes(FormattedMessage message, float visibleWeight)
        {
            var remaining = visibleWeight;
            var count = 0;

            foreach (var node in message.Nodes)
            {
                if (node.Name != null)
                    continue;

                var text = node.Value.StringValue;
                if (text == null || remaining <= 0f)
                    continue;

                foreach (var rune in text.EnumerateRunes())
                {
                    if (remaining <= 0f)
                        break;

                    count++;
                    remaining -= GetRevealWeight(rune);
                }
            }

            return count;
        }

        private static FormattedMessage CreateRevealedMessage(FormattedMessage message, int visibleRunes)
        {
            var result = new FormattedMessage(message.Count);
            var remaining = visibleRunes;

            foreach (var node in message.Nodes)
            {
                if (node.Name != null)
                {
                    result.AddMarkupOrThrow(node.ToString());
                    continue;
                }

                var text = node.Value.StringValue;
                if (text == null)
                    continue;

                AddRevealedText(result, text, ref remaining);
            }

            return result;
        }

        private static void AddRevealedText(FormattedMessage result, string text, ref int remainingVisibleRunes)
        {
            var visible = new StringBuilder();
            var hidden = new StringBuilder();

            foreach (var rune in text.EnumerateRunes())
            {
                if (remainingVisibleRunes > 0)
                {
                    visible.Append(rune);
                    remainingVisibleRunes--;
                    continue;
                }

                hidden.Append(rune);
            }

            if (visible.Length > 0)
                result.AddText(visible.ToString());

            if (hidden.Length == 0)
                return;

            result.PushColor(Color.Transparent);
            result.AddText(hidden.ToString());
            result.Pop();
        }

        private static float GetRevealWeight(Rune rune)
        {
            return rune.Value is ' ' or '\n' or '\t'
                ? SpaceRevealWeight
                : 1f;
        }

        private sealed class SpeechTextReveal(RichTextLabel label, FormattedMessage message, float revealWeight)
        {
            public readonly RichTextLabel Label = label;
            public readonly FormattedMessage Message = message;
            public readonly float RevealWeight = revealWeight;
            public int LastVisibleRunes = -1;
        }
        // Arcane-end

        /// <summary>
        ///     Causes the speech bubble to start fading IMMEDIATELY.
        /// </summary>
        public void FadeNow()
        {
            if (_deathTime > _timing.RealTime)
            {
                _deathTime = _timing.RealTime + FadeTime;
            }
        }

        protected FormattedMessage FormatSpeech(string message, Color? fontColor = null)
        {
            var msg = new FormattedMessage();
            if (fontColor != null)
                msg.PushColor(fontColor.Value);
            msg.AddMarkupOrThrow(message);
            return msg;
        }

        protected FormattedMessage ExtractAndFormatSpeechSubstring(ChatMessage message, string tag, Color? fontColor = null)
        {
            return FormatSpeech(SharedChatSystem.GetStringInsideTag(message, tag), fontColor);
        }

    }

    public sealed class TextSpeechBubble : SpeechBubble
    {
        protected override float RevealSpeedMultiplier => 3f; // Arcane

        public TextSpeechBubble(ChatMessage message, EntityUid senderEntity, string speechStyleClass, Color? fontColor = null)
            : base(message, senderEntity, speechStyleClass, fontColor)
        {
        }

        protected override Control BuildBubble(ChatMessage message, string speechStyleClass, Color? fontColor = null)
        {
            var label = new RichTextLabel
            {
                MaxWidth = SpeechMaxWidth,
            };

            SetRevealedMessage(label, FormatSpeech(message.WrappedMessage, fontColor)); // Arcane

            var panel = new PanelContainer
            {
                StyleClasses = { "speechBox", speechStyleClass },
                Children = { label },
                ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleBackgroundOpacity))
            };

            return panel;
        }
    }

    public sealed class FancyTextSpeechBubble : SpeechBubble
    {

        public FancyTextSpeechBubble(ChatMessage message, EntityUid senderEntity, string speechStyleClass, Color? fontColor = null)
            : base(message, senderEntity, speechStyleClass, fontColor)
        {
        }

        protected override Control BuildBubble(ChatMessage message, string speechStyleClass, Color? fontColor = null)
        {
            if (!ConfigManager.GetCVar(CCVars.ChatEnableFancyBubbles))
            {
                var label = new RichTextLabel
                {
                    MaxWidth = SpeechMaxWidth
                };

                SetRevealedMessage(label, ExtractAndFormatSpeechSubstring(message, "BubbleContent", fontColor)); // Arcane

                var unfanciedPanel = new PanelContainer
                {
                    StyleClasses = { "speechBox", speechStyleClass },
                    Children = { label },
                    ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleBackgroundOpacity)),
                };
                return unfanciedPanel;
            }

            var bubbleHeader = new RichTextLabel
            {
                ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleSpeakerOpacity)),
                Margin = new Thickness(1, 1, 1, 1),
            };

            var bubbleContent = new RichTextLabel
            {
                ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleTextOpacity)),
                MaxWidth = SpeechMaxWidth,
                Margin = new Thickness(2, 2, 2, 2),
                StyleClasses = { "bubbleContent" },
            };

            //We'll be honest. *Yes* this is hacky. Doing this in a cleaner way would require a bottom-up refactor of how saycode handles sending chat messages. -Myr
            bubbleHeader.SetMessage(ExtractAndFormatSpeechSubstring(message, "BubbleHeader", fontColor));
            SetRevealedMessage(bubbleContent, ExtractAndFormatSpeechSubstring(message, "BubbleContent", fontColor)); // Arcane

            //As for below: Some day this could probably be converted to xaml. But that is not today. -Myr
            var mainPanel = new PanelContainer
            {
                StyleClasses = { "speechBox", speechStyleClass },
                Children = { bubbleContent },
                ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleBackgroundOpacity)),
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(4, 0, 4, 2) // Arcane
            };

            var headerPanel = new PanelContainer
            {
                StyleClasses = { "speechBox", speechStyleClass },
                Children = { bubbleHeader },
                ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.ChatFancyNameBackground) ? ConfigManager.GetCVar(CCVars.SpeechBubbleBackgroundOpacity) : 0f),
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Top
            };

            // Arcane-start
            var panel = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalAlignment = HAlignment.Center,
                Children = { headerPanel, mainPanel }
            };
            // Arcane-end

            return panel;
        }
    }
}
