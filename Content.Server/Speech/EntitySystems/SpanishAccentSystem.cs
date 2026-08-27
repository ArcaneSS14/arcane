// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using Content.Server.Speech.Components;
using Content.Shared.Speech;

namespace Content.Server.Speech.EntitySystems
{
    public sealed class SpanishAccentSystem : EntitySystem
    {
        public override void Initialize()
        {
            SubscribeLocalEvent<SpanishAccentComponent, AccentGetEvent>(OnAccent);
        }

        public string Accentuate(string message)
        {
            // Insert E before every S
            message = InsertS(message);
            // If a sentence ends with ?, insert a reverse ? at the beginning of the sentence
            message = ReplacePunctuation(message);
            return message;
        }

        private string InsertS(string message)
        {
            // Arcane-Edit-Start
            // Replace every new Word that starts with s/S
            var msg = message.Replace(" с", " ес").Replace(" С", " Ес");

            // Still need to check if the beginning of the message starts
            if (msg.StartsWith("с", StringComparison.Ordinal))
            {
                return msg.Remove(0, 1).Insert(0, "ес");
            }

            if (msg.StartsWith("С", StringComparison.Ordinal))
            {
                return msg.Remove(0, 1).Insert(0, "Ес");
            }
            // Arcane-Edit-End

            return msg;
        }

        private string ReplacePunctuation(string message)
        {
            var sentences = AccentSystem.SentenceRegex.Split(message);
            var msg = new StringBuilder();
            foreach (var s in sentences)
            {
                var toInsert = new StringBuilder();
                for (var i = s.Length - 1; i >= 0 && "?!‽".Contains(s[i]); i--)
                {
                    toInsert.Append(s[i] switch
                    {
                        '?' => '¿',
                        '!' => '¡',
                        '‽' => '⸘',
                        _ => ' '
                    });
                }
                if (toInsert.Length == 0)
                {
                    msg.Append(s);
                } else
                {
                    msg.Append(s.Insert(s.Length - s.TrimStart().Length, toInsert.ToString()));
                }
            }
            return msg.ToString();
        }

        private void OnAccent(EntityUid uid, SpanishAccentComponent component, AccentGetEvent args)
        {
            args.Message = Accentuate(args.Message);
        }
    }
}
