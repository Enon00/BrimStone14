using System.Diagnostics.CodeAnalysis;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Weariness.Components;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Content.Shared.Mood;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Configuration;
using Content.Shared.CCVar;
using Content.Shared.Bed.Sleep;


namespace Content.Shared.Weariness.EntitySystems;

public sealed class WearinessSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private readonly SharedJetpackSystem _jetpack = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;

    [ValidatePrototypeId<SatiationIconPrototype>]
    private const string WearinessIconEnergizedId = "WearinessIconEnergized";

    [ValidatePrototypeId<SatiationIconPrototype>]
    private const string WearinessIconTiredId = "WearinessIconTired";

    [ValidatePrototypeId<SatiationIconPrototype>]
    private const string WearinessIconExhaustedId = "WearinessIconExhausted";

    private SatiationIconPrototype? _wearinessIconEnergized;
    private SatiationIconPrototype? _wearinessIconTired;
    private SatiationIconPrototype? _wearinessIconExhausted;

    public override void Initialize()
    {
        base.Initialize();

        DebugTools.Assert(_prototype.TryIndex(WearinessIconEnergizedId, out _wearinessIconEnergized) &&
                          _prototype.TryIndex(WearinessIconTiredId, out _wearinessIconTired) &&
                          _prototype.TryIndex(WearinessIconExhaustedId, out _wearinessIconExhausted));

        SubscribeLocalEvent<WearinessComponent, SleepStateChangedEvent>(OnSleepStateChanged);
        SubscribeLocalEvent<WearinessComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<WearinessComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<WearinessComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<WearinessComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnMapInit(EntityUid uid, WearinessComponent component, MapInitEvent args)
    {
        var amount = _random.Next(
            (int) component.Thresholds[WearinessThreshold.Tired] + 10,
            (int) component.Thresholds[WearinessThreshold.Okay]);
        SetWeariness(uid, amount, component);
    }

    private void OnShutdown(EntityUid uid, WearinessComponent component, ComponentShutdown args)
    {
        _alerts.ClearAlertCategory(uid, component.WearinessAlertCategory);
    }

    private void OnRefreshMovespeed(EntityUid uid, WearinessComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (_config.GetCVar(CCVars.MoodEnabled)
            || component.CurrentThreshold > WearinessThreshold.Exhausted
            || _jetpack.IsUserFlying(uid))
            return;

        args.ModifySpeed(component.ExhaustedSlowdownModifier, component.ExhaustedSlowdownModifier);
    }

    private void OnRejuvenate(EntityUid uid, WearinessComponent component, RejuvenateEvent args)
    {
        SetWeariness(uid, component.Thresholds[WearinessThreshold.Okay], component);
    }

    /// <summary>
    /// Adds to the current hunger of an entity by the specified value
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="amount"></param>
    /// <param name="component"></param>
    public void ModifyWeariness(EntityUid uid, float amount, WearinessComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;
        SetWeariness(uid, component.CurrentWeariness + amount, component);
    }

    /// <summary>
    /// Sets the current hunger of an entity to the specified value
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="amount"></param>
    /// <param name="component"></param>
    public void SetWeariness(EntityUid uid, float amount, WearinessComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;
        component.CurrentWeariness = Math.Clamp(amount,
            component.Thresholds[WearinessThreshold.Dead],
            component.Thresholds[WearinessThreshold.Energized]);
        UpdateCurrentThreshold(uid, component);
        Dirty(uid, component);
    }

    private void UpdateCurrentThreshold(EntityUid uid, WearinessComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var calculatedWearinessThreshold = GetWearinessThreshold(component);
        if (calculatedWearinessThreshold == component.CurrentThreshold)
            return;
        component.CurrentThreshold = calculatedWearinessThreshold;
        DoWearinessThresholdEffects(uid, component);
        Dirty(uid, component);
    }

    private void DoWearinessThresholdEffects(EntityUid uid, WearinessComponent? component = null, bool force = false)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.CurrentThreshold == component.LastThreshold && !force)
            return;

        if (GetMovementThreshold(component.CurrentThreshold) != GetMovementThreshold(component.LastThreshold))
        {
            if (!_config.GetCVar(CCVars.MoodEnabled))
                _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
            else if (_net.IsServer)
            {
                var ev = new MoodEffectEvent("Weariness" + component.CurrentThreshold);
                RaiseLocalEvent(uid, ev);
            }
        }

        if (component.WearinessThresholdAlerts.TryGetValue(component.CurrentThreshold, out var alertId))
        {
            _alerts.ShowAlert(uid, alertId);
        }
        else
        {
            _alerts.ClearAlertCategory(uid, component.WearinessAlertCategory);
        }

        if (component.WearinessThresholdDecayModifiers.TryGetValue(component.CurrentThreshold, out var modifier))
        {
            component.ActualDecayRate = component.BaseDecayRate * modifier;
        }

        component.LastThreshold = component.CurrentThreshold;
    }

    private void DoContinuousWearinessEffects(EntityUid uid, WearinessComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.CurrentThreshold <= WearinessThreshold.Exhausted &&
            component.StarvationDamage is { } damage &&
            !_mobState.IsDead(uid))
        {
            _damageable.TryChangeDamage(uid, damage, true, false);
        }
    }

    /// <summary>
    /// Gets the Weariness threshold for an entity based on the amount of food specified.
    /// If a specific amount isn't specified, just uses the current Weariness of the entity
    /// </summary>
    /// <param name="component"></param>
    /// <param name="food"></param>
    /// <returns></returns>
    public WearinessThreshold GetWearinessThreshold(WearinessComponent component, float? food = null)
    {
        food ??= component.CurrentWeariness;
        var result = WearinessThreshold.Dead;
        var value = component.Thresholds[WearinessThreshold.Energized];
        foreach (var threshold in component.Thresholds)
        {
            if (threshold.Value <= value && threshold.Value >= food)
            {
                result = threshold.Key;
                value = threshold.Value;
            }
        }
        return result;
    }

    /// <summary>
    /// A check that returns if the entity is below a hunger threshold.
    /// </summary>
    public bool IsWearinessBelowState(EntityUid uid, WearinessThreshold threshold, float? food = null, WearinessComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false; // It's never going to go hungry, so it's probably fine to assume that it's not... you know, hungry.

        return GetWearinessThreshold(comp, food) < threshold;
    }

    private bool GetMovementThreshold(WearinessThreshold threshold)
    {
        switch (threshold)
        {
            case WearinessThreshold.Energized:
            case WearinessThreshold.Okay:
                return true;
            case WearinessThreshold.Tired:
            case WearinessThreshold.Exhausted:
            case WearinessThreshold.Dead:
                return false;
            default:
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold, null);
        }
    }

    public bool TryGetStatusIconPrototype(WearinessComponent component, [NotNullWhen(true)] out SatiationIconPrototype? prototype)
    {
        switch (component.CurrentThreshold)
        {
            case WearinessThreshold.Energized:
                prototype = _wearinessIconEnergized;
                break;
            case WearinessThreshold.Tired:
                prototype = _wearinessIconTired;
                break;
            case WearinessThreshold.Exhausted:
                prototype = _wearinessIconExhausted;
                break;
            default:
                prototype = null;
                break;
        }

        return prototype != null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WearinessComponent>();
        while (query.MoveNext(out var uid, out var weariness))
        {
            if (_timing.CurTime < weariness.NextUpdateTime)
                continue;
            weariness.NextUpdateTime = _timing.CurTime + weariness.UpdateRate;

            ModifyWeariness(uid, -weariness.ActualDecayRate, weariness);
            DoContinuousWearinessEffects(uid, weariness);
        }
    }
    private void OnSleepStateChanged(EntityUid uid, WearinessComponent component, SleepStateChangedEvent args)
    {
        if (HasComp<SleepingComponent>(uid))
        {
            component.ActualDecayRate = -2f;  // Set decay rate when sleeping
        }
        else
        {
            component.ActualDecayRate = 0.1f;  // Set decay rate when not sleeping
        }
    }
}
