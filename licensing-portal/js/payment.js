// Payment Processing - License Portal

// Initialize payment form
document.addEventListener('DOMContentLoaded', function() {
    if (document.getElementById('checkoutForm')) {
        initializeCheckout();
    }
});

// Initialize checkout page
async function initializeCheckout() {
    const productId = getURLParameter('product');
    const planId = getURLParameter('plan');
    
    if (!productId || !planId) {
        window.location.href = 'pricing.html';
        return;
    }
    
    await loadOrderSummary(productId, planId);
    setupFormValidation();
    setupCardInputFormatting();
}

// Load order summary
async function loadOrderSummary(productId, planId) {
    try {
        const response = await fetch('../config/products.json');
        const products = await response.json();
        const product = products.find(p => p.id === productId);
        const plan = product?.plans.find(p => p.id === planId);
        
        if (!product || !plan) {
            window.location.href = 'pricing.html';
            return;
        }
        
        document.getElementById('summaryProduct').querySelector('.summary-value').textContent = product.name;
        document.getElementById('summaryPlan').querySelector('.summary-value').textContent = plan.name;
        document.getElementById('summaryDuration').querySelector('.summary-value').textContent = plan.period;
        document.getElementById('summaryTotal').textContent = formatCurrency(plan.price);
        
        // Store in sessionStorage for form submission
        sessionStorage.setItem('checkout_product', productId);
        sessionStorage.setItem('checkout_plan', planId);
    } catch (error) {
        console.error('Error loading order summary:', error);
    }
}

// Setup form validation
function setupFormValidation() {
    const form = document.getElementById('checkoutForm');
    
    form.addEventListener('submit', async function(e) {
        e.preventDefault();
        
        if (!validateForm()) {
            return;
        }
        
        await processPayment();
    });
}

// Validate form
function validateForm() {
    const email = document.getElementById('email').value;
    const firstName = document.getElementById('firstName').value;
    const lastName = document.getElementById('lastName').value;
    const paymentMethod = document.querySelector('input[name="paymentMethod"]:checked').value;
    const terms = document.getElementById('terms').checked;
    
    if (!email || !firstName || !lastName || !terms) {
        alert('Please fill in all required fields');
        return false;
    }
    
    if (paymentMethod === 'card') {
        const cardNumber = document.getElementById('cardNumber').value;
        const cardExpiry = document.getElementById('cardExpiry').value;
        const cardCVC = document.getElementById('cardCVC').value;
        const cardName = document.getElementById('cardName').value;
        
        if (!cardNumber || !cardExpiry || !cardCVC || !cardName) {
            alert('Please fill in all card details');
            return false;
        }
        
        if (!validateCardNumber(cardNumber)) {
            alert('Invalid card number');
            return false;
        }
        
        if (!validateExpiry(cardExpiry)) {
            alert('Invalid expiry date');
            return false;
        }
        
        if (!validateCVC(cardCVC)) {
            alert('Invalid CVC');
            return false;
        }
    }
    
    return true;
}

// Setup card input formatting
function setupCardInputFormatting() {
    const cardNumber = document.getElementById('cardNumber');
    const cardExpiry = document.getElementById('cardExpiry');
    const cardCVC = document.getElementById('cardCVC');
    
    if (cardNumber) {
        cardNumber.addEventListener('input', function(e) {
            let value = e.target.value.replace(/\s/g, '');
            value = value.replace(/(.{4})/g, '$1 ').trim();
            e.target.value = value;
        });
    }
    
    if (cardExpiry) {
        cardExpiry.addEventListener('input', function(e) {
            let value = e.target.value.replace(/\D/g, '');
            if (value.length >= 2) {
                value = value.substring(0, 2) + '/' + value.substring(2, 4);
            }
            e.target.value = value;
        });
    }
    
    if (cardCVC) {
        cardCVC.addEventListener('input', function(e) {
            e.target.value = e.target.value.replace(/\D/g, '');
        });
    }
}

// Validate card number (Luhn algorithm)
function validateCardNumber(number) {
    const cleaned = number.replace(/\s/g, '');
    if (!/^\d{13,19}$/.test(cleaned)) {
        return false;
    }
    
    let sum = 0;
    let isEven = false;
    
    for (let i = cleaned.length - 1; i >= 0; i--) {
        let digit = parseInt(cleaned[i]);
        
        if (isEven) {
            digit *= 2;
            if (digit > 9) {
                digit -= 9;
            }
        }
        
        sum += digit;
        isEven = !isEven;
    }
    
    return sum % 10 === 0;
}

// Validate expiry date
function validateExpiry(expiry) {
    const match = expiry.match(/^(\d{2})\/(\d{2})$/);
    if (!match) return false;
    
    const month = parseInt(match[1]);
    const year = parseInt('20' + match[2]);
    
    if (month < 1 || month > 12) return false;
    
    const now = new Date();
    const expiryDate = new Date(year, month - 1);
    
    return expiryDate >= now;
}

// Validate CVC
function validateCVC(cvc) {
    return /^\d{3,4}$/.test(cvc);
}

// Process payment
async function processPayment() {
    const submitButton = document.getElementById('submitButton');
    const submitButtonText = document.getElementById('submitButtonText');
    const submitButtonLoading = document.getElementById('submitButtonLoading');
    
    submitButton.disabled = true;
    submitButtonText.style.display = 'none';
    submitButtonLoading.style.display = 'inline';
    
    try {
        const formData = collectFormData();
        const paymentMethod = document.querySelector('input[name="paymentMethod"]:checked').value;
        
        // In production, this would call your backend API
        // For now, simulate payment processing
        const result = await simulatePayment(formData, paymentMethod);
        
        if (result.success) {
            // Redirect to success page with license key
            window.location.href = `success.html?license=${result.licenseKey}&email=${encodeURIComponent(formData.email)}`;
        } else {
            throw new Error(result.error || 'Payment failed');
        }
    } catch (error) {
        alert('Payment failed: ' + error.message);
        submitButton.disabled = false;
        submitButtonText.style.display = 'inline';
        submitButtonLoading.style.display = 'none';
    }
}

// Collect form data
function collectFormData() {
    return {
        email: document.getElementById('email').value,
        firstName: document.getElementById('firstName').value,
        lastName: document.getElementById('lastName').value,
        company: document.getElementById('company').value,
        paymentMethod: document.querySelector('input[name="paymentMethod"]:checked').value,
        cardNumber: document.getElementById('cardNumber')?.value,
        cardExpiry: document.getElementById('cardExpiry')?.value,
        cardCVC: document.getElementById('cardCVC')?.value,
        cardName: document.getElementById('cardName')?.value,
        productId: sessionStorage.getItem('checkout_product'),
        planId: sessionStorage.getItem('checkout_plan'),
        newsletter: document.getElementById('newsletter').checked
    };
}

// Simulate payment (replace with actual API call)
async function simulatePayment(formData, paymentMethod) {
    // Simulate API delay
    await new Promise(resolve => setTimeout(resolve, 2000));
    
    // In production, call your backend API:
    // const response = await fetch('/api/process-payment', {
    //     method: 'POST',
    //     headers: { 'Content-Type': 'application/json' },
    //     body: JSON.stringify(formData)
    // });
    // return await response.json();
    
    // For demo, generate a mock license key
    const licenseKey = generateLicenseKey();
    
    return {
        success: true,
        licenseKey: licenseKey,
        transactionId: 'TXN-' + Date.now()
    };
}

// Generate license key
function generateLicenseKey() {
    const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';
    const parts = [];
    
    for (let i = 0; i < 4; i++) {
        let part = '';
        for (let j = 0; j < 5; j++) {
            part += chars.charAt(Math.floor(Math.random() * chars.length));
        }
        parts.push(part);
    }
    
    return parts.join('-');
}

// Utility functions from main.js
function getURLParameter(name) {
    const urlParams = new URLSearchParams(window.location.search);
    return urlParams.get(name);
}

function formatCurrency(amount) {
    return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD'
    }).format(amount);
}

