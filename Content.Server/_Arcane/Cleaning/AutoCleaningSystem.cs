using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared._Arcane.CCVars;
using Content.Shared.GameTicking;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Log;

namespace Content.Server._Arcane.Cleaning;

public sealed partial class AutoCleaningSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    // Логгер
    private ISawmill _sawmill = default!;

    private bool _autoCleaningEnabled = false;
    private bool _isActive = false;
    private bool _isWarned = false;
    private static TimeSpan _nextUpdate = TimeSpan.MaxValue;
    private static TimeSpan _updateInterval = TimeSpan.FromMinutes(20);
    private static TimeSpan _warningWaiting = TimeSpan.FromSeconds(10);
    private static HashSet<ProtoId<TagPrototype>> _cleaningTags = ["Trash", "Cartridge"];
    private static HashSet<ProtoId<TagPrototype>> _disallowedTags = ["Cigarette", "CigPack", "Syringe", "LightTube", "LightBulb", "VimPilot"];

    public override void Initialize()
    {
        base.Initialize();

        // Инициализируем логгер
        _sawmill = Logger.GetSawmill("autocleaning");

        Subs.CVar(_cfg, ACCVars.AutoCleaningEnabled, SetAutoCleaningEnabled, true);

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnded);
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        _nextUpdate = _timing.CurTime + _updateInterval;
        _isActive = true;
        _isWarned = false;

        _sawmill.Info("AutoCleaning system activated for the round");
    }

    private void OnRoundEnded(RoundEndedEvent args)
    {
        _isActive = false;
        _isWarned = false;
        _nextUpdate = TimeSpan.MaxValue;

        _sawmill.Info("AutoCleaning system deactivated (round ended)");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_isActive || !_autoCleaningEnabled)
            return;

        if (_nextUpdate < _timing.CurTime)
        {
            if (_isWarned)
            {
                ProccessCleaning();
                return;
            }

            _nextUpdate = _timing.CurTime + _warningWaiting;
            _isWarned = true;

            _chat.DispatchGlobalAnnouncement(Loc.GetString("cent-com-cleaning-warning", ("seconds", _warningWaiting.Seconds)), playSound: false, colorOverride: Color.Aqua);

            _sawmill.Info($"AutoCleaning warning sent. Cleaning in {_warningWaiting.Seconds} seconds");
        }
    }

    private void ProccessCleaning()
    {
        _nextUpdate = _timing.CurTime + _updateInterval;
        _isWarned = false;

        _sawmill.Info("Starting floor cleaning cycle...");

        var cleanedCount = CleanFloorItems();

        if (cleanedCount > 0)
        {
            _chat.DispatchGlobalAnnouncement(
                Loc.GetString("cent-com-cleaning-announce", ("count", cleanedCount)),
                playSound: false,
                colorOverride: Color.Aqua
            );

            _sawmill.Info($"Cleaning completed: {cleanedCount} items removed from floor");
        }
        else
        {
            _sawmill.Info("Cleaning completed: no items to remove");
        }
    }

    private int CleanFloorItems()
    {
        var deletedCount = 0;
        var skippedContainer = 0;
        var skippedAnchored = 0;
        var skippedDisallowed = 0;

        var query = EntityQueryEnumerator<TagComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var tag, out var transform))
        {
            // Пропускаем, если нет нужных тегов или есть запрещённые
            if (!tag.Tags.Intersect(_cleaningTags).Any())
                continue;

            if (tag.Tags.Intersect(_disallowedTags).Any())
            {
                skippedDisallowed++;
                continue;
            }

            // Объект внутри контейнера/инвентаря — пропускаем
            if (transform.ParentUid != transform.GridUid && transform.ParentUid != transform.MapUid)
            {
                skippedContainer++;
                continue;
            }

            // Объект прикручен (лампы, трубы и т.д.) — пропускаем
            if (transform.Anchored)
            {
                skippedAnchored++;
                continue;
            }

            // Объект лежит на полу — удаляем
            QueueDel(uid);
            deletedCount++;
        }

        // Подробное логирование
        _sawmill.Debug($"Cleaning stats: deleted={deletedCount}, skipped (container={skippedContainer}, anchored={skippedAnchored}, disallowed={skippedDisallowed})");
        return deletedCount;
    }

    private void SetAutoCleaningEnabled(bool value)
    {
        _autoCleaningEnabled = value;
        _sawmill.Info($"AutoCleaning enabled set to: {value}");
    }
}
