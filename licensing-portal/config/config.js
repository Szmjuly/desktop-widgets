// Configuration - License Portal
// NOTE: In production, move sensitive values to environment variables

const CONFIG = {
    // API Configuration
    API_BASE_URL: 'https://api.example.com', // Update with your API URL
    
    // Payment Providers
    STRIPE_PUBLISHABLE_KEY: 'pk_test_...', // Update with your Stripe key
    PAYPAL_CLIENT_ID: '...', // Update with your PayPal client ID
    
    // Application Settings
    APP_NAME: 'License Portal',
    APP_VERSION: '1.0.0',
    
    // Features
    FEATURES: {
        PAYPAL_ENABLED: true,
        STRIPE_ENABLED: true,
        EMAIL_NOTIFICATIONS: true,
        AUTO_RENEWAL: true
    },
    
    // Email Configuration
    EMAIL: {
        FROM: 'noreply@example.com',
        SUPPORT: 'support@example.com'
    },
    
    // Security
    SECURITY: {
        CSRF_TOKEN: '', // Set dynamically
        RATE_LIMIT_ENABLED: true
    }
};

// Environment-specific overrides
if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
    CONFIG.API_BASE_URL = 'http://localhost:3000/api';
    CONFIG.STRIPE_PUBLISHABLE_KEY = 'pk_test_localhost';
}

// Export for use in other scripts
if (typeof module !== 'undefined' && module.exports) {
    module.exports = CONFIG;
}

