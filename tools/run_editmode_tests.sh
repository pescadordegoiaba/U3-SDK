#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_PATH="${UNITY_PATH:-${ROOT_DIR}/Unity/Editor/Unity}"
if [[ ! -x "${UNITY_PATH}" ]]; then
	UNITY_PATH="${UNITY_PATH_FALLBACK:-/home/gullin/Unity/Hub/Editor/2022.3.62f3/Editor/Unity}"
fi
LOG_DIR="${ROOT_DIR}/Logs/linux-performance"
RESULTS_DIR="${ROOT_DIR}/TestResults"
mkdir -p "${LOG_DIR}" "${RESULTS_DIR}"

RESULT_FILE="${RESULTS_DIR}/editmode-linux-performance.xml"
rm -f "${RESULT_FILE}"

UNITY_EXIT=0
"${UNITY_PATH}" -batchmode -automated -quit -projectPath "${ROOT_DIR}" -runTests -testPlatform EditMode -testResults "${RESULT_FILE}" -logFile "${LOG_DIR}/editmode.log" || UNITY_EXIT=$?
if [[ "${UNITY_EXIT}" -ne 0 ]]; then
	echo "Unity retornou código ${UNITY_EXIT} durante testes EditMode. Verifique ${LOG_DIR}/editmode.log" >&2
	exit "${UNITY_EXIT}"
fi

if [[ ! -f "${RESULT_FILE}" ]]; then
	echo "Unity não gerou resultados EditMode em ${RESULT_FILE}. Verifique ${LOG_DIR}/editmode.log" >&2
	exit 3
fi

python3 - "${RESULT_FILE}" <<'PY'
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
root = ET.parse(path).getroot()
total = int(root.attrib.get("total", "0"))
failed = int(root.attrib.get("failed", "0"))
passed = int(root.attrib.get("passed", "0"))
skipped = int(root.attrib.get("skipped", "0"))
if total <= 0:
    print(f"XML de EditMode não contém testes: {path}", file=sys.stderr)
    sys.exit(4)
print(f"EditMode: total={total} passed={passed} failed={failed} skipped={skipped}")
if failed:
    sys.exit(5)
PY
