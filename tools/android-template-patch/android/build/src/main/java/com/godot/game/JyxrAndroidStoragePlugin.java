package com.godot.game;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.provider.Settings;
import android.util.Log;

import org.godotengine.godot.Godot;
import org.godotengine.godot.plugin.GodotPlugin;
import org.godotengine.godot.plugin.UsedByGodot;

import java.io.File;

public final class JyxrAndroidStoragePlugin extends GodotPlugin {
	private static final String PLUGIN_NAME = "JyxrAndroidStorage";
	private static final String TAG = "JyxrAndroidStorage";
	private static final String PROJECT_DIRECTORY_NAME = "JYXR";

	public JyxrAndroidStoragePlugin(Godot godot) {
		super(godot);
	}

	@Override
	public String getPluginName() {
		return PLUGIN_NAME;
	}

	@UsedByGodot
	public int isAllFilesAccessGranted() {
		return hasAllFilesAccess() ? 1 : 0;
	}

	@UsedByGodot
	public int openAllFilesAccessSettings() {
		if (Build.VERSION.SDK_INT < Build.VERSION_CODES.R) {
			return 1;
		}

		Activity activity = getActivity();
		if (activity == null) {
			return 0;
		}

		Intent appSettingsIntent = new Intent(Settings.ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION);
		appSettingsIntent.setData(Uri.parse("package:" + activity.getPackageName()));
		if (tryStartActivity(activity, appSettingsIntent)) {
			return 1;
		}

		return tryStartActivity(activity, new Intent(Settings.ACTION_MANAGE_ALL_FILES_ACCESS_PERMISSION)) ? 1 : 0;
	}

	@UsedByGodot
	public String getProjectDataRootPath() {
		return getProjectDataRootDirectory().getAbsolutePath();
	}

	@UsedByGodot
	public int ensureProjectDataDirectories() {
		File rootDirectory = getProjectDataRootDirectory();
		boolean ready = ensureDirectory(rootDirectory)
				&& ensureDirectory(new File(rootDirectory, "mods"))
				&& ensureDirectory(new File(rootDirectory, "launcher"))
				&& ensureDirectory(new File(rootDirectory, "userdata"));

		if (!ready) {
			Log.e(TAG, getDebugState());
		}

		return ready ? 1 : 0;
	}

	@UsedByGodot
	public String getDebugState() {
		File rootDirectory = getProjectDataRootDirectory();
		return "plugin=" + PLUGIN_NAME
				+ "\nsdk=" + Build.VERSION.SDK_INT
				+ "\nallFilesAccessGranted=" + hasAllFilesAccess()
				+ "\nexternalStorageState=" + Environment.getExternalStorageState()
				+ "\nrootPath=" + rootDirectory.getAbsolutePath()
				+ "\nrootExists=" + rootDirectory.exists()
				+ "\nrootIsDirectory=" + rootDirectory.isDirectory()
				+ "\nrootCanRead=" + rootDirectory.canRead()
				+ "\nrootCanWrite=" + rootDirectory.canWrite()
				+ "\nmodsExists=" + new File(rootDirectory, "mods").exists()
				+ "\nlauncherExists=" + new File(rootDirectory, "launcher").exists()
				+ "\nuserdataExists=" + new File(rootDirectory, "userdata").exists();
	}

	private static boolean hasAllFilesAccess() {
		return Build.VERSION.SDK_INT < Build.VERSION_CODES.R || Environment.isExternalStorageManager();
	}

	private static File getProjectDataRootDirectory() {
		return new File(Environment.getExternalStorageDirectory(), PROJECT_DIRECTORY_NAME);
	}

	private static boolean ensureDirectory(File directory) {
		return directory.isDirectory() || directory.mkdirs();
	}

	private static boolean tryStartActivity(Activity activity, Intent intent) {
		try {
			activity.startActivity(intent);
			return true;
		} catch (Exception exception) {
			Log.e(TAG, "Unable to open Android storage permission settings.", exception);
			return false;
		}
	}
}
