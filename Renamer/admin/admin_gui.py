#!/usr/bin/env python3
"""
GUI Admin Tool for License Management
Provides a user-friendly interface for creating and managing licenses across all applications.
"""

import sys
import json
from pathlib import Path
from datetime import datetime, timedelta
from typing import Optional

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))
sys.path.insert(0, str(Path(__file__).parent))

from PySide6.QtCore import Qt, QThread, Signal, Slot
from PySide6.QtWidgets import (
    QApplication, QMainWindow, QWidget, QVBoxLayout, QHBoxLayout, QGridLayout,
    QLabel, QLineEdit, QPushButton, QComboBox, QSpinBox, QTextEdit, QTabWidget,
    QTableWidget, QTableWidgetItem, QMessageBox, QGroupBox, QCheckBox,
    QHeaderView, QDialog, QDialogButtonBox, QFormLayout
)
from PySide6.QtGui import QFont, QColor

# Import admin license manager
from admin.admin_license_manager import LicenseManager


class LicenseWorker(QThread):
    """Worker thread for Firebase operations."""
    finished = Signal(bool, str)
    
    def __init__(self, operation, manager, **kwargs):
        super().__init__()
        self.operation = operation
        self.manager = manager
        self.kwargs = kwargs
    
    def run(self):
        try:
            if self.operation == 'create':
                license_key = self.manager.create_license(**self.kwargs)
                self.finished.emit(True, f"License created successfully!\n\nLicense Key: {license_key}")
            elif self.operation == 'list':
                # List operation returns None, data is printed
                self.manager.list_licenses(**self.kwargs)
                self.finished.emit(True, "Licenses retrieved successfully")
            elif self.operation == 'revoke':
                self.manager.revoke_license(self.kwargs['license_key'])
                self.finished.emit(True, "License revoked successfully")
            elif self.operation == 'extend':
                self.manager.extend_license(self.kwargs['license_key'], self.kwargs['days'])
                self.finished.emit(True, f"License extended by {self.kwargs['days']} days")
        except Exception as e:
            self.finished.emit(False, f"Error: {str(e)}")


class CreateLicenseTab(QWidget):
    """Tab for creating new licenses."""
    
    def __init__(self, manager):
        super().__init__()
        self.manager = manager
        self.init_ui()
    
    def init_ui(self):
        layout = QVBoxLayout()
        
        # Form
        form_group = QGroupBox("License Details")
        form_layout = QGridLayout()
        
        # Email
        form_layout.addWidget(QLabel("Customer Email:"), 0, 0)
        self.email_input = QLineEdit()
        self.email_input.setPlaceholderText("customer@example.com")
        form_layout.addWidget(self.email_input, 0, 1)
        
        # App ID
        form_layout.addWidget(QLabel("Application:"), 1, 0)
        self.app_combo = QComboBox()
        self.app_combo.addItems([
            "spec-updater",
            "coffee-stock-widget",
            "custom"
        ])
        self.app_combo.currentTextChanged.connect(self.on_app_changed)
        form_layout.addWidget(self.app_combo, 1, 1)
        
        # Custom App ID (hidden by default)
        self.custom_app_label = QLabel("Custom App ID:")
        self.custom_app_input = QLineEdit()
        self.custom_app_input.setPlaceholderText("my-new-app")
        self.custom_app_label.hide()
        self.custom_app_input.hide()
        form_layout.addWidget(self.custom_app_label, 2, 0)
        form_layout.addWidget(self.custom_app_input, 2, 1)
        
        # Plan
        form_layout.addWidget(QLabel("Plan:"), 3, 0)
        self.plan_combo = QComboBox()
        self.plan_combo.addItems(["free", "basic", "premium", "business"])
        self.plan_combo.setCurrentText("premium")
        form_layout.addWidget(self.plan_combo, 3, 1)
        
        # Duration
        form_layout.addWidget(QLabel("Duration (days):"), 4, 0)
        self.duration_spin = QSpinBox()
        self.duration_spin.setRange(1, 3650)
        self.duration_spin.setValue(365)
        form_layout.addWidget(self.duration_spin, 4, 1)
        
        # Max Devices
        form_layout.addWidget(QLabel("Max Devices:"), 5, 0)
        devices_layout = QHBoxLayout()
        self.devices_spin = QSpinBox()
        self.devices_spin.setRange(-1, 100)
        self.devices_spin.setValue(3)
        self.unlimited_devices_check = QCheckBox("Unlimited")
        self.unlimited_devices_check.stateChanged.connect(self.on_unlimited_devices)
        devices_layout.addWidget(self.devices_spin)
        devices_layout.addWidget(self.unlimited_devices_check)
        form_layout.addLayout(devices_layout, 5, 1)
        
        # Documents Limit
        form_layout.addWidget(QLabel("Documents Limit:"), 6, 0)
        docs_layout = QHBoxLayout()
        self.docs_spin = QSpinBox()
        self.docs_spin.setRange(-1, 1000000)
        self.docs_spin.setValue(-1)
        self.unlimited_docs_check = QCheckBox("Unlimited")
        self.unlimited_docs_check.setChecked(True)
        self.unlimited_docs_check.stateChanged.connect(self.on_unlimited_docs)
        docs_layout.addWidget(self.docs_spin)
        docs_layout.addWidget(self.unlimited_docs_check)
        form_layout.addLayout(docs_layout, 6, 1)
        
        form_group.setLayout(form_layout)
        layout.addWidget(form_group)
        
        # Create Button
        self.create_btn = QPushButton("Create License")
        self.create_btn.setStyleSheet("QPushButton { background-color: #4CAF50; color: white; font-weight: bold; padding: 10px; }")
        self.create_btn.clicked.connect(self.create_license)
        layout.addWidget(self.create_btn)
        
        # Result
        self.result_text = QTextEdit()
        self.result_text.setReadOnly(True)
        self.result_text.setMaximumHeight(150)
        layout.addWidget(QLabel("Result:"))
        layout.addWidget(self.result_text)
        
        layout.addStretch()
        self.setLayout(layout)
    
    def on_app_changed(self, text):
        """Handle app selection change."""
        if text == "custom":
            self.custom_app_label.show()
            self.custom_app_input.show()
        else:
            self.custom_app_label.hide()
            self.custom_app_input.hide()
    
    def on_unlimited_devices(self, state):
        """Handle unlimited devices checkbox."""
        if state == Qt.Checked:
            self.devices_spin.setValue(-1)
            self.devices_spin.setEnabled(False)
        else:
            self.devices_spin.setValue(3)
            self.devices_spin.setEnabled(True)
    
    def on_unlimited_docs(self, state):
        """Handle unlimited documents checkbox."""
        if state == Qt.Checked:
            self.docs_spin.setValue(-1)
            self.docs_spin.setEnabled(False)
        else:
            self.docs_spin.setValue(1000)
            self.docs_spin.setEnabled(True)
    
    def create_license(self):
        """Create a new license."""
        # Validate inputs
        email = self.email_input.text().strip()
        if not email:
            QMessageBox.warning(self, "Validation Error", "Please enter a customer email")
            return
        
        # Get app_id
        if self.app_combo.currentText() == "custom":
            app_id = self.custom_app_input.text().strip()
            if not app_id:
                QMessageBox.warning(self, "Validation Error", "Please enter a custom app ID")
                return
        else:
            app_id = self.app_combo.currentText()
        
        # Get other values
        plan = self.plan_combo.currentText()
        duration = self.duration_spin.value()
        max_devices = self.devices_spin.value()
        docs_limit = self.docs_spin.value()
        
        # Disable button
        self.create_btn.setEnabled(False)
        self.result_text.clear()
        self.result_text.append("Creating license...")
        
        # Create worker thread
        self.worker = LicenseWorker(
            'create',
            self.manager,
            email=email,
            app_id=app_id,
            plan=plan,
            duration_days=duration,
            max_devices=max_devices,
            documents_limit=docs_limit
        )
        self.worker.finished.connect(self.on_create_finished)
        self.worker.start()
    
    @Slot(bool, str)
    def on_create_finished(self, success, message):
        """Handle creation completion."""
        self.create_btn.setEnabled(True)
        self.result_text.clear()
        
        if success:
            self.result_text.setTextColor(QColor("green"))
            self.result_text.append("✅ " + message)
            # Clear form
            self.email_input.clear()
        else:
            self.result_text.setTextColor(QColor("red"))
            self.result_text.append("❌ " + message)


class ListLicensesTab(QWidget):
    """Tab for listing and managing licenses."""
    
    def __init__(self, manager):
        super().__init__()
        self.manager = manager
        self.init_ui()
    
    def init_ui(self):
        layout = QVBoxLayout()
        
        # Filters
        filter_group = QGroupBox("Filters")
        filter_layout = QHBoxLayout()
        
        filter_layout.addWidget(QLabel("Email:"))
        self.email_filter = QLineEdit()
        self.email_filter.setPlaceholderText("Filter by email")
        filter_layout.addWidget(self.email_filter)
        
        filter_layout.addWidget(QLabel("App:"))
        self.app_filter = QComboBox()
        self.app_filter.addItems(["All", "spec-updater", "coffee-stock-widget"])
        filter_layout.addWidget(self.app_filter)
        
        filter_layout.addWidget(QLabel("Status:"))
        self.status_filter = QComboBox()
        self.status_filter.addItems(["All", "active", "expired", "suspended"])
        filter_layout.addWidget(self.status_filter)
        
        self.refresh_btn = QPushButton("Refresh")
        self.refresh_btn.clicked.connect(self.refresh_licenses)
        filter_layout.addWidget(self.refresh_btn)
        
        filter_group.setLayout(filter_layout)
        layout.addWidget(filter_group)
        
        # Table
        self.table = QTableWidget()
        self.table.setColumnCount(7)
        self.table.setHorizontalHeaderLabels([
            "License Key", "App ID", "Email", "Plan", "Status", "Expires", "Actions"
        ])
        self.table.horizontalHeader().setSectionResizeMode(QHeaderView.Stretch)
        layout.addWidget(self.table)
        
        self.setLayout(layout)
        
        # Load licenses on init
        self.refresh_licenses()
    
    def refresh_licenses(self):
        """Refresh the license list."""
        # Get filter values
        email = self.email_filter.text().strip() or None
        app_id = self.app_filter.currentText()
        if app_id == "All":
            app_id = None
        status = self.status_filter.currentText()
        if status == "All":
            status = None
        
        # Get licenses
        try:
            licenses = self.manager.licenses_ref.get()
            if not licenses:
                self.table.setRowCount(0)
                return
            
            # Filter
            filtered = []
            for key, data in licenses.items():
                if email and data.get('email') != email:
                    continue
                if app_id and data.get('app_id') != app_id:
                    continue
                if status and data.get('status') != status:
                    continue
                
                # Check expiration
                expires_at = data.get('expires_at', '')
                try:
                    expiry = datetime.fromisoformat(expires_at)
                    is_expired = datetime.utcnow() > expiry
                    if status == 'expired' and not is_expired:
                        continue
                    if status == 'active' and is_expired:
                        continue
                except:
                    pass
                
                filtered.append((key, data))
            
            # Populate table
            self.table.setRowCount(len(filtered))
            for row, (key, data) in enumerate(filtered):
                self.table.setItem(row, 0, QTableWidgetItem(key[:20] + "..."))
                self.table.setItem(row, 1, QTableWidgetItem(data.get('app_id', 'N/A')))
                self.table.setItem(row, 2, QTableWidgetItem(data.get('email', '')))
                self.table.setItem(row, 3, QTableWidgetItem(data.get('plan', '')))
                self.table.setItem(row, 4, QTableWidgetItem(data.get('status', '')))
                
                # Format expiry date
                try:
                    expiry = datetime.fromisoformat(data.get('expires_at', ''))
                    self.table.setItem(row, 5, QTableWidgetItem(expiry.strftime('%Y-%m-%d')))
                except:
                    self.table.setItem(row, 5, QTableWidgetItem('Invalid'))
                
                # Action buttons
                actions_widget = QWidget()
                actions_layout = QHBoxLayout()
                actions_layout.setContentsMargins(0, 0, 0, 0)
                
                info_btn = QPushButton("Info")
                info_btn.clicked.connect(lambda checked, k=key: self.show_info(k))
                actions_layout.addWidget(info_btn)
                
                revoke_btn = QPushButton("Revoke")
                revoke_btn.setStyleSheet("background-color: #f44336; color: white;")
                revoke_btn.clicked.connect(lambda checked, k=key: self.revoke_license(k))
                actions_layout.addWidget(revoke_btn)
                
                actions_widget.setLayout(actions_layout)
                self.table.setCellWidget(row, 6, actions_widget)
        
        except Exception as e:
            QMessageBox.critical(self, "Error", f"Failed to load licenses: {str(e)}")
    
    def show_info(self, license_key):
        """Show detailed license information."""
        try:
            license_data = self.manager.licenses_ref.child(license_key).get()
            if not license_data:
                QMessageBox.warning(self, "Not Found", "License not found")
                return
            
            # Get active devices
            activations = self.manager.activations_ref.get() or {}
            active_devices = []
            for device_id, activation in activations.items():
                if activation.get('license_key') == license_key:
                    active_devices.append(f"- {activation.get('device_name', 'Unknown')} ({device_id[:8]}...)")
            
            info = f"""License Information:

License Key: {license_key}
App ID: {license_data.get('app_id', 'N/A')}
Email: {license_data.get('email')}
Plan: {license_data.get('plan')}
Status: {license_data.get('status')}
Created: {license_data.get('created_at')}
Expires: {license_data.get('expires_at')}
Max Devices: {license_data.get('max_devices')}
Documents: {license_data.get('documents_used', 0)} / {license_data.get('documents_limit', 0)}

Active Devices ({len(active_devices)}):
""" + "\n".join(active_devices) if active_devices else "None"
            
            QMessageBox.information(self, "License Details", info)
        except Exception as e:
            QMessageBox.critical(self, "Error", f"Failed to get license info: {str(e)}")
    
    def revoke_license(self, license_key):
        """Revoke a license."""
        reply = QMessageBox.question(
            self,
            "Confirm Revoke",
            f"Are you sure you want to revoke license:\n{license_key}?",
            QMessageBox.Yes | QMessageBox.No
        )
        
        if reply == QMessageBox.Yes:
            try:
                self.manager.revoke_license(license_key)
                QMessageBox.information(self, "Success", "License revoked successfully")
                self.refresh_licenses()
            except Exception as e:
                QMessageBox.critical(self, "Error", f"Failed to revoke license: {str(e)}")


class AdminGUI(QMainWindow):
    """Main admin GUI window."""
    
    def __init__(self):
        super().__init__()
        self.manager = None
        self.init_ui()
        self.init_firebase()
    
    def init_ui(self):
        self.setWindowTitle("License Manager - Admin Tool")
        self.setGeometry(100, 100, 1000, 700)
        
        # Central widget
        central = QWidget()
        self.setCentralWidget(central)
        layout = QVBoxLayout(central)
        
        # Title
        title = QLabel("🔑 License Management System")
        title.setFont(QFont("Arial", 18, QFont.Bold))
        title.setAlignment(Qt.AlignCenter)
        layout.addWidget(title)
        
        # Connection status
        self.status_label = QLabel("🔴 Not connected to Firebase")
        self.status_label.setStyleSheet("padding: 5px; background-color: #ffebee;")
        layout.addWidget(self.status_label)
        
        # Tabs
        self.tabs = QTabWidget()
        layout.addWidget(self.tabs)
        
        # Status bar
        self.statusBar().showMessage("Ready")
    
    def init_firebase(self):
        """Initialize Firebase connection."""
        try:
            # Check multiple locations for firebase_config.json
            config_locations = [
                Path(__file__).parent.parent / 'firebase_config.json',  # Renamer root folder
                Path(__file__).parent / 'firebase_config.json',  # admin folder (fallback)
                Path(__file__).parent.parent.parent / 'firebase_config.json',  # desktop-widgets folder
            ]
            
            config_path = None
            for path in config_locations:
                if path.exists():
                    config_path = path
                    break
            
            if not config_path:
                locations_str = "\n  - ".join([str(p) for p in config_locations])
                QMessageBox.critical(
                    self,
                    "Configuration Error",
                    f"Firebase config not found in any of these locations:\n  - {locations_str}\n\n"
                    f"Please create firebase_config.json in the Renamer root folder.\n"
                    f"You can copy from config/firebase_config.example.json"
                )
                return
            
            # Check multiple locations for admin key
            admin_key_locations = [
                Path(__file__).parent.parent / 'firebase-admin-key.json',  # Renamer root folder
                Path(__file__).parent / 'firebase-admin-key.json',  # admin folder (fallback)
                Path(__file__).parent.parent.parent / 'firebase-admin-key.json',  # desktop-widgets folder
            ]
            
            admin_key_path = None
            for path in admin_key_locations:
                if path.exists():
                    admin_key_path = path
                    break
            
            if not admin_key_path:
                locations_str = "\n  - ".join([str(p) for p in admin_key_locations])
                QMessageBox.critical(
                    self,
                    "Configuration Error",
                    f"Admin key not found in any of these locations:\n  - {locations_str}\n\n"
                    f"Please download firebase-admin-key.json from Firebase Console\n"
                    f"and place it in the Renamer folder."
                )
                return
            
            with open(config_path) as f:
                config = json.load(f)
            
            database_url = config.get('databaseURL')
            if not database_url:
                QMessageBox.critical(self, "Configuration Error", "databaseURL not found in firebase_config.json")
                return
            
            # Initialize manager
            self.manager = LicenseManager(admin_key_path, database_url)
            
            # Update status
            self.status_label.setText("✅ Connected to Firebase")
            self.status_label.setStyleSheet("padding: 5px; background-color: #e8f5e9;")
            
            # Add tabs
            self.tabs.addTab(CreateLicenseTab(self.manager), "Create License")
            self.tabs.addTab(ListLicensesTab(self.manager), "Manage Licenses")
            
            self.statusBar().showMessage("Connected to Firebase")
            
        except Exception as e:
            QMessageBox.critical(self, "Firebase Error", f"Failed to initialize Firebase:\n\n{str(e)}")
            self.statusBar().showMessage("Firebase connection failed")


def main():
    app = QApplication(sys.argv)
    window = AdminGUI()
    window.show()
    sys.exit(app.exec())


if __name__ == '__main__':
    main()
