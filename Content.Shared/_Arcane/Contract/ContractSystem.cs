using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Paper;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Arcane.Contract;

public sealed partial class ContractSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private StampDisplayInfo _stampInfo = new()
    {
        StampedName = "Юр. Департамент ЦК",
        StampedColor = Color.DarkGreen,
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null || !_prototypeManager.TryIndex<JobPrototype>(args.JobId, out var job))
            return;

        if (job.Contracts == null)
            return;

        if (!_inventory.TryGetSlotEntity(args.Mob, "back", out var backUid) || !TryComp<StorageComponent>(backUid.Value, out var storage))
            return;

        foreach (var contract in job.Contracts)
        {
            var spawnedEntity = Spawn(contract);

            if (!TryComp<PaperComponent>(spawnedEntity, out var paper))
            {
                QueueDel(spawnedEntity);
                continue;
            }

            var signStamp = new StampDisplayInfo()
            {
                StampedName = MetaData(args.Mob).EntityName,
                StampedColor = Color.DarkGray,
            };

            _paper.TryStamp((spawnedEntity, paper), signStamp, "paper_stamp-signature");
            _paper.TryStamp((spawnedEntity, paper), _stampInfo, "paper_stamp-centcom");

            _metadata.SetEntityName(spawnedEntity, Loc.GetString("contract-paper-name", ("number", _random.Next(1000000, 9999999))));
            _metadata.SetEntityDescription(spawnedEntity, Loc.GetString("contract-paper-description"));

            _storage.Insert(backUid.Value, spawnedEntity, out _, storageComp: storage, playSound: false);
        }
    }
}
