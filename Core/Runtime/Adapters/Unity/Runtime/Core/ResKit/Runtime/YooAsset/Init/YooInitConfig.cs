#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

namespace YokiFrame.Unity
{
    /// <summary>
    /// YooAsset 初始化配置。
    /// </summary>
    [Serializable]
    public sealed class YooInitConfig
    {
        /// <summary>
        /// 默认 YooAsset 资源包名称。
        /// </summary>
        public const string DEFAULT_PACKAGE_NAME = "DefaultPackage";

        /// <summary>
        /// 默认 XOR 加密密钥种子。
        /// </summary>
        public const string DEFAULT_XOR_KEY_SEED = "YokiFrame_XOR_Key_Seed_2025!@#$";

        /// <summary>
        /// 默认 AES 密码。
        /// </summary>
        public const string DEFAULT_AES_PASSWORD = "YokiFrame_AES_2025";

        /// <summary>
        /// 默认 AES 盐值。
        /// </summary>
        public const string DEFAULT_AES_SALT = "YokiFram";

        /// <summary>
        /// 默认文件偏移量。
        /// </summary>
        public const int DEFAULT_FILE_OFFSET = 32;

        /// <summary>
        /// 默认资源清单请求超时时间，单位秒。
        /// </summary>
        public const int DEFAULT_MANIFEST_TIMEOUT_SECONDS = 60;

        /// <summary>
        /// 编辑器下的资源加载模式。
        /// </summary>
        [Tooltip("编辑器下的资源加载模式")]
        public EPlayMode EditorPlayMode = EPlayMode.EditorSimulateMode;

        /// <summary>
        /// 真机或打包后的资源加载模式。
        /// </summary>
        [Tooltip("真机/打包后的资源加载模式")]
        public EPlayMode RuntimePlayMode = EPlayMode.OfflinePlayMode;

        /// <summary>
        /// 资源包名称列表，第一个包会作为 ResKit 默认包。
        /// </summary>
        [Tooltip("资源包名称列表，第一个包会作为 ResKit 默认包")]
        public List<string> PackageNames = new() { DEFAULT_PACKAGE_NAME };

        /// <summary>
        /// 初始化包后是否自动请求版本并加载资源清单。
        /// </summary>
        [Tooltip("初始化包后是否自动请求版本并加载资源清单")]
        public bool AutoLoadManifest = true;

        /// <summary>
        /// 请求版本和清单时的超时时间，单位秒。
        /// </summary>
        [Tooltip("请求版本和清单时的超时时间，单位秒")]
        public int ManifestTimeoutSeconds = DEFAULT_MANIFEST_TIMEOUT_SECONDS;

        /// <summary>
        /// Host/Web 模式主资源服务器地址。
        /// </summary>
        [Tooltip("Host/Web 模式主资源服务器地址")]
        public string DefaultHostServer;

        /// <summary>
        /// Host/Web 模式备用资源服务器地址。
        /// </summary>
        [Tooltip("Host/Web 模式备用资源服务器地址")]
        public string FallbackHostServer;

        /// <summary>
        /// 资源加密类型。
        /// </summary>
        [Tooltip("资源加密类型")]
        public YooEncryptionType EncryptionType = YooEncryptionType.None;

        /// <summary>
        /// XOR 密钥种子。
        /// </summary>
        [Tooltip("XOR 密钥种子")]
        public string XorKeySeed = DEFAULT_XOR_KEY_SEED;

        /// <summary>
        /// 文件偏移量。
        /// </summary>
        [Tooltip("文件偏移量")]
        public int FileOffset = DEFAULT_FILE_OFFSET;

        /// <summary>
        /// AES 密码。
        /// </summary>
        [Tooltip("AES 密码")]
        public string AesPassword = DEFAULT_AES_PASSWORD;

        /// <summary>
        /// AES 盐值。
        /// </summary>
        [Tooltip("AES 盐值，建议至少 8 个 ASCII 字符")]
        public string AesSalt = DEFAULT_AES_SALT;

        /// <summary>
        /// 获取当前平台使用的 YooAsset 加载模式。
        /// </summary>
        public EPlayMode PlayMode
        {
            get
            {
#if UNITY_EDITOR
                return EditorPlayMode;
#else
                return RuntimePlayMode;
#endif
            }
        }

        /// <summary>
        /// 获取或设置默认资源包名称。
        /// </summary>
        public string PrimaryPackageName
        {
            get
            {
                if (PackageNames != null && PackageNames.Count > 0 && !string.IsNullOrEmpty(PackageNames[0]))
                    return PackageNames[0];

                return DEFAULT_PACKAGE_NAME;
            }
            set
            {
                if (PackageNames == null)
                    PackageNames = new();

                if (PackageNames.Count == 0)
                    PackageNames.Add(value);
                else
                    PackageNames[0] = value;
            }
        }

        /// <summary>
        /// 获取有效的资源清单请求超时时间。
        /// </summary>
        /// <returns>大于零的超时时间，单位秒。</returns>
        public int GetManifestTimeoutSeconds()
        {
            return ManifestTimeoutSeconds > 0 ? ManifestTimeoutSeconds : DEFAULT_MANIFEST_TIMEOUT_SECONDS;
        }
    }
}
#endif
#endif
