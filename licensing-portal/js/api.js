// API Client - License Portal

const API_BASE_URL = '/api'; // Update with your actual API endpoint

// API endpoints
const API_ENDPOINTS = {
    LOOKUP_LICENSE: '/license/lookup',
    RENEW_LICENSE: '/license/renew',
    UPGRADE_LICENSE: '/license/upgrade',
    GET_DEVICES: '/license/devices',
    GET_HISTORY: '/license/history'
};

// Lookup license
async function lookupLicense(licenseKey, email) {
    try {
        const response = await fetch(`${API_BASE_URL}${API_ENDPOINTS.LOOKUP_LICENSE}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ licenseKey, email })
        });
        
        if (!response.ok) {
            throw new Error('License not found');
        }
        
        return await response.json();
    } catch (error) {
        console.error('Error looking up license:', error);
        throw error;
    }
}

// Renew license
async function renewLicense(licenseKey, planId) {
    try {
        const response = await fetch(`${API_BASE_URL}${API_ENDPOINTS.RENEW_LICENSE}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ licenseKey, planId })
        });
        
        if (!response.ok) {
            throw new Error('Renewal failed');
        }
        
        return await response.json();
    } catch (error) {
        console.error('Error renewing license:', error);
        throw error;
    }
}

// Upgrade license
async function upgradeLicense(licenseKey, newPlanId) {
    try {
        const response = await fetch(`${API_BASE_URL}${API_ENDPOINTS.UPGRADE_LICENSE}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ licenseKey, newPlanId })
        });
        
        if (!response.ok) {
            throw new Error('Upgrade failed');
        }
        
        return await response.json();
    } catch (error) {
        console.error('Error upgrading license:', error);
        throw error;
    }
}

// Get devices
async function getDevices(licenseKey) {
    try {
        const response = await fetch(`${API_BASE_URL}${API_ENDPOINTS.GET_DEVICES}?licenseKey=${encodeURIComponent(licenseKey)}`);
        
        if (!response.ok) {
            throw new Error('Failed to fetch devices');
        }
        
        return await response.json();
    } catch (error) {
        console.error('Error fetching devices:', error);
        throw error;
    }
}

// Get billing history
async function getBillingHistory(licenseKey) {
    try {
        const response = await fetch(`${API_BASE_URL}${API_ENDPOINTS.GET_HISTORY}?licenseKey=${encodeURIComponent(licenseKey)}`);
        
        if (!response.ok) {
            throw new Error('Failed to fetch history');
        }
        
        return await response.json();
    } catch (error) {
        console.error('Error fetching history:', error);
        throw error;
    }
}

// Initialize license management page
document.addEventListener('DOMContentLoaded', function() {
    if (document.getElementById('lookupForm')) {
        initializeLicenseLookup();
    }
});

// Initialize license lookup
function initializeLicenseLookup() {
    const form = document.getElementById('lookupForm');
    
    form.addEventListener('submit', async function(e) {
        e.preventDefault();
        
        const licenseKey = document.getElementById('licenseKey').value;
        const email = document.getElementById('lookupEmail').value;
        
        if (!licenseKey || !email) {
            alert('Please enter both license key and email');
            return;
        }
        
        try {
            // In production, use actual API call
            // const licenseData = await lookupLicense(licenseKey, email);
            
            // For demo, use mock data
            const licenseData = {
                licenseKey: licenseKey,
                status: 'active',
                plan: 'Premium',
                expiryDate: '2025-12-31',
                daysRemaining: 365,
                devicesUsed: 2,
                maxDevices: 5
            };
            
            displayLicenseDetails(licenseData);
            loadBillingHistory(licenseKey);
        } catch (error) {
            alert('License not found. Please check your license key and email.');
        }
    });
}

// Display license details
function displayLicenseDetails(data) {
    document.getElementById('displayLicenseKey').textContent = data.licenseKey;
    document.getElementById('displayStatus').textContent = data.status;
    document.getElementById('displayPlan').textContent = data.plan;
    document.getElementById('displayExpiry').textContent = formatDate(data.expiryDate);
    document.getElementById('displayDaysRemaining').textContent = data.daysRemaining + ' days';
    document.getElementById('displayDevices').textContent = `${data.devicesUsed} / ${data.maxDevices}`;
    
    document.getElementById('licenseDetails').style.display = 'block';
    document.querySelector('.license-lookup').style.display = 'none';
}

// Load billing history
async function loadBillingHistory(licenseKey) {
    try {
        // In production, use actual API call
        // const history = await getBillingHistory(licenseKey);
        
        // For demo, use mock data
        const history = [
            {
                date: '2024-01-15',
                description: 'Premium Plan - Annual',
                amount: 99.00,
                status: 'Paid',
                invoice: 'INV-2024-001'
            },
            {
                date: '2023-01-15',
                description: 'Premium Plan - Annual',
                amount: 99.00,
                status: 'Paid',
                invoice: 'INV-2023-001'
            }
        ];
        
        displayBillingHistory(history);
    } catch (error) {
        console.error('Error loading billing history:', error);
    }
}

// Display billing history
function displayBillingHistory(history) {
    const tbody = document.getElementById('historyTableBody');
    tbody.innerHTML = '';
    
    history.forEach(item => {
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${formatDate(item.date)}</td>
            <td>${item.description}</td>
            <td>${formatCurrency(item.amount)}</td>
            <td><span class="status-badge status-${item.status.toLowerCase()}">${item.status}</span></td>
            <td><a href="#invoice-${item.invoice}">${item.invoice}</a></td>
        `;
        tbody.appendChild(row);
    });
}

// License management functions
function copyLicenseKey() {
    const licenseKey = document.getElementById('displayLicenseKey').textContent;
    navigator.clipboard.writeText(licenseKey).then(() => {
        alert('License key copied to clipboard!');
    });
}

function renewLicense() {
    if (confirm('Do you want to renew your license?')) {
        window.location.href = '../pages/checkout.html?action=renew';
    }
}

function upgradeLicense() {
    window.location.href = '../pages/pricing.html';
}

function downloadLicense() {
    const licenseKey = document.getElementById('displayLicenseKey').textContent;
    const blob = new Blob([`License Key: ${licenseKey}\nValid until: ${document.getElementById('displayExpiry').textContent}`], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'license-key.txt';
    a.click();
}

function viewDevices() {
    alert('Device management feature coming soon!');
}

// Utility functions
function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' });
}

function formatCurrency(amount) {
    return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD'
    }).format(amount);
}

