#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;

namespace YokiFrame.Unity
{
    /// <summary>标记 YooAsset 构建加密或运行时解密实现所属的统一方案。</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class YooAssetEncryptionImplementationAttribute : Attribute
    {
        /// <summary>创建实现方案标记。</summary>
        /// <param name="mode">实现对应的初始化加密方案。</param>
        /// <param name="role">实现承担的构建加密或运行时解密职责。</param>
        public YooAssetEncryptionImplementationAttribute(
            YooAssetEncryptionMode mode,
            YooAssetEncryptionImplementationRole role)
        {
            Mode = mode;
            Role = role;
        }

        /// <summary>获取实现对应的初始化加密方案。</summary>
        public YooAssetEncryptionMode Mode { get; }

        /// <summary>获取实现承担的构建加密或运行时解密职责。</summary>
        public YooAssetEncryptionImplementationRole Role { get; }
    }

    /// <summary>标识 YooAsset 实现用于构建加密还是运行时解密。</summary>
    public enum YooAssetEncryptionImplementationRole
    {
        /// <summary>构建阶段写入加密 Bundle。</summary>
        Encryption,

        /// <summary>运行时读取并解密 Bundle。</summary>
        Decryption
    }
}
#endif
