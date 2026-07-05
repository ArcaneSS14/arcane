using Content.Shared._EinsteinEngines.Language;
using Content.Shared.Chat;
using Robust.Shared.Player;

namespace Content.Shared._Art.TTS;

public sealed class TTSRadioPlayEvent(Filter filter, string message, LanguagePrototype language, string voice) : EntityEventArgs
{
    public Filter Recievers { get; } = filter;
    public string Message { get; } = message;
    public LanguagePrototype Language { get; } = language;
    public string Voice { get; } = voice;
}

public sealed class TTSAnnouncePlayEvent(string message, EntityUid? sender, Filter filter) : EntityEventArgs
{
    public string Message { get; } = message;
    public EntityUid? Sender { get; } = sender;
    public Filter Recievers { get; } = filter;
}
