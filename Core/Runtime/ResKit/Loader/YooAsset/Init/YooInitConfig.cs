#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 初始化配置
    /// </summary>
    [Serializable]
    public partial class YooInitConfig
    {
        #region 常量

        /// <summary>
        /// 默认资源包名称
        /// </summary>
        public const string DEFAULT_PACKAGE_NAME = "DefaultPackage";

        /// <summary>
        /// 默认 XOR 密钥种子
        /// </summary>
        public const string DEFAULT_XOR_SEED = "YokiFrame_XOR_Key_Seed_2025!@#$";

        /// <summary>
        /// 默认 AES 密码
        /// </summary>
        public const string DEFAULT_AES_PASSWORD = "YokiFrame_AES_2025";

        #endregion

        #region 基础配置

        /// <summary>
        /// 编辑器下的资源加载模式
        /// </summary>
        [Tooltip("编辑器下的资源加载模式")]
        public EPlayMode EditorPlayMode = EPlayMode.EditorSimulateMode;

        /// <summary>
        /// 真机/打包后的资源加载模式
        /// </summary>
        [Tooltip("真机/打包后的资源加载模式")]
        public EPlayMode RuntimePlayMode = EPlayMode.OfflinePlayMode;

        /// <summary>
        /// 获取当前环境的加载模式（编辑器/真机自动切换）
        /// </summary>
        public EPlayMode PlayMode =>
#if UNITY_EDITOR
            EditorPlayMode;
#else
            RuntimePlayMode;
#endif

        /// <summary>
        /// 资源包名称列表（第一个为默认包）
        /// </summary>
        [Tooltip("资源包名称列表，第一个为默认包")]
        public List<string> PackageNames = new() { DEFAULT_PACKAGE_NAME };

        #endregion

        #region 兼容属性

        /// <summary>
        /// 默认资源包名称（兼容旧版，返回第一个包名）
        /// </summary>
        public string DefaultPackageName
        {
            get => PackageNames is { Count: > 0 } ? PackageNames[0] : DEFAULT_PACKAGE_NAME;
            set
            {
                if (PackageNames == null) PackageNames = new List<string>();
                if (PackageNames.Count == 0) PackageNames.Add(value);
                else PackageNames[0] = value;
            }
        }

        #endregion

        #region 加密配置

        /// <summary>
        /// 资源加密类型
        /// </summary>
        [Tooltip("资源加密类型")]
        public YooEncryptionType EncryptionType = YooEncryptionType.None;

        /// <summary>
        /// XOR 密钥种子（用于生成 32 字节密钥）
        /// </summary>
        [Tooltip("XOR 密钥种子")]
        public string XorKeySeed = DEFAULT_XOR_SEED;

        /// <summary>
        /// 文件偏移量
        /// </summary>
        [Tooltip("文件偏移量")]
        public int FileOffset = 32;

        /// <summary>
        /// AES 密码
        /// </summary>
        [Tooltip("AES 密码")]
        public string AesPassword = DEFAULT_AES_PASSWORD;

        /// <summary>
        /// AES 盐值（8 字节）
        /// </summary>
        [Tooltip("AES 盐值")]
        public string AesSalt = "YokiFram";

        #endregion
    }

    /// <summary>
    /// YooAsset 资源加密类型
    /// </summary>
    public enum YooEncryptionType
    {
        /// <summary>
        /// 不加密
        /// </summary>
        None,

        /// <summary>
        /// XOR 流式加密（推荐：性能好，安全性适中）
        /// </summary>
        XorStream,

        /// <summary>
        /// 文件偏移（仅防止直接打开，无实际加密）
        /// </summary>
        FileOffset,

        /// <summary>
        /// AES 整包加密（安全性高，性能开销大）
        /// </summary>
        Aes,

        /// <summary>
        /// 自定义加密（需要设置 YooInitConfig.CustomEncryptionFactory/CustomDecryptionFactory）
        /// </summary>
        Custom
    }
}
#endif
