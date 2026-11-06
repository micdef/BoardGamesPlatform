# Test vectors of the secure channel

`SecureChannelVectors.cs` holds values computed by `generate_vectors.py`, with the Python package `cryptography` (OpenSSL) for P-256 and an HKDF written from RFC 5869 with the standard library of Python: never by the code under test.

To compute them again, in a virtual environment outside the repository:

```bash
python -m venv vectors-venv
vectors-venv/Scripts/python -m pip install cryptography
vectors-venv/Scripts/python -I test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/Vectors/generate_vectors.py
```

On Linux, the interpreter is `vectors-venv/bin/python`.
The inputs are fixed, so every value but the signature comes out the same; ECDSA draws a random value, so each run gives another valid signature of the same message.
The vectors were computed with `cryptography` 50.0.2.
