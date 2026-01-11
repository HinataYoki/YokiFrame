using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace YokiFrame
{
    /// <summary>
    /// 输入管理静态入口类
    /// 提供类型安全的输入访问和管理
    /// </summary>
    public static partial class InputKit
    {
        #region 配置字段

        private static readonly Dictionary<Type, IInputActionCollection2> sInputInstances = new();
        private static IInputPersistence sPersistence;
        private static string sPersistenceKey = "InputKit_Bindings";
        private static bool sIsInitialized;
        private static InputActionAsset sActionAsset;
        private static InputDeviceType sCurrentDeviceType = InputDeviceType.Unknown;

        #endregion

        #region 属性

        /// <summary>是否已初始化</summary>
        public static bool IsInitialized => sIsInitialized;

        /// <summary>当前活动设备类型</summary>
        public static InputDeviceType CurrentDeviceType => sCurrentDeviceType;

        /// <summary>当前是否使用键鼠</summary>
        public static bool IsUsingKeyboardMouse => sCurrentDeviceType == InputDeviceType.KeyboardMouse;

        /// <summary>当前是否使用手柄</summary>
        public static bool IsUsingGamepad => sCurrentDeviceType == InputDeviceType.Gamepad;

        /// <summary>当前是否使用触屏</summary>
        public static bool IsUsingTouch => sCurrentDeviceType == InputDeviceType.Touch;

        /// <summary>当前 InputActionAsset</summary>
        public static InputActionAsset Asset => sActionAsset;

        /// <summary>是否有手柄连接</summary>
        public static bool IsGamepadConnected => Gamepad.current != default;

        #endregion

        #region 事件

        /// <summary>设备切换事件</summary>
        public static event Action<InputDeviceType> OnDeviceChanged;

        /// <summary>绑定变更事件</summary>
        public static event Action<InputAction, int> OnBindingChanged;

        #endregion

        #region 泛型注册与获取

        /// <summary>
        /// 注册输入类实例（类型安全）
        /// </summary>
        /// <typeparam name="T">InputSystem 生成的输入类</typeparam>
        public static void Register<T>() where T : class, IInputActionCollection2, new()
        {
            var type = typeof(T);
            if (sInputInstances.ContainsKey(type))
            {
                Debug.LogWarning($"[InputKit] 类型 {type.Name} 已注册，忽略重复注册");
                return;
            }

            var instance = new T();
            sInputInstances[type] = instance;
            
            // 更新 ActionAsset 引用
            if (sActionAsset == default)
            {
                sActionAsset = ExtractAssetFromCollection(instance);
            }

            Debug.Log($"[InputKit] 已注册输入类: {type.Name}");
        }

        /// <summary>
        /// 注册输入类实例（传入已有实例）
        /// </summary>
        /// <typeparam name="T">InputSystem 生成的输入类</typeparam>
        /// <param name="instance">输入类实例</param>
        public static void Register<T>(T instance) where T : class, IInputActionCollection2
        {
            if (instance == default)
            {
                Debug.LogError("[InputKit] 实例不能为空");
                return;
            }

            var type = typeof(T);
            if (sInputInstances.ContainsKey(type))
            {
                Debug.LogWarning($"[InputKit] 类型 {type.Name} 已注册，忽略重复注册");
                return;
            }

            sInputInstances[type] = instance;
            
            // 更新 ActionAsset 引用
            if (sActionAsset == default)
            {
                sActionAsset = ExtractAssetFromCollection(instance);
            }

            Debug.Log($"[InputKit] 已注册输入类: {type.Name}");
        }

        /// <summary>
        /// 获取已注册的输入类实例
        /// </summary>
        /// <typeparam name="T">InputSystem 生成的输入类</typeparam>
        /// <returns>输入类实例，未注册时返回 default</returns>
        public static T Get<T>() where T : class, IInputActionCollection2
        {
            var type = typeof(T);
            if (sInputInstances.TryGetValue(type, out var instance))
            {
                return instance as T;
            }

            Debug.LogWarning($"[InputKit] 类型 {type.Name} 未注册");
            return default;
        }

        /// <summary>
        /// 检查是否已注册指定类型
        /// </summary>
        /// <typeparam name="T">InputSystem 生成的输入类</typeparam>
        public static bool IsRegistered<T>() where T : class, IInputActionCollection2
        {
            return sInputInstances.ContainsKey(typeof(T));
        }

        #endregion

        #region 配置方法

        /// <summary>
        /// 设置持久化实现
        /// </summary>
        /// <param name="persistence">持久化实现</param>
        public static void SetPersistence(IInputPersistence persistence)
        {
            sPersistence = persistence;
        }

        /// <summary>
        /// 设置持久化存储键
        /// </summary>
        /// <param name="key">存储键</param>
        public static void SetPersistenceKey(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                sPersistenceKey = key;
            }
        }

        #endregion

        #region 生命周期

        /// <summary>
        /// 初始化（自动加载持久化绑定并启用输入）
        /// </summary>
        public static void Initialize()
        {
            if (sIsInitialized)
            {
                Debug.LogWarning("[InputKit] 已初始化，请勿重复调用");
                return;
            }

            if (sInputInstances.Count == 0)
            {
                Debug.LogError("[InputKit] 未注册任何输入类，请先调用 Register<T>()");
                return;
            }

            // 设置默认持久化
            if (sPersistence == default)
            {
                sPersistence = new PlayerPrefsPersistence();
            }

            // 加载持久化绑定
            LoadBindings();

            // 启用所有已注册的输入类
            foreach (var kvp in sInputInstances)
            {
                kvp.Value.Enable();
            }

            // 监听设备切换
            InputSystem.onActionChange += OnActionChange;

            // 检测初始设备类型
            DetectCurrentDevice();

            sIsInitialized = true;
            Debug.Log($"[InputKit] 初始化完成，已注册 {sInputInstances.Count} 个输入类");
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public static void Dispose()
        {
            if (!sIsInitialized) return;

            InputSystem.onActionChange -= OnActionChange;

            // 释放所有已注册的输入类
            foreach (var kvp in sInputInstances)
            {
                kvp.Value.Disable();
                if (kvp.Value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            sInputInstances.Clear();
            sActionAsset = default;
            sIsInitialized = false;
            OnDeviceChanged = null;
            OnBindingChanged = null;

            Debug.Log("[InputKit] 已释放资源");
        }

        /// <summary>
        /// 重置所有配置（测试用）
        /// </summary>
        public static void Reset()
        {
            Dispose();
            sPersistence = default;
            sPersistenceKey = "InputKit_Bindings";
            sCurrentDeviceType = InputDeviceType.Unknown;
            
            // 重置子系统
            ResetBuffer();
            ResetCombo();
            ResetContext();
            ResetTouch();
            ResetHaptic();
        }

        #endregion

        #region 设备检测

        private static void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed) return;
            if (obj is not InputAction action) return;

            var control = action.activeControl;
            if (control == default) return;

            var newDeviceType = GetDeviceType(control.device);
            if (newDeviceType != sCurrentDeviceType)
            {
                sCurrentDeviceType = newDeviceType;
                OnDeviceChanged?.Invoke(sCurrentDeviceType);
            }
        }

        private static void DetectCurrentDevice()
        {
            // 优先检测手柄
            if (Gamepad.current != default)
            {
                sCurrentDeviceType = InputDeviceType.Gamepad;
                return;
            }

            // 检测触屏
            if (Touchscreen.current != default)
            {
                sCurrentDeviceType = InputDeviceType.Touch;
                return;
            }

            // 默认键鼠
            sCurrentDeviceType = InputDeviceType.KeyboardMouse;
        }

        /// <summary>
        /// 获取设备类型
        /// </summary>
        /// <param name="device">输入设备</param>
        /// <returns>设备类型</returns>
        public static InputDeviceType GetDeviceType(InputDevice device)
        {
            return device switch
            {
                Gamepad => InputDeviceType.Gamepad,
                Touchscreen => InputDeviceType.Touch,
                Keyboard or Mouse => InputDeviceType.KeyboardMouse,
                _ => InputDeviceType.Unknown
            };
        }

        #endregion

        #region 内部方法

        internal static void RaiseBindingChanged(InputAction action, int bindingIndex)
        {
            OnBindingChanged?.Invoke(action, bindingIndex);
        }

        /// <summary>
        /// 从 IInputActionCollection2 提取 InputActionAsset
        /// </summary>
        /// <param name="collection">输入集合</param>
        /// <returns>InputActionAsset，提取失败返回 default</returns>
        private static InputActionAsset ExtractAssetFromCollection(IInputActionCollection2 collection)
        {
            if (collection == default) return default;

            // 方式1：通过反射获取 asset 属性（Unity 生成的类都有此属性）
            var assetProperty = collection.GetType().GetProperty("asset");
            if (assetProperty != default)
            {
                return assetProperty.GetValue(collection) as InputActionAsset;
            }

            // 方式2：通过 actions 获取（备用方案）
            foreach (var action in collection)
            {
                if (action.actionMap?.asset != default)
                {
                    return action.actionMap.asset;
                }
            }

            return default;
        }

        #endregion
    }
}
