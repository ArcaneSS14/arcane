// SPDX-License-Identifier: MIT

namespace Content.Shared.Speech
{
    public sealed class SpeakAttemptEvent : CancellableEntityEventArgs
    {
        public SpeakAttemptEvent(EntityUid uid, bool isWhisper = false) // Arcane-Edit
        {
            Uid = uid;
            IsWhisper = isWhisper; // Arcane
        }

        public EntityUid Uid { get; }

        // Arcane-Start
        /// <summary>
        ///     Whether this speech attempt is a whisper.
        /// </summary>
        public bool IsWhisper { get; }
        // Arcane-End
    }
}
