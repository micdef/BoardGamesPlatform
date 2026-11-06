# Certificates of the links

From 0.5.0, the links Gateway → Back (port 7821) and Back → Very Back (port 7822) run over **mutual TLS 1.3**: each end presents the certificate of its role, and each end checks the other one. This document explains how the certificates are made, installed, renewed and withdrawn.
The certificate of the website (the public wildcard behind the reverse proxy) is not concerned: it never reaches these links.

## Principles

- **The platform trusts its own authority only.** Its root is self-signed and is the only trust anchor of the links: the certificate store of the system is never used, so a certificate issued by any other authority, public or private, is refused.
- **The authority is an offline tool**, `src/BoardGamesPlatform.CertificateAuthority`. It opens no port: it is run by hand, on one machine, when a tier needs a certificate. An authority that is not running cannot be attacked.
- **A tier makes its own key.** It sends a request (CSR) to the authority; its private key never leaves its machine.
- **Identity by role.** A certificate carries the name of its role as its only DNS name, and the usages of the side the role takes on the links:

  | Role | DNS name | Usages | Accepted by | Accepts |
  |------|----------|--------|-------------|---------|
  | Gateway | `gateway.<suffix>` | client | the Back | nobody |
  | Back | `back.<suffix>` | server and client | the Very Back | the Gateway |
  | Very Back | `veryback.<suffix>` | server | the Back | nobody |

  The suffix is the one of the deployment (`Tls:IdentitySuffix`, for example `bgp.internal`). The name never depends on the machine: two tiers on one host keep their own identity, and a tier can move without a new certificate. A valid certificate of another role is refused: a stolen Gateway certificate does not open the Very Back.
- **No pinning of a leaf certificate.** A tier trusts the root and checks the role; it never pins the thumbprint of its peer, so a renewed certificate is accepted without touching the other tiers. A certificate to withdraw is listed in `Tls:RefusedThumbprints` (see below).
- **Keys**: ECDSA on the NIST P-256 curve, signed with SHA-256. The root may sign end certificates only (path length 0). A certificate of a tier is valid 365 days by default, 397 at most (the limit of the public authorities), and never beyond the root.
- **No session resumption**: each connection runs a full handshake, so that the refused list and the validity are checked every time.
- **Usages required**: a certificate without its extended usages is refused, though a chain alone would accept it for any usage.

## Prerequisites

| System | Requirement |
|--------|-------------|
| Windows | **Windows Server 2022** or Windows 11, or later: TLS 1.3 is supported by Schannel from these versions only, and enabling it on earlier versions is not a safe configuration ([Microsoft Learn](https://learn.microsoft.com/windows/win32/secauthn/protocols-in-tls-ssl--schannel-ssp-#tls-protocol-version-support)). A tier on Windows Server 2019 or older refuses to start, and says why. |
| Linux | .NET uses OpenSSL; TLS 1.3 needs OpenSSL 3 (the current Fedora releases ship it; OpenSSL 1.1.1, the first with TLS 1.3, is no longer supported by its project). Keep it updated with `dnf upgrade --refresh openssl`. |

## The authority tool

```powershell
dotnet run --project src/BoardGamesPlatform.CertificateAuthority -- <command> <options>
```

| Command | Run on | Does |
|---------|--------|------|
| `init --dir <folder> --name <common name> [--years <1-20>]` | the machine of the authority | Creates the root: `platform-ca.pfx` (the root key, protected by its password) and `platform-ca.crt` (the public certificate every tier trusts). 10 years by default |
| `request --dir <folder> --role <gateway\|back\|veryback> --suffix <suffix>` | the machine of the tier | Creates the key of the tier (`<role>.key`, encrypted) and its request (`<role>.csr`) |
| `sign --dir <folder> --request <file.csr> --role <role> --suffix <suffix> [--days <1-397>] --out <file.crt>` | the machine of the authority | Checks the signature of the request (the proof that the requester holds the key) and that its name is the one of the role, then signs it with the name, usages and validity decided by the tool, never taken from the request. Records it in `issued.csv` |
| `install --key <file.key> --certificate <file.crt> --authority <platform-ca.crt> --out <file.pfx>` | the machine of the tier | Checks that the certificate comes from the authority, is valid now and matches the key, then writes the PFX the tier loads |
| `list --dir <folder>` | the machine of the authority | Shows the certificates issued (`issued.csv`: date, serial number, SHA-256 thumbprint, role, name, validity), to renew them in time or to refuse one |
| `signing-key --role <back\|veryback> --out <folder>` | the machine of the tier | Creates the signing key of the secure channel of the Back → Very Back link (`<role>-signing.key`, encrypted) and its public key (`<role>-signing.pub`, one line for the file of the peer), and shows its key id. It is not a certificate: the authority never sees it ([secure-channel.md](../dev/secure-channel.md#keys)) |

- **Passwords**: the root key is read with `BOARDGAMESPLATFORM_CA_PASSWORD`, the key of a tier (and its PFX, protected by the same password) with `BOARDGAMESPLATFORM_CERTIFICATE_PASSWORD`, the signing key of a tier with `BOARDGAMESPLATFORM_SIGNING_KEY_PASSWORD` (never the password of its PFX); without the variable, the tool asks on the console, twice for a new password. A password is never given on the command line (it would show in the process list and the history). A new password has at least 12 characters.
- **No command ever overwrites a file.** On Linux, the files that hold a private key are readable by their owner only.
- **Exit code**: 0 on success, 1 when a file, a password, a key or a certificate is wrong (nothing is written over), 2 when the command line is wrong (the help is written).

### The machine of the authority

Keep `platform-ca.pfx` and its password on one machine, off the network when possible, with a backup on an offline medium (the password kept apart from it). Whoever holds both can issue a certificate of any role.
`issued.csv` is the register of the authority: keep it with the root, and back it up too.

## Issuing the certificate of a tier

1. **On the machine of the tier**: `request --dir <work folder> --role back --suffix bgp.internal`, with the password of the tier in `BOARDGAMESPLATFORM_CERTIFICATE_PASSWORD`.
2. Copy `back.csr` (public) to the machine of the authority. **Never copy `back.key`.**
3. **On the machine of the authority**: `sign --dir <authority folder> --request back.csr --role back --suffix bgp.internal --out back.crt`. Note the thumbprint it shows.
4. Copy `back.crt` and `platform-ca.crt` (both public) back to the machine of the tier.
5. **On the machine of the tier**: `install --key back.key --certificate back.crt --authority platform-ca.crt --out back.pfx`, then delete `back.key`: the PFX holds it.
6. Give the PFX to the account of the service, readable by it alone (see [Protecting the files](#protecting-the-files)): `install` writes it readable by the user who runs it, an administrator, so the service cannot read it yet and would refuse to start.
7. Point the settings of the tier to the PFX and to `platform-ca.crt`, and set the password as `Tls__CertificatePassword` for the service (see [Protecting the files](#protecting-the-files) and [configuration.md](configuration.md)).

## Protecting the files

| What | Windows | Fedora |
|------|---------|--------|
| PFX of a tier | Readable by the account of the service only: `icacls back.pfx /inheritance:r /grant:r "NT SERVICE\<service>:R" /grant:r "Administrators:F"` (a folder created under `C:\` gives read rights to every user by inheritance) | Owned by the account of the service, mode `0400`: `chown <account>: back.pfx` then `chmod 0400 back.pfx` |
| Password of the PFX | The `Environment` value of the service in the registry, writable by the administrators only | An `EnvironmentFile=` of the unit, owned by `root`, mode `0600` (`Tls__CertificatePassword=…`), or `LoadCredential=`; **never** `Environment=` in the unit, whose variables are exposed to unprivileged clients through D-Bus ([systemd.exec](https://www.freedesktop.org/software/systemd/man/latest/systemd.exec.html)) |
| Root key `platform-ca.pfx` and `issued.csv` | The folder of the authority readable by its administrator only (`icacls <folder> /inheritance:r /grant:r "<administrator>:F"`) | Written mode `0600` by the tool; keep the folder `0700` |
| Signing key `<role>-signing.key` (Back, Very Back) | Readable by the account of the service, writable by the administrators only: `icacls back-signing.key /inheritance:r /grant:r "NT SERVICE\<service>:R" /grant:r "Administrators:F"` | Owned by `root`, group of the service, mode `0640`: the service reads it and never writes it |
| Public keys of the peer (the file of `Handshake:PeerPublicKeysPath`) | **A trust anchor**: whoever writes it adds a key, taken within a minute. Text in ASCII or UTF-8 (a byte order mark is accepted), never UTF-16: on Windows PowerShell 5, write it with `Set-Content -Encoding ascii`, not `Out-File`. To revoke a key, remove its line, never the file: a missing file keeps the current keys. Readable by the account of the service, writable by the administrators only, as the signing key | Owned by `root`, mode `0644` or `0640` with the group of the service |
| Password of the signing key | As the password of the PFX (`Handshake__SigningKeyPassword=…`), and never the same password | As the password of the PFX |

A key file is replaced **atomically**: write the new content to a temporary file in the same folder, created with the rights above (`install -o root -g <group> -m 640 …` on Fedora, then `restorecon` after the move), then move it over the old one (`mv`, or `Move-Item -Force` on Windows), so that a tier never reads a file half written.

The exact commands per host are written with the native installation (0.25.0); the password moves to the vault of the platform in 0.7.0.

## Settings

The same section `Tls` in the Gateway, the Back and the Very Back:

| Key | Default | Meaning |
|-----|---------|---------|
| `Tls:IdentitySuffix` | `bgp.internal` | Suffix of the deployment in the names of the certificates; lower-case DNS labels. The same in every tier |
| `Tls:CertificatePath` | empty | PFX of the tier. A relative path is taken from the folder of the tier |
| `Tls:CertificatePassword` | empty | Password of the PFX. **A secret**: user secrets in development, environment variable `Tls__CertificatePassword` of the service on the servers |
| `Tls:TrustedRootsPath` | empty | PEM file of the roots of the platform (`platform-ca.crt`), the only ones trusted; it may hold several roots during a change of root |
| `Tls:RefusedThumbprints` | none | SHA-256 thumbprints (64 hexadecimal characters) of the certificates refused, whatever their validity |
| `Tls:ExpiryWarning` | `30.00:00:00` | Delay before the expiry under which a warning is logged; at most 365 days |

**A tier refuses to start** (fail closed), and names the wrong setting without repeating the password, when a setting is missing or invalid, or when its own certificate could not be used: no private key, not signed by a trusted root, not valid now, not the name or the usages of its role, or refused. The Gateway loads its certificate before it serves any front.

## Renewal

- A certificate of a tier is valid one year by default. From 30 days before its expiry (`Tls:ExpiryWarning`), the tier logs a **warning** when it loads it and once a day at most afterwards (event 9036), checked every hour even when its links stay open; once it expired, an **error** every hour (event 9039): its peers refuse every new connection. `list` on the authority shows the dates of every certificate.
- To renew, issue a new certificate as above (a new key, a new request, with the same password in `BOARDGAMESPLATFORM_CERTIFICATE_PASSWORD`), then **replace the PFX file in place**, keeping its path, its password and its rights. The tier checks before each new connection, and every hour, whether its PFX or its file of roots changed: the new files are loaded and checked, then used for the next connections, **without a restart**; the log tells it (event 9037, with the new thumbprint). Established connections keep the certificate they started with until they close.
- If the new files fail the check (a wrong password, another role, an expired certificate…), the tier logs an **error** (event 9038) and keeps the current certificate: a mistake during a renewal never stops a running tier. Fix the files and save them again: a changed file is tried again. A file that cannot be read yet (a copy in progress, a missing right) is tried again at the next check without being saved again.
- A new password needs a restart: it is read from the settings at startup.

## Withdrawing a certificate

A certificate whose key may be compromised, or that must no longer be used before its expiry:

1. Find its SHA-256 thumbprint with `list` on the authority (or in the log of the tier that loaded it, event 9037).
2. Add it to `Tls:RefusedThumbprints` in **every tier that may face it**, and restart them: the list is read from the settings at startup. From then on, a connection presenting it is refused at the handshake, whatever its validity.
3. Issue a new certificate for the tier concerned.

There is no revocation list (CRL) nor OCSP: the links never download anything, so the refused list is the only withdrawal, and it must be kept until the certificate expires.

## Changing the root

A new root (expiry, compromise of the root key):

1. `init` a new authority, and issue a new certificate for every tier.
2. Put **both roots** in the file of `Tls:TrustedRootsPath` of every tier (one PEM after the other): old and new certificates are then accepted.
3. Replace the PFX of every tier with its new certificate.
4. Once every tier runs on a new certificate, remove the old root from the file. If the old root key was compromised, skip the transition: replace everything at once and accept the interruption.

## Signing keys of the secure channel (Back → Very Back)

From 0.6.0 the Back → Very Back link adds a secure channel inside TLS ([secure-channel.md](../dev/secure-channel.md)): each tier signs its handshake with a key of its own, and pins the public key of the other. A valid certificate alone never reaches the Very Back.

Setting it up, on each of the two machines:

1. Make the signing key of the tier: `signing-key --role back --out <folder>` on the Back, `--role veryback` on the Very Back; the password comes from `BOARDGAMESPLATFORM_SIGNING_KEY_PASSWORD` or the prompt. The tool shows the key id.
2. Copy the `.pub` file of each tier to the other: it is public, but it decides who is trusted, so copy it by a channel you trust and compare the key id shown by the tool with the one logged by the peer (event 9057).
3. Set the `Handshake` section of each tier: `SigningKeyPath` (its `.key`), `PeerPublicKeysPath` (the file of the public keys of the other), and the password in `Handshake__SigningKeyPassword`.

Rotation of the key of a tier, without a cut of the link:

1. Make the new key, with the same password as the current one (the password is read at startup only).
2. Add its public key as a new line of the file of the peer (at most 4 keys); the peer takes it within a minute.
3. Replace the signing key of the tier by the new one; the tier takes it within a minute (event 9059).
4. Once the tier logs the new key, remove the old public key from the file of the peer: the connections opened with it are closed (event 9061), and the link opens new ones at once.

To **revoke** a key (a tier lost, a key stolen), remove its line from the file of the peer (never delete the file: a missing file keeps the current keys, logged every 5 minutes): within a minute, every connection opened with it is closed, and no new one opens. Each step can also be done with a restart instead of waiting for the check.

## Development

`pwsh tools/certificates/New-DevelopmentCertificates.ps1` creates a development root and the PFX of the three tiers in `.certificates/dev` (ignored by Git), the signing keys of the Back and the Very Back with their `.pub` files, and sets their passwords in the user secrets of each tier; the `appsettings.Development.json` files already point to these files. See [tools/certificates/README.md](../../tools/certificates/README.md). These certificates are for a developer machine only: a server always gets its certificate by the steps above.

The tests generate their certificates at run time with the same code as the tool: no private key is ever committed.

## Logs

| Event | Level | Meaning |
|-------|-------|---------|
| 9031 | Warning | A connection is refused, with the reason (never a text of the certificate) |
| 9032 | Information | A connection is authenticated, with the identity and the thumbprint of the peer |
| 9033 | Warning | A TLS handshake failed |
| 9034 | Warning | A tier refuses the server it connects to, with the reason |
| 9035 | Information | A TLS session is opened with the server |
| 9036 | Warning | The certificate of the tier expires soon: renew it |
| 9037 | Information | The certificate of the tier is loaded, with its thumbprint and its expiry |
| 9038 | Error | Changed certificate files were refused; the current certificate is kept |
| 9039 | Error | The certificate of the tier has expired: its peers refuse every new connection |
| 9040 | Debug | A connection failed its establishment (TLS or secure channel), logged by the listener as 9031, 9033 or 9051 |
| 9051 | Warning or Error | The Very Back refuses a handshake of the secure channel, with the reason (`unknownKey` with the key id, `badSignature`, …) |
| 9052 | Warning or Error | The Back refuses the Very Back in the secure channel, with the reason |
| 9053 | Information | A secure channel is opened, with the key id of the peer |
| 9054 | Error | A record was refused (tampering or a bug): the connection is closed |
| 9055 | Warning | A connection reached the limit of records of its keys: closed, and a new one makes new keys |
| 9057 | Information | The keys of the secure channel are loaded: the key id of the tier and the ones of its peer |
| 9058 | Warning | The public keys of the peer changed, with the key ids added and removed |
| 9059 | Information | A new signing key is taken for the new connections |
| 9060 | Error | Changed key files were refused; the current keys are kept |
| 9061 | Information | A connection is closed because the key of its peer was removed |
| 9062 | Information | The Very Back closed a connection older than 48 hours, never renewed |
| 9063 | Error | A removed key could not be told to the connections at once: the periodic check closes them within a minute (event 9061) |
| 9064 | Warning | The close record of a connection closed for a removed key or its age could not be sent; the connection is cut |
| 9065 | Warning | The Back could not close a connection after the Very Back was refused |
| 9066 | Warning | A connection closed for a removed key or its age could not be closed cleanly; its socket is released |
| 9067 | Error | The periodic check of the connections of the secure channel failed; it runs again in a minute |
| 9068 | Debug | The close record of a normal close could not be sent: the connection broke meanwhile (its cause logged already), or the peer is gone or did not read within one second; the peer may log a truncation |
| 9069 | Debug | The keys of a refused handshake could not be erased (does not happen in practice; the refusal itself is logged) |
| 9020 | Debug | The Back sent a request again on its new connection, the replaced one having refused it before sending it |
| 9009, 9010 | Warning, Error | The Back could not renew its connection to the Very Back: a failure of the network (9009), or a refusal by a key, a certificate or the protocol (9010) to fix before the current connection reaches its limit |

The refusals (9031, 9033, 9051) are logged once every 10 seconds at most, the next entry giving the number held back, so that a peer that fails handshakes in a loop cannot flood the logs.

The password is never logged.
