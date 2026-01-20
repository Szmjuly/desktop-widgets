"""
Spec Header Date & Phase Updater - V2 UI
A cleaner, more organized interface with collapsible panels.

To use this version instead of main.py:
1. Rename main.py to main_old.py
2. Rename main_v2.py to main.py
OR
3. Update build_exe.py to point to main_v2.py
"""

from ast import Attribute
import sys, re, shutil, time, gc, webbrowser, json
from pathlib import Path
from datetime import datetime
from typing import Dict

# ---- third-party deps
from PySide6.QtCore import Qt, QThread, Signal, Slot, QDate, QTimer, QPropertyAnimation, QEasingCurve, Property, QPoint
from PySide6.QtWidgets import (
    QApplication, QWidget, QGridLayout, QLabel, QLineEdit, QPushButton, QFileDialog,
    QCheckBox, QDateEdit, QTextEdit, QHBoxLayout, QVBoxLayout, QProgressBar,
    QListWidget, QListWidgetItem, QMessageBox, QGroupBox, QDialog, QDialogButtonBox,
    QFormLayout, QFrame, QScrollArea, QSizePolicy, QSpinBox, QToolButton, QSplitter,
    QStackedWidget, QButtonGroup, QRadioButton, QComboBox
)
from PySide6.QtGui import QFont, QColor, QPalette, QIcon, QTextCursor, QPixmap

from docx import Document
from docx.shared import Pt
import psutil

# Local imports - Conditional licensing
from src.build_config import INCLUDE_LICENSING

if INCLUDE_LICENSING:
    from src.subscription import SubscriptionManager

# ============================================================================
# Import all backend logic from main.py (no duplication)
# ============================================================================
from src.main import (
    # Constants
    DEFAULT_FONT_NAME, DEFAULT_FONT_SIZE, DATE_RX, PHASE_RX,
    # Functions
    ensure_word, check_word_available, convert_doc_to_docx, export_pdf, export_pdf_fast, safe_close_word,
    replace_in_headerlike, normalize_whitespace_in_header_footer,
    normalize_fonts_in_document, update_docx_dates,
    # Worker class
    UpdateWorker,
)


def asset_path(relative_path: str) -> Path:
    base = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent.parent))
    return base / relative_path

# ============================================================================
# Theme System - Dark and Light Mode Support
# ============================================================================
class ThemeColors:
    """Color definitions for dark and light themes."""
    
    DARK = {
        # Primary colors
        "PRIMARY": "#6366F1",           # Indigo
        "PRIMARY_HOVER": "#4F46E5",
        "PRIMARY_LIGHT": "#818CF8",
        # Status colors
        "SUCCESS": "#10B981",           # Green
        "WARNING": "#F59E0B",           # Amber
        "DANGER": "#EF4444",            # Red
        "INFO": "#3B82F6",              # Blue
        # Background layers
        "BG_DARK": "#0F172A",           # Slate 900
        "BG_MAIN": "#1E293B",           # Slate 800
        "BG_CARD": "#334155",           # Slate 700
        "BG_INPUT": "#475569",          # Slate 600
        "BG_HOVER": "#3B4F6B",
        # Text
        "TEXT_PRIMARY": "#F8FAFC",      # Slate 50
        "TEXT_SECONDARY": "#94A3B8",    # Slate 400
        "TEXT_MUTED": "#64748B",        # Slate 500
        # Borders
        "BORDER": "#475569",
        "BORDER_FOCUS": "#6366F1",
        # Accents
        "ACCENT_PURPLE": "#A855F7",
        "ACCENT_CYAN": "#22D3EE",
    }
    
    LIGHT = {
        # Primary colors
        "PRIMARY": "#4F46E5",           # Slightly darker indigo for light mode
        "PRIMARY_HOVER": "#4338CA",
        "PRIMARY_LIGHT": "#6366F1",
        # Status colors
        "SUCCESS": "#059669",           # Darker green
        "WARNING": "#D97706",           # Darker amber
        "DANGER": "#DC2626",            # Darker red
        "INFO": "#2563EB",              # Darker blue
        # Background layers
        "BG_DARK": "#E2E8F0",           # Slate 200
        "BG_MAIN": "#F1F5F9",           # Slate 100
        "BG_CARD": "#FFFFFF",           # White
        "BG_INPUT": "#F8FAFC",          # Slate 50
        "BG_HOVER": "#E2E8F0",          # Slate 200
        # Text
        "TEXT_PRIMARY": "#0F172A",      # Slate 900
        "TEXT_SECONDARY": "#475569",    # Slate 600
        "TEXT_MUTED": "#64748B",        # Slate 500
        # Borders
        "BORDER": "#CBD5E1",            # Slate 300
        "BORDER_FOCUS": "#4F46E5",
        # Accents
        "ACCENT_PURPLE": "#7C3AED",
        "ACCENT_CYAN": "#0891B2",
    }


def _detect_system_theme() -> str:
    """Detect system theme preference. Returns 'dark' or 'light'."""
    try:
        import winreg
        key = winreg.OpenKey(
            winreg.HKEY_CURRENT_USER,
            r"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
        )
        value, _ = winreg.QueryValueEx(key, "AppsUseLightTheme")
        winreg.CloseKey(key)
        return "light" if value == 1 else "dark"
    except Exception:
        return "dark"  # Default to dark if detection fails


class Colors:
    """Dynamic color provider that switches between themes."""
    
    _current_theme = "dark"
    _colors = ThemeColors.DARK.copy()
    
    # Class attributes that will be updated dynamically
    PRIMARY = ThemeColors.DARK["PRIMARY"]
    PRIMARY_HOVER = ThemeColors.DARK["PRIMARY_HOVER"]
    PRIMARY_LIGHT = ThemeColors.DARK["PRIMARY_LIGHT"]
    SUCCESS = ThemeColors.DARK["SUCCESS"]
    WARNING = ThemeColors.DARK["WARNING"]
    DANGER = ThemeColors.DARK["DANGER"]
    INFO = ThemeColors.DARK["INFO"]
    BG_DARK = ThemeColors.DARK["BG_DARK"]
    BG_MAIN = ThemeColors.DARK["BG_MAIN"]
    BG_CARD = ThemeColors.DARK["BG_CARD"]
    BG_INPUT = ThemeColors.DARK["BG_INPUT"]
    BG_HOVER = ThemeColors.DARK["BG_HOVER"]
    TEXT_PRIMARY = ThemeColors.DARK["TEXT_PRIMARY"]
    TEXT_SECONDARY = ThemeColors.DARK["TEXT_SECONDARY"]
    TEXT_MUTED = ThemeColors.DARK["TEXT_MUTED"]
    BORDER = ThemeColors.DARK["BORDER"]
    BORDER_FOCUS = ThemeColors.DARK["BORDER_FOCUS"]
    ACCENT_PURPLE = ThemeColors.DARK["ACCENT_PURPLE"]
    ACCENT_CYAN = ThemeColors.DARK["ACCENT_CYAN"]
    
    @classmethod
    def set_theme(cls, theme: str):
        """Set the current theme ('dark' or 'light')."""
        cls._current_theme = theme
        colors = ThemeColors.DARK if theme == "dark" else ThemeColors.LIGHT
        cls._colors = colors.copy()
        
        # Update all class attributes
        for key, value in colors.items():
            setattr(cls, key, value)
    
    @classmethod
    def get_theme(cls) -> str:
        """Get current theme name."""
        return cls._current_theme
    
    @classmethod
    def is_dark(cls) -> bool:
        """Check if currently using dark theme."""
        return cls._current_theme == "dark"
    
    @classmethod
    def init_from_system(cls):
        """Initialize theme based on system preference."""
        system_theme = _detect_system_theme()
        cls.set_theme(system_theme)


# ============================================================================
# Collapsible Section Widget
# ============================================================================
class CollapsibleSection(QWidget):
    """A section that can be expanded/collapsed."""

    # Emitted when the section is expanded/collapsed
    expanded = Signal(bool)

    def __init__(self, title: str, expanded: bool = True, parent=None):
        super().__init__(parent)
        self.is_expanded = expanded
        
        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 8)
        layout.setSpacing(0)
        
        # Header button
        arrow = "▾" if expanded else "▸"
        self.toggle_btn = QPushButton(f"{arrow}  {title}")
        self.toggle_btn.setCheckable(True)
        self.toggle_btn.setChecked(expanded)
        self.toggle_btn.clicked.connect(self._toggle)
        self.toggle_btn.setCursor(Qt.CursorShape.PointingHandCursor)
        self.toggle_btn.setStyleSheet(f"""
            QPushButton {{
                background: transparent;
                color: {Colors.TEXT_PRIMARY};
                border: none;
                border-bottom: 1px solid {Colors.BORDER};
                border-radius: 0;
                padding: 8px 0;
                text-align: left;
                font-weight: 600;
                font-size: 11px;
                letter-spacing: 0.3px;
            }}
            QPushButton:hover {{
                color: {Colors.PRIMARY_LIGHT};
            }}
        """)
        layout.addWidget(self.toggle_btn)
        
        # Content container
        self.content = QWidget()
        self.content_layout = QVBoxLayout(self.content)
        self.content_layout.setContentsMargins(0, 12, 0, 4)
        self.content_layout.setSpacing(6)
        self.content.setVisible(expanded)
        layout.addWidget(self.content)
        
        self.title = title
    
    def _toggle(self):
        self.is_expanded = self.toggle_btn.isChecked()
        self.content.setVisible(self.is_expanded)
        arrow = "▾" if self.is_expanded else "▸"
        self.toggle_btn.setText(f"{arrow}  {self.title}")
        # Notify listeners (e.g. to auto-scroll when expanded)
        self.expanded.emit(self.is_expanded)
    
    def addWidget(self, widget):
        self.content_layout.addWidget(widget)
    
    def addLayout(self, layout):
        self.content_layout.addLayout(layout)


# ============================================================================
# Toggle Switch Widget
# ============================================================================
class ToggleSwitch(QWidget):
    """Clean toggle switch with label."""
    toggled = Signal(bool)
    
    def __init__(self, label: str, parent=None):
        super().__init__(parent)
        
        layout = QHBoxLayout(self)
        layout.setContentsMargins(0, 4, 0, 4)
        layout.setSpacing(12)
        
        self.checkbox = QCheckBox()
        self.checkbox.toggled.connect(self.toggled.emit)
        self.checkbox.setStyleSheet(f"""
            QCheckBox {{
                spacing: 0px;
            }}
            QCheckBox::indicator {{
                width: 36px;
                height: 18px;
                border-radius: 9px;
                background: {Colors.BG_INPUT};
                border: 1px solid {Colors.BORDER};
            }}
            QCheckBox::indicator:checked {{
                background: {Colors.PRIMARY};
                border-color: {Colors.PRIMARY};
            }}
            QCheckBox::indicator:hover {{
                border-color: {Colors.TEXT_MUTED};
            }}
            QCheckBox::indicator:checked:hover {{
                background: {Colors.PRIMARY_HOVER};
            }}
        """)
        
        self.label = QLabel(label)
        self.label.setStyleSheet(f"color: {Colors.TEXT_SECONDARY}; font-size: 12px;")
        
        layout.addWidget(self.checkbox)
        layout.addWidget(self.label)
        layout.addStretch()
    
    def isChecked(self):
        return self.checkbox.isChecked()
    
    def setChecked(self, checked):
        self.checkbox.setChecked(checked)
    
    def setEnabled(self, enabled):
        self.checkbox.setEnabled(enabled)
        self.label.setEnabled(enabled)


# ============================================================================
# Styled Input Components
# ============================================================================
class StyledLineEdit(QLineEdit):
    def __init__(self, placeholder="", parent=None):
        super().__init__(parent)
        self.setPlaceholderText(placeholder)
        self.setFixedHeight(32)
        self.setStyleSheet(f"""
            QLineEdit {{
                background: transparent;
                color: {Colors.TEXT_PRIMARY};
                border: none;
                border-bottom: 1px solid {Colors.BORDER};
                border-radius: 0;
                min-height: 28px;
                padding: 8px 2px 8px 2px;
                font-size: 13px;
            }}
            QLineEdit:focus {{
                border-bottom: 2px solid {Colors.PRIMARY};
                padding: 8px 2px 7px 2px;
            }}
            QLineEdit:disabled {{
                color: {Colors.TEXT_MUTED};
            }}
            QLineEdit::placeholder {{
                color: {Colors.TEXT_MUTED};
            }}
        """)


class StyledButton(QPushButton):
    def __init__(self, text, variant="primary", parent=None):
        super().__init__(text, parent)
        
        if variant == "primary":
            self.setStyleSheet(f"""
                QPushButton {{
                    background: {Colors.PRIMARY};
                    color: white;
                    border: none;
                    border-radius: 3px;
                    padding: 8px 16px;
                    font-weight: 500;
                    font-size: 12px;
                }}
                QPushButton:hover {{
                    background: {Colors.PRIMARY_HOVER};
                }}
                QPushButton:disabled {{
                    background: {Colors.BG_INPUT};
                    color: {Colors.TEXT_MUTED};
                }}
            """)
        elif variant == "success":
            self.setStyleSheet(f"""
                QPushButton {{
                    background: {Colors.SUCCESS};
                    color: white;
                    border: none;
                    border-radius: 3px;
                    padding: 8px 16px;
                    font-weight: 500;
                    font-size: 12px;
                }}
                QPushButton:hover {{
                    background: #059669;
                }}
                QPushButton:disabled {{
                    background: {Colors.BG_INPUT};
                    color: {Colors.TEXT_MUTED};
                }}
            """)
        elif variant == "danger":
            self.setStyleSheet(f"""
                QPushButton {{
                    background: {Colors.DANGER};
                    color: white;
                    border: none;
                    border-radius: 3px;
                    padding: 8px 16px;
                    font-weight: 500;
                    font-size: 12px;
                }}
                QPushButton:hover {{
                    background: #DC2626;
                }}
            """)
        elif variant == "ghost":
            self.setStyleSheet(f"""
                QPushButton {{
                    background: transparent;
                    color: {Colors.TEXT_SECONDARY};
                    border: 1px solid {Colors.BORDER};
                    border-radius: 3px;
                    padding: 8px 16px;
                    font-size: 12px;
                }}
                QPushButton:hover {{
                    background: {Colors.BG_CARD};
                    color: {Colors.TEXT_PRIMARY};
                }}
            """)


# ============================================================================
# Font Warning Dialog (Styled completion dialog)
# ============================================================================
class FontWarningDialog(QDialog):
    """Styled dialog shown after font normalization to warn about layout changes."""
    
    def __init__(self, folder_path: str, updated: int, errors: int, parent=None):
        super().__init__(parent)
        self.folder_path = folder_path
        self.setWindowTitle("Update Complete")
        self.setFixedSize(420, 260)
        self.setModal(True)
        
        # Apply dark theme
        self.setStyleSheet(f"""
            QDialog {{
                background: {Colors.BG_MAIN};
                border: 1px solid {Colors.BORDER};
                border-radius: 12px;
            }}
        """)
        
        layout = QVBoxLayout(self)
        layout.setContentsMargins(24, 24, 24, 20)
        layout.setSpacing(16)
        
        # Icon and title row
        title_row = QHBoxLayout()
        title_row.setSpacing(12)
        
        icon_label = QLabel("⚠️")
        icon_label.setStyleSheet("font-size: 28px;")
        title_row.addWidget(icon_label)
        
        title = QLabel("Font Changes Applied")
        title.setStyleSheet(f"color: {Colors.WARNING}; font-size: 18px; font-weight: 600;")
        title_row.addWidget(title)
        title_row.addStretch()
        layout.addLayout(title_row)
        
        # Stats line
        stats = QLabel(f"Updated: {updated} document{'s' if updated != 1 else ''}  •  Errors: {errors}")
        stats.setStyleSheet(f"color: {Colors.TEXT_SECONDARY}; font-size: 12px;")
        layout.addWidget(stats)
        
        # Warning message
        msg = QLabel(
            "Font normalization may affect document layouts.\n\n"
            "Please review the updated documents to ensure:\n"
            "• Tables and columns are aligned correctly\n"
            "• Page breaks are in the right places\n"
            "• Headers and footers display properly"
        )
        msg.setWordWrap(True)
        msg.setStyleSheet(f"color: {Colors.TEXT_PRIMARY}; font-size: 13px; line-height: 1.5;")
        layout.addWidget(msg)
        
        layout.addStretch()
        
        # Buttons
        btn_layout = QHBoxLayout()
        btn_layout.setSpacing(12)
        
        open_btn = QPushButton("Open Folder")
        open_btn.setCursor(Qt.CursorShape.PointingHandCursor)
        open_btn.clicked.connect(self._open_folder)
        open_btn.setStyleSheet(f"""
            QPushButton {{
                background: {Colors.PRIMARY};
                color: white;
                border: none;
                border-radius: 6px;
                padding: 10px 20px;
                font-weight: 600;
                font-size: 13px;
            }}
            QPushButton:hover {{
                background: {Colors.PRIMARY_HOVER};
            }}
        """)
        
        close_btn = QPushButton("Close")
        close_btn.setCursor(Qt.CursorShape.PointingHandCursor)
        close_btn.clicked.connect(self.accept)
        close_btn.setStyleSheet(f"""
            QPushButton {{
                background: transparent;
                color: {Colors.TEXT_SECONDARY};
                border: 1px solid {Colors.BORDER};
                border-radius: 6px;
                padding: 10px 20px;
                font-size: 13px;
            }}
            QPushButton:hover {{
                background: {Colors.BG_CARD};
                color: {Colors.TEXT_PRIMARY};
            }}
        """)
        
        btn_layout.addStretch()
        btn_layout.addWidget(open_btn)
        btn_layout.addWidget(close_btn)
        layout.addLayout(btn_layout)
    
    def _open_folder(self):
        """Open the folder in the system file explorer."""
        import subprocess
        import os
        
        if os.name == 'nt':  # Windows
            subprocess.Popen(['explorer', self.folder_path])
        elif sys.platform == 'darwin':  # macOS
            subprocess.Popen(['open', self.folder_path])
        else:  # Linux
            subprocess.Popen(['xdg-open', self.folder_path])


# ============================================================================
# Log Panel (Slide-out panel that extends window)
# ============================================================================
class LogPanel(QWidget):
    """Log panel content - managed by MainWindow for slide-out behavior."""
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setFixedWidth(340)
        
        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(0)
        
        self.header = QWidget()
        self.header.setObjectName("logPanelHeader")
        self.header.setFixedHeight(44)
        header_layout = QHBoxLayout(self.header)
        header_layout.setContentsMargins(0, 0, 0, 0)
        header_layout.setSpacing(0)

        self.header_accent = QWidget()
        self.header_accent.setFixedWidth(3)
        self.header_accent.setSizePolicy(QSizePolicy.Policy.Fixed, QSizePolicy.Policy.Expanding)
        header_layout.addWidget(self.header_accent)

        self.header_content = QWidget()
        header_content_layout = QHBoxLayout(self.header_content)
        header_content_layout.setContentsMargins(12, 0, 12, 0)
        header_content_layout.setSpacing(12)

        self.caret = QLabel("❮")
        self.caret.setStyleSheet(f"color: {Colors.TEXT_MUTED}; font-size: 12px;")
        header_content_layout.addWidget(self.caret)

        self.header_title = QLabel("Activity Log")
        self.header_title.setStyleSheet(f"color: {Colors.TEXT_PRIMARY}; font-weight: 600; font-size: 13px;")
        header_content_layout.addWidget(self.header_title)
        header_content_layout.addStretch()

        self.copy_btn = QPushButton("Copy")
        self.copy_btn.setCursor(Qt.CursorShape.PointingHandCursor)
        self.copy_btn.clicked.connect(self.copy_log)
        header_content_layout.addWidget(self.copy_btn)

        self.clear_btn = QPushButton("Clear")
        self.clear_btn.setCursor(Qt.CursorShape.PointingHandCursor)
        self.clear_btn.clicked.connect(self.clear_log)
        header_content_layout.addWidget(self.clear_btn)

        header_layout.addWidget(self.header_content, 1)
        
        layout.addWidget(self.header)
        
        # Log content
        self.log_text = QTextEdit()
        self.log_text.setReadOnly(True)
        layout.addWidget(self.log_text)
        
        # Apply initial theme
        self.apply_theme()
    
    def apply_theme(self):
        """Apply current theme colors to all panel elements."""
        self.setAutoFillBackground(True)
        palette = self.palette()
        palette.setColor(QPalette.ColorRole.Window, QColor(Colors.BG_DARK))
        self.setPalette(palette)
        
        # Header styling - use #id selector to target only the header, not children
        self.header.setStyleSheet(f"""
            #logPanelHeader {{
                background: {Colors.BG_CARD};
            }}
        """)

        self.header_accent.setStyleSheet(f"background: {Colors.PRIMARY};")
        self.header_content.setStyleSheet("background: transparent;")
        
        self.caret.setStyleSheet(f"color: {Colors.PRIMARY}; font-size: 12px;")
        self.header_title.setStyleSheet(f"color: {Colors.TEXT_PRIMARY}; font-weight: 600; font-size: 13px;")
        
        btn_style = f"""
            QPushButton {{
                background: transparent;
                color: {Colors.TEXT_MUTED};
                border: 1px solid transparent;
                border-radius: 3px;
                padding: 4px 8px;
                font-size: 11px;
            }}
            QPushButton:hover {{
                color: {Colors.TEXT_PRIMARY};
                background: {Colors.BG_HOVER};
            }}
        """
        self.copy_btn.setStyleSheet(btn_style)
        self.clear_btn.setStyleSheet(btn_style)
        
        # Log text area
        self.log_text.setStyleSheet(f"""
            QTextEdit {{
                background: {Colors.BG_DARK};
                color: {Colors.TEXT_SECONDARY};
                border: none;
                font-family: 'Cascadia Code', 'Consolas', monospace;
                font-size: 11px;
                padding: 12px 16px;
            }}
        """)
    
    def append(self, text: str):
        """Append text to log with auto-scroll."""
        self.log_text.append(text)
        cursor = self.log_text.textCursor()
        cursor.movePosition(QTextCursor.MoveOperation.End)
        self.log_text.setTextCursor(cursor)
    
    def clear_log(self):
        self.log_text.clear()

    def copy_log(self):
        text = self.log_text.toPlainText().strip()
        if not text:
            return
        cb = QApplication.clipboard()
        if cb is not None:
            cb.setText(text)


# ============================================================================
# Main Window V2
# ============================================================================
class MainWindowV2(QWidget):
    # Window dimensions
    BASE_WIDTH = 580
    BASE_HEIGHT = 700
    PANEL_WIDTH = 340
    MIN_HEIGHT = 520
    
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Spec Header Date & Phase Updater")
        self.setMinimumSize(self.BASE_WIDTH, self.MIN_HEIGHT)
        self.resize(self.BASE_WIDTH, self.BASE_HEIGHT)
        
        # Load app configuration
        self.app_config = self._load_app_config()
        
        # Log panel state
        self._log_visible = False
        # Timing for progress / ETA
        self._run_start_time = None

        # Initialize subscription manager if licensing enabled
        self.subscription_mgr = None
        if INCLUDE_LICENSING:
            try:
                self.subscription_mgr = SubscriptionManager()
            except Exception as e:
                print(f"Subscription init error: {e}")
        
        self.worker = None
        
        self._setup_ui()
        self._apply_global_styles()
    
    def _apply_global_styles(self):
        """Apply global stylesheet."""
        self.setStyleSheet(f"""
            QWidget {{
                background: {Colors.BG_MAIN};
                color: {Colors.TEXT_PRIMARY};
                font-family: 'Segoe UI', -apple-system, sans-serif;
            }}
            QScrollBar:vertical {{
                background: {Colors.BG_DARK};
                width: 10px;
                border-radius: 5px;
            }}
            QScrollBar::handle:vertical {{
                background: {Colors.BG_INPUT};
                border-radius: 5px;
                min-height: 30px;
            }}
            QScrollBar::handle:vertical:hover {{
                background: {Colors.TEXT_MUTED};
            }}
            QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {{
                height: 0;
            }}
            QToolTip {{
                background: {Colors.BG_CARD};
                color: {Colors.TEXT_PRIMARY};
                border: 1px solid {Colors.BORDER};
                padding: 6px 10px;
                border-radius: 4px;
            }}
        """)
    
    def _setup_ui(self):
        """Build the UI layout."""
        main_layout = QHBoxLayout(self)
        main_layout.setContentsMargins(0, 0, 0, 0)
        main_layout.setSpacing(0)
        
        # ============================================================
        # LEFT SIDE: Main Content
        # ============================================================
        self.left_panel = QWidget()
        left_layout = QVBoxLayout(self.left_panel)
        left_layout.setContentsMargins(24, 20, 24, 20)
        left_layout.setSpacing(16)
        
        # Header with title and log toggle
        header = QHBoxLayout()
        header.setSpacing(12)
        
        # Header logo (transparent PNG) - left of title
        self.logoLabel = QLabel()
        self.logoLabel.setFixedSize(36, 36)
        self.logoLabel.setStyleSheet("background: transparent;")
        logo_file = asset_path("assets/CES_logo.png")
        if logo_file.exists():
            pix = QPixmap(str(logo_file))
            if not pix.isNull():
                self.logoLabel.setPixmap(
                    pix.scaled(
                        self.logoLabel.size(),
                        Qt.AspectRatioMode.KeepAspectRatio,
                        Qt.TransformationMode.SmoothTransformation,
                    )
                )
        header.addWidget(self.logoLabel)
        
        self.titleLabel = QLabel("Spec Header Updater")
        self.titleLabel.setStyleSheet(f"color: {Colors.TEXT_PRIMARY}; font-size: 18px; font-weight: 600; letter-spacing: -0.5px;")
        header.addWidget(self.titleLabel)
        header.addStretch()
        
        # Theme toggle button
        self.btnThemeToggle = QPushButton("🌙" if Colors.is_dark() else "☀️")
        self.btnThemeToggle.setToolTip("Switch to light mode" if Colors.is_dark() else "Switch to dark mode")
        self.btnThemeToggle.setCursor(Qt.CursorShape.PointingHandCursor)
        self.btnThemeToggle.clicked.connect(self._toggle_theme)
        self.btnThemeToggle.setStyleSheet(f"""
            QPushButton {{
                background: transparent;
                color: {Colors.TEXT_SECONDARY};
                border: 1px solid {Colors.BORDER};
                border-radius: 4px;
                padding: 4px 10px;
                font-size: 12px;
            }}
            QPushButton:hover {{
                background: {Colors.BG_CARD};
                border-color: {Colors.TEXT_MUTED};
            }}
        """)
        header.addWidget(self.btnThemeToggle)
        
        # Log toggle button
        self.btnLogToggle = QPushButton("📋 Show Log")
        self.btnLogToggle.setCursor(Qt.CursorShape.PointingHandCursor)
        self.btnLogToggle.clicked.connect(self._toggle_log_panel)
        self.btnLogToggle.setStyleSheet(f"""
            QPushButton {{
                background: transparent;
                color: {Colors.TEXT_SECONDARY};
                border: 1px solid {Colors.BORDER};
                border-radius: 4px;
                padding: 6px 12px;
                font-size: 11px;
            }}
            QPushButton:hover {{
                background: {Colors.BG_CARD};
                color: {Colors.TEXT_PRIMARY};
                border-color: {Colors.TEXT_MUTED};
            }}
        """)
        header.addWidget(self.btnLogToggle)
        
        left_layout.addLayout(header)
        
        # Scroll area for main content
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.Shape.NoFrame)
        scroll.setStyleSheet(f"""
            QScrollArea {{
                background: transparent;
                border: none;
            }}
            QScrollBar:vertical {{
                background: {Colors.BG_MAIN};
                width: 8px;
                border-radius: 4px;
            }}
            QScrollBar::handle:vertical {{
                background: {Colors.BORDER};
                border-radius: 4px;
                min-height: 20px;
            }}
            QScrollBar::handle:vertical:hover {{
                background: {Colors.TEXT_MUTED};
            }}
            QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {{
                height: 0px;
            }}
            QScrollBar::add-page:vertical, QScrollBar::sub-page:vertical {{
                background: transparent;
            }}
        """)
        
        scroll_content = QWidget()
        scroll_layout = QVBoxLayout(scroll_content)
        scroll_layout.setContentsMargins(0, 0, 10, 0)
        scroll_layout.setSpacing(12)

        # Storing the scroll area to be used later
        self.scroll_area = scroll
        
        # ---- SECTION: Core Settings (always visible) ----
        core_section = QWidget()
        core_layout = QVBoxLayout(core_section)
        core_layout.setContentsMargins(0, 0, 0, 12)
        core_layout.setSpacing(10)
        
        core_title = QLabel("Core Settings")
        core_title.setStyleSheet(f"""
            color: {Colors.TEXT_PRIMARY};
            font-weight: 600;
            font-size: 11px;
            letter-spacing: 0.3px;
            padding-bottom: 8px;
            border-bottom: 1px solid {Colors.BORDER};
        """)
        core_layout.addWidget(core_title)
        
        # Folder selection
        folder_row = QHBoxLayout()
        folder_label = QLabel("Specs Folder:")
        folder_label.setStyleSheet(f"color: {Colors.TEXT_SECONDARY}; font-size: 12px; min-width: 80px;")
        self.txtFolder = StyledLineEdit("Select specifications folder...")
        self.txtFolder.setReadOnly(True)
        folder_btn = StyledButton("Browse", "ghost")
        folder_btn.setFixedWidth(80)
        folder_btn.clicked.connect(self._browse_folder)
        folder_row.addWidget(folder_label)
        folder_row.addWidget(self.txtFolder)
        folder_row.addWidget(folder_btn)
        core_layout.addLayout(folder_row)
        
        # Date selection
        date_row = QHBoxLayout()
        date_label = QLabel("New Date:")
        date_label.setStyleSheet(f"color: {Colors.TEXT_SECONDARY}; font-size: 12px; min-width: 80px;")
        self.dateEdit = QDateEdit()
        self.dateEdit.setDate(QDate.currentDate())
        self.dateEdit.setCalendarPopup(True)
        self.dateEdit.setDisplayFormat("MMMM d, yyyy")
        self.dateEdit.setButtonSymbols(QDateEdit.ButtonSymbols.NoButtons)
        self.dateEdit.setMinimumHeight(32)
        self.dateEdit.setFixedHeight(32)
        self.dateEdit.setStyleSheet(f"""
            QDateEdit {{
                background: transparent;
                color: {Colors.TEXT_PRIMARY};
                border: none;
                border-bottom: 1px solid {Colors.BORDER};
                padding: 8px 2px;
                font-size: 13px;
            }}
            QDateEdit:focus {{
                border-bottom: 2px solid {Colors.PRIMARY};
                padding-bottom: 7px;
            }}
            
            /* Calendar popup styling */
            QCalendarWidget {{
                background: {Colors.BG_CARD};
                border: 1px solid {Colors.BORDER};
            }}
            QCalendarWidget QToolButton {{
                color: {Colors.TEXT_PRIMARY};
                background: transparent;
                border: none;
                padding: 6px;
                font-weight: 600;
            }}
            QCalendarWidget QToolButton:hover {{
                background: {Colors.BG_HOVER};
                border-radius: 4px;
            }}
            QCalendarWidget QMenu {{
                background: {Colors.BG_CARD};
                color: {Colors.TEXT_PRIMARY};
                border: 1px solid {Colors.BORDER};
            }}
            QCalendarWidget QSpinBox {{
                background: {Colors.BG_INPUT};
                color: {Colors.TEXT_PRIMARY};
                border: 1px solid {Colors.BORDER};
                border-radius: 4px;
                padding: 2px 6px;
            }}
            QCalendarWidget QAbstractItemView {{
                background: {Colors.BG_MAIN};
                selection-background-color: {Colors.PRIMARY};
                selection-color: white;
                border: none;
            }}
            QCalendarWidget QAbstractItemView:enabled {{
                color: {Colors.TEXT_PRIMARY};
            }}
            QCalendarWidget QAbstractItemView:disabled {{
                color: {Colors.TEXT_MUTED};
            }}
            QCalendarWidget QWidget#qt_calendar_navigationbar {{
                background: {Colors.BG_CARD};
                border-bottom: 1px solid {Colors.BORDER};
            }}
        """)
        
        # Style the calendar widget directly
        cal_widget = self.dateEdit.calendarWidget()
        if cal_widget:
            cal_widget.setMinimumSize(380, 280)
            cal_widget.setGridVisible(False)
            cal_widget.setStyleSheet(f"""
                QCalendarWidget {{
                    background: {Colors.BG_CARD};
                    border: 1px solid {Colors.BORDER};
                    border-radius: 8px;
                }}
                QCalendarWidget QWidget {{
                    alternate-background-color: {Colors.BG_MAIN};
                    border-radius: 6px;
                }}
                QCalendarWidget QToolButton {{
                    color: {Colors.TEXT_PRIMARY};
                    background: {Colors.BG_CARD};
                    border: none;
                    padding: 8px 12px;
                    font-weight: 600;
                    font-size: 13px;
                    border-radius: 4px;
                }}
                QCalendarWidget QToolButton:hover {{
                    background: {Colors.BG_HOVER};
                }}
                QCalendarWidget QToolButton::menu-indicator {{
                    image: none;
                }}
                QCalendarWidget #qt_calendar_navigationbar {{
                    background: {Colors.BG_CARD};
                    padding: 8px;
                    border-top-left-radius: 8px;
                    border-top-right-radius: 8px;
                }}
                QCalendarWidget QMenu {{
                    background: {Colors.BG_CARD};
                    color: {Colors.TEXT_PRIMARY};
                    border: 1px solid {Colors.BORDER};
                    border-radius: 6px;
                    padding: 4px;
                }}
                QCalendarWidget QMenu::item {{
                    padding: 6px 12px;
                    border-radius: 4px;
                }}
                QCalendarWidget QMenu::item:selected {{
                    background: {Colors.PRIMARY};
                }}
                QCalendarWidget QSpinBox {{
                    background: {Colors.BG_INPUT};
                    color: {Colors.TEXT_PRIMARY};
                    border: 1px solid {Colors.BORDER};
                    border-radius: 4px;
                    padding: 4px 8px;
                    font-size: 13px;
                    selection-background-color: {Colors.PRIMARY};
                }}
                QCalendarWidget QSpinBox::up-button, QCalendarWidget QSpinBox::down-button {{
                    background: {Colors.BG_CARD};
                    border: none;
                    border-radius: 2px;
                }}
                QCalendarWidget QTableView {{
                    background: {Colors.BG_MAIN};
                    selection-background-color: {Colors.PRIMARY};
                    selection-color: white;
                    outline: none;
                    border-bottom-left-radius: 8px;
                    border-bottom-right-radius: 8px;
                    font-size: 12px;
                }}
                QCalendarWidget QTableView::item {{
                    padding: 10px;
                    border-radius: 4px;
                }}
                QCalendarWidget QTableView::item:hover {{
                    background: {Colors.BG_HOVER};
                }}
                QCalendarWidget QTableView::item:selected {{
                    background: {Colors.PRIMARY};
                    color: white;
                }}
                QCalendarWidget QHeaderView::section {{
                    background: {Colors.BG_CARD};
                    color: {Colors.TEXT_MUTED};
                    border: none;
                    padding: 6px;
                    font-weight: 600;
                    font-size: 11px;
                }}
            """)
        
        # Store reference to dateEdit for calendar button
        dateEditRef = self.dateEdit
        
        # Create integrated date field with calendar button
        self.date_container = QWidget()
        self.date_container.setStyleSheet(f"""
            QWidget {{
                background: transparent;
                border-bottom: 1px solid {Colors.BORDER};
            }}
            QWidget:focus-within {{
                border-bottom: 2px solid {Colors.PRIMARY};
            }}
        """)
        date_container_layout = QHBoxLayout(self.date_container)
        date_container_layout.setContentsMargins(0, 0, 0, 0)
        date_container_layout.setSpacing(0)
        
        # Remove border and hide the dropdown arrow completely
        self.dateEdit.setStyleSheet(f"""
            QDateEdit {{
                background: transparent;
                color: {Colors.TEXT_PRIMARY};
                border: none;
                min-height: 32px;
                padding: 8px 2px;
                font-size: 13px;
            }}
            QDateEdit::drop-down {{
                border: none;
                background: transparent;
                width: 0px;
                height: 0px;
            }}
            QDateEdit::down-arrow {{
                image: none;
                border: none;
                width: 0px;
                height: 0px;
            }}
        """)
        date_container_layout.addWidget(self.dateEdit)
        
        # Calendar button integrated into the field
        self.cal_btn = QPushButton("📅")
        self.cal_btn.setFixedSize(28, 28)
        self.cal_btn.setCursor(Qt.CursorShape.PointingHandCursor)
        self.cal_btn.setToolTip("Open calendar")
        
        def open_calendar():
            # Get the calendar widget and show it positioned below the date field
            cal = dateEditRef.calendarWidget()
            if cal:
                # Calculate position below the date field
                pos = dateEditRef.mapToGlobal(dateEditRef.rect().bottomLeft())
                cal.window().move(pos)
                cal.window().show()
                cal.setFocus()
        self.cal_btn.clicked.connect(open_calendar)
        self.cal_btn.setStyleSheet(f"""
            QPushButton {{
                background: transparent;
                border: none;
                font-size: 14px;
                padding-bottom: 4px;
            }}
            QPushButton:hover {{
                background: {Colors.BG_CARD};
                border-radius: 4px;
            }}
        """)
        date_container_layout.addWidget(self.cal_btn)
        
        date_row.addWidget(date_label)
        date_row.addWidget(self.date_container)
        date_row.addStretch()
        core_layout.addLayout(date_row)
        
        # Phase text
        phase_row = QHBoxLayout()
        phase_label = QLabel("Phase Text:")
        phase_label.setStyleSheet(f"color: {Colors.TEXT_SECONDARY}; font-size: 12px; min-width: 80px;")
        self.txtPhase = StyledLineEdit("e.g., 100% Construction Documents")
        phase_row.addWidget(phase_label)
        phase_row.addWidget(self.txtPhase)
        core_layout.addLayout(phase_row)
        
        # Include subfolders toggle
        self.chkRecursive = ToggleSwitch("Include files in subfolders")
        self.chkRecursive.setToolTip("Search for documents in all subdirectories,\nnot just the selected folder.")
        self.chkRecursive.setChecked(True)
        core_layout.addWidget(self.chkRecursive)
        
        scroll_layout.addWidget(core_section)

        self.core_section = core_section
        
        # ---- SECTION: Processing Options ----
        proc_section = CollapsibleSection("Processing Options", expanded=False)
        
        # Operation mode
        mode_label = QLabel("Operation Mode:")
        mode_label.setStyleSheet(f"color: {Colors.TEXT_SECONDARY}; font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px;")
        proc_section.addWidget(mode_label)
        
        self.chkDryRun = ToggleSwitch("Dry-run (preview only, no changes)")
        self.chkDryRun.setToolTip("Simulate the update process without modifying any files.\nUseful for previewing what changes will be made.")
        proc_section.addWidget(self.chkDryRun)

        output_label = QLabel("Output Mode:")
        output_label.setStyleSheet(f"color: {Colors.TEXT_SECONDARY}; font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; margin-top: 6px;")
        proc_section.addWidget(output_label)

        output_row = QWidget()
        output_row_layout = QHBoxLayout(output_row)
        output_row_layout.setContentsMargins(0, 0, 0, 0)
        output_row_layout.setSpacing(12)

        self.cmbOutputMode = QComboBox()
        self.cmbOutputMode.addItem("Update Word docs only", "docs_only")
        self.cmbOutputMode.addItem("Update Word docs + reprint PDFs", "docs_and_pdf")
        self.cmbOutputMode.addItem("Reprint PDFs only (no doc changes)", "pdf_only")
        self.cmbOutputMode.setCurrentIndex(1)
        self.cmbOutputMode.setToolTip(
            "Choose what this run will do:\n"
            "• Update Word docs only: updates headers in .docx/.doc\n"
            "• Update + reprint PDFs: updates docs then regenerates same-name PDFs\n"
            "• Reprint PDFs only: regenerates PDFs from existing .docx (no document edits)"
        )
        self.cmbOutputMode.setFixedHeight(32)
        self.cmbOutputMode.setStyleSheet(f"""
            QComboBox {{
                background: {Colors.BG_CARD};
                color: {Colors.TEXT_PRIMARY};
                border: 1px solid {Colors.BORDER};
                border-radius: 6px;
                padding: 6px 10px;
                font-size: 12px;
            }}
            QComboBox:hover {{
                border-color: {Colors.TEXT_MUTED};
            }}
            QComboBox::drop-down {{
                border: none;
                width: 26px;
            }}
            QComboBox QAbstractItemView {{
                background: {Colors.BG_CARD};
                color: {Colors.TEXT_PRIMARY};
                border: 1px solid {Colors.BORDER};
                selection-background-color: {Colors.PRIMARY};
                selection-color: white;
            }}
        """)
        output_row_layout.addWidget(self.cmbOutputMode, 1)
        proc_section.addWidget(output_row)
        
        # Separator
        sep1 = QFrame()
        sep1.setFrameShape(QFrame.Shape.HLine)
        sep1.setStyleSheet(f"background: {Colors.BORDER}; max-height: 1px; margin: 8px 0;")
        proc_section.addWidget(sep1)
        
        # Document options
        doc_label = QLabel("Document Handling:")
        doc_label.setStyleSheet(f"color: {Colors.TEXT_SECONDARY}; font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px;")
        proc_section.addWidget(doc_label)
        
        self.chkIncludeDoc = ToggleSwitch("Include legacy .doc files (requires Word)")
        self.chkIncludeDoc.setToolTip("Process older .doc format files by converting them to .docx.\nRequires Microsoft Word to be installed.")
        proc_section.addWidget(self.chkIncludeDoc)
        
        self.chkReplaceDoc = ToggleSwitch("Replace .doc with .docx (delete original)")
        self.chkReplaceDoc.setToolTip("After converting .doc to .docx, delete the original .doc file.\nLeave unchecked to keep both versions.")
        proc_section.addWidget(self.chkReplaceDoc)
        
        self.chkSkipTOC = ToggleSwitch("Skip 'Table of Contents' files")
        self.chkSkipTOC.setToolTip("Ignore files containing 'Table of Contents' in the filename.\nThese files typically don't need date/phase updates.")
        self.chkSkipTOC.setChecked(True)
        proc_section.addWidget(self.chkSkipTOC)
        
        scroll_layout.addWidget(proc_section)

        self.proc_section = proc_section
        proc_section.expanded.connect(self._scroll_to_processing)
        
        # ---- SECTION: Font Normalization ----
        font_section = CollapsibleSection("Font Normalization", expanded=False)
        
        self.chkNormalizeFonts = ToggleSwitch("Normalize all fonts")
        self.chkNormalizeFonts.setToolTip("Change all fonts in the document to a single font family.\n⚠️ May affect document layout - review after processing.")
        self.chkNormalizeFonts.toggled.connect(self._on_normalize_fonts_toggled)
        font_section.addWidget(self.chkNormalizeFonts)
        
        # Font options (shown when enabled)
        self.font_options = QWidget()
        font_opts_layout = QHBoxLayout(self.font_options)
        font_opts_layout.setContentsMargins(32, 0, 0, 0)
        font_opts_layout.setSpacing(16)
        
        font_name_label = QLabel("Font:")
        font_name_label.setStyleSheet(f"color: {Colors.TEXT_SECONDARY}; font-size: 12px;")
        self.txtTargetFont = StyledLineEdit()
        self.txtTargetFont.setText(DEFAULT_FONT_NAME)
        self.txtTargetFont.setFixedWidth(120)
        
        self.chkNormalizeFontSize = QCheckBox("Set size:")
        self.chkNormalizeFontSize.setToolTip("Also normalize all font sizes to a single value.\n⚠️ May significantly affect document layout.")
        self.chkNormalizeFontSize.setStyleSheet(f"color: {Colors.TEXT_SECONDARY}; font-size: 12px;")
        self.chkNormalizeFontSize.toggled.connect(lambda c: self.spnFontSize.setEnabled(c))
        
        self.spnFontSize = QSpinBox()
        self.spnFontSize.setRange(6, 72)
        self.spnFontSize.setValue(DEFAULT_FONT_SIZE)
        self.spnFontSize.setSuffix(" pt")
        self.spnFontSize.setEnabled(False)
        self.spnFontSize.setStyleSheet(f"""
            QSpinBox {{
                background: {Colors.BG_INPUT};
                color: {Colors.TEXT_PRIMARY};
                border: 1px solid {Colors.BORDER};
                border-radius: 6px;
                padding: 6px 10px;
                font-size: 12px;
            }}
            QSpinBox:disabled {{
                background: {Colors.BG_CARD};
                color: {Colors.TEXT_MUTED};
            }}
        """)
        
        font_opts_layout.addWidget(font_name_label)
        font_opts_layout.addWidget(self.txtTargetFont)
        font_opts_layout.addWidget(self.chkNormalizeFontSize)
        font_opts_layout.addWidget(self.spnFontSize)
        font_opts_layout.addStretch()
        
        self.font_options.setVisible(False)
        font_section.addWidget(self.font_options)
        
        scroll_layout.addWidget(font_section)

        self.font_section = font_section
        
        # ---- SECTION: Backup ----
        backup_section = CollapsibleSection("Backup", expanded=False)
        
        self.chkBackup = ToggleSwitch("Create backups before editing")
        self.chkBackup.setToolTip("Copy original documents to a backup folder before making changes.\nRecommended for first-time use or large batch operations.")
        self.chkBackup.toggled.connect(lambda c: self.backup_options.setVisible(c))
        backup_section.addWidget(self.chkBackup)
        
        self.backup_options = QWidget()
        backup_opts_layout = QHBoxLayout(self.backup_options)
        backup_opts_layout.setContentsMargins(32, 0, 0, 0)
        
        self.txtBackup = StyledLineEdit("Select backup folder...")
        self.txtBackup.setReadOnly(True)
        backup_browse = StyledButton("Browse", "ghost")
        backup_browse.setFixedWidth(80)
        backup_browse.clicked.connect(self._browse_backup)
        
        backup_opts_layout.addWidget(self.txtBackup)
        backup_opts_layout.addWidget(backup_browse)
        
        self.backup_options.setVisible(False)
        backup_section.addWidget(self.backup_options)
        
        scroll_layout.addWidget(backup_section)

        self.backup_section = backup_section
        
        # ---- SECTION: Exclude Folders ----
        exclude_section = CollapsibleSection("Exclude Folders", expanded=False)
        
        exclude_desc = QLabel("Skip folders with these names (case-insensitive):")
        exclude_desc.setStyleSheet(f"color: {Colors.TEXT_SECONDARY}; font-size: 11px;")
        exclude_section.addWidget(exclude_desc)
        
        self.lstExclude = QListWidget()
        self.lstExclude.setMaximumHeight(80)
        self.lstExclude.setStyleSheet(f"""
            QListWidget {{
                background: transparent;
                border: 1px solid {Colors.BORDER};
                border-radius: 3px;
            }}
            QListWidget::item {{
                color: {Colors.TEXT_SECONDARY};
                padding: 3px 8px;
                border: none;
            }}
            QListWidget::item:selected {{
                background: {Colors.PRIMARY};
                color: white;
            }}
            QListWidget::item:hover {{
                background: {Colors.BG_CARD};
            }}
        """)
        # Default excludes
        for folder in ["_archive", "archive"]:
            self.lstExclude.addItem(folder)
        exclude_section.addWidget(self.lstExclude)
        
        exclude_btns = QHBoxLayout()
        self.txtExcludeAdd = StyledLineEdit("Folder name...")
        self.txtExcludeAdd.setFixedWidth(200)
        add_exclude_btn = StyledButton("+ Add", "ghost")
        add_exclude_btn.setFixedWidth(70)
        add_exclude_btn.clicked.connect(self._add_exclude)
        remove_exclude_btn = StyledButton("− Remove", "ghost")
        remove_exclude_btn.setFixedWidth(80)
        remove_exclude_btn.clicked.connect(self._remove_exclude)
        
        exclude_btns.addWidget(self.txtExcludeAdd)
        exclude_btns.addWidget(add_exclude_btn)
        exclude_btns.addWidget(remove_exclude_btn)
        exclude_btns.addStretch()
        exclude_section.addLayout(exclude_btns)
        
        scroll_layout.addWidget(exclude_section)
        
        # Remember the exclude section for auto-scroll
        self.exclude_section = exclude_section
        exclude_section.expanded.connect(self._scroll_to_exclude)
        
        scroll.setWidget(scroll_content)
        left_layout.addWidget(scroll)
        
        # ---- Progress Bar ----
        progress_container = QWidget()
        progress_container.setStyleSheet(f"background: {Colors.BG_CARD}; border-radius: 6px;")
        progress_layout = QHBoxLayout(progress_container)
        progress_layout.setContentsMargins(16, 12, 16, 12)
        progress_layout.setSpacing(16)
        
        self.progressBar = QProgressBar()
        self.progressBar.setTextVisible(False)
        self.progressBar.setFixedHeight(8)
        self.progressBar.setStyleSheet(f"""
            QProgressBar {{
                background: {Colors.BG_INPUT};
                border: none;
                border-radius: 4px;
            }}
            QProgressBar::chunk {{
                background: qlineargradient(x1:0, y1:0, x2:1, y2:0,
                    stop:0 {Colors.PRIMARY}, stop:1 {Colors.SUCCESS});
                border-radius: 4px;
            }}
        """)
        
        self.progressLabel = QLabel("Ready")
        self.progressLabel.setStyleSheet(f"color: {Colors.TEXT_SECONDARY}; font-size: 12px; font-weight: 500;")
        self.progressLabel.setAlignment(Qt.AlignmentFlag.AlignRight | Qt.AlignmentFlag.AlignVCenter)
        self.progressLabel.setFixedWidth(80)
        
        progress_layout.addWidget(self.progressBar)
        progress_layout.addWidget(self.progressLabel)
        
        left_layout.addWidget(progress_container)
        
        # ---- Action Buttons ----
        action_row = QHBoxLayout()
        action_row.setContentsMargins(0, 8, 0, 0)
        action_row.setSpacing(10)
        
        self.btnRun = StyledButton("▶  Run Update", "success")
        self.btnRun.setMinimumWidth(140)
        self.btnRun.setFixedHeight(38)
        self.btnRun.clicked.connect(self.startRun)
        
        self.btnCancel = StyledButton("Cancel", "danger")
        self.btnCancel.setFixedHeight(38)
        self.btnCancel.setFixedWidth(80)
        self.btnCancel.setEnabled(False)
        self.btnCancel.clicked.connect(self.cancelRun)
        
        action_row.addStretch()
        action_row.addWidget(self.btnRun)
        action_row.addWidget(self.btnCancel)
        
        left_layout.addLayout(action_row)
        
        main_layout.addWidget(self.left_panel, 1)
        
        # ============================================================
        # RIGHT SIDE: Log Panel (starts hidden)
        # ============================================================
        self.log_panel = LogPanel()
        self.log_panel.setVisible(False)
        main_layout.addWidget(self.log_panel)
    
    # ----------------------------------------------------------------
    # UI Helpers
    # ----------------------------------------------------------------
    def _scroll_to_widget(self, widget: QWidget, align_top: bool = False, margin: int = 32):
        """Ensure the given widget is visible inside the main scroll area.

        If align_top is True, the section header is brought near the top.
        Otherwise we do the minimum scroll needed so the whole section is visible.
        """
        if not hasattr(self, "scroll_area") or self.scroll_area is None:
            return
        content = self.scroll_area.widget()
        if content is None or widget is None:
            return

        # Position of the widget relative to the scroll content
        pos = widget.mapTo(content, QPoint(0, 0))
        bar = self.scroll_area.verticalScrollBar()
        viewport = self.scroll_area.viewport()
        if viewport is None:
            return

        viewport_h = viewport.height()
        current = bar.value()
        top = pos.y()
        bottom = top + widget.height()

        if align_top:
            # Always bring the header near the top
            target = max(0, top - margin)
        else:
            # Minimal scroll to keep the whole section in view
            view_top = current
            view_bottom = current + viewport_h
            target = current

            # If top is above the visible area, scroll up
            if top < view_top + margin:
                target = max(0, top - margin)
            # If bottom is below the visible area, scroll down
            elif bottom > view_bottom - margin:
                target = max(0, bottom - viewport_h + margin)

        bar.setValue(target)

    def _scroll_to_exclude(self, is_expanded: bool):
        """Auto-scroll to the Exclude Folders section when it is expanded."""
        if is_expanded:
            # Defer until after layout has updated
            QTimer.singleShot(0, lambda: self._scroll_to_widget(self.exclude_section, align_top=False))

    def _scroll_to_processing(self, is_expanded: bool):
        """Scroll Processing Options to the top when expanded (it's a large section)."""
        if is_expanded:
            QTimer.singleShot(0, lambda: self._scroll_to_widget(self.proc_section, align_top=True))

    def _format_eta(self, seconds: float) -> str:
        """Format a number of seconds as a short human-readable duration."""
        seconds = int(max(0, seconds))
        minutes, sec = divmod(seconds, 60)
        hours, mins = divmod(minutes, 60)
        if hours:
            return f"{hours}h {mins:02d}m"
        if minutes:
            return f"{minutes}m {sec:02d}s"
        return f"{sec}s"

    def _toggle_log_panel(self):
        """Toggle log panel visibility and resize window."""
        self._log_visible = not self._log_visible
        
        if self._log_visible:
            # Expand window to show log
            self.log_panel.setVisible(True)
            new_width = self.width() + self.PANEL_WIDTH
            self.resize(new_width, self.height())
            self.btnLogToggle.setText("📋 Hide Log")
            self.btnLogToggle.setStyleSheet(f"""
                QPushButton {{
                    background: {Colors.PRIMARY};
                    color: white;
                    border: none;
                    border-radius: 4px;
                    padding: 6px 12px;
                    font-size: 11px;
                }}
                QPushButton:hover {{
                    background: {Colors.PRIMARY_HOVER};
                }}
            """)
        else:
            # Collapse window to hide log
            self.log_panel.setVisible(False)
            new_width = self.width() - self.PANEL_WIDTH
            self.resize(new_width, self.height())
            self.btnLogToggle.setText("📋 Show Log")
            self.btnLogToggle.setStyleSheet(f"""
                QPushButton {{
                    background: transparent;
                    color: {Colors.TEXT_SECONDARY};
                    border: 1px solid {Colors.BORDER};
                    border-radius: 4px;
                    padding: 6px 12px;
                    font-size: 11px;
                }}
                QPushButton:hover {{
                    background: {Colors.BG_CARD};
                    color: {Colors.TEXT_PRIMARY};
                    border-color: {Colors.TEXT_MUTED};
                }}
            """)
    
    def _show_log_panel(self):
        """Show log panel if hidden."""
        if not self._log_visible:
            self._toggle_log_panel()
    
    # ----------------------------------------------------------------
    # Theme Toggle
    # ----------------------------------------------------------------
    def _toggle_theme(self):
        """Toggle between dark and light themes."""
        new_theme = "light" if Colors.is_dark() else "dark"
        Colors.set_theme(new_theme)
        self._apply_theme()
    
    def _apply_theme(self):
        """Reapply all styles with current theme colors."""
        # Update main window background
        self.setStyleSheet(f"background: {Colors.BG_MAIN};")
        
        # Update left panel background
        if hasattr(self, 'left_panel'):
            self.left_panel.setStyleSheet(f"background: {Colors.BG_MAIN};")
        
        # Update theme toggle button
        is_dark = Colors.is_dark()
        self.btnThemeToggle.setText("🌙" if is_dark else "☀️")
        self.btnThemeToggle.setToolTip("Switch to light mode" if is_dark else "Switch to dark mode")
        self.btnThemeToggle.setStyleSheet(f"""
            QPushButton {{
                background: transparent;
                color: {Colors.TEXT_SECONDARY};
                border: 1px solid {Colors.BORDER};
                border-radius: 4px;
                padding: 4px 10px;
                font-size: 12px;
            }}
            QPushButton:hover {{
                background: {Colors.BG_CARD};
                border-color: {Colors.TEXT_MUTED};
            }}
        """)
        
        # Update title
        self.titleLabel.setStyleSheet(f"color: {Colors.TEXT_PRIMARY}; font-size: 18px; font-weight: 600; letter-spacing: -0.5px;")
        
        # Update log toggle button
        if self._log_visible:
            self.btnLogToggle.setStyleSheet(f"""
                QPushButton {{
                    background: {Colors.PRIMARY};
                    color: white;
                    border: none;
                    border-radius: 4px;
                    padding: 6px 12px;
                    font-size: 11px;
                }}
                QPushButton:hover {{
                    background: {Colors.PRIMARY_HOVER};
                }}
            """)
        else:
            self.btnLogToggle.setStyleSheet(f"""
                QPushButton {{
                    background: transparent;
                    color: {Colors.TEXT_SECONDARY};
                    border: 1px solid {Colors.BORDER};
                    border-radius: 4px;
                    padding: 6px 12px;
                    font-size: 11px;
                }}
                QPushButton:hover {{
                    background: {Colors.BG_CARD};
                    color: {Colors.TEXT_PRIMARY};
                    border-color: {Colors.TEXT_MUTED};
                }}
            """)
        
        # Update log panel via its own theme method
        self.log_panel.apply_theme()
        
        # Update scroll area with consistent scrollbar styling
        self.scroll_area.setStyleSheet(f"""
            QScrollArea {{
                background: transparent;
                border: none;
            }}
            QScrollBar:vertical {{
                background: {Colors.BG_MAIN};
                width: 8px;
                border-radius: 4px;
            }}
            QScrollBar::handle:vertical {{
                background: {Colors.BORDER};
                border-radius: 4px;
                min-height: 20px;
            }}
            QScrollBar::handle:vertical:hover {{
                background: {Colors.TEXT_MUTED};
            }}
            QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {{
                height: 0px;
            }}
            QScrollBar::add-page:vertical, QScrollBar::sub-page:vertical {{
                background: transparent;
            }}
        """)
        
        # Update core section title
        if hasattr(self, 'core_section'):
            for child in self.core_section.findChildren(QLabel):
                if child.text() == "Core Settings":
                    child.setStyleSheet(f"""
                        color: {Colors.TEXT_PRIMARY};
                        font-weight: 600;
                        font-size: 11px;
                        letter-spacing: 0.3px;
                        padding-bottom: 8px;
                        border-bottom: 1px solid {Colors.BORDER};
                    """)
                    break
        
        # Update StyledLineEdit inputs only.
        # IMPORTANT: Do not mass-style all QLineEdit widgets here, because composite widgets
        # (notably QDateEdit) contain internal QLineEdit editors whose styling can cause
        # intermittent clipping/resizing when the theme is toggled.
        input_style = f"""
            QLineEdit {{
                background: transparent;
                color: {Colors.TEXT_PRIMARY};
                border: none;
                border-bottom: 1px solid {Colors.BORDER};
                border-radius: 0;
                min-height: 28px;
                padding: 8px 2px 8px 2px;
                font-size: 13px;
            }}
            QLineEdit:focus {{
                border-bottom: 2px solid {Colors.PRIMARY};
                padding: 8px 2px 7px 2px;
            }}
            QLineEdit:disabled {{
                color: {Colors.TEXT_MUTED};
            }}
            QLineEdit::placeholder {{
                color: {Colors.TEXT_MUTED};
            }}
        """
        for line_edit in self.findChildren(StyledLineEdit):
            line_edit.setStyleSheet(input_style)

        if hasattr(self, "cmbOutputMode"):
            self.cmbOutputMode.setStyleSheet(f"""
                QComboBox {{
                    background: {Colors.BG_CARD};
                    color: {Colors.TEXT_PRIMARY};
                    border: 1px solid {Colors.BORDER};
                    border-radius: 6px;
                    padding: 6px 10px;
                    font-size: 12px;
                }}
                QComboBox:hover {{
                    border-color: {Colors.TEXT_MUTED};
                }}
                QComboBox::drop-down {{
                    border: none;
                    width: 26px;
                }}
                QComboBox QAbstractItemView {{
                    background: {Colors.BG_CARD};
                    color: {Colors.TEXT_PRIMARY};
                    border: 1px solid {Colors.BORDER};
                    selection-background-color: {Colors.PRIMARY};
                    selection-color: white;
                }}
            """)
        
        # Update buttons
        for btn in [self.btnRun, self.btnCancel]:
            if btn == self.btnRun:
                btn.setStyleSheet(f"""
                    QPushButton {{
                        background: {Colors.PRIMARY};
                        color: white;
                        border: none;
                        border-radius: 6px;
                        padding: 12px 24px;
                        font-weight: 600;
                        font-size: 13px;
                    }}
                    QPushButton:hover {{
                        background: {Colors.PRIMARY_HOVER};
                    }}
                    QPushButton:disabled {{
                        background: {Colors.BG_CARD};
                        color: {Colors.TEXT_MUTED};
                    }}
                """)
            else:
                btn.setStyleSheet(f"""
                    QPushButton {{
                        background: {Colors.DANGER};
                        color: white;
                        border: none;
                        border-radius: 3px;
                        padding: 8px 16px;
                        font-weight: 500;
                        font-size: 12px;
                    }}
                    QPushButton:hover {{
                        background: #DC2626;
                    }}
                """)
        
        # Update progress bar and container
        self.progressBar.setStyleSheet(f"""
            QProgressBar {{
                background: {Colors.BG_INPUT};
                border: none;
                border-radius: 4px;
            }}
            QProgressBar::chunk {{
                background: qlineargradient(x1:0, y1:0, x2:1, y2:0,
                    stop:0 {Colors.PRIMARY}, stop:1 {Colors.SUCCESS});
                border-radius: 4px;
            }}
        """)
        self.progressLabel.setStyleSheet(f"color: {Colors.TEXT_SECONDARY}; font-size: 12px; font-weight: 500;")
        
        # Update progress container
        if hasattr(self, 'progressBar') and self.progressBar.parent():
            self.progressBar.parent().setStyleSheet(f"background: {Colors.BG_CARD}; border-radius: 6px;")
        
        # Update date container and date edit (must match original styling exactly)
        if hasattr(self, 'date_container'):
            self.date_container.setStyleSheet(f"""
                QWidget {{
                    background: transparent;
                    border-bottom: 1px solid {Colors.BORDER};
                }}
                QWidget:focus-within {{
                    border-bottom: 2px solid {Colors.PRIMARY};
                }}
            """)
        
        if hasattr(self, 'dateEdit'):
            self.dateEdit.setStyleSheet(f"""
                QDateEdit {{
                    background: transparent;
                    color: {Colors.TEXT_PRIMARY};
                    border: none;
                    min-height: 32px;
                    padding: 8px 2px;
                    font-size: 13px;
                }}
                QDateEdit::drop-down {{
                    border: none;
                    background: transparent;
                    width: 0px;
                    height: 0px;
                }}
                QDateEdit::down-arrow {{
                    image: none;
                    border: none;
                    width: 0px;
                    height: 0px;
                }}
            """)
            self.dateEdit.setMinimumHeight(32)
            self.dateEdit.setFixedHeight(32)
        
        if hasattr(self, 'cal_btn'):
            self.cal_btn.setStyleSheet(f"""
                QPushButton {{
                    background: transparent;
                    border: none;
                    font-size: 14px;
                    padding-bottom: 4px;
                }}
                QPushButton:hover {{
                    background: {Colors.BG_CARD};
                    border-radius: 4px;
                }}
            """)
        
        # Update spinbox
        if hasattr(self, 'spnFontSize'):
            self.spnFontSize.setStyleSheet(f"""
                QSpinBox {{
                    background: {Colors.BG_INPUT};
                    color: {Colors.TEXT_PRIMARY};
                    border: 1px solid {Colors.BORDER};
                    border-radius: 6px;
                    padding: 6px 10px;
                    font-size: 12px;
                }}
                QSpinBox:disabled {{
                    background: {Colors.BG_CARD};
                    color: {Colors.TEXT_MUTED};
                }}
            """)
        
        # Update checkboxes (like chkNormalizeFontSize)
        checkbox_style = f"""
            QCheckBox {{
                color: {Colors.TEXT_SECONDARY};
                font-size: 12px;
            }}
            QCheckBox::indicator {{
                width: 16px;
                height: 16px;
                border-radius: 3px;
                border: 1px solid {Colors.BORDER};
                background: {Colors.BG_INPUT};
            }}
            QCheckBox::indicator:checked {{
                background: {Colors.PRIMARY};
                border-color: {Colors.PRIMARY};
            }}
        """
        if hasattr(self, 'chkNormalizeFontSize'):
            self.chkNormalizeFontSize.setStyleSheet(checkbox_style)
        
        # Update all toggle switches
        toggle_style = f"""
            QCheckBox {{
                spacing: 0px;
            }}
            QCheckBox::indicator {{
                width: 36px;
                height: 18px;
                border-radius: 9px;
                background: {Colors.BG_INPUT};
                border: 1px solid {Colors.BORDER};
            }}
            QCheckBox::indicator:checked {{
                background: {Colors.PRIMARY};
                border-color: {Colors.PRIMARY};
            }}
            QCheckBox::indicator:hover {{
                border-color: {Colors.TEXT_MUTED};
            }}
            QCheckBox::indicator:checked:hover {{
                background: {Colors.PRIMARY_HOVER};
            }}
        """
        for toggle in self.findChildren(ToggleSwitch):
            toggle.checkbox.setStyleSheet(toggle_style)
            toggle.label.setStyleSheet(f"color: {Colors.TEXT_SECONDARY}; font-size: 12px;")
        
        # Update secondary labels
        label_style = f"color: {Colors.TEXT_SECONDARY}; font-size: 12px;"
        for label in self.findChildren(QLabel):
            # Skip the title label
            if label == self.titleLabel:
                continue
            # Update labels that look like secondary text
            current_style = label.styleSheet()
            if "TEXT_SECONDARY" in current_style or "font-size: 12px" in current_style:
                label.setStyleSheet(label_style)
        
        # Update list widgets (exclude list)
        list_style = f"""
            QListWidget {{
                background: {Colors.BG_CARD};
                border: 1px solid {Colors.BORDER};
                border-radius: 6px;
                padding: 4px;
                color: {Colors.TEXT_PRIMARY};
            }}
            QListWidget::item {{
                padding: 6px 8px;
                border-radius: 4px;
            }}
            QListWidget::item:selected {{
                background: {Colors.PRIMARY};
                color: white;
            }}
            QListWidget::item:hover:!selected {{
                background: {Colors.BG_HOVER};
            }}
        """
        for list_widget in self.findChildren(QListWidget):
            list_widget.setStyleSheet(list_style)
        
        # Update ghost buttons
        ghost_style = f"""
            QPushButton {{
                background: transparent;
                color: {Colors.TEXT_SECONDARY};
                border: 1px solid {Colors.BORDER};
                border-radius: 3px;
                padding: 8px 16px;
                font-size: 12px;
            }}
            QPushButton:hover {{
                background: {Colors.BG_CARD};
                color: {Colors.TEXT_PRIMARY};
            }}
        """
        for btn in self.findChildren(StyledButton):
            if btn not in [self.btnRun, self.btnCancel]:
                btn.setStyleSheet(ghost_style)
        
        # Update all section headers
        for section in [self.proc_section, self.font_section, self.backup_section, self.exclude_section]:
            if hasattr(section, 'toggle_btn'):
                section.toggle_btn.setStyleSheet(f"""
                    QPushButton {{
                        background: transparent;
                        color: {Colors.TEXT_PRIMARY};
                        border: none;
                        text-align: left;
                        padding: 8px 0;
                        font-weight: 600;
                        font-size: 11px;
                        letter-spacing: 0.3px;
                        border-bottom: 1px solid {Colors.BORDER};
                    }}
                    QPushButton:hover {{
                        color: {Colors.PRIMARY_LIGHT};
                    }}
                """)
    
    # ----------------------------------------------------------------
    def _on_normalize_fonts_toggled(self, checked):
        self.font_options.setVisible(checked)
        if not checked:
            self.chkNormalizeFontSize.setChecked(False)
    
    def _browse_folder(self):
        folder = QFileDialog.getExistingDirectory(self, "Select Specifications Folder")
        if folder:
            self.txtFolder.setText(folder)
    
    def _browse_backup(self):
        folder = QFileDialog.getExistingDirectory(self, "Select Backup Folder")
        if folder:
            self.txtBackup.setText(folder)
    
    def _add_exclude(self):
        text = self.txtExcludeAdd.text().strip()
        if text and not self.lstExclude.findItems(text, Qt.MatchFlag.MatchExactly):
            self.lstExclude.addItem(text)
            self.txtExcludeAdd.clear()
    
    def _remove_exclude(self):
        for item in self.lstExclude.selectedItems():
            self.lstExclude.takeItem(self.lstExclude.row(item))
    # ----------------------------------------------------------------
    # Run Logic
    # ----------------------------------------------------------------
    def setUIEnabled(self, enabled: bool):
        self.btnRun.setEnabled(enabled)
        self.btnCancel.setEnabled(not enabled)
    
    def appendLog(self, text: str):
        self.log_panel.append(text)
    
    @Slot(int, int)
    def onProgress(self, current: int, total: int):
        pct = int(current / total * 100) if total else 0
        self.progressBar.setValue(pct)
        # Default text if we cannot estimate time yet
        label = f"{current}/{total}" if total else "Processing..."

        if total and current > 0 and self._run_start_time is not None:
            elapsed = time.monotonic() - self._run_start_time
            # Simple average-based ETA
            est_total = elapsed * (total / current)
            remaining = max(0.0, est_total - elapsed)
            eta_text = self._format_eta(remaining)
            label = f"{current}/{total} • {pct}% • ~{eta_text} left"

        self.progressLabel.setText(label)
    
    @Slot(int, int, dict)
    def onFinished(self, updated: int, errors: int, stats: dict):
        self.setUIEnabled(True)
        self.progressBar.setValue(100)
        # Show total elapsed time if we tracked it
        elapsed_text = None
        if self._run_start_time is not None:
            elapsed = time.monotonic() - self._run_start_time
            elapsed_text = self._format_eta(elapsed)
        self._run_start_time = None

        if elapsed_text:
            self.progressLabel.setText(f"Complete • {elapsed_text}")
        else:
            self.progressLabel.setText("Complete")
        self.progressLabel.setStyleSheet(f"color: {Colors.SUCCESS}; font-size: 11px;")
        self.appendLog(f"\n✅ Done. Updated: {updated}, Errors: {errors}")
        
        # Show log panel if hidden to display results
        self._show_log_panel()
        
        # Show font warning dialog if font normalization was enabled
        if self.chkNormalizeFonts.isChecked() or self.chkNormalizeFontSize.isChecked():
            folder_path = self.txtFolder.text().strip()
            dialog = FontWarningDialog(folder_path, updated, errors, self)
            dialog.exec()
    
    @Slot(str)
    def onNeedsWord(self, msg: str):
        QMessageBox.warning(self, "Word Required", msg)
        self.setUIEnabled(True)
    
    def _center_dialog(self, dialog):
        """Center a dialog on the main window."""
        if dialog and self.isVisible():
            parent_geo = self.geometry()
            dialog_size = dialog.sizeHint()
            x = parent_geo.x() + (parent_geo.width() - dialog_size.width()) // 2
            y = parent_geo.y() + (parent_geo.height() - dialog_size.height()) // 2
            dialog.move(x, y)
    
    def _load_app_config(self) -> dict:
        """Load application configuration."""
        # Check multiple locations for app_config.json
        config_locations = [
            Path(__file__).parent.parent / 'app_config.json',  # Renamer root folder
            Path(__file__).parent / 'app_config.json',  # src folder (fallback)
            Path(__file__).parent.parent.parent / 'app_config.json',  # desktop-widgets folder
        ]
        
        config_file = None
        for path in config_locations:
            if path.exists():
                config_file = path
                break
        
        default_config = {
            "require_subscription": True,
            "require_word_check": True,
            "app_name": "Spec Header Date Updater",
            "app_version": "1.0.0",
            "dark_mode": False,
            "min_plan": "free"
        }
        
        try:
            if config_file and config_file.exists():
                with open(config_file, 'r') as f:
                    return json.load(f)
        except Exception as e:
            print(f"Error loading config: {e}")
        
        return default_config
    
    def _scan_for_legacy_doc_files(self, root: Path, recursive: bool, exclude_folders: list[str], skip_toc: bool) -> int:
        """Quick scan to count legacy .doc files in the target directory."""
        count = 0
        iterator = root.rglob("*.doc") if recursive else root.glob("*.doc")
        for f in iterator:
            if f.name.startswith("~$"):
                continue
            if any(part.lower() in exclude_folders for part in f.parts):
                continue
            if skip_toc and "table of contents" in f.stem.lower():
                continue
            count += 1
        return count
    
    def startRun(self):
        root = self.txtFolder.text().strip()
        if not root or not Path(root).is_dir():
            QMessageBox.warning(self, "Invalid Folder", "Please select a valid specifications folder.")
            return
        
        # Collect settings
        date_str = self.dateEdit.date().toString("MMMM d, yyyy")
        phase_text = self.txtPhase.text().strip()

        output_mode = "docs_only"
        if hasattr(self, "cmbOutputMode"):
            output_mode = self.cmbOutputMode.currentData() or "docs_only"

        if output_mode == "pdf_only":
            phase_text = None
        elif phase_text == "":
            mb = QMessageBox(self)
            mb.setIcon(QMessageBox.Icon.Warning)
            mb.setWindowTitle("Phase Text Blank")
            mb.setText("Phase Text is blank.")
            mb.setInformativeText("Choose whether to leave the existing phase text unchanged or remove it from documents.")
            btn_leave = mb.addButton("Leave Phase Unchanged", QMessageBox.ButtonRole.AcceptRole)
            btn_remove = mb.addButton("Remove Phase Text", QMessageBox.ButtonRole.DestructiveRole)
            btn_cancel = mb.addButton("Cancel", QMessageBox.ButtonRole.RejectRole)
            mb.setDefaultButton(btn_leave)
            self._center_dialog(mb)
            mb.exec()
            clicked = mb.clickedButton()
            if clicked == btn_cancel:
                return
            if clicked == btn_leave:
                phase_text = None
            else:
                phase_text = ""
        else:
            if not PHASE_RX.fullmatch(phase_text):
                mb = QMessageBox(self)
                mb.setIcon(QMessageBox.Icon.Warning)
                mb.setWindowTitle("Unrecognized Phase Text")
                mb.setText("Phase Text doesn't match the expected format.")
                mb.setInformativeText(
                    "Expected something like '100% Construction Documents'.\n"
                    "If you proceed, future runs may not be able to find and update phase text automatically."
                )
                btn_date_only = mb.addButton("Update Date Only", QMessageBox.ButtonRole.AcceptRole)
                btn_proceed = mb.addButton("Proceed Anyway", QMessageBox.ButtonRole.DestructiveRole)
                btn_cancel = mb.addButton("Cancel", QMessageBox.ButtonRole.RejectRole)
                mb.setDefaultButton(btn_date_only)
                self._center_dialog(mb)
                mb.exec()
                clicked = mb.clickedButton()
                if clicked == btn_cancel:
                    return
                if clicked == btn_date_only:
                    phase_text = None
        recursive = self.chkRecursive.isChecked()
        dry_run = self.chkDryRun.isChecked()
        
        include_doc = self.chkIncludeDoc.isChecked()
        replace_doc = self.chkReplaceDoc.isChecked()
        skip_toc = self.chkSkipTOC.isChecked()

        reprint_only = output_mode == "pdf_only"
        reprint_pdf = output_mode == "docs_and_pdf"
        
        normalize_fonts = self.chkNormalizeFonts.isChecked()
        target_font = self.txtTargetFont.text().strip() or DEFAULT_FONT_NAME
        target_font_size = self.spnFontSize.value() if self.chkNormalizeFontSize.isChecked() else None
        
        backup_dir = self.txtBackup.text().strip() if self.chkBackup.isChecked() else None
        
        exclude = [self.lstExclude.item(i).text() for i in range(self.lstExclude.count())]
        
        # Pre-scan for legacy .doc files if not included
        if not include_doc:
            root_path = Path(root)
            exclude_lower = [x.lower() for x in exclude]
            doc_count = self._scan_for_legacy_doc_files(root_path, recursive, exclude_lower, skip_toc)
            
            if doc_count > 0:
                # Check if Word is available (unless admin disabled the check)
                require_word_check = self.app_config.get('require_word_check', True)
                word_available, word_reason = check_word_available()
                
                if require_word_check and not word_available:
                    # Word not available - inform user
                    mb = QMessageBox(self)
                    mb.setIcon(QMessageBox.Icon.Warning)
                    mb.setWindowTitle("Legacy Files Detected - Word Required")
                    mb.setText(f"Found {doc_count} legacy .doc file(s) in the target directory.")
                    mb.setInformativeText(
                        f"Legacy .doc files require Microsoft Word to process.\n\n"
                        f"Issue: {word_reason}\n\n"
                        f"Options:\n"
                        f"• Install Microsoft Word to process .doc files\n"
                        f"• Convert .doc files to .docx manually\n"
                        f"• Skip the {doc_count} .doc file(s) and process only .docx files"
                    )
                    btn_skip = mb.addButton(f"Skip .doc Files", QMessageBox.ButtonRole.AcceptRole)
                    btn_cancel = mb.addButton("Cancel", QMessageBox.ButtonRole.RejectRole)
                    mb.setDefaultButton(btn_skip)
                    
                    self._center_dialog(mb)
                    result = mb.exec()
                    clicked = mb.clickedButton()
                    
                    if clicked == btn_cancel or clicked is None:
                        return
                else:
                    # Word available or check disabled - show normal options
                    mb = QMessageBox(self)
                    mb.setIcon(QMessageBox.Icon.Warning)
                    mb.setWindowTitle("Legacy Files Detected")
                    mb.setText(f"Found {doc_count} legacy .doc file(s) in the target directory.")
                    mb.setInformativeText(
                        "Legacy .doc files require Microsoft Word to process.\n\n"
                        "Choose how to handle these files:"
                    )
                    
                    btn_replace = mb.addButton("Convert to .docx (Delete .doc)", QMessageBox.ButtonRole.AcceptRole)
                    btn_keep = mb.addButton("Convert to .docx (Keep .doc)", QMessageBox.ButtonRole.AcceptRole)
                    btn_skip = mb.addButton(f"Skip All .doc Files", QMessageBox.ButtonRole.DestructiveRole)
                    btn_cancel = mb.addButton("Cancel", QMessageBox.ButtonRole.RejectRole)
                    mb.setDefaultButton(btn_replace)
                    
                    self._center_dialog(mb)
                    result = mb.exec()
                    clicked = mb.clickedButton()
                    
                    # Handle X button or Cancel button as cancel
                    if clicked == btn_cancel or clicked is None:
                        return
                    elif clicked == btn_skip:
                        pass
                    elif clicked == btn_replace:
                        include_doc = True
                        replace_doc = True
                        self.chkIncludeDoc.setChecked(True)
                        self.chkReplaceDoc.setChecked(True)
                    elif clicked == btn_keep:
                        include_doc = True
                        replace_doc = False
                        self.chkIncludeDoc.setChecked(True)
                        self.chkReplaceDoc.setChecked(False)
        
        # Clear log and reset progress
        self._run_start_time = time.monotonic()
        self.log_panel.clear_log()
        self.progressBar.setValue(0)
        self.progressLabel.setText("Processing...")
        self.progressLabel.setStyleSheet(f"color: {Colors.TEXT_MUTED}; font-size: 11px;")
        self.setUIEnabled(False)
        
        # Create worker
        subscription_mgr = self.subscription_mgr if INCLUDE_LICENSING else None
        
        self.worker = UpdateWorker(
            root=root,
            date_str=date_str,
            phase_text=phase_text,
            recursive=recursive,
            dry_run=dry_run,
            backup_dir=backup_dir,
            include_doc=include_doc,
            replace_doc_inplace=replace_doc,
            reprint_pdf=reprint_pdf,
            exclude_folders=exclude,
            subscription_mgr=subscription_mgr,
            normalize_fonts=normalize_fonts,
            target_font=target_font,
            target_font_size=target_font_size,
            skip_toc=skip_toc,
            reprint_only=reprint_only
        )
        self.worker.log.connect(self.appendLog)
        self.worker.progress.connect(self.onProgress)
        self.worker.finished.connect(self.onFinished)
        self.worker.needsWord.connect(self.onNeedsWord)
        self.worker.enableUI.connect(self.setUIEnabled)
        self.worker.start()
    
    def cancelRun(self):
        if self.worker:
            self.worker._cancel = True
            self.appendLog("⏹ Cancelling...")

# ============================================================================
# Main Entry Point
# ============================================================================
def main():
    app = QApplication(sys.argv)
    app.setStyle("Fusion")
    
    # Initialize theme from system preference
    Colors.init_from_system()

    app_icon = None
    icon_file = asset_path("assets/Logo.png")
    if icon_file.exists():
        app_icon = QIcon(str(icon_file))
        app.setWindowIcon(app_icon)
    
    window = MainWindowV2()
    if app_icon is not None:
        window.setWindowIcon(app_icon)
    window.show()
    
    sys.exit(app.exec())


if __name__ == "__main__":
    main()
