# .signing/

This folder holds the **private** RSA key used by
[scripts/sign-update.ps1](../scripts/sign-update.ps1) to sign release
artifacts. Everything in here except this README and `.gitkeep` is
gitignored (`.signing/` pattern in [.gitignore](../.gitignore)).

See [assets/update-keys/README.md](../assets/update-keys/README.md) for
how to generate the keypair and for the key-rotation procedure.

**Never commit contents of this folder.** If you think you may have
committed a private key, assume it is compromised, generate a new one,
and run the rotation procedure.
