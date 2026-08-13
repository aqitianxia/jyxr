using Game.Presentation.Map;

namespace Game.Tests;

public sealed class MapLocationTooltipInteractionStateTests
{
	private static readonly MapLocationTooltipKey FirstLocation = new("world", "luoyang");
	private static readonly MapLocationTooltipKey SecondLocation = new("world", "changan");

	[Fact]
	public void FirstTapShowsTooltip()
	{
		var state = new MapLocationTooltipInteractionState();

		var intent = state.Tap(FirstLocation, hasTooltip: true);

		Assert.Equal(MapLocationTooltipIntent.ShowTooltip, intent);
		Assert.Equal(FirstLocation, state.PreviewedLocation);
	}

	[Fact]
	public void SecondTapOnSameLocationActivatesAndClearsTooltip()
	{
		var state = new MapLocationTooltipInteractionState();
		state.Tap(FirstLocation, hasTooltip: true);

		var intent = state.Tap(FirstLocation, hasTooltip: true);

		Assert.Equal(MapLocationTooltipIntent.ActivateLocation, intent);
		Assert.Null(state.PreviewedLocation);
	}

	[Fact]
	public void TapOnAnotherLocationSwitchesTooltipWithoutActivating()
	{
		var state = new MapLocationTooltipInteractionState();
		state.Tap(FirstLocation, hasTooltip: true);

		var intent = state.Tap(SecondLocation, hasTooltip: true);

		Assert.Equal(MapLocationTooltipIntent.ShowTooltip, intent);
		Assert.Equal(SecondLocation, state.PreviewedLocation);
	}

	[Fact]
	public void DismissClearsCurrentTooltip()
	{
		var state = new MapLocationTooltipInteractionState();
		state.Tap(FirstLocation, hasTooltip: true);

		var intent = state.Dismiss();

		Assert.Equal(MapLocationTooltipIntent.DismissTooltip, intent);
		Assert.Null(state.PreviewedLocation);
		Assert.Equal(MapLocationTooltipIntent.None, state.Dismiss());
	}

	[Fact]
	public void LocationWithoutTooltipActivatesImmediatelyAndClearsPreview()
	{
		var state = new MapLocationTooltipInteractionState();
		state.Tap(FirstLocation, hasTooltip: true);

		var intent = state.Tap(SecondLocation, hasTooltip: false);

		Assert.Equal(MapLocationTooltipIntent.ActivateLocation, intent);
		Assert.Null(state.PreviewedLocation);
	}
}
