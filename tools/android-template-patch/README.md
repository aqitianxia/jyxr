# Android 外部 MOD 存储补丁

本目录维护 Android 自定义构建模板需要的最小补丁。仓库中的 `android/` 是 Godot 生成目录，不提交；重新生成模板后，通过这里的脚本恢复外部 MOD 存储插件。

项目版本以 `engine-free-rpg.csproj` 为准。当前必须使用 Godot 4.7.1 Mono，脚本会拒绝修改其他版本生成的模板。

## 功能

- 注册 Godot Android 插件 `JyxrAndroidStorage`。
- 申请 Android 11+ 的“所有文件访问权限”。
- 创建并访问 `/storage/emulated/0/JYXR`。
- 创建 `mods`、`launcher`、`userdata` 子目录。
- 启动器从系统权限页返回后自动重新发现 MOD。

## 安装

1. 使用与 `engine-free-rpg.csproj` 相同版本的 Godot Mono 安装 Android 构建模板。
2. 在 Godot 中为项目生成 `android/` 自定义构建目录。
3. 应用并验证补丁：

```bash
tools/android-template-patch/apply_android_template_patch.sh
GODOT_BIN="/path/to/Godot_mono" tools/android-template-patch/verify_android_export_setup.sh
```

重新生成 `android/` 后必须重新运行补丁脚本。脚本只修改生成的 Android 模板，不修改 `export_presets.cfg` 或 `project.godot`。

## 手机目录

```text
/storage/emulated/0/JYXR/
  mods/
    <modId>/
      mod.json
      data/
      *.pck
  launcher/
  userdata/
```

导出 APK 后，首次启动会跳转到系统权限页。授权并返回游戏后，启动器应能发现 `mods` 目录下的有效 MOD。
