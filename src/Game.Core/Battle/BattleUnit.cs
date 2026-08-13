using Game.Core;
using Game.Core.Affix;
using Game.Core.Model;
using Game.Core.Model.Character;

namespace Game.Core.Battle;

public sealed class BattleUnit
{
    public const int MaxRage = CharacterInstance.MaxBattleRage;

    private readonly List<BattleBuffInstance> _buffs = [];
    private readonly HashSet<string> _disabledSkillIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _abilityUsageCounts = new(StringComparer.Ordinal);
    private BattleEffectProjectionResolver? _projectionResolver;
    private AffixProjection? _localProjection;

    public BattleUnit(
        string id,
        CharacterInstance character,
        int team,
        GridPosition position,
        BattleFacing facing = BattleFacing.Right,
        int? hp = null,
        int? mp = null,
        int rage = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(character);
        ArgumentOutOfRangeException.ThrowIfNegative(rage);

        Id = id;
        Character = character;
        Team = team;
        Position = position;
        Facing = facing;
        AiType = character.AiType;

        var initialMaxHp = MaxHp;
        var initialMaxMp = MaxMp;
        Hp = Math.Clamp(hp ?? initialMaxHp, 0, initialMaxHp);
        Mp = Math.Clamp(mp ?? initialMaxMp, 0, initialMaxMp);
        Rage = Math.Clamp(rage, 0, MaxRage);
    }

    public string Id { get; }

    public CharacterInstance Character { get; }

    public int Team { get; }

    public GridPosition Position { get; internal set; }

    public BattleFacing Facing { get; internal set; }

    public BattleAiType AiType { get; private set; }

    public int MaxHp => ResolveMaxHp();

    public int Hp { get; private set; }

    public int MaxMp => ResolveMaxMp();

    public int Mp { get; private set; }

    public int Rage { get; private set; }

    public double ActionSpeed => GetActionSpeed();

    public int MovePower => GetMovePower();

    public double ActionGauge { get; set; }

    public int ItemCooldown { get; private set; }

    public string? LastUsedSkillId { get; private set; }

    public bool IsAlive => Hp > 0;

    public IReadOnlyList<BattleBuffInstance> Buffs => _buffs;

    public IEnumerable<BattleBuffInstance> ActiveBuffs => _buffs.Where(static buff => !buff.IsExpired);

    public IReadOnlySet<string> DisabledSkillIds => _disabledSkillIds;

    public IReadOnlyDictionary<string, int> AbilityUsageCounts => _abilityUsageCounts;

    public void SetAiType(BattleAiType aiType)
    {
        AiType = aiType;
    }

    public bool HasTrait(TraitId traitId) => _projectionResolver is null
        ? DetachedProjection.Traits.Contains(traitId)
        : Resolver.HasTrait(this, traitId);

    internal void BindProjectionResolver(BattleEffectProjectionResolver resolver) =>
        _projectionResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    public void AddDisabledSkill(string skillId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId);
        _disabledSkillIds.Add(skillId);
    }

    public bool RemoveDisabledSkill(string skillId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId);
        return _disabledSkillIds.Remove(skillId);
    }

    public int GetAbilityUsageCount(string abilityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(abilityId);
        return _abilityUsageCounts.GetValueOrDefault(abilityId);
    }

    public int RecordAbilityUsage(string abilityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(abilityId);
        var count = checked(GetAbilityUsageCount(abilityId) + 1);
        _abilityUsageCounts[abilityId] = count;
        return count;
    }

    internal void ClampResourcesToLimits()
    {
        Hp = Math.Clamp(Hp, 0, MaxHp);
        Mp = Math.Clamp(Mp, 0, MaxMp);
    }

    public void SpendMp(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (Mp < amount)
        {
            throw new InvalidOperationException($"Unit '{Id}' does not have enough MP.");
        }

        Mp -= amount;
    }

    public void SpendRage(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (Rage < amount)
        {
            throw new InvalidOperationException($"Unit '{Id}' does not have enough rage.");
        }

        Rage -= amount;
    }

    public int RestoreHp(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var before = Hp;
        Hp = Math.Min(MaxHp, Hp + amount);
        return Hp - before;
    }

    public int RestoreMp(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var before = Mp;
        Mp = Math.Min(MaxMp, Mp + amount);
        return Mp - before;
    }

    public int DamageMp(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var before = Mp;
        Mp = Math.Max(0, Mp - amount);
        return before - Mp;
    }

    public void AddRage(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Rage = Math.Min(MaxRage, Rage + amount);
    }

    public void SetRage(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Rage = Math.Clamp(value, 0, MaxRage);
    }

    public void SetActionGauge(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ActionGauge = value;
    }

    public void AddItemCooldown(int cooldown)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cooldown);
        ItemCooldown = checked(ItemCooldown + cooldown);
    }

    public void RecoverItemCooldown()
    {
        if (ItemCooldown > 0)
        {
            ItemCooldown--;
        }
    }

    public void RecordUsedSkill(string skillId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId);
        LastUsedSkillId = skillId;
    }

    public int TakeDamage(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var before = Hp;
        Hp = Math.Max(0, Hp - amount);
        return before - Hp;
    }

    public bool TryApplyBuff(BattleBuffInstance buff)
    {
        ArgumentNullException.ThrowIfNull(buff);

        var existing = _buffs.FirstOrDefault(existingBuff =>
            string.Equals(existingBuff.Definition.Id, buff.Definition.Id, StringComparison.Ordinal));
        if (existing is not null &&
            !existing.IsExpired &&
            (buff.Level < existing.Level ||
             buff.Level == existing.Level && buff.RemainingTurns < existing.RemainingTurns))
        {
            return false;
        }

        if (existing is not null)
        {
            _buffs.Remove(existing);
        }

        _buffs.Add(buff);
        _localProjection = null;
        ClampResourcesToLimits();
        return true;
    }

    public IReadOnlyList<BattleBuffInstance> RemoveBuffs(Func<BattleBuffInstance, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var removed = _buffs
            .Where(predicate)
            .ToList();
        _buffs.RemoveAll(buff => removed.Contains(buff));
        if (removed.Count > 0)
        {
            _localProjection = null;
        }
        ClampResourcesToLimits();
        return removed
            .Where(static buff => !buff.IsExpired)
            .ToList();
    }

    internal bool RemoveBuff(BattleBuffInstance buff)
    {
        ArgumentNullException.ThrowIfNull(buff);
        if (!_buffs.Remove(buff))
        {
            return false;
        }

        _localProjection = null;
        ClampResourcesToLimits();
        return true;
    }

    public double GetStat(StatType statType) =>
        _projectionResolver is null
            ? GetBucket(DetachedProjection.StatModifierBuckets, statType).Evaluate(Character.GetBaseStat(statType))
            : Resolver.GetStat(this, statType);

    public double GetWeaponBonusValue(WeaponType weaponType, double baseValue) =>
        _projectionResolver is null
            ? GetBucket(DetachedProjection.WeaponModifierBuckets, weaponType).Evaluate(baseValue)
            : Resolver.GetWeaponBonus(this, weaponType, baseValue);

    public int GetSkillTargetingValue(string sourceSkillId, SkillTargetingField field, int baseValue) =>
        _projectionResolver is null
            ? (int)Math.Round(
                GetBucket(DetachedProjection.TargetingModifierBuckets, new SkillTargetingModifierKey(null, field))
                    .Combine(GetBucket(DetachedProjection.TargetingModifierBuckets, new SkillTargetingModifierKey(sourceSkillId, field)))
                    .Evaluate(baseValue))
            : Resolver.GetSkillTargeting(this, sourceSkillId, field, baseValue);

    public double GetActionSpeed()
    {
        if (HasTrait(TraitId.Ghost))
        {
            return 3.5d;
        }

        var speed = GetStat(StatType.Shenfa) / 100d + GetStat(StatType.Gengu) / 130d;
        speed = Math.Clamp(speed, 1d, 2.2d);
        speed += GetStat(StatType.Speed);

        if (TryGetBuff(BattleContentIds.Paralysis) is { Level: > 0 } paralysis)
        {
            speed -= paralysis.Level * 0.2d;
        }

        if (TryGetBuff(BattleContentIds.Swift) is { } swift)
        {
            speed += swift.Level * 0.2d;
        }

        speed = Math.Clamp(speed, 0.8d, 2.5d);
        if (HasBuff(BattleContentIds.Stun))
        {
            return 0d;
        }

        return speed;
    }

    public int GetMovePower()
    {
        if (HasTrait(TraitId.CannotMove))
        {
            return 0;
        }

        var movePower = 2;
        var shenfa = GetStat(StatType.Shenfa);
        if (shenfa > 100d)
        {
            movePower++;
        }

        if (shenfa > 180d)
        {
            movePower++;
        }

        if (shenfa > 250d)
        {
            movePower++;
        }

        movePower += (int)GetStat(StatType.Movement);
        if (TryGetBuff(BattleContentIds.Slow) is { } slow)
        {
            movePower -= (int)(slow.Level * 1.5d);
        }

        if (TryGetBuff(BattleContentIds.LightBody) is { } lightBody)
        {
            movePower += lightBody.Level + 1;
        }

        return Math.Clamp(movePower, 1, 5);
    }

    public bool HasBuff(string buffId) => TryGetBuff(buffId) is not null;

    public BattleBuffInstance? TryGetBuff(string buffId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buffId);

        return _buffs.FirstOrDefault(buff =>
            !buff.IsExpired &&
            string.Equals(buff.Definition.Id, buffId, StringComparison.Ordinal));
    }

    internal IReadOnlyList<BattleBuffInstance> RemoveExpiredBuffs()
    {
        var expired = _buffs.Where(static buff => buff.IsExpired).ToList();
        _buffs.RemoveAll(static buff => buff.IsExpired);
        if (expired.Count > 0)
        {
            _localProjection = null;
        }
        ClampResourcesToLimits();
        return expired;
    }

    internal AffixProjection GetLocalBattleProjection() => _localProjection ??= AffixProjectionBuilder.Build(
        _buffs.Where(static buff => !buff.IsExpired).SelectMany(buff =>
            buff.Definition.Affixes.Select((affix, order) => new ActiveAffixEntry(
                affix,
                new BuffAffixOrigin(buff.Definition.Id, buff.AppliedAtActionSerial),
                Provider: null,
                SourceLevel: buff.Level,
                AffixOrder: order,
                SourceSequence: buff.AppliedAtActionSerial))));

    internal void InvalidateLocalBattleProjection() => _localProjection = null;

    private BattleEffectProjectionResolver Resolver => _projectionResolver ??
        throw new InvalidOperationException($"Battle unit '{Id}' is not attached to a battle state.");

    private AffixProjection DetachedProjection =>
        AffixProjectionCombiner.Combine(Character.Projection.Affixes, GetLocalBattleProjection());

    private static ModifierBucket GetBucket<TKey>(IReadOnlyDictionary<TKey, ModifierBucket> buckets, TKey key)
        where TKey : notnull => buckets.TryGetValue(key, out var bucket) ? bucket : ModifierBucket.Empty;

    private int ResolveMaxHp() =>
        ResolvePositiveStat(StatType.MaxHp, 1);

    private int ResolveMaxMp() =>
        ResolvePositiveStat(StatType.MaxMp, 0);

    private int ResolvePositiveStat(StatType statType, int fallback)
    {
        var value = (int)Math.Round(GetStat(statType));
        return value > 0 ? value : fallback;
    }
}
