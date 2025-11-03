// Main JavaScript - License Portal

// Initialize on page load
document.addEventListener('DOMContentLoaded', function() {
    initializeFAQ();
    initializePaymentToggle();
    // Products section removed - loadProducts() no longer needed
});

// Load products from config
async function loadProducts() {
    try {
        const response = await fetch('config/products.json');
        const products = await response.json();
        
        if (document.getElementById('productsGrid')) {
            displayProducts(products);
        }
        
        if (document.getElementById('productSelect')) {
            populateProductSelect(products);
        }
    } catch (error) {
        console.error('Error loading products:', error);
    }
}

// Display products on homepage
function displayProducts(products) {
    const grid = document.getElementById('productsGrid');
    grid.innerHTML = '';
    
    products.forEach(product => {
        const card = document.createElement('div');
        card.className = 'product-card';
        card.innerHTML = `
            <div class="product-icon">${product.icon || '📱'}</div>
            <h3>${product.name}</h3>
            <p>${product.description}</p>
            <a href="pages/pricing.html?product=${product.id}" class="btn btn-primary">View Plans</a>
        `;
        grid.appendChild(card);
    });
}

// Populate product selector
function populateProductSelect(products) {
    const select = document.getElementById('productSelect');
    select.innerHTML = '<option value="">Select an application...</option>';
    
    products.forEach(product => {
        const option = document.createElement('option');
        option.value = product.id;
        option.textContent = product.name;
        select.appendChild(option);
    });
    
    // Load pricing for selected product
    select.addEventListener('change', function() {
        if (this.value) {
            loadPricingForProduct(this.value);
        }
    });
    
    // Check URL parameter
    const urlParams = new URLSearchParams(window.location.search);
    const productId = urlParams.get('product');
    if (productId) {
        select.value = productId;
        loadPricingForProduct(productId);
    }
}

// Load pricing for specific product
async function loadPricingForProduct(productId) {
    try {
        const response = await fetch('../config/products.json');
        const products = await response.json();
        const product = products.find(p => p.id === productId);
        
        if (!product) return;
        
        displayPricing(product);
        displayComparison(product);
    } catch (error) {
        console.error('Error loading pricing:', error);
    }
}

// Display pricing cards
function displayPricing(product) {
    const grid = document.getElementById('pricingGrid');
    if (!grid) return;
    
    grid.innerHTML = '';
    
    product.plans.forEach((plan, index) => {
        const isFeatured = index === 2; // Premium plan is featured
        const card = document.createElement('div');
        card.className = `pricing-card ${isFeatured ? 'featured' : ''}`;
        card.innerHTML = `
            <div class="pricing-header">
                <div class="pricing-name">${plan.name}</div>
                <div class="pricing-price">$${plan.price}</div>
                <div class="pricing-period">/${plan.period}</div>
            </div>
            <ul class="pricing-features">
                ${plan.features.map(f => `<li>${f}</li>`).join('')}
            </ul>
            <div class="pricing-action">
                <a href="checkout.html?product=${product.id}&plan=${plan.id}" class="btn btn-primary btn-large">
                    Choose ${plan.name}
                </a>
            </div>
        `;
        grid.appendChild(card);
    });
}

// Display comparison table
function displayComparison(product) {
    const tbody = document.getElementById('comparisonTableBody');
    if (!tbody) return;
    
    const features = [
        { name: 'Max Devices', key: 'maxDevices' },
        { name: 'Documents/Month', key: 'documentsLimit' },
        { name: 'Email Support', key: 'emailSupport' },
        { name: 'Priority Support', key: 'prioritySupport' },
        { name: 'API Access', key: 'apiAccess' },
        { name: 'Auto-Renewal', key: 'autoRenewal' }
    ];
    
    tbody.innerHTML = '';
    
    features.forEach(feature => {
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${feature.name}</td>
            ${product.plans.map(plan => {
                const value = plan[feature.key];
                if (typeof value === 'boolean') {
                    return `<td>${value ? '✓' : '✗'}</td>`;
                }
                return `<td>${value !== undefined ? value : 'N/A'}</td>`;
            }).join('')}
        `;
        tbody.appendChild(row);
    });
}

// Initialize FAQ accordion
function initializeFAQ() {
    const faqItems = document.querySelectorAll('.faq-item');
    
    faqItems.forEach(item => {
        const question = item.querySelector('.faq-question');
        question.addEventListener('click', function() {
            const isActive = item.classList.contains('active');
            
            // Close all FAQ items
            faqItems.forEach(i => i.classList.remove('active'));
            
            // Open clicked item if it wasn't active
            if (!isActive) {
                item.classList.add('active');
            }
        });
    });
}

// Initialize payment method toggle
function initializePaymentToggle() {
    const paymentMethods = document.querySelectorAll('input[name="paymentMethod"]');
    const cardSection = document.getElementById('cardPaymentSection');
    const paypalSection = document.getElementById('paypalPaymentSection');
    
    if (!paymentMethods.length) return;
    
    paymentMethods.forEach(method => {
        method.addEventListener('change', function() {
            if (this.value === 'card') {
                cardSection.style.display = 'block';
                paypalSection.style.display = 'none';
            } else if (this.value === 'paypal') {
                cardSection.style.display = 'none';
                paypalSection.style.display = 'block';
            }
        });
    });
}

// Utility: Format currency
function formatCurrency(amount) {
    return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD'
    }).format(amount);
}

// Utility: Get URL parameter
function getURLParameter(name) {
    const urlParams = new URLSearchParams(window.location.search);
    return urlParams.get(name);
}

// Utility: Format license key (XXXXX-XXXXX-XXXXX-XXXXX)
function formatLicenseKey(key) {
    return key.replace(/(.{5})/g, '$1-').replace(/-$/, '');
}

