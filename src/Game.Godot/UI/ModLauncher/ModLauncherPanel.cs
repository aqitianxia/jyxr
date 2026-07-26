using Game.Application.Mods;
using Godot;

namespace Game.Godot.UI.ModLauncher;

public partial class ModLauncherPanel : Control
{
	private const string ClientAuthor = "虹乡俗人";
	private const string ClientVersionSetting = "application/config/version";
	private const string AndroidProjectDataRootPath = "/storage/emulated/0/JYXR";

	private TextureButton _refreshButton = null!;
	private ModShowcasePage _showcasePage = null!;
	private Label _clientVersionLabel = null!;
	private ColorRect _loadingOverlay = null!;
	private Label _loadingTitleLabel = null!;
	private ProjectDataRoot _projectDataRoot = null!;
	private bool _isStarting;

	public override void _Ready()
	{
		_refreshButton = GetNode<TextureButton>("%LocalModButton");
		_showcasePage = GetNode<ModShowcasePage>("%ModShowcasePage");
		_clientVersionLabel = GetNode<Label>("%ClientVersionLabel");
		_loadingOverlay = GetNode<ColorRect>("%LoadingOverlay");
		_loadingTitleLabel = GetNode<Label>("%LoadingTitleLabel");

		var clientVersion = ProjectSettings.GetSetting(ClientVersionSetting).AsString();
		_clientVersionLabel.Text = $"XR 客户端 v{clientVersion} · 重制：{ClientAuthor} · 原作：汉家松鼠";
		_refreshButton.Pressed += RefreshMods;
		_showcasePage.StartRequested += OnStartRequested;

		_projectDataRoot = ProjectDataRoot.FromPath(ResolveProjectDataRootPath());
		RefreshMods();
	}

	private void RefreshMods()
	{
		if (!Directory.Exists(_projectDataRoot.ModsDirectoryPath))
		{
			_showcasePage.Configure([]);
			return;
		}

		var mods = new ModRegistry(_projectDataRoot).DiscoverMods();
		_showcasePage.Configure(mods);

		if (mods.Count == 0)
		{
			GD.PushWarning($"No valid mods found under '{_projectDataRoot.ModsDirectoryPath}'.");
		}
	}

	private async void OnStartRequested(ModContext context)
	{
		if (_isStarting)
		{
			return;
		}

		_isStarting = true;
		_loadingTitleLabel.Text = $"正在加载《{context.Manifest.Name.Trim()}》";
		_loadingOverlay.Show();

		try
		{
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			GameRuntimeBootstrap.Initialize(context, GetTree());
			SaveLauncherSettings(context);
			var error = GetTree().ChangeSceneToFile(GameFlow.MainMenuScenePath);
			if (error != Error.Ok)
			{
				throw new InvalidOperationException($"Changing to main menu failed: {error}.");
			}
		}
		catch (Exception exception)
		{
			_loadingOverlay.Hide();
			_isStarting = false;
			GD.PushError(exception.ToString());
			OS.Alert(exception.Message, "MOD 启动失败");
		}
	}

	private void SaveLauncherSettings(ModContext context)
	{
		var store = new LauncherSettingsStore(_projectDataRoot.LauncherSettingsPath);
		store.Save(new LauncherSettingsRecord(
			LauncherSettingsRecord.CurrentVersion,
			context.ModId));
	}

	private static string ResolveProjectDataRootPath()
	{
		if (OS.HasFeature("editor"))
		{
			return ProjectSettings.GlobalizePath("res://");
		}

		if (OS.HasFeature("android") || OS.HasFeature("web_android"))
		{
			return AndroidProjectDataRootPath;
		}

		return Path.GetDirectoryName(OS.GetExecutablePath()) ?? OS.GetUserDataDir();
	}
}
