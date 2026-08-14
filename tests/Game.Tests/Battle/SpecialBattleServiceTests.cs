using Game.Application;
using Game.Core.Definitions;
using Game.Core.Model;
using Game.Core.Story;
using Game.Expressions;

namespace Game.Tests;

public sealed class SpecialBattleServiceTests
{
    [Fact]
    public async Task TowerRewardClaimsAreScopedByTowerStageAndReward()
    {
        var reward = CreateItem("rare_reward");
        var tower = CreateTwoStageTower(reward.Id);
        var session = CreateSession(tower, reward);
        var host = new TowerRuntimeHost(
            [["hero"], ["ally"]],
            [true, true]);

        await session.SpecialBattleService.RunTowerAsync(host);

        var entry = Assert.Single(session.State.Inventory.Entries.OfType<StackInventoryEntry>());
        Assert.Equal(reward.Id, entry.Item.Id);
        Assert.Equal(2, entry.Quantity);
        Assert.Equal(1, session.State.SpecialBattle.GetTowerRewardClaimCount("tower", "stage_a", "rare_reward"));
        Assert.Equal(1, session.State.SpecialBattle.GetTowerRewardClaimCount("tower", "stage_b", "rare_reward"));
    }

    [Fact]
    public async Task TowerRewardClaimsAreOnlyConsumedWhenRewardsAreGranted()
    {
        var reward = CreateItem("rare_reward");
        var tower = CreateTwoStageTower(reward.Id);
        var session = CreateSession(tower, reward);
        var host = new TowerRuntimeHost(
            [["hero"], ["ally"]],
            [true, false]);

        await session.SpecialBattleService.RunTowerAsync(host);

        Assert.Empty(session.State.Inventory.Entries);
        Assert.Empty(session.State.SpecialBattle.TowerRewardClaimCounts);
    }

    [Fact]
    public async Task TowerFallsBackWhenEveryConfiguredRewardReachedItsClaimLimit()
    {
        var reward = CreateItem("rare_reward");
        var fallback = CreateItem("黑玉断续膏");
        var tower = CreateSingleStageTower(reward.Id);
        var session = CreateSession(tower, reward, fallback);

        await session.SpecialBattleService.RunTowerAsync(
            new TowerRuntimeHost([["hero"]], [true]));
        await session.SpecialBattleService.RunTowerAsync(
            new TowerRuntimeHost([["hero"]], [true]));

        Assert.Equal(
            1,
            session.State.Inventory.Entries.OfType<StackInventoryEntry>()
                .Single(entry => entry.Item.Id == reward.Id)
                .Quantity);
        Assert.Equal(
            1,
            session.State.Inventory.Entries.OfType<StackInventoryEntry>()
                .Single(entry => entry.Item.Id == fallback.Id)
                .Quantity);
    }

    [Fact]
    public async Task TowerGrantsConfiguredQuantityForOneClaim()
    {
        var reward = CreateItem("stack_reward");
        var tower = CreateSingleStageTower(reward.Id, quantity: 3);
        var session = CreateSession(tower, reward);

        await session.SpecialBattleService.RunTowerAsync(
            new TowerRuntimeHost([["hero"]], [true]));

        Assert.Equal(3, Assert.Single(session.State.Inventory.Entries.OfType<StackInventoryEntry>()).Quantity);
        Assert.Equal(
            1,
            session.State.SpecialBattle.GetTowerRewardClaimCount("tower", "stage_a", "stack_reward"));
    }

    [Fact]
    public async Task TowerRewardClaimsUseRewardDefinitionIdInsteadOfRewardContent()
    {
        var reward = CreateItem("rare_reward");
        var tower = CreateSingleStageTower(reward.Id, rewardDefinitionId: "limited_slot");
        var session = CreateSession(tower, reward);

        await session.SpecialBattleService.RunTowerAsync(
            new TowerRuntimeHost([["hero"]], [true]));

        Assert.Equal(
            1,
            session.State.SpecialBattle.GetTowerRewardClaimCount("tower", "stage_a", "limited_slot"));
        Assert.Equal(
            0,
            session.State.SpecialBattle.GetTowerRewardClaimCount("tower", "stage_a", "rare_reward"));
    }

    [Fact]
    public async Task TowerEquipmentRewardsAreGrantedAsRandomAffixInstances()
    {
        var reward = TestContentFactory.CreateEquipment("rare_sword");
        var tower = CreateSingleStageTower(reward.Id);
        var session = CreateSession(
            tower,
            [reward],
            [CreateFourRandomAffixTable()]);
        var host = new TowerRuntimeHost(
            [["hero"]],
            [true]);

        await session.SpecialBattleService.RunTowerAsync(host);

        var entry = Assert.Single(session.State.Inventory.Entries.OfType<EquipmentInstanceInventoryEntry>());
        Assert.Equal(reward.Id, entry.Equipment.Definition.Id);
        Assert.Equal(4, EquipmentAffixGroups.Count(entry.Equipment.ExtraAffixes));
        Assert.Empty(session.State.Inventory.Entries.OfType<StackInventoryEntry>());
    }

    [Fact]
    public async Task TowerEquipmentRewardsUseFewerAffixesWhenUniqueOptionsAreInsufficient()
    {
        var reward = TestContentFactory.CreateEquipment("rare_sword");
        var tower = CreateSingleStageTower(reward.Id);
        var session = CreateSession(
            tower,
            [reward],
            [CreateSingleRandomAffixTable()]);
        var host = new TowerRuntimeHost(
            [["hero"]],
            [true]);

        await session.SpecialBattleService.RunTowerAsync(host);

        var entry = Assert.Single(session.State.Inventory.Entries.OfType<EquipmentInstanceInventoryEntry>());
        Assert.Equal(1, EquipmentAffixGroups.Count(entry.Equipment.ExtraAffixes));
    }

    private static GameSession CreateSession(
        TowerDefinition tower,
        params ItemDefinition[] items) =>
        CreateSession(tower, items, []);

    private static GameSession CreateSession(
        TowerDefinition tower,
        ItemDefinition[] items,
        EquipmentRandomAffixTableDefinition[] equipmentRandomAffixTables)
    {
        var heroDefinition = TestContentFactory.CreateCharacterDefinition("hero");
        var allyDefinition = TestContentFactory.CreateCharacterDefinition("ally");
        var state = new GameState();
        state.Party.AddMember(TestContentFactory.CreateCharacterInstance("hero", heroDefinition));
        state.Party.AddMember(TestContentFactory.CreateCharacterInstance("ally", allyDefinition));

        var repository = TestContentFactory.CreateRepository(
            characters: [heroDefinition, allyDefinition],
            items: items,
            battles:
            [
                CreateBattle("battle_a"),
                CreateBattle("battle_b"),
            ],
            towers: [tower],
            equipmentRandomAffixTables: equipmentRandomAffixTables);
        return new GameSession(state, repository);
    }

    private static TowerDefinition CreateSingleStageTower(
        string rewardId,
        int quantity = 1,
        string? rewardDefinitionId = null) =>
        new()
        {
            Id = "tower",
            Name = "tower",
            Stages =
            [
                CreateStage("stage_a", "battle_a", rewardId, 0, quantity, rewardDefinitionId),
            ],
        };

    private static TowerDefinition CreateTwoStageTower(string rewardId) =>
        new()
        {
            Id = "tower",
            Name = "tower",
            Stages =
            [
                CreateStage("stage_a", "battle_a", rewardId, 0),
                CreateStage("stage_b", "battle_b", rewardId, 1),
            ],
        };

    private static TowerStageDefinition CreateStage(
        string id,
        string battleId,
        string rewardId,
        int index,
        int quantity = 1,
        string? rewardDefinitionId = null) =>
        new()
        {
            Id = id,
            Name = id,
            BattleId = battleId,
            Index = index,
            Rewards =
            [
                new TowerRewardDefinition
                {
                    Id = rewardDefinitionId ?? rewardId,
                    Reward = new ItemRewardDefinition { ItemId = rewardId, Quantity = quantity },
                    Weight = 1d,
                    MaxClaims = 1,
                },
            ],
        };

    private static BattleDefinition CreateBattle(string id) =>
        new()
        {
            Id = id,
            Name = id,
            Background = "battle_bg/map",
        };

    private static NormalItemDefinition CreateItem(string id) =>
        new()
        {
            Id = id,
            Name = id,
            Type = ItemType.Consumable,
            ConsumeOnUse = true,
            CanDrop = true,
        };

    private static EquipmentRandomAffixTableDefinition CreateFourRandomAffixTable() =>
        new()
        {
            Id = "four-affixes",
            When = Expr("true"),
            Options =
            [
                new EquipmentRandomAffixOptionDefinition
                {
                    Kind = EquipmentRandomAffixKind.Accuracy,
                    Weight = 1,
                    Ranges = [Range("1", "1")],
                },
                new EquipmentRandomAffixOptionDefinition
                {
                    Kind = EquipmentRandomAffixKind.CritMult,
                    Weight = 1,
                    Ranges = [Range("1", "1")],
                },
                new EquipmentRandomAffixOptionDefinition
                {
                    Kind = EquipmentRandomAffixKind.Lifesteal,
                    Weight = 1,
                    Ranges = [Range("1", "1")],
                },
                new EquipmentRandomAffixOptionDefinition
                {
                    Kind = EquipmentRandomAffixKind.AntiDebuff,
                    Weight = 1,
                    Ranges = [Range("1", "1")],
                },
            ],
        };

    private static EquipmentRandomAffixTableDefinition CreateSingleRandomAffixTable() =>
        new()
        {
            Id = "single-affix",
            When = Expr("true"),
            Options =
            [
                new EquipmentRandomAffixOptionDefinition
                {
                    Kind = EquipmentRandomAffixKind.Accuracy,
                    Weight = 1,
                    Ranges = [Range("1", "1")],
                },
            ],
        };

    private static ParsedExpression Expr(string source) => new ExpressionParser().ParseExpression(source);

    private static EquipmentRandomAffixRangeDefinition Range(string min, string max) => new()
    {
        Min = Expr(min),
        Max = Expr(max),
    };

    private sealed class TowerRuntimeHost : IRuntimeHost, ISpecialBattleRuntimeHost
    {
        private readonly Queue<IReadOnlyList<string>> _selectedCharacterIds;
        private readonly Queue<bool> _battleResults;

        public TowerRuntimeHost(
            IEnumerable<IReadOnlyList<string>> selectedCharacterIds,
            IEnumerable<bool> battleResults)
        {
            _selectedCharacterIds = new Queue<IReadOnlyList<string>>(selectedCharacterIds);
            _battleResults = new Queue<bool>(battleResults);
        }

        public ValueTask DialogueAsync(DialogueContext dialogue, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask<int> ChooseOptionAsync(ChoiceContext choice, CancellationToken cancellationToken) =>
            ValueTask.FromResult(0);

        public ValueTask<BattleOutcome> ResolveBattleAsync(BattleContext battle, CancellationToken cancellationToken) =>
            ValueTask.FromResult(BattleOutcome.Win);

        public ValueTask<ExpressionValue> GetVariableAsync(string name, CancellationToken cancellationToken) =>
            ValueTask.FromException<ExpressionValue>(new InvalidOperationException($"Unknown variable '{name}'."));

        public ValueTask<bool> EvaluatePredicateAsync(
            string name,
            IReadOnlyList<ExpressionValue> args,
            CancellationToken cancellationToken) =>
            ValueTask.FromException<bool>(new InvalidOperationException($"Unknown predicate '{name}'."));

        public ValueTask<StoryCommandResult> ExecuteCommandAsync(
            string name,
            IReadOnlyList<ExpressionValue> args,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(StoryCommandResult.None);

        public ValueTask<IReadOnlyList<string>> SelectCombatantsAsync(
            CombatantSelectionRequest request,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_selectedCharacterIds.Dequeue());

        public ValueTask<bool> RunBattleAsync(
            SpecialBattleRequest request,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_battleResults.Dequeue());
    }
}
