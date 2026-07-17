using Godot;

namespace Game.Godot.Platform;

public sealed class AndroidExternalStorageAccess
{
	private const string PluginName = "JyxrAndroidStorage";
	private const string FallbackProjectDataRootPath = "/storage/emulated/0/JYXR";

	private readonly GodotObject? _plugin;

	private AndroidExternalStorageAccess(GodotObject? plugin)
	{
		_plugin = plugin;
	}

	public bool IsAvailable => _plugin is not null;

	public static AndroidExternalStorageAccess Create()
	{
		return Engine.HasSingleton(PluginName)
			? new AndroidExternalStorageAccess(Engine.GetSingleton(PluginName))
			: new AndroidExternalStorageAccess(null);
	}

	public string GetProjectDataRootPath()
	{
		if (_plugin is null)
		{
			return FallbackProjectDataRootPath;
		}

		var path = _plugin.Call("getProjectDataRootPath").AsString();
		return string.IsNullOrWhiteSpace(path) ? FallbackProjectDataRootPath : path;
	}

	public bool IsAllFilesAccessGranted()
	{
		return _plugin is not null
			&& _plugin.Call("isAllFilesAccessGranted").AsInt32() == 1;
	}

	public bool OpenAllFilesAccessSettings()
	{
		return _plugin is not null
			&& _plugin.Call("openAllFilesAccessSettings").AsInt32() == 1;
	}

	public bool EnsureProjectDataDirectories()
	{
		return _plugin is not null
			&& _plugin.Call("ensureProjectDataDirectories").AsInt32() == 1;
	}

	public string GetDebugState()
	{
		return _plugin is null
			? $"plugin={PluginName}\navailable=false\nrootPath={FallbackProjectDataRootPath}"
			: _plugin.Call("getDebugState").AsString();
	}
}
