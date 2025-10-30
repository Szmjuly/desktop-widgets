#!/usr/bin/env python3
"""
Modern License Management System - Admin GUI v2
Beautiful, clean, and professional interface for managing licenses.
"""

import sys
import json
from pathlib import Path
from datetime import datetime, timedelta
from typing import Optional, List, Dict

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))
sys.path.insert(0, str(Path(__file__).parent))

from PySide6.QtCore import Qt, QThread, Signal, Slot, QTimer
from PySide6.QtWidgets import (
    QApplication, QMainWindow, QWidget, QVBoxLayout, QHBoxLayout, QGridLayout,
    QLabel, QLineEdit, QPushButton, QComboBox, QSpinBox, QTextEdit, QTabWidget,
    QTableWidget, QTableWidgetItem, QMessageBox, QGroupBox, QCheckBox,
    QHeaderView, QDialog, QDialogButtonBox, QFormLayout, QFrame, QScrollArea,
    QSplitter, QStackedWidget, QListWidget, QListWidgetItem, QProgressBar
)
from PySide6.QtGui import QFont, QColor, QPalette, QIcon

# Import admin license manager
from admin.admin_license_manager import LicenseManager


# Modern color scheme - Professional and clean
class Colors:
    PRIMARY = "#6366F1"      # Indigo 500
    PRIMARY_HOVER = "#4F46E5"  # Indigo 600
    SUCCESS = "#10B981"      # Emerald 500
    DANGER = "#EF4444"       # Red 500
    WARNING = "#F59E0B"      # Amber 500
    INFO = "#3B82F6"         # Blue 500
    
    # Backgrounds
    BACKGROUND = "#F8FAFC"   # Slate 50 - Very light blue-gray
    CARD = "#FFFFFF"         # Pure white
    INPUT_BG = "#FFFFFF"     # White inputs
    
    # Borders
    BORDER = "#E2E8F0"       # Slate 200
    BORDER_FOCUS = "#6366F1" # Indigo 500
    
    # Text
    TEXT = "#0F172A"         # Slate 900
    TEXT_SECONDARY = "#64748B"  # Slate 500
    TEXT_MUTED = "#94A3B8"   # Slate 400
    
    # Shadows
    SHADOW = "rgba(0, 0, 0, 0.05)"
    SHADOW_MD = "rgba(0, 0, 0, 0.1)"


class LicenseWorker(QThread):
    """Worker thread for Firebase operations."""
    finished = Signal(bool, str, object)  # success, message, data
    progress = Signal(str)  # progress message
    
    def __init__(self, operation, manager, **kwargs):
        super().__init__()
        self.operation = operation
        self.manager = manager
        self.kwargs = kwargs
    
    def run(self):
        try:
            if self.operation == 'create':
                self.progress.emit("Creating license...")
                license_key = self.manager.create_license(**self.kwargs)
                self.finished.emit(True, f"License created successfully!", license_key)
                
            elif self.operation == 'list':
                self.progress.emit("Fetching licenses...")
                licenses = self.manager.get_all_licenses(**self.kwargs)
                self.finished.emit(True, "Licenses retrieved", licenses)
                
            elif self.operation == 'revoke':
                self.progress.emit("Revoking license...")
                self.manager.revoke_license(self.kwargs['license_key'])
                self.finished.emit(True, "License revoked successfully", None)
                
            elif self.operation == 'extend':
                self.progress.emit("Extending license...")
                self.manager.extend_license(self.kwargs['license_key'], self.kwargs['days'])
                self.finished.emit(True, f"License extended by {self.kwargs['days']} days", None)
                
        except Exception as e:
            self.finished.emit(False, f"Error: {str(e)}", None)


class ModernCard(QFrame):
    """Modern card widget with shadow effect."""
    def __init__(self, title=None, parent=None):
        super().__init__(parent)
        self.setStyleSheet(f"""
            QFrame {{
                background-color: {Colors.CARD};
                border: 1px solid {Colors.BORDER};
                border-radius: 10px;
                padding: 15px;
            }}
        """)
        
        layout = QVBoxLayout(self)
        layout.setSpacing(10)
        
        if title:
            title_label = QLabel(title)
            title_label.setStyleSheet(f"""
                font-size: 16px;
                font-weight: bold;
                color: {Colors.TEXT};
                padding-bottom: 10px;
                border-bottom: 2px solid {Colors.BORDER};
            """)
            layout.addWidget(title_label)
        
        self.content_layout = QVBoxLayout()
        layout.addLayout(self.content_layout)
    
    def add_widget(self, widget):
        """Add widget to card content."""
        self.content_layout.addWidget(widget)


class ModernButton(QPushButton):
    """Styled button with different variants."""
    def __init__(self, text, variant="primary", icon=None, parent=None):
        super().__init__(text, parent)
        
        colors = {
            "primary": (Colors.PRIMARY, "#FFFFFF"),
            "success": (Colors.SUCCESS, "#FFFFFF"),
            "danger": (Colors.DANGER, "#FFFFFF"),
            "secondary": ("#E5E7EB", Colors.TEXT),
        }
        
        bg_color, text_color = colors.get(variant, colors["primary"])
        
        self.setStyleSheet(f"""
            QPushButton {{
                background-color: {bg_color};
                color: {text_color};
                border: none;
                border-radius: 6px;
                padding: 10px 20px;
                font-size: 14px;
                font-weight: 600;
            }}
            QPushButton:hover {{
                opacity: 0.9;
                background-color: {bg_color};
            }}
            QPushButton:pressed {{
                opacity: 0.8;
            }}
            QPushButton:disabled {{
                background-color: #D1D5DB;
                color: #9CA3AF;
            }}
        """)
        
        if icon:
            self.setText(f"{icon} {text}")
        
        self.setCursor(Qt.PointingHandCursor)


class CreateLicensePanel(QWidget):
    """Modern panel for creating licenses."""
    license_created = Signal(str)  # Emits license key
    
    def __init__(self, manager, parent=None):
        super().__init__(parent)
        self.manager = manager
        self.init_ui()
    
    def init_ui(self):
        layout = QVBoxLayout(self)
        layout.setSpacing(8)
        layout.setContentsMargins(20, 15, 20, 15)
        
        # Header
        header = QLabel("Create New License")
        header.setStyleSheet(f"""
            font-size: 18px;
            font-weight: bold;
            color: {Colors.TEXT};
            margin-bottom: 0px;
        """)
        layout.addWidget(header)
        
        # Form Card
        form_card = ModernCard()
        form_layout = QGridLayout()
        form_layout.setSpacing(10)
        form_layout.setVerticalSpacing(8)
        
        # Email
        email_label = QLabel("Customer Email")
        email_label.setStyleSheet(f"color: {Colors.TEXT}; font-weight: 600; font-size: 13px;")
        self.email_input = QLineEdit()
        self.email_input.setPlaceholderText("customer@example.com")
        self.email_input.setStyleSheet(self._input_style())
        self.email_input.setMinimumHeight(36)
        form_layout.addWidget(email_label, 0, 0, Qt.AlignVCenter)
        form_layout.addWidget(self.email_input, 0, 1)
        
        # Application
        app_label = QLabel("Application")
        app_label.setStyleSheet(f"color: {Colors.TEXT}; font-weight: 600; font-size: 13px;")
        self.app_combo = QComboBox()
        self.app_combo.addItems(["spec-updater", "coffee-stock-widget", "custom"])
        self.app_combo.setStyleSheet(self._combo_style())
        self.app_combo.setMinimumHeight(36)
        self.app_combo.currentTextChanged.connect(self.on_app_changed)
        form_layout.addWidget(app_label, 1, 0, Qt.AlignVCenter)
        form_layout.addWidget(self.app_combo, 1, 1)
        
        # Custom App ID
        self.custom_app_label = QLabel("Custom App ID")
        self.custom_app_label.setStyleSheet(f"color: {Colors.TEXT}; font-weight: 600; font-size: 13px;")
        self.custom_app_input = QLineEdit()
        self.custom_app_input.setPlaceholderText("my-custom-app")
        self.custom_app_input.setStyleSheet(self._input_style())
        self.custom_app_input.setMinimumHeight(36)
        self.custom_app_label.hide()
        self.custom_app_input.hide()
        form_layout.addWidget(self.custom_app_label, 2, 0, Qt.AlignVCenter)
        form_layout.addWidget(self.custom_app_input, 2, 1)
        
        # Plan
        plan_label = QLabel("Plan")
        plan_label.setStyleSheet(f"color: {Colors.TEXT}; font-weight: 600; font-size: 13px;")
        self.plan_combo = QComboBox()
        self.plan_combo.addItems(["free", "basic", "premium", "business"])
        self.plan_combo.setCurrentText("premium")
        self.plan_combo.setStyleSheet(self._combo_style())
        self.plan_combo.setMinimumHeight(36)
        form_layout.addWidget(plan_label, 3, 0, Qt.AlignVCenter)
        form_layout.addWidget(self.plan_combo, 3, 1)
        
        # Duration
        duration_label = QLabel("Duration (days)")
        duration_label.setStyleSheet(f"color: {Colors.TEXT}; font-weight: 600; font-size: 13px;")
        duration_layout = QHBoxLayout()
        self.duration_spin = QSpinBox()
        self.duration_spin.setRange(1, 3650)
        self.duration_spin.setValue(365)
        self.duration_spin.setStyleSheet(self._input_style())
        self.duration_spin.setMinimumHeight(36)
        duration_layout.addWidget(self.duration_spin)
        
        # Quick duration buttons
        for days, label in [(30, "1 Month"), (365, "1 Year"), (730, "2 Years")]:
            btn = QPushButton(label)
            btn.setStyleSheet(f"""
                QPushButton {{
                    background-color: {Colors.BACKGROUND};
                    border: 2px solid {Colors.BORDER};
                    border-radius: 6px;
                    padding: 8px 14px;
                    font-size: 12px;
                    font-weight: 500;
                    color: {Colors.TEXT};
                    min-height: 32px;
                }}
                QPushButton:hover {{
                    background-color: {Colors.BORDER};
                    border-color: {Colors.TEXT_MUTED};
                }}
            """)
            btn.setCursor(Qt.PointingHandCursor)
            btn.clicked.connect(lambda checked, d=days: self.duration_spin.setValue(d))
            duration_layout.addWidget(btn)
        
        form_layout.addWidget(duration_label, 4, 0, Qt.AlignVCenter)
        form_layout.addLayout(duration_layout, 4, 1)
        
        # Max Devices
        devices_label = QLabel("Max Devices")
        devices_label.setStyleSheet(f"color: {Colors.TEXT}; font-weight: 600; font-size: 13px;")
        devices_layout = QHBoxLayout()
        self.devices_spin = QSpinBox()
        self.devices_spin.setRange(-1, 100)
        self.devices_spin.setValue(3)
        self.devices_spin.setStyleSheet(self._input_style())
        self.devices_spin.setMinimumHeight(36)
        self.unlimited_devices_check = QCheckBox("Unlimited")
        self.unlimited_devices_check.stateChanged.connect(self.on_unlimited_devices_changed)
        devices_layout.addWidget(self.devices_spin)
        devices_layout.addWidget(self.unlimited_devices_check)
        form_layout.addWidget(devices_label, 5, 0, Qt.AlignVCenter)
        form_layout.addLayout(devices_layout, 5, 1)
        
        # Documents Limit
        docs_label = QLabel("Documents Limit")
        docs_label.setStyleSheet(f"color: {Colors.TEXT}; font-weight: 600; font-size: 13px;")
        docs_layout = QHBoxLayout()
        self.docs_spin = QSpinBox()
        self.docs_spin.setRange(-1, 1000000)
        self.docs_spin.setValue(-1)
        self.docs_spin.setStyleSheet(self._input_style())
        self.docs_spin.setMinimumHeight(36)
        self.unlimited_docs_check = QCheckBox("Unlimited")
        self.unlimited_docs_check.setChecked(True)
        self.unlimited_docs_check.stateChanged.connect(self.on_unlimited_docs_changed)
        docs_layout.addWidget(self.docs_spin)
        docs_layout.addWidget(self.unlimited_docs_check)
        form_layout.addWidget(docs_label, 6, 0, Qt.AlignVCenter)
        form_layout.addLayout(docs_layout, 6, 1)
        
        form_card.content_layout.addLayout(form_layout)
        layout.addWidget(form_card)
        
        # Create Button
        button_layout = QHBoxLayout()
        button_layout.addStretch()
        self.create_btn = ModernButton("🔑 Create License", "success")
        self.create_btn.clicked.connect(self.create_license)
        button_layout.addWidget(self.create_btn)
        layout.addLayout(button_layout)
        
        # Result Card
        self.result_card = ModernCard("Result")
        self.result_text = QTextEdit()
        self.result_text.setReadOnly(True)
        self.result_text.setMaximumHeight(120)
        self.result_text.setStyleSheet(f"""
            QTextEdit {{
                background-color: {Colors.BACKGROUND};
                border: 1px solid {Colors.BORDER};
                border-radius: 4px;
                padding: 10px;
                font-family: monospace;
                font-size: 13px;
            }}
        """)
        self.result_card.add_widget(self.result_text)
        self.result_card.hide()
        layout.addWidget(self.result_card)
    
    def _input_style(self):
        return f"""
            QLineEdit, QSpinBox {{
                border: 2px solid {Colors.BORDER};
                border-radius: 6px;
                padding: 8px 12px;
                font-size: 14px;
                background-color: {Colors.INPUT_BG};
                color: {Colors.TEXT};
                selection-background-color: {Colors.PRIMARY};
            }}
            QLineEdit:hover, QSpinBox:hover {{
                border-color: {Colors.TEXT_MUTED};
            }}
            QLineEdit:focus, QSpinBox:focus {{
                border: 2px solid {Colors.PRIMARY};
                outline: none;
            }}
            QSpinBox::up-button, QSpinBox::down-button {{
                background-color: {Colors.BACKGROUND};
                border: 1px solid {Colors.BORDER};
                border-radius: 3px;
                width: 18px;
            }}
            QSpinBox::up-button:hover, QSpinBox::down-button:hover {{
                background-color: {Colors.BORDER};
            }}
        """
    
    def _combo_style(self):
        return f"""
            QComboBox {{
                border: 2px solid {Colors.BORDER};
                border-radius: 6px;
                padding: 8px 12px;
                font-size: 14px;
                background-color: {Colors.INPUT_BG};
                color: {Colors.TEXT};
            }}
            QComboBox:hover {{
                border-color: {Colors.TEXT_MUTED};
            }}
            QComboBox:focus {{
                border: 2px solid {Colors.PRIMARY};
            }}
            QComboBox::drop-down {{
                border: none;
                width: 25px;
            }}
            QComboBox::down-arrow {{
                image: none;
                border-left: 4px solid transparent;
                border-right: 4px solid transparent;
                border-top: 5px solid {Colors.TEXT_SECONDARY};
                margin-right: 8px;
            }}
            QComboBox QAbstractItemView {{
                border: 2px solid {Colors.BORDER};
                border-radius: 6px;
                background-color: {Colors.CARD};
                selection-background-color: {Colors.PRIMARY};
                selection-color: white;
                padding: 4px;
            }}
        """
    
    def on_app_changed(self, text):
        is_custom = text == "custom"
        self.custom_app_label.setVisible(is_custom)
        self.custom_app_input.setVisible(is_custom)
    
    def on_unlimited_devices_changed(self, state):
        if state == Qt.Checked:
            self.devices_spin.setValue(-1)
            self.devices_spin.setEnabled(False)
        else:
            self.devices_spin.setValue(3)
            self.devices_spin.setEnabled(True)
    
    def on_unlimited_docs_changed(self, state):
        if state == Qt.Checked:
            self.docs_spin.setValue(-1)
            self.docs_spin.setEnabled(False)
        else:
            self.docs_spin.setValue(1000)
            self.docs_spin.setEnabled(True)
    
    def create_license(self):
        # Validate inputs
        email = self.email_input.text().strip()
        if not email or '@' not in email:
            QMessageBox.warning(self, "Invalid Input", "Please enter a valid email address")
            return
        
        app_id = self.app_combo.currentText()
        if app_id == "custom":
            app_id = self.custom_app_input.text().strip()
            if not app_id:
                QMessageBox.warning(self, "Invalid Input", "Please enter a custom app ID")
                return
        
        # Disable button
        self.create_btn.setEnabled(False)
        self.create_btn.setText("⏳ Creating...")
        
        # Create worker
        self.worker = LicenseWorker(
            'create',
            self.manager,
            email=email,
            app_id=app_id,
            plan=self.plan_combo.currentText(),
            duration_days=self.duration_spin.value(),
            max_devices=self.devices_spin.value(),
            documents_limit=self.docs_spin.value()
        )
        self.worker.finished.connect(self.on_create_finished)
        self.worker.start()
    
    def on_create_finished(self, success, message, license_key):
        self.create_btn.setEnabled(True)
        self.create_btn.setText("🔑 Create License")
        
        if success:
            self.result_text.setHtml(f"""
                <div style='color: {Colors.SUCCESS}; font-weight: bold;'>✓ Success!</div>
                <div style='margin-top: 10px;'>
                    <strong>License Key:</strong><br>
                    <span style='font-size: 16px; color: {Colors.PRIMARY};'>{license_key}</span>
                </div>
                <div style='margin-top: 10px; color: {Colors.TEXT_SECONDARY}; font-size: 12px;'>
                    License created for {self.email_input.text()}
                </div>
            """)
            self.result_card.show()
            self.license_created.emit(license_key)
        else:
            self.result_text.setHtml(f"""
                <div style='color: {Colors.DANGER}; font-weight: bold;'>✗ Error</div>
                <div style='margin-top: 10px;'>{message}</div>
            """)
            self.result_card.show()


class ManageLicensesPanel(QWidget):
    """Panel for viewing and managing existing licenses."""
    
    def __init__(self, manager, parent=None):
        super().__init__(parent)
        self.manager = manager
        self.init_ui()
    
    def init_ui(self):
        layout = QVBoxLayout(self)
        layout.setSpacing(10)
        layout.setContentsMargins(20, 15, 20, 15)
        
        # Header with filters
        header_layout = QHBoxLayout()
        
        title = QLabel("Manage Licenses")
        title.setStyleSheet(f"""
            font-size: 18px;
            font-weight: bold;
            color: {Colors.TEXT};
        """)
        header_layout.addWidget(title)
        header_layout.addStretch()
        
        # Filter dropdown
        filter_label = QLabel("Filter:")
        filter_label.setStyleSheet(f"color: {Colors.TEXT}; font-weight: 600;")
        header_layout.addWidget(filter_label)
        
        self.filter_combo = QComboBox()
        self.filter_combo.addItems(["All", "Active", "Expired", "Revoked"])
        self.filter_combo.setStyleSheet(f"""
            QComboBox {{
                border: 2px solid {Colors.BORDER};
                border-radius: 6px;
                padding: 6px 12px;
                font-size: 13px;
                background-color: {Colors.INPUT_BG};
                min-width: 120px;
            }}
        """)
        self.filter_combo.currentTextChanged.connect(self.load_licenses)
        header_layout.addWidget(self.filter_combo)
        
        # Refresh button
        refresh_btn = ModernButton("🔄 Refresh", "secondary")
        refresh_btn.clicked.connect(self.load_licenses)
        header_layout.addWidget(refresh_btn)
        
        layout.addLayout(header_layout)
        
        # Table
        self.table = QTableWidget()
        self.table.setColumnCount(8)
        self.table.setHorizontalHeaderLabels([
            "License Key", "Email", "App", "Plan", "Status", 
            "Expires", "Devices", "Actions"
        ])
        
        # Style the table
        self.table.setStyleSheet(f"""
            QTableWidget {{
                background-color: {Colors.CARD};
                border: 1px solid {Colors.BORDER};
                border-radius: 8px;
                gridline-color: {Colors.BORDER};
            }}
            QTableWidget::item {{
                padding: 12px 8px;
                border-bottom: 1px solid {Colors.BORDER};
                min-height: 50px;
            }}
            QTableWidget::item:selected {{
                background-color: {Colors.PRIMARY};
                color: white;
            }}
            QHeaderView::section {{
                background-color: {Colors.BACKGROUND};
                color: {Colors.TEXT};
                font-weight: 600;
                padding: 12px 10px;
                border: none;
                border-bottom: 2px solid {Colors.BORDER};
                border-right: 1px solid {Colors.BORDER};
            }}
        """)
        
        # Table settings
        self.table.horizontalHeader().setStretchLastSection(False)
        self.table.horizontalHeader().setSectionResizeMode(0, QHeaderView.Stretch)  # License Key
        self.table.horizontalHeader().setSectionResizeMode(1, QHeaderView.ResizeToContents)  # Email
        self.table.horizontalHeader().setSectionResizeMode(2, QHeaderView.ResizeToContents)  # App
        self.table.horizontalHeader().setSectionResizeMode(3, QHeaderView.ResizeToContents)  # Plan
        self.table.horizontalHeader().setSectionResizeMode(4, QHeaderView.ResizeToContents)  # Status
        self.table.horizontalHeader().setSectionResizeMode(5, QHeaderView.ResizeToContents)  # Expires
        self.table.horizontalHeader().setSectionResizeMode(6, QHeaderView.ResizeToContents)  # Devices
        self.table.horizontalHeader().setSectionResizeMode(7, QHeaderView.Fixed)  # Actions
        self.table.setColumnWidth(7, 280)
        
        self.table.setSelectionBehavior(QTableWidget.SelectRows)
        self.table.setSelectionMode(QTableWidget.SingleSelection)
        self.table.verticalHeader().setVisible(False)
        self.table.verticalHeader().setDefaultSectionSize(60)  # Set row height to fit buttons
        self.table.setAlternatingRowColors(True)
        
        layout.addWidget(self.table)
        
        # Load licenses on init
        self.load_licenses()
    
    def load_licenses(self):
        """Load and display licenses from Firebase."""
        try:
            # Show loading
            self.table.setRowCount(0)
            
            # Get filter
            filter_status = self.filter_combo.currentText().lower()
            
            # Load licenses
            licenses = self.manager.get_all_licenses()
            
            # Filter licenses
            from datetime import datetime
            now = datetime.utcnow()
            
            filtered_licenses = []
            for license in licenses:
                status = license.get('status', 'active')
                expires_str = license.get('expires_at', '')
                
                # Determine actual status
                if status == 'revoked':
                    actual_status = 'revoked'
                elif expires_str:
                    try:
                        expires = datetime.fromisoformat(expires_str)
                        actual_status = 'expired' if now > expires else 'active'
                    except:
                        actual_status = 'active'
                else:
                    actual_status = 'active'
                
                # Apply filter
                if filter_status == 'all' or filter_status == actual_status:
                    license['actual_status'] = actual_status
                    filtered_licenses.append(license)
            
            # Populate table
            self.table.setRowCount(len(filtered_licenses))
            
            for row, license in enumerate(filtered_licenses):
                # License Key
                key_item = QTableWidgetItem(license.get('license_key', '')[:20] + "...")
                key_item.setToolTip(license.get('license_key', ''))
                key_item.setTextAlignment(Qt.AlignLeft | Qt.AlignVCenter)
                key_item.setForeground(QColor(Colors.TEXT))
                self.table.setItem(row, 0, key_item)
                
                # Email
                email_item = QTableWidgetItem(license.get('email', ''))
                email_item.setTextAlignment(Qt.AlignLeft | Qt.AlignVCenter)
                email_item.setForeground(QColor(Colors.TEXT))
                self.table.setItem(row, 1, email_item)
                
                # App
                app_item = QTableWidgetItem(license.get('app_id', ''))
                app_item.setTextAlignment(Qt.AlignLeft | Qt.AlignVCenter)
                app_item.setForeground(QColor(Colors.TEXT))
                self.table.setItem(row, 2, app_item)
                
                # Plan
                plan_item = QTableWidgetItem(license.get('plan', '').upper())
                plan_item.setTextAlignment(Qt.AlignCenter | Qt.AlignVCenter)
                plan_item.setForeground(QColor(Colors.TEXT))
                self.table.setItem(row, 3, plan_item)
                
                # Status
                status = license['actual_status']
                status_item = QTableWidgetItem(status.upper())
                status_item.setTextAlignment(Qt.AlignCenter | Qt.AlignVCenter)
                if status == 'active':
                    status_item.setForeground(QColor(Colors.SUCCESS))
                elif status == 'expired':
                    status_item.setForeground(QColor(Colors.WARNING))
                else:
                    status_item.setForeground(QColor(Colors.DANGER))
                self.table.setItem(row, 4, status_item)
                
                # Expires
                expires_str = license.get('expires_at', '')
                if expires_str:
                    try:
                        expires = datetime.fromisoformat(expires_str)
                        expires_display = expires.strftime('%Y-%m-%d')
                    except:
                        expires_display = expires_str
                else:
                    expires_display = 'N/A'
                expires_item = QTableWidgetItem(expires_display)
                expires_item.setTextAlignment(Qt.AlignCenter | Qt.AlignVCenter)
                expires_item.setForeground(QColor(Colors.TEXT))
                self.table.setItem(row, 5, expires_item)
                
                # Devices
                max_devices = license.get('max_devices', 0)
                devices_str = 'Unlimited' if max_devices < 0 else str(max_devices)
                devices_item = QTableWidgetItem(devices_str)
                devices_item.setTextAlignment(Qt.AlignCenter | Qt.AlignVCenter)
                devices_item.setForeground(QColor(Colors.TEXT))
                self.table.setItem(row, 6, devices_item)
                
                # Actions - buttons
                actions_widget = QWidget()
                actions_layout = QHBoxLayout(actions_widget)
                actions_layout.setContentsMargins(6, 6, 6, 6)
                actions_layout.setSpacing(6)
                
                # Revoke button (only if active)
                if status != 'revoked':
                    revoke_btn = QPushButton("🚫 Revoke")
                    revoke_btn.setStyleSheet(f"""
                        QPushButton {{
                            background-color: {Colors.DANGER};
                            color: white;
                            border: none;
                            border-radius: 5px;
                            padding: 6px 12px;
                            font-size: 12px;
                            min-height: 28px;
                            font-weight: 500;
                        }}
                        QPushButton:hover {{
                            background-color: #DC2626;
                        }}
                    """)
                    revoke_btn.setCursor(Qt.PointingHandCursor)
                    revoke_btn.clicked.connect(lambda checked, key=license.get('license_key'): self.revoke_license(key))
                    actions_layout.addWidget(revoke_btn)
                
                # Extend button (only if not revoked)
                if status != 'revoked':
                    extend_btn = QPushButton("➕ Extend")
                    extend_btn.setStyleSheet(f"""
                        QPushButton {{
                            background-color: {Colors.INFO};
                            color: white;
                            border: none;
                            border-radius: 5px;
                            padding: 6px 12px;
                            font-size: 12px;
                            min-height: 28px;
                            font-weight: 500;
                        }}
                        QPushButton:hover {{
                            background-color: #2563EB;
                        }}
                    """)
                    extend_btn.setCursor(Qt.PointingHandCursor)
                    extend_btn.clicked.connect(lambda checked, key=license.get('license_key'): self.extend_license(key))
                    actions_layout.addWidget(extend_btn)
                
                # Delete button (always available)
                delete_btn = QPushButton("🗑️ Delete")
                delete_btn.setStyleSheet(f"""
                    QPushButton {{
                        background-color: #64748B;
                        color: white;
                        border: none;
                        border-radius: 5px;
                        padding: 6px 12px;
                        font-size: 12px;
                        min-height: 28px;
                        font-weight: 500;
                    }}
                    QPushButton:hover {{
                        background-color: #475569;
                    }}
                """)
                delete_btn.setCursor(Qt.PointingHandCursor)
                delete_btn.clicked.connect(lambda checked, key=license.get('license_key'): self.delete_license(key))
                actions_layout.addWidget(delete_btn)
                
                self.table.setCellWidget(row, 7, actions_widget)
            
        except Exception as e:
            QMessageBox.warning(self, "Error", f"Failed to load licenses:\n{str(e)}")
    
    def revoke_license(self, license_key):
        """Revoke a license."""
        reply = QMessageBox.question(
            self,
            "Confirm Revoke",
            f"Are you sure you want to revoke this license?\n\n{license_key}",
            QMessageBox.Yes | QMessageBox.No
        )
        
        if reply == QMessageBox.Yes:
            try:
                self.manager.revoke_license(license_key)
                QMessageBox.information(self, "Success", "License revoked successfully!")
                self.load_licenses()
            except Exception as e:
                QMessageBox.warning(self, "Error", f"Failed to revoke license:\n{str(e)}")
    
    def extend_license(self, license_key):
        """Extend a license by 365 days."""
        reply = QMessageBox.question(
            self,
            "Confirm Extend",
            f"Extend this license by 365 days?\n\n{license_key}",
            QMessageBox.Yes | QMessageBox.No
        )
        
        if reply == QMessageBox.Yes:
            try:
                self.manager.extend_license(license_key, 365)
                QMessageBox.information(self, "Success", "License extended by 365 days!")
                self.load_licenses()
            except Exception as e:
                QMessageBox.warning(self, "Error", f"Failed to extend license:\n{str(e)}")
    
    def delete_license(self, license_key):
        """Permanently delete a license from Firebase."""
        reply = QMessageBox.warning(
            self,
            "Confirm Delete",
            f"⚠️ PERMANENTLY DELETE this license?\n\n{license_key}\n\nThis action CANNOT be undone!",
            QMessageBox.Yes | QMessageBox.No,
            QMessageBox.No  # Default to No for safety
        )
        
        if reply == QMessageBox.Yes:
            try:
                # Delete from Firebase
                self.manager.licenses_ref.child(license_key).delete()
                QMessageBox.information(self, "Success", "License permanently deleted!")
                self.load_licenses()
            except Exception as e:
                QMessageBox.warning(self, "Error", f"Failed to delete license:\n{str(e)}")


class LicenseManagerWindow(QMainWindow):
    """Modern main window for license management."""
    
    def __init__(self):
        super().__init__()
        self.manager = None
        self.init_ui()
        self.init_firebase()
    
    def init_ui(self):
        self.setWindowTitle("License Management System")
        self.setMinimumSize(1000, 650)
        self.resize(1000, 650)
        
        # Set modern style
        self.setStyleSheet(f"""
            QMainWindow {{
                background-color: {Colors.BACKGROUND};
            }}
            QLabel {{
                color: {Colors.TEXT};
            }}
        """)
        
        # Central widget
        central = QWidget()
        self.setCentralWidget(central)
        layout = QVBoxLayout(central)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(0)
        
        # Header
        header = self.create_header()
        layout.addWidget(header)
        
        # Content
        self.content_stack = QStackedWidget()
        layout.addWidget(self.content_stack)
        
        # Status bar
        self.statusBar().showMessage("Initializing...")
        self.statusBar().setStyleSheet(f"""
            QStatusBar {{
                background-color: {Colors.CARD};
                color: {Colors.TEXT_SECONDARY};
                border-top: 1px solid {Colors.BORDER};
                padding: 4px 10px;
                font-size: 12px;
                max-height: 28px;
            }}
        """)
    
    def create_header(self):
        """Create modern header with navigation."""
        header = QFrame()
        header.setStyleSheet(f"""
            QFrame {{
                background-color: {Colors.CARD};
                border-bottom: 1px solid {Colors.BORDER};
                padding: 12px 20px;
            }}
        """)
        
        layout = QHBoxLayout(header)
        layout.setSpacing(15)
        
        # Logo and title
        title_layout = QVBoxLayout()
        title_layout.setSpacing(2)
        title = QLabel("🔑 License Management")
        title.setStyleSheet(f"""
            font-size: 18px;
            font-weight: bold;
            color: {Colors.TEXT};
        """)
        subtitle = QLabel("Manage licenses for all applications")
        subtitle.setStyleSheet(f"""
            font-size: 12px;
            color: {Colors.TEXT_SECONDARY};
        """)
        title_layout.addWidget(title)
        title_layout.addWidget(subtitle)
        layout.addLayout(title_layout)
        
        layout.addStretch()
        
        # Navigation buttons
        self.create_btn = ModernButton("➕ Create License", "primary")
        self.create_btn.clicked.connect(lambda: self.content_stack.setCurrentIndex(0))
        layout.addWidget(self.create_btn)
        
        self.manage_btn = ModernButton("📋 Manage Licenses", "secondary")
        self.manage_btn.clicked.connect(lambda: self.content_stack.setCurrentIndex(1))
        layout.addWidget(self.manage_btn)
        
        return header
    
    def init_firebase(self):
        """Initialize Firebase connection."""
        try:
            # Check multiple locations for firebase_config.json
            config_locations = [
                Path(__file__).parent.parent / 'firebase_config.json',
                Path(__file__).parent / 'firebase_config.json',
                Path(__file__).parent.parent.parent / 'firebase_config.json',
            ]
            
            config_path = None
            for path in config_locations:
                if path.exists():
                    config_path = path
                    break
            
            if not config_path:
                QMessageBox.critical(
                    self,
                    "Configuration Error",
                    "Firebase config not found!\n\nPlease create firebase_config.json"
                )
                return
            
            # Check for admin key
            admin_key_locations = [
                Path(__file__).parent.parent / 'firebase-admin-key.json',
                Path(__file__).parent / 'firebase-admin-key.json',
                Path(__file__).parent.parent.parent / 'firebase-admin-key.json',
            ]
            
            admin_key_path = None
            for path in admin_key_locations:
                if path.exists():
                    admin_key_path = path
                    break
            
            if not admin_key_path:
                QMessageBox.critical(
                    self,
                    "Configuration Error",
                    "firebase-admin-key.json not found!"
                )
                return
            
            with open(config_path) as f:
                config = json.load(f)
            
            database_url = config.get('databaseURL')
            if not database_url:
                QMessageBox.critical(self, "Configuration Error", "databaseURL not found")
                return
            
            # Initialize manager
            self.manager = LicenseManager(str(admin_key_path), database_url)
            
            # Create panels now that manager is ready
            self.create_panels()
            
            self.statusBar().showMessage("✓ Connected to Firebase", 3000)
            
        except Exception as e:
            QMessageBox.critical(self, "Firebase Error", f"Failed to initialize:\n{str(e)}")
            self.statusBar().showMessage("✗ Firebase connection failed")
    
    def create_panels(self):
        """Create content panels."""
        # Create License Panel (no scroll area - fits in window)
        create_panel = CreateLicensePanel(self.manager)
        create_panel.setStyleSheet(f"background-color: {Colors.BACKGROUND};")
        self.content_stack.addWidget(create_panel)
        
        # Manage Licenses Panel
        self.manage_panel = ManageLicensesPanel(self.manager)
        self.manage_panel.setStyleSheet(f"background-color: {Colors.BACKGROUND};")
        self.content_stack.addWidget(self.manage_panel)


def main():
    app = QApplication(sys.argv)
    
    # Set application-wide font
    font = QFont("Segoe UI", 10)
    app.setFont(font)
    
    window = LicenseManagerWindow()
    window.show()
    
    sys.exit(app.exec())


if __name__ == '__main__':
    main()
