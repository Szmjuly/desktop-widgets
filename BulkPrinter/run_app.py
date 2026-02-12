#!/usr/bin/env python3
"""
BulkPrinter - PDF Bulk Copy Utility
Copies all PDFs from subfolders of a source directory
to a destination directory, optionally preserving folder structure.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / 'src'))

from main import main

if __name__ == '__main__':
    main()
