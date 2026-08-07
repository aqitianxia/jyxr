using Godot;

namespace Game.Godot.UI.Story;

public sealed partial class StoryIntertitlePanel : Control
{
	private const string PositionNames = "upper, center, lower";
	private const string ModeNames = "typewriter, instant";
	private TaskCompletionSource? _completion;
	private Control _textRegion = null!;
	private RichTextLabel _contentLabel = null!;
	private Label _continueHint = null!;
	private bool _isTyping;
	private double _charactersPerSecond;
	private double _typewriterProgress;
	private int _targetCharacters;

	public override void _Ready()
	{
		_textRegion = GetNode<Control>("%TextRegion");
		_contentLabel = GetNode<RichTextLabel>("%ContentLabel");
		_continueHint = GetNode<Label>("%ContinueHint");
		GuiInput += OnGuiInput;
		SetProcess(false);
		HidePresentation();
	}

	public override void _ExitTree()
	{
		_completion?.TrySetCanceled();
		_completion = null;
		SetProcess(false);
	}

	public override void _Process(double delta)
	{
		if (!_isTyping)
		{
			return;
		}

		_typewriterProgress += delta * _charactersPerSecond;
		var visibleCharacters = Math.Min(_targetCharacters, (int)Math.Floor(_typewriterProgress));
		_contentLabel.VisibleCharacters = visibleCharacters;
		if (visibleCharacters >= _targetCharacters)
		{
			RevealFullText();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsActive())
		{
			return;
		}

		if (@event.IsActionPressed("ui_accept") ||
			@event.IsActionPressed("ui_select") ||
			@event.IsActionPressed("ui_text_submit"))
		{
			Advance();
			AcceptEvent();
		}
	}

	public async Task ShowAsync(
		string text,
		string position,
		string mode,
		double speed,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(text);
		var normalizedPosition = ParsePosition(position);
		var normalizedMode = ParseMode(mode);
		ValidateSpeed(speed);

		ResetImmediate();
		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_completion = completion;
		_charactersPerSecond = speed;
		ApplyPosition(normalizedPosition);
		_contentLabel.Text = text;
		_targetCharacters = _contentLabel.GetTotalCharacterCount();
		Show();

		if (normalizedMode == "typewriter" && _targetCharacters > 0)
		{
			StartTypewriter();
		}
		else
		{
			RevealFullText();
		}

		using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
		try
		{
			await completion.Task;
		}
		finally
		{
			if (ReferenceEquals(_completion, completion))
			{
				_completion = null;
				HidePresentation();
			}
		}
	}

	public void ResetImmediate()
	{
		var completion = _completion;
		_completion = null;
		HidePresentation();
		completion?.TrySetCanceled();
	}

	private void OnGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton
			{
				Pressed: true,
				ButtonIndex: MouseButton.Left,
			} || !IsActive())
		{
			return;
		}

		Advance();
		AcceptEvent();
	}

	private bool IsActive() =>
		Visible && _completion is not null && !_completion.Task.IsCompleted;

	private void StartTypewriter()
	{
		_typewriterProgress = 0d;
		_contentLabel.VisibleCharacters = 0;
		_continueHint.Hide();
		_isTyping = true;
		SetProcess(true);
	}

	private void RevealFullText()
	{
		_isTyping = false;
		SetProcess(false);
		_contentLabel.VisibleCharacters = -1;
		_continueHint.Show();
	}

	private void Advance()
	{
		if (_isTyping)
		{
			RevealFullText();
			return;
		}

		_completion?.TrySetResult();
	}

	private void HidePresentation()
	{
		_isTyping = false;
		SetProcess(false);
		_contentLabel.VisibleCharacters = -1;
		_continueHint.Hide();
		Hide();
	}

	private void ApplyPosition(string position)
	{
		(float AnchorTop, float AnchorBottom) anchors = position switch
		{
			"upper" => (0.10f, 0.40f),
			"center" => (0.35f, 0.65f),
			"lower" => (0.60f, 0.90f),
			_ => throw new InvalidOperationException($"Unsupported intertitle position '{position}'."),
		};

		_textRegion.AnchorLeft = 0.125f;
		_textRegion.AnchorTop = anchors.AnchorTop;
		_textRegion.AnchorRight = 0.875f;
		_textRegion.AnchorBottom = anchors.AnchorBottom;
		_textRegion.OffsetLeft = 0f;
		_textRegion.OffsetTop = 0f;
		_textRegion.OffsetRight = 0f;
		_textRegion.OffsetBottom = 0f;
	}

	private static string ParsePosition(string position) => position.Trim() switch
	{
		"upper" => "upper",
		"center" => "center",
		"lower" => "lower",
		_ => throw new InvalidOperationException(
			$"Unsupported intertitle position '{position}'. Use one of: {PositionNames}."),
	};

	private static string ParseMode(string mode) => mode.Trim() switch
	{
		"typewriter" => "typewriter",
		"instant" => "instant",
		_ => throw new InvalidOperationException(
			$"Unsupported intertitle mode '{mode}'. Use one of: {ModeNames}."),
	};

	private static void ValidateSpeed(double speed)
	{
		if (!double.IsFinite(speed) || speed <= 0d)
		{
			throw new ArgumentOutOfRangeException(
				nameof(speed),
				"Command 'intertitle' speed must be finite and positive.");
		}
	}
}
