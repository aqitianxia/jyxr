using Game.Core.Model;
using Game.Presentation.Hero;

namespace Game.Tests;

public sealed class HeroFavorabilityPresenterTests
{
    [Fact]
    public void Build_ShowsOnlyRecordedTargets()
    {
        var adventure = new AdventureState();
        adventure.ChangeFavorability(10);
        adventure.ChangeFavorability("李文秀", 5);
        var heroineDefinition = TestContentFactory.CreateCharacterDefinition("女主");
        var heroine = TestContentFactory.CreateCharacterInstance("女主", heroineDefinition);
        heroine.Name = "铃兰";
        var party = new Party();
        party.AddFollower(heroine);
        var repository = TestContentFactory.CreateRepository(
            characters:
            [
                heroineDefinition,
                TestContentFactory.CreateCharacterDefinition("李文秀"),
                TestContentFactory.CreateCharacterDefinition("木婉清"),
            ]);

        var result = HeroFavorabilityPresenter.Build(adventure, party, repository);

        Assert.Collection(
            result,
            heroine =>
            {
                Assert.Equal("女主", heroine.TargetId);
                Assert.Equal("铃兰", heroine.DisplayName);
                Assert.Equal(60, heroine.Value);
            },
            liWenxiu =>
            {
                Assert.Equal("李文秀", liWenxiu.TargetId);
                Assert.Equal("李文秀", liWenxiu.DisplayName);
                Assert.Equal(55, liWenxiu.Value);
            });
    }

    [Fact]
    public void Build_DoesNotShowUnrecordedHeroine()
    {
        var result = HeroFavorabilityPresenter.Build(
            new AdventureState(),
            new Party(),
            TestContentFactory.CreateRepository(
                characters: [TestContentFactory.CreateCharacterDefinition("女主")]));

        Assert.Empty(result);
    }

    [Fact]
    public void Build_UsesRosterNameBeforeDefinitionName()
    {
        var adventure = new AdventureState();
        adventure.ChangeFavorability("李文秀", 5);
        var definition = TestContentFactory.CreateCharacterDefinition("李文秀");
        var character = TestContentFactory.CreateCharacterInstance("李文秀", definition);
        character.Name = "秀秀";
        var party = new Party();
        party.AddReserve(character);
        var repository = TestContentFactory.CreateRepository(characters: [definition]);

        var result = HeroFavorabilityPresenter.Build(adventure, party, repository);

        Assert.Equal("秀秀", result[0].DisplayName);
    }

    [Fact]
    public void Build_OrdersAdditionalTargetsByStableIdAndFallsBackToId()
    {
        var adventure = new AdventureState();
        adventure.ChangeFavorability("王语嫣", 3);
        adventure.ChangeFavorability("木婉清", -5);

        var result = HeroFavorabilityPresenter.Build(
            adventure,
            new Party(),
            TestContentFactory.CreateRepository());

        Assert.Equal(["木婉清", "王语嫣"], result.Select(static item => item.TargetId));
        Assert.Equal(["木婉清", "王语嫣"], result.Select(static item => item.DisplayName));
    }
}
