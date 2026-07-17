#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

PROJECT_FILE="${REPO_ROOT}/engine-free-rpg.csproj"
BUILD_VERSION_FILE="${REPO_ROOT}/android/.build_version"
MANIFEST="${REPO_ROOT}/android/build/src/main/AndroidManifest.xml"
PLUGIN_SOURCE="${SCRIPT_DIR}/android/build/src/main/java/com/godot/game/JyxrAndroidStoragePlugin.java"
PLUGIN_TARGET="${REPO_ROOT}/android/build/src/main/java/com/godot/game/JyxrAndroidStoragePlugin.java"
EXPORT_PRESETS="${REPO_ROOT}/export_presets.cfg"

SDK_VERSION="$(sed -nE 's|.*<Project Sdk="Godot.NET.Sdk/([^"]+)">.*|\1|p' "${PROJECT_FILE}")"
FAILED=0

report_error() {
	echo "ERROR: $1" >&2
	FAILED=1
}

GODOT_BIN="${GODOT_BIN:-$(command -v godot || true)}"
if [[ -z "${GODOT_BIN}" ]]; then
	report_error "Godot was not found. Set GODOT_BIN to Godot ${SDK_VERSION} Mono."
else
	GODOT_VERSION="$("${GODOT_BIN}" --version | head -n 1)"
	if [[ "${GODOT_VERSION}" != "${SDK_VERSION}."* || "${GODOT_VERSION}" != *mono* ]]; then
		report_error "Godot must be ${SDK_VERSION} Mono, but '${GODOT_BIN}' reports '${GODOT_VERSION}'."
	fi
fi

EXPECTED_TEMPLATE_VERSION="${SDK_VERSION}.stable.mono"
if [[ ! -f "${BUILD_VERSION_FILE}" ]]; then
	report_error "Android template is missing. Generate android/ with Godot ${SDK_VERSION} Mono."
else
	ACTUAL_TEMPLATE_VERSION="$(tr -d '\r\n' < "${BUILD_VERSION_FILE}")"
	if [[ "${ACTUAL_TEMPLATE_VERSION}" != "${EXPECTED_TEMPLATE_VERSION}" ]]; then
		report_error "Android template is '${ACTUAL_TEMPLATE_VERSION}', expected '${EXPECTED_TEMPLATE_VERSION}'."
	fi
fi

if [[ ! -f "${PLUGIN_TARGET}" ]] || ! cmp -s "${PLUGIN_SOURCE}" "${PLUGIN_TARGET}"; then
	report_error "Android storage plugin is missing or stale. Run apply_android_template_patch.sh."
fi

if [[ ! -f "${MANIFEST}" ]] \
	|| ! grep -q 'android.permission.MANAGE_EXTERNAL_STORAGE' "${MANIFEST}" \
	|| ! grep -q 'org.godotengine.plugin.v2.JyxrAndroidStorage' "${MANIFEST}"; then
	report_error "AndroidManifest.xml is not patched. Run apply_android_template_patch.sh."
fi

if ! grep -q '^platform="Android"$' "${EXPORT_PRESETS}" \
	|| ! grep -q '^gradle_build/use_gradle_build=true$' "${EXPORT_PRESETS}" \
	|| ! grep -q '^gradle_build/gradle_build_directory="res://android"$' "${EXPORT_PRESETS}" \
	|| ! grep -q '^permissions/manage_external_storage=true$' "${EXPORT_PRESETS}"; then
	report_error "The main Android export preset is missing required custom-build or storage settings."
fi

if ! grep -q '^textures/vram_compression/import_etc2_astc=true$' "${REPO_ROOT}/project.godot"; then
	report_error "Android ETC2/ASTC texture import is not enabled in project.godot."
fi

if (( FAILED != 0 )); then
	exit 1
fi

echo "Android export setup is valid for Godot ${SDK_VERSION} Mono."
