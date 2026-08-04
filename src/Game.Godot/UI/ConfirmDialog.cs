using Godot;

namespace Game.Godot.UI;

public enum ConfirmDialogTone
{
	Normal,
	Warning,
	Danger,
}

public partial class ConfirmDialog : Control
{
	private RichTextLabel _contentLabel = null!;
	private BaseButton _confirmButton = null!;
	private BaseButton _cancelButton = null!;
	private TaskCompletionSource<bool>? _completion;

	public override void _Ready()
	{
		_contentLabel = GetNode<RichTextLabel>("%ContentLabel");
		_confirmButton = GetNode<BaseButton>("%ConfirmButton");
		_cancelButton = GetNode<BaseButton>("%CancelButton");
		_confirmButton.Pressed += OnConfirmPressed;
		_cancelButton.Pressed += OnCancelPressed;
		Hide();
	}

	public async Task<bool> ShowConfirmAsync(
		string text,
		ConfirmDialogTone tone = ConfirmDialogTone.Normal,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(text);

		_completion?.TrySetResult(false);
		_completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		_contentLabel.Text = text;
		_contentLabel.Modulate = tone switch
		{
			ConfirmDialogTone.Warning => new Color(0.98f, 0.72f, 0.24f),
			ConfirmDialogTone.Danger => new Color(0.98f, 0.36f, 0.28f),
			_ => Colors.White,
		};
		_confirmButton.Modulate = _contentLabel.Modulate;
		Show();
		MoveToFront();

		try
		{
			using var registration = cancellationToken.Register(static state =>
			{
				((TaskCompletionSource<bool>)state!).TrySetCanceled();
			}, _completion);
			return await _completion.Task;
		}
		finally
		{
			Hide();
			_completion = null;
		}
	}

	private void OnConfirmPressed()
	{
		_completion?.TrySetResult(true);
	}

	private void OnCancelPressed()
	{
		_completion?.TrySetResult(false);
	}
}
