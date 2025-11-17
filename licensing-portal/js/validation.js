// Form Validation - License Portal

// Validate email
function validateEmail(email) {
    const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return re.test(email);
}

// Validate required field
function validateRequired(value) {
    return value && value.trim().length > 0;
}

// Validate license key format
function validateLicenseKeyFormat(key) {
    const re = /^[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}$/;
    return re.test(key);
}

// Show field error
function showFieldError(fieldId, message) {
    const field = document.getElementById(fieldId);
    const errorElement = document.getElementById(fieldId + 'Error') || createErrorElement(fieldId);
    
    field.classList.add('error');
    errorElement.textContent = message;
    errorElement.style.display = 'block';
}

// Hide field error
function hideFieldError(fieldId) {
    const field = document.getElementById(fieldId);
    const errorElement = document.getElementById(fieldId + 'Error');
    
    field.classList.remove('error');
    if (errorElement) {
        errorElement.style.display = 'none';
    }
}

// Create error element
function createErrorElement(fieldId) {
    const field = document.getElementById(fieldId);
    const errorElement = document.createElement('span');
    errorElement.id = fieldId + 'Error';
    errorElement.className = 'field-error';
    errorElement.style.color = 'var(--danger)';
    errorElement.style.fontSize = 'var(--font-size-sm)';
    errorElement.style.display = 'none';
    field.parentNode.appendChild(errorElement);
    return errorElement;
}

// Real-time validation
document.addEventListener('DOMContentLoaded', function() {
    // Email validation
    const emailField = document.getElementById('email');
    if (emailField) {
        emailField.addEventListener('blur', function() {
            if (this.value && !validateEmail(this.value)) {
                showFieldError('email', 'Please enter a valid email address');
            } else {
                hideFieldError('email');
            }
        });
    }
    
    // License key validation
    const licenseKeyField = document.getElementById('licenseKey');
    if (licenseKeyField) {
        licenseKeyField.addEventListener('blur', function() {
            if (this.value && !validateLicenseKeyFormat(this.value)) {
                showFieldError('licenseKey', 'Invalid license key format');
            } else {
                hideFieldError('licenseKey');
            }
        });
    }
    
    // Required field validation
    const requiredFields = document.querySelectorAll('[required]');
    requiredFields.forEach(field => {
        field.addEventListener('blur', function() {
            if (!validateRequired(this.value)) {
                showFieldError(this.id, 'This field is required');
            } else {
                hideFieldError(this.id);
            }
        });
    });
});

// Add error styling
const style = document.createElement('style');
style.textContent = `
    .form-input.error {
        border-color: var(--danger);
    }
    .field-error {
        display: block;
        margin-top: var(--spacing-xs);
        color: var(--danger);
        font-size: var(--font-size-sm);
    }
`;
document.head.appendChild(style);

