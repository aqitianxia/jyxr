namespace Game.Presentation.Map;

public readonly record struct MapLocationTooltipKey(string MapId, string LocationId);

public enum MapLocationTooltipIntent
{
	None,
	ShowTooltip,
	ActivateLocation,
	DismissTooltip,
}

public sealed class MapLocationTooltipInteractionState
{
	public MapLocationTooltipKey? PreviewedLocation { get; private set; }

	public MapLocationTooltipIntent Tap(MapLocationTooltipKey location, bool hasTooltip)
	{
		if (!hasTooltip)
		{
			PreviewedLocation = null;
			return MapLocationTooltipIntent.ActivateLocation;
		}

		if (PreviewedLocation == location)
		{
			PreviewedLocation = null;
			return MapLocationTooltipIntent.ActivateLocation;
		}

		PreviewedLocation = location;
		return MapLocationTooltipIntent.ShowTooltip;
	}

	public MapLocationTooltipIntent Dismiss()
	{
		if (PreviewedLocation is null)
		{
			return MapLocationTooltipIntent.None;
		}

		PreviewedLocation = null;
		return MapLocationTooltipIntent.DismissTooltip;
	}
}
