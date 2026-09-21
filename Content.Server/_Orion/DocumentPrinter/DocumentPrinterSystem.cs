using Content.Server._Orion.Time;
using Content.Server.GameTicking;
using Content.Shared._Orion.DocumentPrinter;
using Content.Shared.Access.Components;
using Content.Shared.Inventory;
using Content.Shared.Paper;
using Content.Shared.PDA;
using Robust.Shared.Timing;

namespace Content.Server._Orion.DocumentPrinter;

public sealed class DocumentPrinterSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly TimeSystem _timeSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly PaperSystem _paper = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DocumentPrinterComponent, PrintingDocumentEvent>(OnPrinting);
    }

    private void OnPrinting(Entity<DocumentPrinterComponent> ent, ref PrintingDocumentEvent args)
    {
        if (!TryComp<PaperComponent>(args.Paper, out var paper))
            return;

        IdCardComponent? idCard = null;
        PdaComponent? pda = null;
        var timeText = string.Empty;

        if (ent.Comp.IsOnAutocomplete)
        {
            if (_inventory.TryGetSlotEntity(args.Actor, "id", out var slot))
            {
                TryComp(slot, out pda);
                TryComp(pda?.ContainedId ?? slot, out idCard);
            }

            var elapsed = _gameTiming.CurTime - _ticker.RoundStartTimeSpan;
            timeText = $@"{elapsed:hh\:mm\:ss} / {_timeSystem.GetStationDate():dd.MM.yyyy}";
        }

        var text = paper.Content.Replace("$time$", timeText);

        if (pda?.StationName is { } stationName)
            text = text.Replace("Station XX-000", stationName);

        _paper.SetContent((args.Paper, paper), text
            .Replace("$name$", idCard?.FullName ?? string.Empty)
            .Replace("$job$", idCard?.LocalizedJobTitle ?? string.Empty));
    }
}
