using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems;

public sealed class SpawnPointSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning);
    }

    private void OnPlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null)
            return;

        // TODO: Cache all this if it ends up important.
        var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        var possibleSpawns = new List<(EntityUid Uid, EntityCoordinates Coordinates, SpawnPointComponent SpawnPoint)>();

        while (points.MoveNext(out var uid, out var spawnPoint, out var xform))
        {
            if (args.Station != null && _stationSystem.GetOwningStation(uid, xform) != args.Station)
                continue;

            if (args.DesiredSpawnPointType != SpawnPointType.Unset)
            {
                var isMatchingJob = spawnPoint.SpawnType == SpawnPointType.Job &&
                    (args.Job == null || spawnPoint.Job?.ID == args.Job.Prototype);

                switch (args.DesiredSpawnPointType)
                {
                    case SpawnPointType.Job when isMatchingJob:
                    case SpawnPointType.LateJoin when spawnPoint.SpawnType == SpawnPointType.LateJoin:
                    case SpawnPointType.Observer when spawnPoint.SpawnType == SpawnPointType.Observer:
                        possibleSpawns.Add((uid, xform.Coordinates, spawnPoint));
                        break;
                    default:
                        continue;
                }
            }
        }

        if (possibleSpawns.Count == 0)
        {
            Log.Error("No valid spawn points found!");
            return;
        }

        // Select a random spawn point
        var (selectedUid, spawnLoc, selectedSpawnPoint) = _random.Pick(possibleSpawns);

        // Spawn the player mob
        args.SpawnResult = _stationSpawning.SpawnPlayerMob(
            spawnLoc,
            args.Job,
            args.HumanoidCharacterProfile,
            args.Station);

        // If the spawn was successful and `DeleteOnSpawn` is enabled, delete the entity
        if (args.SpawnResult != null && selectedSpawnPoint.DeleteOnSpawn)
        {
            EntityManager.DeleteEntity(selectedUid);
        }
    }
}
