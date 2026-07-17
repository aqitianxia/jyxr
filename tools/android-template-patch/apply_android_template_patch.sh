#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

PROJECT_FILE="${REPO_ROOT}/engine-free-rpg.csproj"
BUILD_VERSION_FILE="${REPO_ROOT}/android/.build_version"
MANIFEST="${REPO_ROOT}/android/build/src/main/AndroidManifest.xml"
PLUGIN_SOURCE="${SCRIPT_DIR}/android/build/src/main/java/com/godot/game/JyxrAndroidStoragePlugin.java"
PLUGIN_TARGET="${REPO_ROOT}/android/build/src/main/java/com/godot/game/JyxrAndroidStoragePlugin.java"

SDK_VERSION="$(sed -nE 's|.*<Project Sdk="Godot.NET.Sdk/([^"]+)">.*|\1|p' "${PROJECT_FILE}")"
if [[ -z "${SDK_VERSION}" ]]; then
	echo "Unable to read the Godot SDK version from ${PROJECT_FILE}." >&2
	exit 1
fi

EXPECTED_TEMPLATE_VERSION="${SDK_VERSION}.stable.mono"
if [[ ! -f "${BUILD_VERSION_FILE}" ]]; then
	echo "Android template is missing. Generate android/ with Godot ${SDK_VERSION} Mono first." >&2
	exit 1
fi

ACTUAL_TEMPLATE_VERSION="$(tr -d '\r\n' < "${BUILD_VERSION_FILE}")"
if [[ "${ACTUAL_TEMPLATE_VERSION}" != "${EXPECTED_TEMPLATE_VERSION}" ]]; then
	echo "Android template version mismatch: expected '${EXPECTED_TEMPLATE_VERSION}', got '${ACTUAL_TEMPLATE_VERSION}'." >&2
	exit 1
fi

if [[ ! -f "${MANIFEST}" ]]; then
	echo "Android manifest was not found: ${MANIFEST}" >&2
	exit 1
fi

mkdir -p "$(dirname "${PLUGIN_TARGET}")"
cp "${PLUGIN_SOURCE}" "${PLUGIN_TARGET}"

if ! grep -q 'android.permission.MANAGE_EXTERNAL_STORAGE' "${MANIFEST}"; then
	perl -0pi -e 's|(\n\s*<application\b)|\n    <uses-permission android:name="android.permission.MANAGE_EXTERNAL_STORAGE" />$1|s' "${MANIFEST}"
fi

if ! grep -q 'org.godotengine.plugin.v2.JyxrAndroidStorage' "${MANIFEST}"; then
	perl -0pi -e 's|(\n\s*</application>)|\n        <meta-data\n            android:name="org.godotengine.plugin.v2.JyxrAndroidStorage"\n            android:value="com.godot.game.JyxrAndroidStoragePlugin" />$1|s' "${MANIFEST}"
fi

if ! grep -q 'android.permission.MANAGE_EXTERNAL_STORAGE' "${MANIFEST}"; then
	echo "Failed to add MANAGE_EXTERNAL_STORAGE to ${MANIFEST}." >&2
	exit 1
fi

if ! grep -q 'org.godotengine.plugin.v2.JyxrAndroidStorage' "${MANIFEST}"; then
	echo "Failed to register JyxrAndroidStorage in ${MANIFEST}." >&2
	exit 1
fi

echo "Android storage template patch applied for Godot ${SDK_VERSION} Mono."
