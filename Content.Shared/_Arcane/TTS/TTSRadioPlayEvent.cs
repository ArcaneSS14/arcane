using Content.Shared._EinsteinEngines.Language;
using Robust.Shared.Player;

namespace Content.Shared._Arcane.TTS;

[ByRefEvent]
public readonly record struct TTSRadioPlayEvent(string Message, LanguagePrototype Language, string Voice);

[ByRefEvent]
public readonly record struct TTSAnnouncePlayEvent(string Message, EntityUid? Sender, Filter Receievers);
