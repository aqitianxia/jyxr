using System.Collections.ObjectModel;
using Game.Core.Definitions;

namespace Game.Core.Affix;

public static class TalentResolver
{
    public static ResolvedAffixSet Resolve(
        IEnumerable<TalentDefinition> learnedTalents,
        IReadOnlyList<AffixDefinition> flattenedAffixes)
    {
        ArgumentNullException.ThrowIfNull(learnedTalents);
        ArgumentNullException.ThrowIfNull(flattenedAffixes);

        // Avoid relying on the default equality semantics of the TalentDefinition record;
        // instead, perform stable deduplication directly by id.
        var candidateTalentsById = new Dictionary<string, TalentDefinition>(StringComparer.Ordinal);
        var resolvedAffixes = new List<AffixDefinition>(flattenedAffixes);

        foreach (var talent in learnedTalents)
        {
            candidateTalentsById[talent.Id] = talent;
        }

        foreach (var affix in flattenedAffixes)
        {
            if (affix is not GrantTalentAffix grantTalentAffix)
            {
                continue;
            }

            candidateTalentsById[grantTalentAffix.Talent.Id] = grantTalentAffix.Talent;
        }

        var orderedEffectiveTalents = candidateTalentsById.Values.ToList();
        var effectiveTalents = orderedEffectiveTalents.ToHashSet();
        var replacedTalentIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var talent in orderedEffectiveTalents)
        {
            foreach (var replacedTalentId in talent.ReplaceTalentIds)
            {
                replacedTalentIds.Add(replacedTalentId);
            }
        }

        foreach (var talent in orderedEffectiveTalents)
        {
            if (replacedTalentIds.Contains(talent.Id))
            {
                continue;
            }

            foreach (var (talentAffix, index) in talent.Affixes.Select((affix, index) => (affix, index)))
            {
                resolvedAffixes.Add(talentAffix with
                {
                    SourceKind = ProviderKind.Talent,
                    SourceId = talent.Id,
                    SourceAffixOrder = index,
                });
            }
        }

        return new ResolvedAffixSet(
            new ReadOnlyCollection<AffixDefinition>(resolvedAffixes),
            new ReadOnlySet<TalentDefinition>(effectiveTalents));
    }
}
