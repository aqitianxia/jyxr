using Godot;

namespace Game.Godot.UI;

public sealed partial class StoryVisualEffects : Control
{
	private const string FilterPresetNames = "grayscale, sepia, cold, warm, poison, night";
	private const string FlashPresetNames = "white, red, gold, blue";

	private readonly TweenChannel _filterChannel = new();
	private readonly TweenChannel _flashChannel = new();
	private readonly TweenChannel _fadeChannel = new();
	private readonly TweenChannel _shakeChannel = new();
	private ColorRect _filterRect = null!;
	private ColorRect _flashRect = null!;
	private ColorRect _fadeRect = null!;
	private ShaderMaterial _filterMaterial = null!;
	private string? _filterPreset;
	private float _filterStrength;
	private float _flashStrength;
	private float _fadeStrength;
	private Vector2 _screenOffset;

	public override void _Ready()
	{
		_filterRect = GetNode<ColorRect>("%FilterRect");
		_flashRect = GetNode<ColorRect>("%FlashRect");
		_fadeRect = GetNode<ColorRect>("%FadeRect");
		_filterMaterial = _filterRect.Material as ShaderMaterial
			?? throw new InvalidOperationException("Story filter rect must use a ShaderMaterial.");
		ResetImmediate();
	}

	public override void _ExitTree()
	{
		CancelChannel(_filterChannel);
		CancelChannel(_flashChannel);
		CancelChannel(_fadeChannel);
		CancelChannel(_shakeChannel);
	}

	public Task FadeAsync(string mode, double duration, CancellationToken cancellationToken)
	{
		ValidateDuration(duration, "fade");
		var target = mode.Trim() switch
		{
			"out" => 1f,
			"in" => 0f,
			_ => throw new InvalidOperationException($"Unsupported fade mode '{mode}'. Use 'out' or 'in'."),
		};

		if (duration == 0d)
		{
			cancellationToken.ThrowIfCancellationRequested();
			CancelChannel(_fadeChannel);
			SetFadeStrength(target);
			return Task.CompletedTask;
		}

		return RunTweenAsync(
			_fadeChannel,
			tween => tween.TweenMethod(Callable.From<float>(SetFadeStrength), _fadeStrength, target, duration),
			cancellationToken);
	}

	public Task FlashAsync(string preset, double duration, double strength, CancellationToken cancellationToken)
	{
		ValidateDuration(duration, "flash");
		var targetStrength = ValidateStrength(strength, "flash");
		var color = ResolveFlashColor(preset);
		CancelChannel(_flashChannel);
		_flashRect.Color = new Color(color.R, color.G, color.B, _flashStrength);

		if (duration == 0d)
		{
			cancellationToken.ThrowIfCancellationRequested();
			SetFlashStrength(0f);
			return Task.CompletedTask;
		}

		var halfDuration = duration / 2d;
		return RunTweenAsync(
			_flashChannel,
			tween =>
			{
				tween.TweenMethod(Callable.From<float>(SetFlashStrength), _flashStrength, targetStrength, halfDuration);
				tween.TweenMethod(Callable.From<float>(SetFlashStrength), targetStrength, 0f, halfDuration);
			},
			cancellationToken);
	}

	public Task ApplyFilterAsync(string preset, double strength, double duration, CancellationToken cancellationToken)
	{
		ValidateDuration(duration, "filter");
		var presetId = ResolveFilterPreset(preset);
		var targetStrength = ValidateStrength(strength, "filter");
		var normalizedPreset = preset.Trim();

		if (duration == 0d)
		{
			cancellationToken.ThrowIfCancellationRequested();
			CancelChannel(_filterChannel);
			SetFilterPreset(normalizedPreset, presetId);
			SetFilterStrength(targetStrength);
			return Task.CompletedTask;
		}

		if (_filterPreset is not null &&
			!string.Equals(_filterPreset, normalizedPreset, StringComparison.Ordinal) &&
			_filterStrength > 0f)
		{
			var halfDuration = duration / 2d;
			return RunTweenAsync(
				_filterChannel,
				tween =>
				{
					tween.TweenMethod(Callable.From<float>(SetFilterStrength), _filterStrength, 0f, halfDuration);
					tween.TweenCallback(Callable.From(() => SetFilterPreset(normalizedPreset, presetId)));
					tween.TweenMethod(Callable.From<float>(SetFilterStrength), 0f, targetStrength, halfDuration);
				},
				cancellationToken);
		}

		SetFilterPreset(normalizedPreset, presetId);
		return RunTweenAsync(
			_filterChannel,
			tween => tween.TweenMethod(Callable.From<float>(SetFilterStrength), _filterStrength, targetStrength, duration),
			cancellationToken);
	}

	public Task ClearFilterAsync(double duration, CancellationToken cancellationToken)
	{
		ValidateDuration(duration, "filter_clear");
		if (duration == 0d || _filterStrength == 0f)
		{
			cancellationToken.ThrowIfCancellationRequested();
			CancelChannel(_filterChannel);
			ClearFilterState();
			return Task.CompletedTask;
		}

		return ClearFilterCoreAsync(duration, cancellationToken);
	}

	public Task ShakeAsync(float amplitude, double duration, CancellationToken cancellationToken)
	{
		ValidateAmplitude(amplitude);
		ValidateDuration(duration, "shake");
		if (duration == 0d)
		{
			cancellationToken.ThrowIfCancellationRequested();
			CancelChannel(_shakeChannel);
			SetScreenOffset(Vector2.Zero);
			return Task.CompletedTask;
		}

		return ShakeCoreAsync(amplitude, duration, cancellationToken);
	}

	public async Task WaitAsync(double duration, CancellationToken cancellationToken)
	{
		ValidateDuration(duration, "wait");
		cancellationToken.ThrowIfCancellationRequested();
		if (duration == 0d)
		{
			return;
		}

		var timer = GetTree().CreateTimer(duration, processAlways: true, processInPhysics: false, ignoreTimeScale: true);
		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		void OnTimeout() => completion.TrySetResult();
		timer.Timeout += OnTimeout;

		using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
		try
		{
			await completion.Task;
		}
		finally
		{
			timer.Timeout -= OnTimeout;
		}
	}

	public void ResetImmediate()
	{
		CancelChannel(_filterChannel);
		CancelChannel(_flashChannel);
		CancelChannel(_fadeChannel);
		CancelChannel(_shakeChannel);
		ClearFilterState();
		SetFlashStrength(0f);
		SetFadeStrength(0f);
		SetScreenOffset(Vector2.Zero);
	}

	private async Task ShakeCoreAsync(float amplitude, double duration, CancellationToken cancellationToken)
	{
		const int vibrationCount = 10;
		var stepDuration = duration / (vibrationCount + 1);

		try
		{
			await RunTweenAsync(
				_shakeChannel,
				tween =>
				{
					var currentOffset = _screenOffset;
					for (var index = 0; index < vibrationCount; index++)
					{
						var strength = amplitude * (1f - (float)index / vibrationCount);
						var offset = new Vector2(
							Random.Shared.NextSingle() * 2f - 1f,
							Random.Shared.NextSingle() * 2f - 1f) * strength;
						tween.TweenMethod(Callable.From<Vector2>(SetScreenOffset), currentOffset, offset, stepDuration);
						currentOffset = offset;
					}

					tween.TweenMethod(Callable.From<Vector2>(SetScreenOffset), currentOffset, Vector2.Zero, stepDuration);
				},
				cancellationToken);
		}
		finally
		{
			if (_shakeChannel.Tween is null)
			{
				SetScreenOffset(Vector2.Zero);
			}
		}
	}

	private async Task ClearFilterCoreAsync(double duration, CancellationToken cancellationToken)
	{
		await RunTweenAsync(
			_filterChannel,
			tween => tween.TweenMethod(Callable.From<float>(SetFilterStrength), _filterStrength, 0f, duration),
			cancellationToken);
		ClearFilterState();
	}

	private Task RunTweenAsync(
		TweenChannel channel,
		Action<Tween> configure,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		CancelChannel(channel);

		var tween = CreateTween();
		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		channel.Tween = tween;
		channel.Completion = completion;

		void OnFinished()
		{
			if (ReferenceEquals(channel.Tween, tween))
			{
				channel.Tween = null;
				channel.Completion = null;
			}

			completion.TrySetResult();
		}

		tween.Finished += OnFinished;
		configure(tween);
		return AwaitTweenAsync(channel, tween, completion, OnFinished, cancellationToken);
	}

	private static async Task AwaitTweenAsync(
		TweenChannel channel,
		Tween tween,
		TaskCompletionSource completion,
		Action finishedHandler,
		CancellationToken cancellationToken)
	{
		using var registration = cancellationToken.Register(() =>
		{
			if (ReferenceEquals(channel.Tween, tween))
			{
				tween.Kill();
				channel.Tween = null;
				channel.Completion = null;
			}

			completion.TrySetCanceled(cancellationToken);
		});

		try
		{
			await completion.Task;
		}
		finally
		{
			tween.Finished -= finishedHandler;
		}
	}

	private static void CancelChannel(TweenChannel channel)
	{
		channel.Tween?.Kill();
		channel.Tween = null;
		channel.Completion?.TrySetCanceled();
		channel.Completion = null;
	}

	private void SetFilterPreset(string preset, int presetId)
	{
		_filterPreset = preset;
		_filterMaterial.SetShaderParameter("preset", presetId);
	}

	private void SetFilterStrength(float strength)
	{
		_filterStrength = strength;
		_filterMaterial.SetShaderParameter("strength", strength);
		UpdateScreenEffectVisibility();
	}

	private void SetScreenOffset(Vector2 offset)
	{
		_screenOffset = offset;
		_filterMaterial.SetShaderParameter("screen_offset", offset);
		UpdateScreenEffectVisibility();
	}

	private void UpdateScreenEffectVisibility() =>
		_filterRect.Visible = _filterStrength > 0f || _screenOffset != Vector2.Zero;

	private void ClearFilterState()
	{
		_filterPreset = null;
		SetFilterStrength(0f);
	}

	private void SetFlashStrength(float strength)
	{
		_flashStrength = strength;
		var color = _flashRect.Color;
		_flashRect.Color = new Color(color.R, color.G, color.B, strength);
		_flashRect.Visible = strength > 0f;
	}

	private void SetFadeStrength(float strength)
	{
		_fadeStrength = strength;
		_fadeRect.Color = new Color(0f, 0f, 0f, strength);
		_fadeRect.Visible = strength > 0f;
	}

	private static int ResolveFilterPreset(string preset) => preset.Trim() switch
	{
		"grayscale" => 0,
		"sepia" => 1,
		"cold" => 2,
		"warm" => 3,
		"poison" => 4,
		"night" => 5,
		_ => throw new InvalidOperationException(
			$"Unsupported filter preset '{preset}'. Use one of: {FilterPresetNames}."),
	};

	private static Color ResolveFlashColor(string preset) => preset.Trim() switch
	{
		"white" => Colors.White,
		"red" => new Color("e53935"),
		"gold" => new Color("ffd54f"),
		"blue" => new Color("42a5f5"),
		_ => throw new InvalidOperationException(
			$"Unsupported flash preset '{preset}'. Use one of: {FlashPresetNames}."),
	};

	private static void ValidateDuration(double duration, string commandName)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(duration, nameof(duration));
		if (!double.IsFinite(duration))
		{
			throw new ArgumentOutOfRangeException(nameof(duration), $"Command '{commandName}' duration must be finite.");
		}
	}

	private static float ValidateStrength(double strength, string commandName)
	{
		if (!double.IsFinite(strength) || strength is < 0d or > 1d)
		{
			throw new ArgumentOutOfRangeException(nameof(strength), $"Command '{commandName}' strength must be between 0 and 1.");
		}

		return (float)strength;
	}

	private static void ValidateAmplitude(float amplitude)
	{
		if (!float.IsFinite(amplitude) || amplitude < 0f)
		{
			throw new ArgumentOutOfRangeException(nameof(amplitude), "Command 'shake' amplitude must be finite and non-negative.");
		}
	}

	private sealed class TweenChannel
	{
		public Tween? Tween { get; set; }
		public TaskCompletionSource? Completion { get; set; }
	}
}
