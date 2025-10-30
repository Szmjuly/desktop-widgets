#!/usr/bin/env python3
"""
Admin GUI Launcher

Launches the graphical admin interface for license management.
"""

import sys
from pathlib import Path

# Add admin directory to Python path
admin_dir = Path(__file__).parent / 'admin'
sys.path.insert(0, str(admin_dir))
sys.path.insert(0, str(Path(__file__).parent))

# Import and run NEW modern admin GUI
from admin.admin_gui_v2 import main

if __name__ == '__main__':
    main()
