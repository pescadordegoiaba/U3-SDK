#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG_DIR="${ROOT_DIR}/Logs/linux-performance"
mkdir -p "${LOG_DIR}"
LOG_FILE="${LOG_DIR}/environment.txt"

{
	echo "== Sistema =="
	uname -a
	echo
	echo "== CPU =="
	lscpu || true
	echo
	echo "== Memória =="
	free -h || true
	echo
	echo "== Ferramentas =="
	for tool in git python3 cmake ninja glslangValidator vulkaninfo glxinfo; do
		if command -v "${tool}" >/dev/null 2>&1; then
			echo "${tool}: $(command -v "${tool}")"
		else
			echo "${tool}: ausente"
		fi
	done
	echo
	echo "== Vulkan =="
	if command -v vulkaninfo >/dev/null 2>&1; then
		vulkaninfo --summary || true
	else
		echo "vulkaninfo ausente"
	fi
	echo
	echo "== OpenGL =="
	if command -v glxinfo >/dev/null 2>&1; then
		glxinfo -B || true
	else
		echo "glxinfo ausente"
	fi
} | tee "${LOG_FILE}"

echo "Log: ${LOG_FILE}"
