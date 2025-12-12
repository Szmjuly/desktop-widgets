#!/usr/bin/env python3
"""
Spec Header Date Updater - Main Launcher

This script launches the main application (V2 UI) or runs build commands.
"""

import sys
import subprocess
from pathlib import Path
import argparse


# Add src directory to Python path so we can import src.main_v2
src_dir = Path(__file__).parent / 'src'
sys.path.insert(0, str(src_dir))


def run_gui() -> None:
    """Launch the V2 UI application."""
    from main_v2 import main as gui_main
    gui_main()


def run_build(clean: bool = False) -> None:
    """Invoke the build script to create/update the executable."""
    project_root = Path(__file__).parent
    cmd = [sys.executable, "build_exe.py"]
    if clean:
        cmd.append("--clean")
    subprocess.run(cmd, cwd=str(project_root))


def main() -> None:
    """Entry point for running the app or building the executable."""
    parser = argparse.ArgumentParser(
        description="Run Spec Header Updater or build the executable.",
    )
    parser.add_argument(
        "--build",
        action="store_true",
        help="Run the build script instead of launching the app.",
    )
    parser.add_argument(
        "--clean",
        action="store_true",
        help="Clean build artifacts when running the build script.",
    )
    args = parser.parse_args()

    if args.build:
        run_build(clean=args.clean)
    else:
        run_gui()


if __name__ == '__main__':
    main()
