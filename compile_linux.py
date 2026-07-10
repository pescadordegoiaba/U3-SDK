#!/usr/bin/env python3
import argparse
import os
import subprocess
import sys
import time
from pathlib import Path


DEFAULT_UNITY_PATH = "/home/gullin/Unity/Hub/Editor/2022.3.62f3/Editor/Unity"
BUILD_METHOD = "BuildMethods.runLinuxBuildFromCommandLine"
ESTIMATED_BUILD_SECONDS = 40 * 60
PROGRESS_BAR_WIDTH = 32


def parse_args() -> argparse.Namespace:
    project_root = Path(__file__).resolve().parent

    parser = argparse.ArgumentParser(
        description="Build Unturned Linux player and dedicated server with Unity."
    )
    parser.add_argument(
        "--unity",
        default=os.environ.get("UNITY_PATH", DEFAULT_UNITY_PATH),
        help="Path to the Unity executable. Defaults to UNITY_PATH or the local 2022.3.62f3 install.",
    )
    parser.add_argument(
        "--project-path",
        default=str(project_root),
        help="Unity project path. Defaults to this repository root.",
    )
    parser.add_argument(
        "--log-file",
        default=str(project_root / "Logs" / "LinuxBuild.log"),
        help="Unity build log path.",
    )
    parser.add_argument(
        "--scripts-only",
        action="store_true",
        help="Pass -scriptsOnly to the Unity build method.",
    )
    return parser.parse_args()


def print_build_notes(log_file: Path) -> None:
    print("Preparing native Linux build for player and dedicated server.")
    print("This project contains large shader sets. First-time shader compilation can take a long time.")
    print("Expected build time: about 30-40 minutes, depending on CPU/cache state.")
    print(f"Unity log: {log_file}")
    print()


def render_progress(elapsed_seconds: float, stage: str) -> None:
    progress = min(elapsed_seconds / ESTIMATED_BUILD_SECONDS, 0.99)
    filled = int(progress * PROGRESS_BAR_WIDTH)
    empty = PROGRESS_BAR_WIDTH - filled
    elapsed_minutes = int(elapsed_seconds // 60)
    elapsed_remainder = int(elapsed_seconds % 60)
    percent = int(progress * 100)
    bar = "#" * filled + "-" * empty
    message = (
        f"\r[{bar}] {percent:3d}% "
        f"elapsed {elapsed_minutes:02d}:{elapsed_remainder:02d} "
        f"{stage[:60]:60s}"
    )
    print(message, end="", flush=True)


def render_completed_progress(elapsed_seconds: float, stage: str) -> None:
    elapsed_minutes = int(elapsed_seconds // 60)
    elapsed_remainder = int(elapsed_seconds % 60)
    bar = "#" * PROGRESS_BAR_WIDTH
    message = (
        f"\r[{bar}] 100% "
        f"elapsed {elapsed_minutes:02d}:{elapsed_remainder:02d} "
        f"{stage[:60]:60s}"
    )
    print(message, flush=True)


def read_build_stage(project_path: Path, log_file: Path) -> str:
    report_path = project_path / "Build_Scripts" / "CI_Report.txt"
    try:
        report_lines = [
            line.strip()
            for line in report_path.read_text(encoding="utf-8", errors="replace").splitlines()
            if line.strip()
        ]
        if report_lines:
            return report_lines[-1]
    except FileNotFoundError:
        pass

    try:
        with log_file.open("rb") as log:
            log.seek(0, os.SEEK_END)
            log.seek(max(log.tell() - 8192, 0), os.SEEK_SET)
            lines = log.read().decode("utf-8", errors="replace").splitlines()
    except FileNotFoundError:
        return "starting Unity"

    for line in reversed(lines):
        line = line.strip()
        if not line:
            continue
        if line.startswith("Compiling shader "):
            return "compiling large shader variants"
        if "CopyFiles Builds/" in line:
            return "copying build files"
        if "Building Linux64" in line:
            return line

    return "running Unity build"


def run_unity_build(command: list[str], project_path: Path, log_file: Path) -> int:
    started_at = time.monotonic()
    process = subprocess.Popen(command)

    while True:
        return_code = process.poll()
        elapsed = time.monotonic() - started_at
        stage = read_build_stage(project_path, log_file)
        render_progress(elapsed, stage)

        if return_code is not None:
            if return_code == 0:
                render_completed_progress(elapsed, stage)
            else:
                print()
            return return_code

        time.sleep(1)


def main() -> int:
    args = parse_args()
    unity_path = Path(args.unity).expanduser()
    project_path = Path(args.project_path).expanduser().resolve()
    log_file = Path(args.log_file).expanduser().resolve()

    if not unity_path.is_file():
        print(f"Unity executable not found: {unity_path}", file=sys.stderr)
        return 2

    if not project_path.joinpath("ProjectSettings", "ProjectVersion.txt").is_file():
        print(f"Unity project not found: {project_path}", file=sys.stderr)
        return 2

    log_file.parent.mkdir(parents=True, exist_ok=True)
    build_scripts_path = project_path.joinpath("Build_Scripts")
    build_scripts_path.mkdir(parents=True, exist_ok=True)
    build_scripts_path.joinpath("CI_Report.txt").unlink(missing_ok=True)

    command = [
        str(unity_path),
        "-batchmode",
        "-quit",
        "-projectPath",
        str(project_path),
        "-executeMethod",
        BUILD_METHOD,
        "-logFile",
        str(log_file),
    ]

    if args.scripts_only:
        command.append("-scriptsOnly")

    print_build_notes(log_file)
    print("Running Unity Linux build:")
    print(" ".join(command))
    print()

    return_code = run_unity_build(command, project_path, log_file)
    if return_code != 0:
        print(f"Unity build failed with exit code {return_code}.", file=sys.stderr)
        print(f"See log: {log_file}", file=sys.stderr)
        return return_code

    print("Unity Linux build completed successfully.")
    print(f"Log: {log_file}")
    print(f"Player: {project_path / 'Builds' / 'Linux64' / 'Unturned.x86_64'}")
    print(f"Server: {project_path / 'Builds' / 'Linux64_Headless' / 'Unturned_Headless.x86_64'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
