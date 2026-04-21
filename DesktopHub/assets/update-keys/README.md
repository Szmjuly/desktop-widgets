# Update-signing keys

Every public key in this folder that matches `*.pub.pem` is embedded into the
client EXE at build time (see
[DesktopHub.UI.csproj](../../src/DesktopHub.UI/DesktopHub.UI.csproj)) and used
by [UpdateVerifier](../../src/DesktopHub.UI/Services/UpdateVerifier.cs) to
validate downloaded auto-updates.

An update installs only if its detached `.sig` verifies against **at least one**
public key in this folder. Multiple keys let us rotate without breaking
already-installed clients.

**Clients without any embedded key refuse all updates** (fail-closed).

---

## One-time setup (do this before the next release)

### 1. Generate a keypair on a trusted machine

```powershell
# From repo root:
openssl genrsa -out .signing/desktophub-updates-private.pem 3072
openssl rsa -in .signing/desktophub-updates-private.pem -pubout -out assets/update-keys/current.pub.pem
```

The `.signing/` folder is gitignored. The `assets/update-keys/` folder is
committed — the public key is safe to check in.

### 2. Back up the private key — THREE copies, all offline

Losing the private key means you can no longer ship signed updates. Users
would have to manually reinstall a new build. So:

1. **Primary:** `.signing/desktophub-updates-private.pem` on the build
   machine (already there from step 1).
2. **Backup #1:** paste the PEM contents into a secure note in 1Password /
   Bitwarden / your password manager.
3. **Backup #2:** print the PEM onto paper and put it in a safe or safety
   deposit box. This is what CAs do for their root keys — boring and 100%
   offline.

Never email it. Never commit it. Never paste it into Slack/Discord.

### 3. Tell the build script where the key lives

Set this environment variable on the build machine (or in a GitHub Actions
secret for CI):

```powershell
setx DH_SIGNING_KEY "C:\Users\<you>\repos\desktop-widgets\DesktopHub\.signing\desktophub-updates-private.pem"
```

[build-single-file.ps1](../../scripts/build-single-file.ps1) reads `DH_SIGNING_KEY`
and calls [sign-update.ps1](../../scripts/sign-update.ps1) after publish.

### 4. Upload both files when releasing

The updater downloads `DesktopHub.exe` **and** `DesktopHub.exe.sig` from the
same folder. Both must be present at the `download_url` referenced in
Firebase `app_versions/desktophub/download_url`.

---

## Rotating keys (do this if the key is ever suspect, or every ~2 years)

Zero-downtime rotation:

1. Generate a new private key on your build machine.
2. Derive its public half and save it here as `next.pub.pem` (keep
   `current.pub.pem` unchanged).
3. Build and release a client update, **still signed with the old private
   key**. This release now has **both** public keys embedded.
4. Wait until your `devices/*/apps/desktophub/installed_version` dashboard
   shows that every active device has picked up that release. (Use
   `push-update.ps1` to nudge stragglers.)
5. Switch `DH_SIGNING_KEY` on your build machine to the new private key.
6. Delete `current.pub.pem`. Rename `next.pub.pem` → `current.pub.pem`.
7. Release the next build signed with the new key. Old installs still
   verify because they have `next.pub.pem` embedded from step 3.

### If you lost the old private key

Skip steps 3–4; there's nothing to sign with. Ship a new manually-downloaded
build containing the new public key; existing installs must manually reinstall
to cross the gap.

---

## What if no keys are present?

If `assets/update-keys/` contains no `*.pub.pem` files, the client's
`UpdateVerifier` logs `"no embedded public keys — update refused"` and
**every auto-update attempt fails closed**. This is intentional — do not
release a client build until step 1 above is done.
