using Godot;

namespace Game.Godot.UI;

public partial class PartyDropSurface : Control
{
	private PartyPanel? _ownerPanel;

	internal void Setup(PartyPanel ownerPanel)
	{
		ArgumentNullException.ThrowIfNull(ownerPanel);
		_ownerPanel = ownerPanel;
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data) =>
		_ownerPanel?.CanDropCharacter(ToGlobalPosition(atPosition), data) ?? false;

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		_ownerPanel?.DropCharacter(ToGlobalPosition(atPosition), data);
	}

	public override void _GuiInput(InputEvent @event)
	{
		base._GuiInput(@event);
		_ownerPanel?.HandleDropSurfaceInput(@event);
	}

	private Vector2 ToGlobalPosition(Vector2 localPosition) =>
		GetGlobalRect().Position + localPosition;
}
