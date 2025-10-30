# Security Documentation

## Overview

This document outlines the security measures implemented in the Spec Header Date Updater subscription system and addresses common security concerns.

---

## 🔐 Security Architecture

### 1. Firebase Security Rules

**Realtime Database Rules:**
```json
{
  "licenses": {
    "READ": "Authenticated users can only read their own license",
    "WRITE": "Denied (server-only via Admin SDK)"
  },
  "device_activations": {
    "READ": "Users can only read their own device activation",
    "WRITE": "Users can create/update their own device record only"
  },
  "usage_logs": {
    "READ": "Denied (admin-only)",
    "WRITE": "Authenticated users can create logs"
  }
}
```

**Key Points:**
- Clients **cannot** create or modify licenses
- Clients **cannot** read other users' data
- All mutations validated server-side
- Timestamp validation prevents replay attacks

### 2. Authentication

**Anonymous Authentication:**
- Each device gets unique Firebase anonymous UID
- UID mapped to device UUID for tracking
- Prevents unauthorized API access
- Token refresh handled automatically

**Why Anonymous Auth?**
- No PII stored in authentication
- Device-specific access control
- Simpler than email/password for desktop apps
- Still provides security through Firebase rules

### 3. License Key Generation

**Algorithm:**
```python
secrets.choice() from string.ascii_uppercase + string.digits
Format: XXXXX-XXXXX-XXXXX-XXXXX
Character set: 62 characters (A-Z, 0-9)
Total combinations: 62^20 ≈ 7.04 × 10^35
```

**Security Properties:**
- Cryptographically secure random generation
- Collision probability: negligible
- Brute force resistant
- Human-readable format

### 4. Device Binding

**Mechanism:**
- Unique UUID generated per device installation
- Stored locally in: `%LOCALAPPDATA%/SpecHeaderUpdater/device_id.txt`
- Transmitted to Firebase for activation tracking
- Max device limits enforced server-side

**Prevents:**
- Unlimited license sharing
- Piracy through key distribution
- Unauthorized multi-device usage

---

## 🛡️ Threat Model

### Threats Addressed

#### 1. SQL Injection
**Status:** ✅ Not Applicable
- Firebase is a NoSQL database
- No SQL queries in application
- Structured data validation via security rules

#### 2. XSS (Cross-Site Scripting)
**Status:** ✅ Not Applicable
- Desktop application (not web)
- No HTML rendering of user input
- PySide6 handles input sanitization

#### 3. License Duplication
**Status:** ✅ Protected
- Device binding with UUID tracking
- Max device limits enforced
- Server-side validation required

**Attack Vector:** User copies license key to multiple devices
**Mitigation:** Firebase tracks active devices per license. Exceeding limit blocks new activations.

#### 4. Man-in-the-Middle (MITM)
**Status:** ✅ Protected
- All Firebase communication over HTTPS
- TLS 1.2+ enforced
- Certificate validation enabled

**Attack Vector:** Intercept network traffic
**Mitigation:** Firebase enforces HTTPS; cannot disable SSL verification.

#### 5. Replay Attacks
**Status:** ✅ Protected
- Timestamp validation on all operations
- Token-based authentication with expiration
- Device activation timestamps tracked

**Attack Vector:** Replay captured API requests
**Mitigation:** Tokens expire; timestamps validated; device IDs must match.

#### 6. Brute Force License Keys
**Status:** ✅ Protected
- 62^20 possible combinations
- Firebase rate limiting
- Security rules prevent enumeration

**Attack Vector:** Try random license keys
**Mitigation:** Astronomical keyspace; Firebase has built-in DDoS protection.

#### 7. Client-Side Tampering
**Status:** ✅ Partially Protected
- Critical validation server-side
- Local cache can be modified but doesn't affect Firebase
- Subscription refreshed periodically

**Attack Vector:** Modify local subscription files
**Mitigation:** Next validation fetches truth from Firebase; local changes ignored.

**Limitation:** Python bytecode can be decompiled. Consider:
- Code obfuscation (PyArmor)
- Binary compilation (Nuitka, PyInstaller with encryption)
- License validation on every operation

#### 8. Credential Theft
**Status:** ⚠️ Requires Vigilance

**firebase_config.json (API Key):**
- **Risk:** Low - API key is public-facing by design
- **Mitigation:** Protected by Firebase security rules
- **Note:** API keys are meant to identify your app, not authenticate users

**firebase-admin-key.json (Service Account):**
- **Risk:** HIGH - Full admin access to Firebase
- **Mitigation:** 
  - Never commit to version control
  - Stored outside application directory
  - File permissions restricted
  - Rotate keys annually

#### 9. Reverse Engineering
**Status:** ⚠️ Python Inherent Risk

**Risk:** Python source code easily readable
**Mitigations:**
1. **Code Obfuscation:** PyArmor, PyMinifier
2. **Binary Compilation:** Nuitka (C compilation)
3. **Server-Side Logic:** Critical validation in Firebase/Cloud Functions
4. **Legal Protection:** License agreement, copyright

**What to Protect:**
- ❌ Don't rely on client-side secrets
- ✅ Assume client code is readable
- ✅ Put security in Firebase rules
- ✅ Use server-side Cloud Functions for critical operations

#### 10. Denial of Service (DoS)
**Status:** ✅ Firebase Protected
- Built-in rate limiting
- Quota management
- DDoS protection

**Additional Recommendations:**
- Monitor Firebase usage
- Set billing alerts
- Implement Cloud Functions rate limiting

---

## 🔧 Security Best Practices

### For Developers

1. **Never Commit Secrets**
   ```bash
   # Add to .gitignore
   firebase_config.json
   firebase-admin-key.json
   .env
   ```

2. **Rotate Service Account Keys**
   - Annually minimum
   - Immediately if compromised
   - Track key creation dates

3. **Monitor Firebase Logs**
   - Check for unusual activity
   - Review usage patterns
   - Set up alerts for anomalies

4. **Validate Input**
   - License key format checking
   - Document count validation
   - Date range verification

5. **Keep Dependencies Updated**
   ```bash
   pip list --outdated
   pip install --upgrade package-name
   ```

6. **Code Review Security Changes**
   - Peer review security-related code
   - Test thoroughly before deployment
   - Document security decisions

### For Administrators

1. **Secure Admin Key Storage**
   - Store `firebase-admin-key.json` securely
   - Use environment variables for paths
   - Restrict file permissions (chmod 600 on Unix)

2. **Regular Backups**
   - Export Firebase data weekly
   - Store backups securely
   - Test restoration process

3. **Monitor License Usage**
   ```bash
   python admin_license_manager.py list
   ```
   - Review active devices
   - Check for suspicious patterns
   - Revoke compromised licenses

4. **Audit Logs**
   - Review usage logs in Firebase Console
   - Check device activations
   - Monitor for abuse

### For End Users

1. **Protect Your License Key**
   - Don't share your license key
   - Store securely (password manager)
   - Report compromised keys immediately

2. **Keep Software Updated**
   - Install updates when available
   - Security patches included in updates

---

## 🚨 Incident Response

### If Service Account Key is Compromised

1. **Immediate Actions:**
   ```bash
   # Revoke compromised key in Firebase Console
   # Settings → Service Accounts → Delete key
   ```

2. **Generate New Key:**
   - Create new service account key
   - Update `firebase-admin-key.json`
   - Update admin scripts

3. **Audit:**
   - Check Firebase logs for unauthorized access
   - Review all licenses for tampering
   - Notify affected customers if needed

### If API Key is Exposed

**Good News:** API keys are public by design!

**Actions:**
1. Verify Firebase security rules are correct
2. Check for unusual usage in Firebase Console
3. Consider rotating key if abuse detected
4. Monitor for anomalous traffic

### If License Keys are Leaked

1. **Identify Affected Licenses:**
   ```bash
   python admin_license_manager.py list
   ```

2. **Revoke Compromised Licenses:**
   ```bash
   python admin_license_manager.py revoke --license KEY
   ```

3. **Issue Replacement Licenses:**
   ```bash
   python admin_license_manager.py create --email customer@example.com --plan premium --duration 365
   ```

4. **Notify Customers:**
   - Email affected users
   - Provide new license keys
   - Explain situation

---

## 📊 Security Monitoring

### Firebase Console Monitoring

**Check Regularly:**
1. **Usage Tab:** Unusual spikes in requests
2. **Authentication:** Anonymous sign-ins count
3. **Realtime Database:** Read/write operations
4. **Rules Simulator:** Test security rules

### Application Logs

**Monitor:**
- Failed validation attempts
- Device activation patterns
- Usage log anomalies
- Error rates

### Alerts to Set Up

1. **Firebase Billing Alerts:** Unusual usage
2. **Error Rate Monitoring:** Application crashes
3. **License Usage Alerts:** Approaching limits
4. **Device Activation Spikes:** Potential abuse

---

## 🎯 Security Recommendations by Priority

### Critical (Implement Now)

- ✅ Firebase security rules deployed
- ✅ Service account key secured
- ✅ `.gitignore` configured
- ✅ Device binding enabled
- ✅ HTTPS enforced

### High Priority (Implement Soon)

- ⬜ **Enable Firebase App Check**
  - Protects against automated abuse
  - Free tier available
  
- ⬜ **Set Up Monitoring**
  - Firebase alerts
  - Usage monitoring
  
- ⬜ **Regular Backups**
  - Weekly Firebase exports
  - Test restoration

### Medium Priority (Consider for Production)

- ⬜ **Cloud Functions for Validation**
  - Server-side license checks
  - Rate limiting
  - Advanced fraud detection
  
- ⬜ **Code Obfuscation**
  - PyArmor or similar
  - Protects IP
  
- ⬜ **Binary Compilation**
  - Nuitka compilation
  - Harder to reverse engineer

### Low Priority (Nice to Have)

- ⬜ **Web Dashboard**
  - Customer license management
  - Usage analytics
  
- ⬜ **Webhook Notifications**
  - License expiration warnings
  - Suspicious activity alerts

---

## 📖 Additional Resources

- [Firebase Security Rules](https://firebase.google.com/docs/rules)
- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [Python Security Best Practices](https://python.readthedocs.io/en/stable/library/security_warnings.html)
- [App Check Documentation](https://firebase.google.com/docs/app-check)

---

## ✅ Security Checklist

Before going to production:

- [ ] Firebase security rules deployed and tested
- [ ] Service account key stored securely
- [ ] `.gitignore` prevents committing secrets
- [ ] Anonymous authentication enabled
- [ ] Device binding implemented
- [ ] Usage logging active
- [ ] Backup strategy in place
- [ ] Monitoring alerts configured
- [ ] Incident response plan documented
- [ ] License agreement in place
- [ ] Regular security review scheduled
- [ ] Dependencies up to date
- [ ] Code review completed
- [ ] Penetration testing considered

---

**Last Updated:** 2025-01-29  
**Next Review:** 2025-04-29 (Quarterly)
