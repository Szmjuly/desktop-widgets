# Licensing Portal - Web Application

A modern, secure web application for purchasing subscriptions and licenses for desktop applications.

## Project Structure

```
licensing-portal/
├── index.html              # Main landing page
├── pages/
│   ├── pricing.html        # Pricing tiers comparison
│   ├── checkout.html        # Checkout page
│   ├── success.html         # Payment success page
│   ├── cancel.html          # Payment cancelled page
│   └── manage.html          # License management portal
├── css/
│   ├── main.css            # Main stylesheet
│   ├── components.css      # Component styles
│   └── responsive.css      # Responsive design
├── js/
│   ├── main.js             # Main JavaScript
│   ├── payment.js          # Payment processing
│   ├── api.js              # API client
│   └── validation.js       # Form validation
├── assets/
│   ├── images/             # Images and icons
│   └── fonts/              # Custom fonts
├── config/
│   ├── config.js           # Configuration (API keys, etc.)
│   └── products.json       # Product/pricing definitions
└── README.md               # This file
```

## Features

- ✅ Modern, responsive design
- ✅ Multiple pricing tiers
- ✅ Secure payment processing (Stripe/PayPal)
- ✅ License key generation and delivery
- ✅ Customer portal for license management
- ✅ Support for multiple apps/products
- ✅ Email notifications

## Security

- HTTPS required
- Secure payment APIs
- Input validation
- CSRF protection
- Rate limiting
- Secure license key generation

## Setup

See `README.md` for detailed setup instructions.

