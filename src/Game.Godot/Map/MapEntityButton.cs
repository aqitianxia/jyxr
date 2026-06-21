using Game.Core.Definitions;
using Game.Godot.Assets;
using Godot;

namespace Game.Godot.Map;

public partial class MapEntityButton : Button
{
	private static MapEntityButton? _activeMobileTooltipOwner;

	[Export]
	public Texture2D? DefaultTexture { get; set; }

	[Export]
	public PackedScene TooltipScene { get; set; } = null!;

	private TextureRect _avatar = null!;
	private Label _nameLabel = null!;
	private TextureRect _notice = null!;
	private Control? _mobileTooltip;

	private (string MapId, MapLocationDefinition Location, MapEventDefinition? Event, int EventIndex)? _location;

	public event Action<(string MapId, MapLocationDefinition Location, MapEventDefinition? Event, int EventIndex)>? LocationPressed;

	public override void _Ready()
	{
		_avatar = GetNode<TextureRect>("%Avatar");
		_nameLabel = GetNode<Label>("%NameLabel");
		_notice = GetNode<TextureRect>("%Notice");
		Pressed += OnPressed;
		Refresh();
	}

	public override string _GetTooltip(Vector2 atPosition) =>
		_location is { } location ? ResolveTooltipText(location) : string.Empty;

	public override Control? _MakeCustomTooltip(string forText) =>
		string.IsNullOrWhiteSpace(forText) ? null : CreateTooltip(forText);

	public override void _ExitTree()
	{
		CloseMobileTooltip();
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsMobileTooltipArmed() || !IsMobileTooltipDismissInput(@event, out var position))
		{
			return;
		}

		if (!GetGlobalRect().HasPoint(position))
		{
			CloseMobileTooltip();
		}
	}

	public void Setup((string MapId, MapLocationDefinition Location, MapEventDefinition? Event, int EventIndex) location)
	{
		_location = location;
		Refresh();
	}

	private void Refresh()
	{
		if (_location is not { } location || !IsInsideTree())
		{
			return;
		}

		_nameLabel.Text = ResolveLocationName(location.Location);
		_notice.Visible = location.Event?.RepeatMode == RepeatMode.Once;
		_avatar.Texture = ResolveAvatarTexture(location.Location, location.Event);
	}

	private Texture2D? ResolveAvatarTexture(MapLocationDefinition location, MapEventDefinition? mapEvent)
	{
		if (mapEvent is null)
		{
			return DefaultTexture;
		}

		var image = mapEvent.Image ?? location.Picture;
		if (image is not null)
		{
			return AssetResolver.LoadTextureResource(image) ?? DefaultTexture;
		}

		return AssetResolver.LoadCharacterPortraitByCharacterId(location.Id) ?? DefaultTexture;
	}

	private void OnPressed()
	{
		if (_location is not { } location)
		{
			return;
		}

		if (location.Event is null)
		{
			return;
		}

		if (!Game.IsMobilePlatform)
		{
			LocationPressed?.Invoke(location);
			return;
		}

		var tooltipText = ResolveTooltipText(location);
		if (string.IsNullOrWhiteSpace(tooltipText))
		{
			LocationPressed?.Invoke(location);
			return;
		}

		if (!IsMobileTooltipArmed())
		{
			ShowMobileTooltip(tooltipText);
			return;
		}

		CloseMobileTooltip();
		LocationPressed?.Invoke(location);
	}

	private void ShowMobileTooltip(string text)
	{
		if (_activeMobileTooltipOwner is not null &&
			_activeMobileTooltipOwner != this &&
			GodotObject.IsInstanceValid(_activeMobileTooltipOwner))
		{
			_activeMobileTooltipOwner.CloseMobileTooltip();
		}

		CloseMobileTooltip();

		var content = CreateTooltip(text);
		IgnoreMouseInputRecursive(content);
		AddChild(content);

		_mobileTooltip = content;
		_activeMobileTooltipOwner = this;
		PositionMobileTooltip(content);
	}

	private Control CreateTooltip(string text)
	{
		if (TooltipScene is null)
		{
			throw new InvalidOperationException("TooltipScene is not assigned.");
		}

		if (TooltipScene.Instantiate() is not MapEntityTooltip tooltip)
		{
			throw new InvalidOperationException("Map entity tooltip scene root must be MapEntityTooltip.");
		}

		tooltip.Setup(text);
		return tooltip;
	}

	private void PositionMobileTooltip(Control content)
	{
		var tooltipSize = content.GetCombinedMinimumSize();
		var viewportRect = GetViewportRect();
		var offset = new Vector2((Size.X - tooltipSize.X) * 0.5f, Size.Y);
		var globalPosition = GlobalPosition + offset;

		if (globalPosition.X + tooltipSize.X > viewportRect.Size.X)
		{
			offset.X = viewportRect.Size.X - GlobalPosition.X - tooltipSize.X;
		}

		if (GlobalPosition.X + offset.X < 0f)
		{
			offset.X = -GlobalPosition.X;
		}

		if (globalPosition.Y + tooltipSize.Y > viewportRect.Size.Y)
		{
			offset.Y = -tooltipSize.Y;
		}

		content.Position = offset;
		content.CustomMinimumSize = tooltipSize;
		content.Size = tooltipSize;
		content.ZIndex = 1000;
		content.ZAsRelative = false;
	}

	private bool IsMobileTooltipArmed() =>
		_activeMobileTooltipOwner == this &&
		_mobileTooltip is not null &&
		GodotObject.IsInstanceValid(_mobileTooltip);

	private void CloseMobileTooltip()
	{
		if (_activeMobileTooltipOwner == this)
		{
			_activeMobileTooltipOwner = null;
		}

		var tooltip = _mobileTooltip;
		_mobileTooltip = null;
		if (tooltip is null || !GodotObject.IsInstanceValid(tooltip))
		{
			return;
		}

		tooltip.QueueFree();
	}

	private static bool IsMobileTooltipDismissInput(InputEvent @event, out Vector2 position)
	{
		switch (@event)
		{
			case InputEventScreenTouch { Pressed: true } screenTouch:
				position = screenTouch.Position;
				return true;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouseButton:
				position = mouseButton.Position;
				return true;
			default:
				position = default;
				return false;
		}
	}

	private static void IgnoreMouseInputRecursive(Control control)
	{
		control.MouseFilter = MouseFilterEnum.Ignore;

		foreach (var child in control.GetChildren())
		{
			if (child is Control childControl)
			{
				IgnoreMouseInputRecursive(childControl);
			}
		}
	}

	private static string ResolveLocationName(MapLocationDefinition location) =>
		location.Name ?? AssetResolver.ResolveCharacterName(location.Id);

	private static string ResolveTooltipText(
		(string MapId, MapLocationDefinition Location, MapEventDefinition? Event, int EventIndex) location)
	{
		var description = !string.IsNullOrWhiteSpace(location.Event?.Description)
			? location.Event.Description
			: location.Location.Description ?? "";
		var consumedTimeSlots = Game.MapService.PreviewInteractionConsumedTimeSlots(location);
		if (consumedTimeSlots <= 0)
		{
			return description;
		}

		var costLine = $"[color=red]耗时：{FormatConsumedTimeSlots(consumedTimeSlots)}[/color]";
		return string.IsNullOrWhiteSpace(description)
			? costLine
			: $"{description}\n{costLine}";
	}

	private static string FormatConsumedTimeSlots(int timeSlots)
	{
		var days = timeSlots / 12;
		var remainingTimeSlots = timeSlots % 12;
		if (days <= 0)
		{
			return $"{remainingTimeSlots}个时辰";
		}

		return remainingTimeSlots <= 0
			? $"{days}天"
			: $"{days}天{remainingTimeSlots}个时辰";
	}
}
