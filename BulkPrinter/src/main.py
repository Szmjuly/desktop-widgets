#!/usr/bin/env python3
"""
BulkPrinter - Word-to-PDF Bulk Converter
Finds Word documents (.docx/.doc) in subfolders of a source directory,
converts them to PDF via Microsoft Word, and outputs the PDFs into
matching subfolders of a destination directory.
"""
import sys
import os
from pathlib import Path
from typing import List, Tuple

from PySide6.QtCore import Qt, QThread, Signal
from PySide6.QtWidgets import (
    QApplication, QWidget, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit,
    QPushButton, QFileDialog, QCheckBox, QTextEdit, QProgressBar, QFrame,
    QMessageBox
)
from PySide6.QtGui import QFont

try:
    import win32com.client
    import pythoncom
    HAS_WIN32 = True
except ImportError:
    HAS_WIN32 = False


# ----------------------- Theme System (from Renamer) -----------------------
class Theme:
    LIGHT = {
        "PRIMARY": "#6366F1",
        "PRIMARY_HOVER": "#4F46E5",
        "SUCCESS": "#10B981",
        "DANGER": "#EF4444",
        "WARNING": "#F59E0B",
        "INFO": "#3B82F6",
        "BACKGROUND": "#F8FAFC",
        "CARD": "#FFFFFF",
        "INPUT_BG": "#FFFFFF",
        "BORDER": "#EDF2F7",
        "BORDER_FOCUS": "#6366F1",
        "TEXT": "#0F172A",
        "TEXT_SECONDARY": "#64748B",
        "TEXT_MUTED": "#94A3B8",
    }

    DARK = {
        "PRIMARY": "#818CF8",
        "PRIMARY_HOVER": "#6366F1",
        "SUCCESS": "#34D399",
        "DANGER": "#F87171",
        "WARNING": "#FBBF24",
        "INFO": "#60A5FA",
        "BACKGROUND": "#0F172A",
        "CARD": "#1E293B",
        "INPUT_BG": "#1E293B",
        "BORDER": "#334155",
        "BORDER_FOCUS": "#818CF8",
        "TEXT": "#F1F5F9",
        "TEXT_SECONDARY": "#CBD5E1",
        "TEXT_MUTED": "#94A3B8",
    }

    @staticmethod
    def get_theme(is_dark: bool):
        return Theme.DARK if is_dark else Theme.LIGHT


class Colors:
    _theme = Theme.LIGHT

    @classmethod
    def set_theme(cls, is_dark: bool):
        cls._theme = Theme.get_theme(is_dark)
        cls._update_colors()

    @classmethod
    def _update_colors(cls):
        cls.PRIMARY = cls._theme["PRIMARY"]
        cls.PRIMARY_HOVER = cls._theme["PRIMARY_HOVER"]
        cls.SUCCESS = cls._theme["SUCCESS"]
        cls.DANGER = cls._theme["DANGER"]
        cls.WARNING = cls._theme["WARNING"]
        cls.INFO = cls._theme["INFO"]
        cls.BACKGROUND = cls._theme["BACKGROUND"]
        cls.CARD = cls._theme["CARD"]
        cls.INPUT_BG = cls._theme["INPUT_BG"]
        cls.BORDER = cls._theme["BORDER"]
        cls.BORDER_FOCUS = cls._theme["BORDER_FOCUS"]
        cls.TEXT = cls._theme["TEXT"]
        cls.TEXT_SECONDARY = cls._theme["TEXT_SECONDARY"]
        cls.TEXT_MUTED = cls._theme["TEXT_MUTED"]

    PRIMARY = Theme.LIGHT["PRIMARY"]
    PRIMARY_HOVER = Theme.LIGHT["PRIMARY_HOVER"]
    SUCCESS = Theme.LIGHT["SUCCESS"]
    DANGER = Theme.LIGHT["DANGER"]
    WARNING = Theme.LIGHT["WARNING"]
    INFO = Theme.LIGHT["INFO"]
    BACKGROUND = Theme.LIGHT["BACKGROUND"]
    CARD = Theme.LIGHT["CARD"]
    INPUT_BG = Theme.LIGHT["INPUT_BG"]
    BORDER = Theme.LIGHT["BORDER"]
    BORDER_FOCUS = Theme.LIGHT["BORDER_FOCUS"]
    TEXT = Theme.LIGHT["TEXT"]
    TEXT_SECONDARY = Theme.LIGHT["TEXT_SECONDARY"]
    TEXT_MUTED = Theme.LIGHT["TEXT_MUTED"]


# ----------------------- UI Components -----------------------
class ModernCard(QFrame):
    def __init__(self, title=None, parent=None):
        super().__init__(parent)
        self.title_label = None
        self._title = title
        self.update_style()

        layout = QVBoxLayout(self)
        layout.setSpacing(4)
        layout.setContentsMargins(0, 0, 0, 0)

        if title:
            self.title_label = QLabel(title)
            layout.addWidget(self.title_label)

        self.content_layout = QVBoxLayout()
        self.content_layout.setSpacing(4)
        layout.addLayout(self.content_layout)

    def update_style(self):
        self.setStyleSheet(f"""
            QFrame {{
                background-color: {Colors.CARD};
                border: 1px solid {Colors.BORDER};
                border-radius: 6px;
                padding: 6px;
            }}
        """)
        if self.title_label:
            self.title_label.setStyleSheet(f"""
                font-size: 12px;
                font-weight: 600;
                color: {Colors.TEXT};
                padding-bottom: 4px;
                border-bottom: 1px solid {Colors.BORDER};
            """)

    def add_widget(self, widget):
        self.content_layout.addWidget(widget)


class ModernButton(QPushButton):
    def __init__(self, text, variant="primary", icon=None, parent=None):
        super().__init__(text, parent)
        self._variant = variant
        self._icon = icon
        if icon:
            self.setText(f"{icon} {text}")
        self.update_style()
        self.setCursor(Qt.PointingHandCursor)

    def update_style(self):
        colors = {
            "primary": (Colors.PRIMARY, "#FFFFFF"),
            "success": (Colors.SUCCESS, "#FFFFFF"),
            "danger": (Colors.DANGER, "#FFFFFF"),
            "secondary": (Colors.BORDER, Colors.TEXT),
        }
        bg_color, text_color = colors.get(self._variant, colors["primary"])
        disabled_bg = "#64748B" if Colors._theme == Theme.DARK else "#D1D5DB"
        disabled_text = "#94A3B8" if Colors._theme == Theme.DARK else "#9CA3AF"

        self.setStyleSheet(f"""
            QPushButton {{
                background-color: {bg_color};
                color: {text_color};
                border: none;
                border-radius: 4px;
                padding: 0px 8px;
                font-size: 11px;
                font-weight: 600;
                height: 26px;
            }}
            QPushButton:hover {{
                opacity: 0.9;
                background-color: {bg_color};
            }}
            QPushButton:pressed {{
                opacity: 0.8;
            }}
            QPushButton:disabled {{
                background-color: {disabled_bg};
                color: {disabled_text};
            }}
        """)


# ----------------------- Worker Thread -----------------------
class PrintToPdfWorker(QThread):
    """Background thread that opens Word docs and saves as PDF."""
    progress = Signal(int, int)      # current, total
    log_message = Signal(str)        # log line
    finished_signal = Signal(int)    # total files converted

    WD_EXPORT_PDF = 17  # wdExportFormatPDF

    def __init__(self, destination: str,
                 file_list: List[Tuple[Path, Path]], parent=None):
        super().__init__(parent)
        self.destination = Path(destination)
        self.file_list = file_list  # list of (doc_path, relative_subfolder)

    def run(self):
        pythoncom.CoInitialize()
        word = None
        total = len(self.file_list)
        converted = 0

        try:
            word = win32com.client.Dispatch("Word.Application")
            word.Visible = False
            word.DisplayAlerts = 0  # wdAlertsNone

            for i, (doc_path, rel_folder) in enumerate(self.file_list):
                try:
                    dest_dir = self.destination / rel_folder
                    dest_dir.mkdir(parents=True, exist_ok=True)

                    pdf_name = doc_path.stem + ".pdf"
                    dest_file = dest_dir / pdf_name

                    # Handle duplicates
                    if dest_file.exists():
                        stem = doc_path.stem
                        counter = 1
                        while dest_file.exists():
                            dest_file = dest_dir / f"{stem} ({counter}).pdf"
                            counter += 1

                    self.log_message.emit(f"  Converting: {rel_folder / doc_path.name}")

                    doc = word.Documents.Open(str(doc_path.resolve()), ReadOnly=True)
                    doc.ExportAsFixedFormat(
                        str(dest_file.resolve()),
                        self.WD_EXPORT_PDF,
                        OpenAfterExport=False,
                        OptimizeFor=0,  # wdExportOptimizeForPrint
                    )
                    doc.Close(SaveChanges=0)
                    converted += 1
                    self.log_message.emit(f"✓ {rel_folder / pdf_name}")
                except Exception as e:
                    self.log_message.emit(f"✗ ERROR: {doc_path.name} — {e}")
                    try:
                        if word and word.Documents.Count > 0:
                            word.Documents.Item(1).Close(SaveChanges=0)
                    except Exception:
                        pass

                self.progress.emit(i + 1, total)

        except Exception as e:
            self.log_message.emit(f"✗ FATAL: Could not start Word — {e}")
        finally:
            if word:
                try:
                    word.Quit()
                except Exception:
                    pass
            pythoncom.CoUninitialize()

        self.finished_signal.emit(converted)


# ----------------------- Main Window -----------------------
class BulkPrinterWindow(QWidget):
    def __init__(self):
        super().__init__()
        self._is_dark = False
        Colors.set_theme(self._is_dark)
        self.worker = None
        self.init_ui()
        self.apply_theme()

    def init_ui(self):
        self.setWindowTitle("BulkPrinter — Word to PDF")
        self.setMinimumSize(650, 580)
        self.resize(720, 620)

        root = QVBoxLayout(self)
        root.setSpacing(8)
        root.setContentsMargins(12, 12, 12, 12)

        # ---- Header row ----
        header_row = QHBoxLayout()
        title = QLabel("BulkPrinter")
        title.setFont(QFont("Segoe UI", 14, QFont.Bold))
        header_row.addWidget(title)
        header_row.addStretch()

        self.theme_btn = ModernButton("🌙 Dark", variant="secondary")
        self.theme_btn.clicked.connect(self.toggle_theme)
        header_row.addWidget(self.theme_btn)
        root.addLayout(header_row)

        subtitle = QLabel("Convert Word documents to PDF across subfolders.")
        subtitle.setFont(QFont("Segoe UI", 9))
        root.addWidget(subtitle)

        # ---- Source folder card ----
        src_card = ModernCard("Source Folder")
        src_row = QHBoxLayout()
        self.src_input = QLineEdit()
        self.src_input.setPlaceholderText("Select the mother folder containing subfolders with Word docs…")
        src_row.addWidget(self.src_input)
        src_browse = ModernButton("Browse…", variant="secondary")
        src_browse.clicked.connect(self.browse_source)
        src_row.addWidget(src_browse)
        w = QWidget(); w.setLayout(src_row)
        src_card.add_widget(w)
        root.addWidget(src_card)

        # ---- Destination folder card ----
        dst_card = ModernCard("Destination Folder")
        dst_row = QHBoxLayout()
        self.dst_input = QLineEdit()
        self.dst_input.setPlaceholderText("Select or type the output folder…")
        dst_row.addWidget(self.dst_input)
        dst_browse = ModernButton("Browse…", variant="secondary")
        dst_browse.clicked.connect(self.browse_destination)
        dst_row.addWidget(dst_browse)
        w2 = QWidget(); w2.setLayout(dst_row)
        dst_card.add_widget(w2)
        root.addWidget(dst_card)

        # ---- Options card ----
        opt_card = ModernCard("Options")

        self.docx_cb = QCheckBox(".docx")
        self.docx_cb.setChecked(True)
        self.doc_cb = QCheckBox(".doc")
        self.doc_cb.setChecked(True)
        ft_row = QHBoxLayout()
        ft_label = QLabel("File types:")
        ft_label.setFont(QFont("Segoe UI", 9))
        ft_row.addWidget(ft_label)
        ft_row.addWidget(self.docx_cb)
        ft_row.addWidget(self.doc_cb)
        ft_row.addStretch()
        ft_w = QWidget(); ft_w.setLayout(ft_row)
        opt_card.add_widget(ft_w)

        if not HAS_WIN32:
            warn = QLabel("⚠ pywin32 not installed — Word-to-PDF conversion unavailable.")
            warn.setStyleSheet("color: #EF4444; font-weight: bold;")
            opt_card.add_widget(warn)

        root.addWidget(opt_card)

        # ---- Action buttons ----
        btn_row = QHBoxLayout()
        self.scan_btn = ModernButton("🔍 Scan", variant="primary")
        self.scan_btn.clicked.connect(self.scan_source)
        btn_row.addWidget(self.scan_btn)

        self.copy_btn = ModernButton("� Print to PDF", variant="success")
        self.copy_btn.setEnabled(False)
        self.copy_btn.clicked.connect(self.start_copy)
        btn_row.addWidget(self.copy_btn)

        self.clear_btn = ModernButton("Clear Log", variant="secondary")
        self.clear_btn.clicked.connect(self.clear_log)
        btn_row.addWidget(self.clear_btn)

        btn_row.addStretch()
        root.addLayout(btn_row)

        # ---- Progress ----
        self.progress_bar = QProgressBar()
        self.progress_bar.setMaximum(100)
        self.progress_bar.setValue(0)
        self.progress_bar.setTextVisible(True)
        root.addWidget(self.progress_bar)

        # ---- Log ----
        self.log = QTextEdit()
        self.log.setReadOnly(True)
        self.log.setFont(QFont("Consolas", 9))
        root.addWidget(self.log, 1)

        # ---- Status bar ----
        self.status = QLabel("Ready")
        self.status.setFont(QFont("Segoe UI", 8))
        root.addWidget(self.status)

        # Internal state
        self.file_list: List[Tuple[Path, Path]] = []

    # ---- Theme ----
    def toggle_theme(self):
        self._is_dark = not self._is_dark
        Colors.set_theme(self._is_dark)
        self.theme_btn.setText("☀️ Light" if self._is_dark else "🌙 Dark")
        self.apply_theme()

    def apply_theme(self):
        self.setStyleSheet(f"""
            QWidget {{
                background-color: {Colors.BACKGROUND};
                color: {Colors.TEXT};
                font-family: 'Segoe UI';
            }}
            QLineEdit {{
                background-color: {Colors.INPUT_BG};
                color: {Colors.TEXT};
                border: 1px solid {Colors.BORDER};
                border-radius: 4px;
                padding: 4px 6px;
                font-size: 11px;
            }}
            QLineEdit:focus {{
                border-color: {Colors.BORDER_FOCUS};
            }}
            QTextEdit {{
                background-color: {Colors.INPUT_BG};
                color: {Colors.TEXT};
                border: 1px solid {Colors.BORDER};
                border-radius: 4px;
                padding: 4px;
                font-size: 10px;
            }}
            QCheckBox {{
                color: {Colors.TEXT};
                font-size: 11px;
                spacing: 6px;
            }}
            QProgressBar {{
                background-color: {Colors.INPUT_BG};
                border: 1px solid {Colors.BORDER};
                border-radius: 4px;
                text-align: center;
                height: 18px;
                font-size: 10px;
                color: {Colors.TEXT};
            }}
            QProgressBar::chunk {{
                background-color: {Colors.PRIMARY};
                border-radius: 3px;
            }}
            QLabel {{
                color: {Colors.TEXT};
            }}
        """)
        # Refresh styled widgets
        for child in self.findChildren(ModernCard):
            child.update_style()
        for child in self.findChildren(ModernButton):
            child.update_style()

    # ---- Folder browsing ----
    def browse_source(self):
        folder = QFileDialog.getExistingDirectory(self, "Select Source (Mother) Folder")
        if folder:
            self.src_input.setText(folder)

    def browse_destination(self):
        folder = QFileDialog.getExistingDirectory(self, "Select Destination Folder")
        if folder:
            self.dst_input.setText(folder)

    # ---- Scan ----
    def scan_source(self):
        src = self.src_input.text().strip()
        if not src:
            QMessageBox.warning(self, "No Source", "Please select a source folder first.")
            return

        src_path = Path(src)
        if not src_path.is_dir():
            QMessageBox.warning(self, "Invalid Path", f"Source folder does not exist:\n{src}")
            return

        self.file_list.clear()
        self.log.clear()
        self.progress_bar.setValue(0)

        # Gather selected extensions
        extensions = set()
        if self.docx_cb.isChecked():
            extensions.add("docx")
        if self.doc_cb.isChecked():
            extensions.add("doc")

        if not extensions:
            QMessageBox.warning(self, "No File Types", "Select at least one Word file type to scan for.")
            return

        # Find all matching Word docs in subfolders
        for f in sorted(src_path.rglob("*")):
            if f.is_file() and f.suffix.lstrip(".").lower() in extensions:
                rel = f.parent.relative_to(src_path)
                self.file_list.append((f, rel))

        if not self.file_list:
            ext_str = ", ".join(f".{e}" for e in sorted(extensions))
            self.log_line(f"⚠ No Word documents found matching: {ext_str}")
            self.status.setText("No documents found.")
            self.copy_btn.setEnabled(False)
            return

        # Group by subfolder for display
        folders = {}
        for pdf, rel in self.file_list:
            key = str(rel) if str(rel) != '.' else '(root)'
            folders.setdefault(key, []).append(pdf.name)

        self.log_line(f"Found {len(self.file_list)} Word doc(s) across {len(folders)} subfolder(s):\n")
        for folder_name, files in sorted(folders.items()):
            self.log_line(f"  📁 {folder_name}  ({len(files)} docs)")
            for f in files:
                self.log_line(f"      • {f}  →  {Path(f).stem}.pdf")
            self.log_line("")

        self.status.setText(f"Scanned: {len(self.file_list)} docs in {len(folders)} folders. Ready to print.")
        self.copy_btn.setEnabled(True)

    # ---- Print to PDF ----
    def start_copy(self):
        if not HAS_WIN32:
            QMessageBox.critical(self, "Missing Dependency",
                                 "pywin32 is required for Word-to-PDF conversion.\n"
                                 "Install it with: pip install pywin32")
            return

        dst = self.dst_input.text().strip()
        if not dst:
            QMessageBox.warning(self, "No Destination", "Please select a destination folder first.")
            return

        if not self.file_list:
            QMessageBox.warning(self, "No Files", "Scan the source folder first.")
            return

        reply = QMessageBox.question(
            self, "Confirm Print to PDF",
            f"Convert {len(self.file_list)} Word doc(s) to PDF\n"
            f"and output into matching subfolders of:\n{dst}",
            QMessageBox.Yes | QMessageBox.No
        )
        if reply != QMessageBox.Yes:
            return

        self.scan_btn.setEnabled(False)
        self.copy_btn.setEnabled(False)
        self.progress_bar.setValue(0)
        self.log_line("\n— Starting Word → PDF conversion —\n")

        self.worker = PrintToPdfWorker(
            destination=dst,
            file_list=self.file_list
        )
        self.worker.progress.connect(self.on_progress)
        self.worker.log_message.connect(self.log_line)
        self.worker.finished_signal.connect(self.on_finished)
        self.worker.start()

    def on_progress(self, current, total):
        pct = int(current / total * 100) if total else 0
        self.progress_bar.setValue(pct)
        self.status.setText(f"Converting… {current}/{total}")

    def on_finished(self, converted):
        self.log_line(f"\n✅ Done — {converted}/{len(self.file_list)} document(s) converted to PDF.")
        self.status.setText(f"Complete: {converted} PDFs created.")
        self.progress_bar.setValue(100)
        self.scan_btn.setEnabled(True)
        self.copy_btn.setEnabled(True)
        self.worker = None

    # ---- Helpers ----
    def log_line(self, text):
        self.log.append(text)

    def clear_log(self):
        self.log.clear()
        self.progress_bar.setValue(0)
        self.status.setText("Ready")


def main():
    app = QApplication(sys.argv)
    window = BulkPrinterWindow()
    window.show()
    sys.exit(app.exec())


if __name__ == '__main__':
    main()
