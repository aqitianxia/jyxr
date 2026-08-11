using Game.Application;
using Godot;

namespace Game.Godot.UI;

public partial class ToastItem : PanelContainer
{
	[Export]
	public StyleBoxFlat NormalStyle { get; set; } = null!;

	[Export]
	public StyleBoxFlat ImportantStyle { get; set; } = null!;

	[Export]
	public StyleBoxFlat ErrorStyle { get; set; } = null!;

	[Export]
	public Color NormalTextColor { get; set; }

	[Export]
	public Color ImportantTextColor { get; set; }

	[Export]
	public Color ErrorTextColor { get; set; }

	[Export]
	public float SingleLineHeight { get; set; }

	[Export]
	public float MultiLineHeight { get; set; }

	private Label _messageLabel = null!;

	public override void _Ready()
	{
		_messageLabel = GetNode<Label>("%MessageLabel");
	}

	public void Configure(ToastTone tone)
	{
		var (style, textColor) = tone switch
		{
			ToastTone.Important => (ImportantStyle, ImportantTextColor),
			ToastTone.Error => (ErrorStyle, ErrorTextColor),
			_ => (NormalStyle, NormalTextColor),
		};

		AddThemeStyleboxOverride("panel", style);
		_messageLabel.AddThemeColorOverride("font_color", textColor);
	}

	public float SetMessage(string text, int count)
	{
		_messageLabel.Text = count > 1
			? $"{text} x{count}"
			: text;

		return _messageLabel.GetLineCount() > 1
			? MultiLineHeight
			: SingleLineHeight;
	}

	public void SetAlpha(float alpha)
	{
		Modulate = new Color(1f, 1f, 1f, alpha);
	}
}
