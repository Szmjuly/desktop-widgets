#!/usr/bin/env python3
"""
Run script for the Portfolio Analysis Dashboard
"""
import subprocess
import sys

if __name__ == "__main__":
    # Run Streamlit app
    subprocess.run([sys.executable, "-m", "streamlit", "run", "app.py"])

