# Licensing Structure - Complete Explanation

## Overview

The licensing system supports **two purchasing models**:
1. **Per-Program Licensing** - Buy individual applications
2. **Suite Bundle Licensing** - Get all programs with one license

---

## Model 1: Per-Program Licensing

### What It Means
- User licenses **one specific program** (e.g., Spec Document Manager)
- They only get access to that single program
- Pricing is per-application

### Pricing Tiers (Per Program)

**Spec Document Manager:**
- **Basic**: $9.99/month
  - 2 devices
  - 100 documents/month
  - .docx support only
  - Email support

- **Premium**: $19.99/month (Most Popular)
  - 5 devices
  - Unlimited documents
  - .docx and .doc support
  - PDF regeneration
  - Priority support

- **Business**: $49.99/month
  - Unlimited devices
  - Unlimited documents
  - Team management
  - Dedicated support
  - Custom templates

### Use Case Example
*"I only need the Spec Document Manager. I don't care about future tools."*
→ Buy Spec Manager Premium for $19.99/month

---

## Model 2: Suite Bundle Licensing

### What It Means
- User gets access to **ALL programs** (current and future)
- Single license key works for everything
- Massive savings compared to buying individually

### Pricing Tiers (Bundle)

- **Basic Bundle**: $24.99/month
  - All programs included
  - 3 devices total (shared across all apps)
  - Basic tier features for each program
  - Email support

- **Premium Bundle**: $39.99/month (Most Popular)
  - All programs included
  - 5 devices total (shared across all apps)
  - Premium tier features for each program
  - Priority support
  - Early access to new programs

- **Business Bundle**: $79.99/month
  - All programs included
  - Unlimited devices
  - Business tier features for each program
  - Dedicated support
  - Team management
  - Custom integrations

### Use Case Example
*"I want Spec Manager now, but I'll probably want your future tools too."*
→ Buy Premium Bundle for $39.99/month (vs $19.99 for just one app)

### Value Proposition
- If you'd ever use 2+ programs, bundle is cheaper
- You get future programs automatically
- One license to manage
- Early access to beta features

---

## Comparison: Per-Program vs Bundle

| Scenario | Per-Program | Bundle | Savings |
|----------|-------------|--------|---------|
| 1 program (Premium) | $19.99/mo | $39.99/mo | Not worth it |
| 2 programs (Premium) | $39.98/mo | $39.99/mo | Save $39.99/year |
| 3 programs (Premium) | $59.97/mo | $39.99/mo | Save $239.76/year |
| Future-proof | Pay per new app | Free access | ∞ |

---

## How Tier Cards Work (Fixed!)

### Homepage Tier Cards
```html
<a href="pages/pricing.html?tier=premium" class="pricing-tier-card clickable">
```

**Before**: Cards were not clickable
**After**: 
- Cards are `<a>` tags, not divs
- Clicking navigates to pricing page
- URL param highlights that tier on pricing page

### What Happens When Clicked

1. User clicks "Premium" tier card on homepage
2. Navigates to `pricing.html?tier=premium`
3. Pricing page JavaScript reads URL param
4. Premium cards are highlighted/scrolled into view
5. User clicks "Choose Premium" for their desired program/bundle

---

## Technical Implementation

### Homepage (`index.html`)

**"How It Works" Section:**
- Explains the two models (per-program vs bundle)
- Two buttons:
  - "View Spec Manager Plans" → `pricing.html?product=spec-updater`
  - "View Bundle Plans" → `pricing.html?bundle=true`

**"Licensing Tiers Explained" Section:**
- Shows what Basic/Premium/Business means
- Cards are clickable (`<a>` tags)
- Navigate to `pricing.html?tier=[tier]`

### Pricing Page (`pricing.html`)

**Tabs:**
- "Spec Document Manager" tab (default)
- "Suite Bundle" tab

**Tab Switching:**
- JavaScript shows/hides sections based on active tab
- URL params auto-select correct tab

**Pricing Cards:**
- Per-program: 3 cards (Basic, Premium, Business) for single app
- Bundle: 3 cards (Basic, Premium, Business) for all apps
- Each card has "Choose [Plan]" button → checkout

### Checkout Page (`checkout.html`)

**URL Params:**
- `?product=spec-updater&plan=premium` → Checkout for Spec Manager Premium
- `?product=bundle&plan=premium` → Checkout for Premium Bundle

**Order Summary:**
- Displays product name
- Displays plan tier
- Shows total price
- Collects payment info

---

## User Journey Examples

### Journey 1: Single App User
1. Homepage → "Explore Programs"
2. Sees "Spec Document Manager"
3. Clicks "View Plans"
4. Sees Basic ($9.99), Premium ($19.99), Business ($49.99)
5. Clicks "Choose Premium"
6. Checkout page
7. Enters payment
8. Gets license key for Spec Manager

### Journey 2: Power User (Multiple Apps)
1. Homepage → Sees "Suite Bundle" option
2. Clicks "View Bundle Plans"
3. Pricing page auto-switches to Bundle tab
4. Sees Basic Bundle ($24.99), Premium Bundle ($39.99), Business Bundle ($79.99)
5. Clicks "Choose Premium Bundle"
6. Checkout page
7. Enters payment
8. Gets license key that works for ALL programs

### Journey 3: Exploring Tiers
1. Homepage → Scrolls to "Licensing Tiers Explained"
2. Clicks "Premium" tier card
3. Pricing page opens with Premium highlighted
4. User compares Premium options for both program and bundle
5. Chooses based on needs

---

## Business Logic

### When to Recommend Per-Program
- User only needs one tool
- User is testing/evaluating
- Budget-conscious and only needs basic features

### When to Recommend Bundle
- User might need multiple tools
- User wants future programs
- User is a power user
- Company/team use (bundle is per-seat)

---

## Future Expansion

When you add a new program:
1. Add to `config/products.json`
2. Update homepage programs section
3. Add new tab to pricing page
4. Bundle users automatically get access
5. Per-program users must buy separately

---

## Key Points

✅ **Clickable tier cards** - Cards on homepage now navigate to pricing
✅ **Tab system** - Pricing page switches between program and bundle
✅ **Clear value prop** - Bundle saves money for multi-app users
✅ **Scalable** - Easy to add new programs
✅ **Professional** - Clean, modern design
✅ **Mobile responsive** - Works on all devices

