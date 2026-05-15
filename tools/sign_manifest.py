"""
Ed25519 manifest signing tool for SW AI Plugin.

Usage:
  python sign_manifest.py generate-key
    Generate Ed25519 key pair. Outputs private PEM + prints public key hex for embedding.

  python sign_manifest.py sign <manifest.json> <private.pem>
    Sign manifest.json and write manifest.sig (64 bytes binary).

Requirements: pip install cryptography
"""

import sys
import os


def generate_key():
    from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
    from cryptography.hazmat.primitives import serialization

    key = Ed25519PrivateKey.generate()

    private_pem = key.private_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PrivateFormat.PKCS8,
        encryption_algorithm=serialization.NoEncryption(),
    )

    priv_path = os.path.join(os.path.dirname(__file__), "ed25519_private.pem")
    with open(priv_path, "wb") as f:
        f.write(private_pem)
    print(f"Private key saved to {priv_path}")

    pub_bytes = key.public_key().public_bytes(
        encoding=serialization.Encoding.Raw,
        format=serialization.PublicFormat.Raw,
    )
    print(f"ED25519_PUBLIC_KEY_HEX={pub_bytes.hex()}")
    print(f"Public key length: {len(pub_bytes)} bytes")
    print()
    print("Copy the hex value above into ManifestVerifier.cs PublicKey constant.")


def sign(manifest_path, key_path):
    from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
    from cryptography.hazmat.primitives import serialization

    with open(key_path, "rb") as f:
        key = serialization.load_pem_private_key(f.read(), password=None)

    with open(manifest_path, "rb") as f:
        data = f.read()

    # cryptography library returns Ed25519PrivateKey, need to cast
    from cryptography.hazmat.primitives.asymmetric import ed25519

    if not isinstance(key, ed25519.Ed25519PrivateKey):
        print("Error: key is not an Ed25519 private key")
        sys.exit(1)

    signature = key.sign(data)

    sig_path = os.path.splitext(manifest_path)[0] + ".sig"
    with open(sig_path, "wb") as f:
        f.write(signature)

    print(f"Signed: {sig_path} ({len(signature)} bytes)")
    print(f"SHA256 of manifest: ", end="")
    import hashlib

    print(hashlib.sha256(data).hexdigest()[:32] + "...")


def verify(manifest_path, sig_path, pub_key_hex):
    from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PublicKey

    pub_bytes = bytes.fromhex(pub_key_hex)
    key = Ed25519PublicKey.from_public_bytes(pub_bytes)

    with open(manifest_path, "rb") as f:
        data = f.read()
    with open(sig_path, "rb") as f:
        signature = f.read()

    try:
        key.verify(signature, data)
        print("Signature VALID")
    except Exception as e:
        print(f"Signature INVALID: {e}")
        sys.exit(1)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    cmd = sys.argv[1]
    if cmd == "generate-key":
        generate_key()
    elif cmd == "sign":
        if len(sys.argv) < 4:
            print("Usage: sign_manifest.py sign <manifest.json> <private.pem>")
            sys.exit(1)
        sign(sys.argv[2], sys.argv[3])
    elif cmd == "verify":
        if len(sys.argv) < 5:
            print("Usage: sign_manifest.py verify <manifest.json> <manifest.sig> <pub_key_hex>")
            sys.exit(1)
        verify(sys.argv[2], sys.argv[3], sys.argv[4])
    else:
        print(f"Unknown command: {cmd}")
        print(__doc__)
        sys.exit(1)
