# Licensing Portal - Complete Setup Guide

## Licensing Structure Explained

### Two Options for Licensing

#### 1. **Per-Program Licensing** (A la carte)
- License individual programs as you need them
- Each program (like Spec Document Manager) has 3 tiers: Basic, Premium, Business
- Example: Spec Document Manager Basic = $9.99/month
- Best for: Users who only need one specific tool

#### 2. **Suite Bundle Licensing** (All programs)
- One license gives access to ALL current and future programs
- Same 3 tiers: Basic Bundle ($24.99), Premium Bundle ($39.99), Business Bundle ($79.99)
- Saves 40% compared to buying programs individually
- Best for: Users who want multiple tools or want to future-proof

### Tier Structure (Applies to Both)

| Tier | Devices | Support | Use Case | Price Range |
|------|---------|---------|----------|-------------|
| **Basic** | 2 | Email | Individual users | $9.99/app or $24.99 bundle |
| **Premium** | 5 | Priority | Power users | $19.99/app or $39.99 bundle |
| **Business** | Unlimited | Dedicated | Teams/orgs | $49.99+/app or $79.99 bundle |

---

## How the Pricing Page Works

### Tab System

1. **Spec Document Manager Tab**
   - Shows 3 pricing cards (Basic, Premium, Business) for the single app
   - Each card has a "Choose [Plan]" button
   - Goes to checkout with `?product=spec-updater&plan=basic`

2. **Suite Bundle Tab**
   - Shows 3 bundle pricing cards
   - All current + future programs included
   - Goes to checkout with `?product=bundle&plan=basic`

### URL Parameters

- `?product=spec-updater` - Shows Spec Manager pricing
- `?bundle=true` - Shows Bundle pricing
- `?tier=basic` - Highlights Basic tier
- `?product=spec-updater&plan=premium` - Goes to checkout

---

## Payment Flow

1. **Homepage** → User sees programs and licensing options
2. **Pricing Page** → User selects program or bundle, then tier
3. **Checkout Page** → User enters payment info
4. **Success Page** → User gets license key
5. **Manage Page** → User can view/renew license

---

## Current Programs

1. **Spec Document Manager** ✅ Available Now
   - Updates dates and status in spec document headers
   - Batch processing
   - .docx and .doc support
   - PDF regeneration
   - Backup and version control

2. **More Coming Soon** 🚀
   - Placeholder for future programs
   - Bundle purchasers get automatic access

---

## Customization Needed

1. **Update product names** in `config/products.json`
2. **Set actual prices** based on your business model
3. **Configure payment provider** (Stripe/PayPal) in `config/config.js`
4. **Backend API** - Connect to Firebase for license management
5. **Email notifications** - Set up transactional emails

---

## Files to Update

- `config/products.json` - Product definitions and pricing
- `config/config.js` - API keys and configuration
- `js/payment.js` - Replace `simulatePayment()` with real API calls
- `js/api.js` - Connect to your backend

---

## Testing

1. Open `index.html` - should show programs and licensing options
2. Click tier cards - should navigate to pricing page
3. Pricing page tabs - should switch between spec manager and bundle
4. Click "Choose [Plan]" - should go to checkout
5. Checkout - currently uses demo data

---

## Security Checklist

- [ ] Enable HTTPS
- [ ] Configure Stripe/PayPal production keys
- [ ] Set up backend API with authentication
- [ ] Implement rate limiting
- [ ] Add CSRF protection
- [ ] Enable email verification
- [ ] Set up webhook handlers for payment events
- [ ] Implement license activation API
- [ ] Add database for storing licenses
- [ ] Configure Firebase security rules

