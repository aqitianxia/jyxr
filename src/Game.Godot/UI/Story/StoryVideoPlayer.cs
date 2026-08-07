using Godot;

namespace Game.Godot.UI.Story;

public sealed partial class StoryVideoPlayer : Control
{
	private const float DefaultAspectRatio = 16f / 9f;
	private TaskCompletionSource? _completion;
	private AspectRatioContainer _aspectRatioContainer = null!;
	private VideoStreamPlayer _videoPlayer = null!;
	private bool _isWaitingForVideoSize;

	public override void _Ready()
	{
		_aspectRatioContainer = GetNode<AspectRatioContainer>("%AspectRatioContainer");
		_videoPlayer = GetNode<VideoStreamPlayer>("%VideoStreamPlayer");
		_videoPlayer.Finished += OnVideoFinished;
		GuiInput += OnGuiInput;
		SetProcess(false);
		HidePresentation();
	}

	public override void _ExitTree()
	{
		_videoPlayer.Finished -= OnVideoFinished;
		var completion = _completion;
		_completion = null;
		HidePresentation();
		completion?.TrySetCanceled();
	}

	public override void _Process(double delta)
	{
		if (!_isWaitingForVideoSize)
		{
			return;
		}

		var size = _videoPlayer.GetVideoTexture().GetSize();
		if (size.X <= 0f || size.Y <= 0f)
		{
			return;
		}

		_aspectRatioContainer.Ratio = size.X / size.Y;
		_isWaitingForVideoSize = false;
		SetProcess(false);
	}

	public async Task PlayAsync(VideoStream stream, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(stream);
		cancellationToken.ThrowIfCancellationRequested();
		if (IsActive())
		{
			throw new InvalidOperationException("A story video is already playing.");
		}

		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_completion = completion;
		_aspectRatioContainer.Ratio = DefaultAspectRatio;
		_videoPlayer.Stream = stream;
		_isWaitingForVideoSize = true;
		SetProcess(true);
		Show();
		_videoPlayer.Play();

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

	private void OnVideoFinished() => _completion?.TrySetResult();

	private void OnGuiInput(InputEvent inputEvent)
	{
		var shouldSkip = inputEvent is InputEventMouseButton
		{
			Pressed: true,
			ButtonIndex: MouseButton.Left,
		} or InputEventScreenTouch
		{
			Pressed: true,
		};
		if (!shouldSkip || !IsActive())
		{
			return;
		}

		_completion?.TrySetResult();
		AcceptEvent();
	}

	private bool IsActive() =>
		Visible && _completion is not null && !_completion.Task.IsCompleted;

	private void HidePresentation()
	{
		_isWaitingForVideoSize = false;
		SetProcess(false);
		_videoPlayer.Stop();
		_videoPlayer.Stream = null;
		Hide();
	}
}
