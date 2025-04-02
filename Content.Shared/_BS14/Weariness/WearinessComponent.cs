using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Weariness.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;

namespace Content.Shared.Weariness.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(WearinessSystem))]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class WearinessComponent : Component
{
    /// <summary>
    /// The current Weariness amount of the entity
    /// </summary>
    [DataField("currentWeariness"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float CurrentWeariness;

    /// <summary>
    /// The base amount at which <see cref="CurrentWeariness"/> decays.
    /// </summary>
    [DataField("baseDecayRate"), ViewVariables(VVAccess.ReadWrite)]
    public float BaseDecayRate = 0.07143f;

    /// <summary>
    /// The actual amount at which <see cref="CurrentWeariness"/> decays.
    /// Affected by <seealso cref="CurrentThreshold"/>
    /// </summary>
    [DataField("actualDecayRate"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float ActualDecayRate;

    /// <summary>
    /// The last threshold this entity was at.
    /// Stored in order to prevent recalculating
    /// </summary>
    [DataField("lastThreshold"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public WearinessThreshold LastThreshold;

    /// <summary>
    /// The current Weariness threshold the entity is at
    /// </summary>
    [DataField("currentThreshold"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public WearinessThreshold CurrentThreshold;

    /// <summary>
    /// A dictionary relating WearinessThreshold to the amount of <see cref="CurrentWeariness"/> needed for each one
    /// </summary>
    [DataField("thresholds", customTypeSerializer: typeof(DictionarySerializer<WearinessThreshold, float>))]
    [AutoNetworkedField]
    public Dictionary<WearinessThreshold, float> Thresholds = new()
    {
        { WearinessThreshold.Energized, 200.0f },
        { WearinessThreshold.Okay, 150.0f },
        { WearinessThreshold.Tired, 100.0f },
        { WearinessThreshold.Exhausted, 50.0f },
        { WearinessThreshold.Dead, 0.0f }
    };

    /// <summary>
    /// A dictionary relating Weariness thresholds to corresponding alerts.
    /// </summary>
    [DataField("WearinessThresholdAlerts")]
    [AutoNetworkedField]
    public Dictionary<WearinessThreshold, ProtoId<AlertPrototype>> WearinessThresholdAlerts = new()
    {
        { WearinessThreshold.Tired, "Tired" },
        { WearinessThreshold.Exhausted, "Exhausted" },
        { WearinessThreshold.Dead, "Exhausted" }
    };

    [DataField]
    public ProtoId<AlertCategoryPrototype> WearinessAlertCategory = "Weariness";

    /// <summary>
    /// A dictionary relating WearinessThreshold to how much they modify <see cref="BaseDecayRate"/>.
    /// </summary>
    [DataField("WearinessThresholdDecayModifiers", customTypeSerializer: typeof(DictionarySerializer<WearinessThreshold, float>))]
    [AutoNetworkedField]
    public Dictionary<WearinessThreshold, float> WearinessThresholdDecayModifiers = new()
    {
        { WearinessThreshold.Energized, 1.2f },
        { WearinessThreshold.Okay, 1f },
        { WearinessThreshold.Tired, 0.8f },
        { WearinessThreshold.Exhausted, 0.6f },
        { WearinessThreshold.Dead, 0.6f }
    };

    /// <summary>
    /// The amount of slowdown applied when an entity is starving
    /// </summary>
    [DataField("starvingSlowdownModifier"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float ExhaustedSlowdownModifier = 0.5f;

    /// <summary>
    /// Damage dealt when your current threshold is at WearinessThreshold.Dead
    /// </summary>
    [DataField("starvationDamage")]
    public DamageSpecifier? StarvationDamage;

    /// <summary>
    /// The time when the Weariness will update next.
    /// </summary>
    [DataField("nextUpdateTime", customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan NextUpdateTime;

    /// <summary>
    /// The time between each update.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(1);
}

[Serializable, NetSerializable]
public enum WearinessThreshold : byte
{
    Energized = 1 << 3,
    Okay = 1 << 2,
    Tired = 1 << 1,
    Exhausted = 1 << 0,
    Dead = 0,
}
