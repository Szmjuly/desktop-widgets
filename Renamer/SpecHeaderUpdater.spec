# -*- mode: python ; coding: utf-8 -*-


a = Analysis(
    ['C:\\Users\\smarkowitz\\repos\\desktop-widgets\\Renamer\\src\\main_v2.py'],
    pathex=[],
    binaries=[],
    datas=[('C:\\Users\\smarkowitz\\repos\\desktop-widgets\\Renamer\\app_config.json', '.'), ('C:\\Users\\smarkowitz\\repos\\desktop-widgets\\Renamer\\assets', 'assets')],
    hiddenimports=['PySide6.QtCore', 'PySide6.QtGui', 'PySide6.QtWidgets', 'firebase_admin', 'firebase_admin.credentials', 'firebase_admin.db', 'firebase_admin.auth', 'google.auth', 'google.auth.transport', 'google.auth.transport.requests', 'google.oauth2', 'google.cloud', 'docx', 'docx.shared', 'psutil', 'win32com', 'win32com.client', 'pythoncom', 'json', 'uuid', 'secrets', 'hashlib', 'src.firebase_config_embedded'],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.datas,
    [],
    name='SpecHeaderUpdater',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon=['C:\\Users\\smarkowitz\\repos\\desktop-widgets\\Renamer\\build\\app.ico'],
)
