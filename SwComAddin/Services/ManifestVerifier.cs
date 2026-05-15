using System;
using System.Runtime.InteropServices;
using System.Security;

namespace SwComAddin.Services
{
    /// <summary>
    /// Ed25519 签名验证器，使用 Windows CNG (Cryptography Next Generation) API。
    /// 无需外部依赖，Windows 10+ 原生支持 Ed25519。
    /// </summary>
    public static class ManifestVerifier
    {
        // 由 tools/sign_manifest.py generate-key 生成后填入
        private static readonly byte[] PublicKey = HexDecode(
            "1a238637b8c1fabd0a433e19b71367635751a6109d5273803eb4360a9361422b");

        public static bool Verify(byte[] manifestBytes, byte[] signature)
        {
            if (manifestBytes == null || signature == null || signature.Length != 64)
                return false;

            try
            {
                return VerifyEd25519(PublicKey, manifestBytes, signature);
            }
            catch
            {
                return false;
            }
        }

        private static byte[] HexDecode(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        // ────────────────────────── CNG P/Invoke ──────────────────────────

        private const string BCRYPT = "bcrypt.dll";
        private const string BCRYPT_ED25519_ALGORITHM = "ED25519";
        private const int BCRYPT_ED25519_PUBLIC_KEY_BLOB = 0x3B;  // BCRYPT_ED25519_PUBLIC_KEY_BLOB_MAGIC but we use raw blob
        private const int STATUS_SUCCESS = 0;

        private static readonly byte[] BCRYPT_ED25519_PUBLIC_KEY_MAGIC = BitConverter.GetBytes(0x31444B45); // "EKD1"

        private static bool VerifyEd25519(byte[] publicKey, byte[] data, byte[] signature)
        {
            IntPtr hAlg = IntPtr.Zero;
            IntPtr hKey = IntPtr.Zero;

            try
            {
                // Open Ed25519 algorithm provider
                int ntstatus = BCryptOpenAlgorithmProvider(out hAlg, BCRYPT_ED25519_ALGORITHM, null!, 0);
                if (ntstatus != STATUS_SUCCESS) return false;

                // Build BCRYPT_ED25519_PUBLIC_KEY_BLOB: magic(4) + cbKey(4) + key(32)
                byte[] keyBlob = new byte[8 + publicKey.Length];
                Buffer.BlockCopy(BCRYPT_ED25519_PUBLIC_KEY_MAGIC, 0, keyBlob, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(publicKey.Length), 0, keyBlob, 4, 4);
                Buffer.BlockCopy(publicKey, 0, keyBlob, 8, publicKey.Length);

                // Import public key
                ntstatus = BCryptImportKeyPair(hAlg, IntPtr.Zero, "EDDSAPUBLICBLOB", out hKey, keyBlob, keyBlob.Length, 0);
                if (ntstatus != STATUS_SUCCESS) return false;

                // Verify signature
                ntstatus = BCryptVerifySignature(hKey, IntPtr.Zero, data, data.Length, signature, signature.Length, 0);
                return ntstatus == STATUS_SUCCESS;
            }
            finally
            {
                if (hKey != IntPtr.Zero) BCryptDestroyKey(hKey);
                if (hAlg != IntPtr.Zero) BCryptCloseAlgorithmProvider(hAlg, 0);
            }
        }

        [DllImport(BCRYPT, CallingConvention = CallingConvention.StdCall)]
        private static extern int BCryptOpenAlgorithmProvider(
            out IntPtr phAlgorithm,
            [MarshalAs(UnmanagedType.LPWStr)] string pszAlgId,
            [MarshalAs(UnmanagedType.LPWStr)] string pszImplementation,
            uint dwFlags);

        [DllImport(BCRYPT, CallingConvention = CallingConvention.StdCall)]
        private static extern int BCryptImportKeyPair(
            IntPtr hAlgorithm,
            IntPtr hImportKey,
            [MarshalAs(UnmanagedType.LPWStr)] string pszBlobType,
            out IntPtr phKey,
            byte[] pbInput,
            int cbInput,
            uint dwFlags);

        [DllImport(BCRYPT, CallingConvention = CallingConvention.StdCall)]
        private static extern int BCryptVerifySignature(
            IntPtr hKey,
            IntPtr pPaddingInfo,
            byte[] pbHash,
            int cbHash,
            byte[] pbSignature,
            int cbSignature,
            uint dwFlags);

        [DllImport(BCRYPT, CallingConvention = CallingConvention.StdCall)]
        private static extern int BCryptDestroyKey(IntPtr hKey);

        [DllImport(BCRYPT, CallingConvention = CallingConvention.StdCall)]
        private static extern int BCryptCloseAlgorithmProvider(IntPtr hAlgorithm, uint dwFlags);
    }
}
