using Game.Application.Mods;
using Game.Godot.Platform;
using Godot;

namespace Game.Godot.UI.ModLauncher;

public partial class ModLauncherPanel : Control
{
	private const string ClientVersion = "V0.1.0";
	private const string AndroidProjectDataRootPath = "/storage/emulated/0/JYXR";

	private TextureButton _refreshButton = null!;
	private ModShowcasePage _showcasePage = null!;
	private Label _clientVersionLabel = null!;
	private ProjectDataRoot _projectDataRoot = null!;
	private AndroidExternalStorageAccess? _androidStorage;
	private bool _waitingForAndroidStoragePermission;

	public override void _Ready()
	{
		_refreshButton = GetNode<TextureButton>("%LocalModButton");
		_showcasePage = GetNode<ModShowcasePage>("%ModShowcasePage");
		_clientVersionLabel = GetNode<Label>("%ClientVersionLabel");

		_clientVersionLabel.Text = $"XR客户端版本: {ClientVersion}";
		_refreshButton.Pressed += OnRefreshRequested;
		_showcasePage.StartRequested += OnStartRequested;

		_projectDataRoot = ProjectDataRoot.FromPath(ResolveProjectDataRootPath());
		if (IsAndroidRuntime())
		{
			InitializeAndroidProjectDataRoot();
			return;
		}

		RefreshMods();
	}

	public override void _Notification(int what)
	{
		if (what != NotificationApplicationResumed || !_waitingForAndroidStoragePermission)
		{
			return;
		}

		_waitingForAndroidStoragePermission = false;
		InitializeAndroidProjectDataRoot();
	}

	private void OnRefreshRequested()
	{
		if (IsAndroidRuntime())
		{
			InitializeAndroidProjectDataRoot();
			return;
		}

		RefreshMods();
	}

	private void InitializeAndroidProjectDataRoot()
	{
		_androidStorage ??= AndroidExternalStorageAccess.Create();
		_projectDataRoot = ProjectDataRoot.FromPath(_androidStorage.GetProjectDataRootPath());

		if (!_androidStorage.IsAvailable)
		{
			_showcasePage.Configure([]);
			OS.Alert("Android 存储插件未注册，无法访问外置 MOD 目录。请重新安装 Android 模板补丁后导出。", "Android 存储不可用");
			return;
		}

		if (!_androidStorage.IsAllFilesAccessGranted())
		{
			_showcasePage.Configure([]);
			_waitingForAndroidStoragePermission = true;
			OS.Alert("需要授予“所有文件访问权限”，用于读取 /storage/emulated/0/JYXR 下的 MOD、存档和设置。授权后请返回游戏。", "需要存储权限");
			if (!_androidStorage.OpenAllFilesAccessSettings())
			{
				_waitingForAndroidStoragePermission = false;
				OS.Alert(_androidStorage.GetDebugState(), "无法打开权限设置");
			}

			return;
		}

		if (!_androidStorage.EnsureProjectDataDirectories())
		{
			_showcasePage.Configure([]);
			OS.Alert(_androidStorage.GetDebugState(), "创建 JYXR 目录失败");
			return;
		}

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

	private void OnStartRequested(ModContext context)
	{
		try
		{
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

		if (IsAndroidRuntime())
		{
			return AndroidProjectDataRootPath;
		}

		return Path.GetDirectoryName(OS.GetExecutablePath()) ?? OS.GetUserDataDir();
	}

	private static bool IsAndroidRuntime()
	{
		return OS.HasFeature("android") || OS.HasFeature("web_android");
	}
}
