#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 加密配置（已废弃，请使用 YooInitConfig）
    /// </summary>
    [Obsolete("请使用 YooInitConfig 中的加密配置")]
    [Serializable]
    public class YooAssetEncryptionConfig
    {
        public byte[] XorKey = GenerateDefaultXorKey();
        public int FileOffset = 32;
        public string AesPassword = "YokiFrame_AES_2025";
        public byte[] AesSalt = { 0x59, 0x6F, 0x6B, 0x69, 0x46, 0x72, 0x61, 0x6D };

        private byte[] mAesKey;
        private byte[] mAesIV;

        private static byte[] GenerateDefaultXorKey()
        {
            const string seed = "YokiFrame_XOR_Key_Seed_2025!@#$";
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
        }

        public byte[] GetAesKey()
        {
            if (mAesKey is null) InitAesKeyIV();
            return mAesKey;
        }

        public byte[] GetAesIV()
        {
            if (mAesIV is null) InitAesKeyIV();
            return mAesIV;
        }

        private void InitAesKeyIV()
        {
            using var deriveBytes = new Rfc2898DeriveBytes(
                AesPassword, AesSalt, iterations: 10000, HashAlgorithmName.SHA256);
            mAesKey = deriveBytes.GetBytes(32);
            mAesIV = deriveBytes.GetBytes(16);
        }

        public IDecryptionServices CreateDecryption(YooEncryptionType type) => type switch
        {
            YooEncryptionType.XorStream => new XorStreamDecryption(XorKey),
            YooEncryptionType.FileOffset => new FileOffsetDecryption(FileOffset),
            YooEncryptionType.Aes => new AesDecryption(GetAesKey(), GetAesIV()),
            _ => null
        };

        public IEncryptionServices CreateEncryption(YooEncryptionType type) => type switch
        {
            YooEncryptionType.XorStream => new XorStreamEncryption(XorKey),
            YooEncryptionType.FileOffset => new FileOffsetEncryption(FileOffset),
            YooEncryptionType.Aes => new AesEncryption(GetAesKey(), GetAesIV()),
            _ => null
        };
    }

    /// <summary>
    /// 已废弃，请使用 YooEncryptionType
    /// </summary>
    [Obsolete("请使用 YooEncryptionType")]
    public enum YooAssetEncryptionType
    {
        None, XorStream, FileOffset, Aes
    }
}
#endif
