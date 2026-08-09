using Godot;

namespace Game.Godot.UI;

[GlobalClass]
[Tool]
public partial class OverflowTextureRect : Control
{
	private Texture2D? _texture;

	[Export]
	public Texture2D? Texture
	{
		get => _texture;
		set
		{
			if (_texture == value)
			{
				return;
			}

			_texture = value;
			QueueRedraw();
		}
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		if (_texture is null || Size.X <= 0f || Size.Y <= 0f)
		{
			return;
		}

		var textureSize = _texture.GetSize();
		if (textureSize.X <= 0f || textureSize.Y <= 0f)
		{
			return;
		}

		var scale = Mathf.Max(Size.X / textureSize.X, Size.Y / textureSize.Y);
		var drawSize = textureSize * scale;
		var drawPosition = (Size - drawSize) * 0.5f;
		DrawTextureRect(_texture, new Rect2(drawPosition, drawSize), false);
	}
}
