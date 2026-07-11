#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC_DIR="${ROOT_DIR}/Native/FidelityFXLinux"
BUILD_TYPE="${BUILD_TYPE:-Release}"
BUILD_DIR="${ROOT_DIR}/Builds/Native/FidelityFXLinux/${BUILD_TYPE}"
PLUGIN_DIR="${ROOT_DIR}/Assets/Plugins/x86_64"

command -v cmake >/dev/null 2>&1 || { echo "cmake não encontrado"; exit 2; }
command -v glslangValidator >/dev/null 2>&1 || { echo "glslangValidator não encontrado"; exit 2; }

UNITY_PATH="${UNITY_PATH:-/home/gullin/Unity/Hub/Editor/2022.3.62f3/Editor/Unity}"
UNITY_PLUGIN_API_DIR="${UNITY_PLUGIN_API_DIR:-$(dirname "${UNITY_PATH}")/Data/PluginAPI}"
[[ -f "${UNITY_PLUGIN_API_DIR}/IUnityGraphicsVulkan.h" ]] || { echo "IUnityGraphicsVulkan.h não encontrado em ${UNITY_PLUGIN_API_DIR}"; exit 2; }

mkdir -p "${BUILD_DIR}" "${PLUGIN_DIR}" "${ROOT_DIR}/Logs/linux-performance"

cmake -S "${SRC_DIR}" -B "${BUILD_DIR}" -DCMAKE_BUILD_TYPE="${BUILD_TYPE}" -DUNITY_PLUGIN_API_DIR="${UNITY_PLUGIN_API_DIR}" -G Ninja
cmake --build "${BUILD_DIR}" --config "${BUILD_TYPE}" --verbose

cp "${BUILD_DIR}/libFidelityFXLinux.so" "${PLUGIN_DIR}/libFidelityFXLinux.so"
echo "Plugin copiado para ${PLUGIN_DIR}/libFidelityFXLinux.so"
