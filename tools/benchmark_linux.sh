#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RESULT_DIR="${ROOT_DIR}/BenchmarkResults"
mkdir -p "${RESULT_DIR}"

COMMIT="$(git -C "${ROOT_DIR}" rev-parse --short HEAD)"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
JSON_FILE="${RESULT_DIR}/linux-benchmark-${STAMP}.json"
CSV_FILE="${RESULT_DIR}/linux-benchmark-${STAMP}.csv"

GPU="$(glxinfo -B 2>/dev/null | awk -F': ' '/OpenGL renderer string/ {print $2; exit}' || true)"
CPU="$(lscpu 2>/dev/null | awk -F': *' '/Model name/ {print $2; exit}' || true)"
KERNEL="$(uname -r)"

cat > "${JSON_FILE}" <<JSON
{
  "commit": "${COMMIT}",
  "kernel": "${KERNEL}",
  "gpu": "${GPU}",
  "cpu": "${CPU}",
  "status": "ferramenta criada; benchmark de gameplay não executado por este script sem cenário automatizado",
  "tested_on_target_hardware": false
}
JSON

cat > "${CSV_FILE}" <<CSV
commit,kernel,gpu,cpu,status,tested_on_target_hardware
"${COMMIT}","${KERNEL}","${GPU}","${CPU}","ferramenta criada; benchmark de gameplay não executado por este script sem cenário automatizado",false
CSV

echo "JSON: ${JSON_FILE}"
echo "CSV: ${CSV_FILE}"
