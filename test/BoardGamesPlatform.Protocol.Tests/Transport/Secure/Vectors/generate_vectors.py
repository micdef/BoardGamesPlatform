"""Test vectors of the secure channel, computed by an implementation other than the code under test.

Run in a virtual environment with the package ``cryptography`` (see README.md of this folder):
the output is pasted into SecureChannelVectors.cs. The inputs are fixed, so the output never changes, except the
signature: ECDSA draws a random value, so each run gives another valid signature of the same message;
the vectors prove that the .NET code computes what the specification (doc/dev/secure-channel.md) says.
"""

from __future__ import annotations

import hashlib
import hmac

from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.primitives.asymmetric.utils import decode_dss_signature

P256_ORDER = 0xFFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551


def scalar(seed: str) -> int:
    """A fixed private scalar of P-256, from a seed text."""
    return int.from_bytes(hashlib.sha256(seed.encode("ascii")).digest(), "big") % (P256_ORDER - 1) + 1


def point(key: ec.EllipticCurvePrivateKey) -> bytes:
    """The public point of a key, uncompressed (0x04, x, y)."""
    numbers = key.public_key().public_numbers()
    return b"\x04" + numbers.x.to_bytes(32, "big") + numbers.y.to_bytes(32, "big")


def hkdf_extract(salt: bytes, ikm: bytes) -> bytes:
    """HKDF-Extract of RFC 5869 with SHA-256, written from the RFC with the standard library."""
    return hmac.new(salt, ikm, hashlib.sha256).digest()


def hkdf_expand(prk: bytes, info: bytes, length: int) -> bytes:
    """HKDF-Expand of RFC 5869 with SHA-256, written from the RFC with the standard library."""
    output = b""
    block = b""
    counter = 1
    while len(output) < length:
        block = hmac.new(prk, block + info + bytes([counter]), hashlib.sha256).digest()
        output += block
        counter += 1
    return output[:length]


def info(length: int, label: str, transcript_hash: bytes) -> bytes:
    """The info of one value: its length, then the label and the hash, each after its length."""
    label_bytes = label.encode("ascii")
    return (
        length.to_bytes(2, "big")
        + bytes([len(label_bytes)])
        + label_bytes
        + bytes([len(transcript_hash)])
        + transcript_hash
    )


def show(name: str, value: bytes) -> None:
    """One line to paste into the C# vectors."""
    print(f'    public const string {name} = "{value.hex().upper()}";')


def main() -> None:
    """Prints every vector."""
    curve = ec.SECP256R1()
    client = ec.derive_private_key(scalar("BGPS test client ephemeral"), curve)
    server = ec.derive_private_key(scalar("BGPS test server ephemeral"), curve)
    shared = client.exchange(ec.ECDH(), server.public_key())
    client_nonce = bytes(range(0, 32))
    server_nonce = bytes(range(32, 64))
    transcript_hash = hashlib.sha256(b"BGPS test transcript").digest()

    prk = hkdf_extract(client_nonce + server_nonce, shared)
    labels = [
        ("ClientToServerKey", "BGPS v1 back-veryback c2s key", 32),
        ("ServerToClientKey", "BGPS v1 back-veryback s2c key", 32),
        ("ClientToServerIv", "BGPS v1 back-veryback c2s iv", 12),
        ("ServerToClientIv", "BGPS v1 back-veryback s2c iv", 12),
    ]

    show("ClientEphemeralPrivate", scalar("BGPS test client ephemeral").to_bytes(32, "big"))
    show("ServerEphemeralPrivate", scalar("BGPS test server ephemeral").to_bytes(32, "big"))
    show("ClientEphemeralPoint", point(client))
    show("ServerEphemeralPoint", point(server))
    show("SharedSecret", shared)
    show("ClientNonce", client_nonce)
    show("ServerNonce", server_nonce)
    show("TranscriptHash", transcript_hash)
    show("PseudoRandomKey", prk)
    for name, label, length in labels:
        show(name, hkdf_expand(prk, info(length, label, transcript_hash), length))

    signing = ec.derive_private_key(scalar("BGPS test signing key"), curve)
    message = b"BGPS v1 back-veryback server signature\x00" + transcript_hash
    r, s = decode_dss_signature(signing.sign(message, ec.ECDSA(hashes.SHA256())))
    show("SigningPoint", point(signing))
    show("SignedMessage", message)
    show("Signature", r.to_bytes(32, "big") + s.to_bytes(32, "big"))

    # The client label, and the composition of the transcript: the hashes of both certificates, then the messages in their order
    client_message = b"BGPS v1 back-veryback client signature\x00" + transcript_hash
    r, s = decode_dss_signature(signing.sign(client_message, ec.ECDSA(hashes.SHA256())))
    show("ClientSignedMessage", client_message)
    show("ClientSignature", r.to_bytes(32, "big") + s.to_bytes(32, "big"))
    cert_s = hashlib.sha256(b"BGPS test server certificate").digest()
    cert_c = hashlib.sha256(b"BGPS test client certificate").digest()
    m1 = bytes(range(135))
    m2 = bytes((index * 7) % 256 for index in range(199))
    m3 = bytes(range(100, 164))
    show("TranscriptServerCertificate", cert_s)
    show("TranscriptClientCertificate", cert_c)
    show("TranscriptM1", m1)
    show("TranscriptM2", m2)
    show("TranscriptM3", m3)
    show("TranscriptT2", hashlib.sha256(cert_s + cert_c + m1 + m2[:135]).digest())
    show("TranscriptT3", hashlib.sha256(cert_s + cert_c + m1 + m2).digest())
    show("TranscriptTh", hashlib.sha256(cert_s + cert_c + m1 + m2 + m3).digest())


if __name__ == "__main__":
    main()
