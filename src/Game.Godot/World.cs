using Game.Application;
using Game.Godot.Assets;
using Game.Godot.Map;
using Game.Godot.Persistence;
using Game.Godot.UI;
using Godot;

namespace Game.Godot;

public partial class World : Control
{
	public static World Instance { get; private set; } = null!;
	
	[Export]
	public PackedScene MapScreenScene { get; set; } = null!;

	private Vector2 _basePosition;
	private Tween? _screenShakeTween;
	private TextureRect _background = null!;

	public Control? CurrentScene { get; private set; }

	public AutoSaveCoordinator AutoSave { get; private set; } = null!;

	public override void _Ready()
	{
		_basePosition = Position;
		_background = GetNode<TextureRect>("%Background");
		AutoSave = GetNode<AutoSaveCoordinator>("%AutoSaveCoordinator");
		Instance = this;
	}

	public MapScreen ShowMap(string mapId)
	{
		var result = Game.MapService.EnterMap(mapId);
		return ShowMap(result);
	}

	public MapScreen EnterMap(string mapId) =>
		ShowMap(Game.MapService.EnterMap(mapId));

	public void ShowStoryAnimation(string animationId)
	{
		if (string.IsNullOrWhiteSpace(animationId))
		{
			throw new ArgumentException("Animation id cannot be empty.", nameof(animationId));
		}

		Game.Logger.Info($"Story animation requested: {animationId}");
	}

	public void SetBackground(string? resourceId)
	{
		_background.Texture = AssetResolver.LoadTextureResource(resourceId);
		_background.Visible = _background.Texture is not null;
	}

	public void PlayScreenShake(float amplitude = 10f, double durationSeconds = 0.5d)
	{
		const int vibrationCount = 10;
		var stepDuration = durationSeconds / (vibrationCount + 1);

		_screenShakeTween?.Kill();
		Position = _basePosition;

		var tween = CreateTween();
		_screenShakeTween = tween;

		for (var index = 0; index < vibrationCount; index++)
		{
			var strength = amplitude * (1f - (float)index / vibrationCount);
			var offset = new Vector2(
				Random.Shared.NextSingle() * 2f - 1f,
				Random.Shared.NextSingle() * 2f - 1f) * strength;
			tween.TweenProperty(this, "position", _basePosition + offset, stepDuration);
		}

		tween.TweenProperty(this, "position", _basePosition, stepDuration);
	}

	private MapScreen ShowMap(MapEnterResult result)
	{
		var instance = MapScreenScene.Instantiate();
		if (instance is not MapScreen mapScreen)
		{
			instance.QueueFree();
			throw new InvalidOperationException("Map screen scene root must be MapScreen.");
		}

		mapScreen.Initialize(result);
		ReplaceCurrentScene(mapScreen);
		return mapScreen;
	}

	public MapScreen RefreshCurrentMap() =>
		ShowMap(Game.State.Location.CurrentMapId);

	private void ReplaceCurrentScene(Control scene)
	{
		CurrentScene?.QueueFree();
		CurrentScene = scene;
		AddChild(scene);

		if (scene is MapScreen mapScreen && UIRoot.Instance is not null)
		{
			mapScreen.SetStoryPresentationActive(UIRoot.Instance.IsStoryPresentationActive);
		}
	}
}
