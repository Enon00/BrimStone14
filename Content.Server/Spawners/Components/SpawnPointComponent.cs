using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Spawners.Components;

[RegisterComponent]
public sealed partial class SpawnPointComponent : Component, ISpawnPoint
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("job_id")]
    private string? _jobId;

    /// <summary>
    /// The type of spawn point
    /// </summary>
    [DataField("spawn_type"), ViewVariables(VVAccess.ReadWrite)]
    public SpawnPointType SpawnType { get; set; } = SpawnPointType.Unset;

    /// <summary>
    /// Whether this spawn point should be deleted after spawning a player.
    /// </summary>
    [DataField("delete_on_spawn"), ViewVariables(VVAccess.ReadWrite)]
    public bool DeleteOnSpawn { get; set; } = false;

    public JobPrototype? Job => string.IsNullOrEmpty(_jobId) ? null : _prototypeManager.Index<JobPrototype>(_jobId);

    public override string ToString()
    {
        return $"{_jobId} {SpawnType}";
    }
}

public enum SpawnPointType
{
    Unset = 0,
    LateJoin,
    Job,
    Observer,
}
