using Game.Core.Definitions;
using Game.Presentation.Map;
using Godot;

namespace Game.Godot.Map;

public partial class MapLocationTooltipLayer : CanvasLayer
{
	[Export]
	public PackedScene TooltipScene { get; set; } = null!;

	private readonly MapLocationTooltipInteractionState _interaction = new();
	private Control? _tooltip;
	private Rect2? _anchor;

	public event Action<(string MapId, MapLocationDefinition Location, MapEventDefinition? Event)>?
		LocationActivated;

	public override void _Ready()
	{
		GetViewport().SizeChanged += Dismiss;
	}

	public override void _ExitTree()
	{
		GetViewport().SizeChanged -= Dismiss;
		Dismiss();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Game.IsMobilePlatform || _interaction.PreviewedLocation is null)
		{
			return;
		}

		Vector2? position = @event switch
		{
			InputEventScreenTouch { Pressed: true } touch => touch.Position,
			InputEventMouseButton
			{
				Pressed: true,
				ButtonIndex: MouseButton.Left,
			} mouseButton when mouseButton.Device != InputEvent.DeviceIdEmulation => mouseButton.Position,
			_ => null,
		};

		if (position is { } pressedPosition &&
			(_anchor is null || !_anchor.Value.HasPoint(pressedPosition)))
		{
			Dismiss();
		}
	}

	public void Request(
		(string MapId, MapLocationDefinition Location, MapEventDefinition? Event) location,
		Rect2 anchor)
	{
		if (!Game.IsMobilePlatform)
		{
			LocationActivated?.Invoke(location);
			return;
		}

		var text = MapEntityPresentation.BuildTooltipText(location);
		var key = new MapLocationTooltipKey(location.MapId, location.Location.Id);
		switch (_interaction.Tap(key, !string.IsNullOrWhiteSpace(text)))
		{
			case MapLocationTooltipIntent.ShowTooltip:
				Show(text, anchor);
				break;
			case MapLocationTooltipIntent.ActivateLocation:
				RemoveTooltip();
				LocationActivated?.Invoke(location);
				break;
		}
	}

	public void Dismiss()
	{
		_interaction.Dismiss();
		RemoveTooltip();
	}

	private void Show(string text, Rect2 anchor)
	{
		RemoveTooltip();
		_anchor = anchor;
		_tooltip = MapEntityTooltip.Show(
			this,
			TooltipScene,
			text,
			anchor,
			GetViewport().GetVisibleRect());
	}

	private void RemoveTooltip()
	{
		_anchor = null;
		var tooltip = _tooltip;
		_tooltip = null;
		if (tooltip is not null && GodotObject.IsInstanceValid(tooltip))
		{
			tooltip.QueueFree();
		}
	}
}
