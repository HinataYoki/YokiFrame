#if !GODOT
using System;
using System.Collections.Generic;
using UnityEngine;
#if YOKIFRAME_INPUTSYSTEM_SUPPORT
using UnityEngine.InputSystem;
#endif
using YokiFrame;

namespace YokiFrame.Unity
{
#if YOKIFRAME_INPUTSYSTEM_SUPPORT
    /// <summary>
    /// InputKit 的 Unity Input System 后端。保留 InputActionAsset/生成类作为 Unity 侧主要接入方式。
    /// </summary>
    public sealed class UnityInputBackend : IInputBackend, IDisposable
    {
        private readonly Dictionary<Type, IInputActionCollection2> mInputCollections = new Dictionary<Type, IInputActionCollection2>();
        private readonly List<InputAction> mActions = new List<InputAction>(64);
        private readonly HashSet<string> mEnabledActionMaps = new HashSet<string>(StringComparer.Ordinal);
        private InputActionAsset mActionAsset;
        private InputDeviceType mCurrentDeviceType = InputDeviceType.Unknown;
        private bool mUseActionMapFilter;
        private bool mIsDisposed;

        public UnityInputBackend()
        {
            InputSystem.onActionChange += OnActionChange;
            DetectCurrentDevice();
        }

        public string BackendName
        {
            get { return "Unity.InputSystem"; }
        }

        public InputDeviceType CurrentDeviceType
        {
            get { return mCurrentDeviceType; }
        }

        public bool IsGamepadConnected
        {
            get { return Gamepad.all.Count > 0; }
        }

        public InputActionAsset ActionAsset
        {
            get { return mActionAsset; }
        }

        public void Register<T>() where T : class, IInputActionCollection2, new()
        {
            Register(new T());
        }

        public void Register<T>(T instance) where T : class, IInputActionCollection2
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var type = typeof(T);
            if (mInputCollections.ContainsKey(type))
                return;

            mInputCollections[type] = instance;
            if (mActionAsset == null)
                SetActionAsset(ExtractAssetFromCollection(instance), true);
            else
                RebuildActionCache();

            instance.Enable();
        }

        public T Get<T>() where T : class, IInputActionCollection2
        {
            IInputActionCollection2 instance;
            return mInputCollections.TryGetValue(typeof(T), out instance) ? instance as T : null;
        }

        public bool IsRegistered<T>() where T : class, IInputActionCollection2
        {
            return mInputCollections.ContainsKey(typeof(T));
        }

        public void SetActionAsset(InputActionAsset actionAsset)
        {
            SetActionAsset(actionAsset, true);
        }

        public void SetActionAsset(InputActionAsset actionAsset, bool enableAllActionMaps)
        {
            mActionAsset = actionAsset;
            RebuildActionCache();
            if (mActionAsset != null && enableAllActionMaps && !mUseActionMapFilter)
                EnableAllActionMaps();
        }

        public InputAction FindAction(string actionNameOrId)
        {
            return mActionAsset != null && !string.IsNullOrEmpty(actionNameOrId)
                ? mActionAsset.FindAction(actionNameOrId, false)
                : null;
        }

        public InputActionMap FindActionMap(string mapName)
        {
            return mActionAsset != null && !string.IsNullOrEmpty(mapName)
                ? mActionAsset.FindActionMap(mapName, false)
                : null;
        }

        public string GetBindingDisplayString(InputAction action, int bindingIndex = 0)
        {
            return action != null ? action.GetBindingDisplayString(bindingIndex) : string.Empty;
        }

        public string GetBindingDisplayString(InputAction action, string controlScheme)
        {
            if (action == null || string.IsNullOrEmpty(controlScheme))
                return string.Empty;

            var bindingIndex = action.GetBindingIndex(InputBinding.MaskByGroup(controlScheme));
            return bindingIndex >= 0 ? action.GetBindingDisplayString(bindingIndex) : string.Empty;
        }

        public void Poll(IInputStateWriter writer)
        {
            if (writer == null)
                return;

            if (mActions.Count == 0)
                RebuildActionCache();

            for (var i = 0; i < mActions.Count; i++)
            {
                var action = mActions[i];
                if (action == null || !IsActionMapEnabled(action.actionMap))
                    continue;

                var value = ReadActionValue(action);
                var isPressed = IsActionPressed(action, value);
                writer.SetAction(GetActionName(action), isPressed, value);
            }
        }

        public void SetEnabledActionMaps(IReadOnlyList<string> actionMapNames)
        {
            mUseActionMapFilter = true;
            mEnabledActionMaps.Clear();

            if (actionMapNames != null)
            {
                for (var i = 0; i < actionMapNames.Count; i++)
                {
                    if (!string.IsNullOrEmpty(actionMapNames[i]))
                        mEnabledActionMaps.Add(actionMapNames[i]);
                }
            }

            ApplyActionMapState();
        }

        public void Dispose()
        {
            if (mIsDisposed)
                return;

            mIsDisposed = true;
            InputSystem.onActionChange -= OnActionChange;
            foreach (var pair in mInputCollections)
            {
                pair.Value.Disable();
                var disposable = pair.Value as IDisposable;
                if (disposable != null)
                    disposable.Dispose();
            }

            mInputCollections.Clear();
            mActions.Clear();
            mEnabledActionMaps.Clear();
            mActionAsset = null;
        }

        private void EnableAllActionMaps()
        {
            if (mActionAsset == null)
                return;

            for (var i = 0; i < mActionAsset.actionMaps.Count; i++)
                mActionAsset.actionMaps[i].Enable();
        }

        private void ApplyActionMapState()
        {
            if (mActionAsset == null)
                return;

            for (var i = 0; i < mActionAsset.actionMaps.Count; i++)
            {
                var map = mActionAsset.actionMaps[i];
                if (mEnabledActionMaps.Contains(map.name))
                    map.Enable();
                else
                    map.Disable();
            }
        }

        private bool IsActionMapEnabled(InputActionMap map)
        {
            if (map == null)
                return !mUseActionMapFilter;

            if (!mUseActionMapFilter)
                return map.enabled;

            return mEnabledActionMaps.Contains(map.name) && map.enabled;
        }

        private void RebuildActionCache()
        {
            mActions.Clear();
            if (mActionAsset != null)
            {
                for (var i = 0; i < mActionAsset.actionMaps.Count; i++)
                {
                    var map = mActionAsset.actionMaps[i];
                    for (var j = 0; j < map.actions.Count; j++)
                        AddAction(map.actions[j]);
                }
            }

            foreach (var pair in mInputCollections)
            {
                foreach (var action in pair.Value)
                    AddAction(action);
            }
        }

        private void AddAction(InputAction action)
        {
            if (action == null)
                return;

            for (var i = 0; i < mActions.Count; i++)
            {
                if (ReferenceEquals(mActions[i], action))
                    return;
            }

            mActions.Add(action);
        }

        private void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed)
                return;

            var action = obj as InputAction;
            if (action == null || action.activeControl == null)
                return;

            SetCurrentDeviceType(GetDeviceType(action.activeControl.device));
        }

        private void DetectCurrentDevice()
        {
            if (Gamepad.all.Count > 0)
            {
                SetCurrentDeviceType(InputDeviceType.Gamepad);
                return;
            }

            if (Touchscreen.current != null)
            {
                SetCurrentDeviceType(InputDeviceType.Touch);
                return;
            }

            SetCurrentDeviceType(InputDeviceType.KeyboardMouse);
        }

        private void SetCurrentDeviceType(InputDeviceType deviceType)
        {
            if (deviceType != InputDeviceType.Unknown)
                mCurrentDeviceType = deviceType;
        }

        private static InputDeviceType GetDeviceType(InputDevice device)
        {
            if (device is Gamepad)
                return InputDeviceType.Gamepad;
            if (device is Touchscreen)
                return InputDeviceType.Touch;
            if (device is Keyboard || device is Mouse)
                return InputDeviceType.KeyboardMouse;

            return InputDeviceType.Unknown;
        }

        private static InputActionAsset ExtractAssetFromCollection(IInputActionCollection2 collection)
        {
            if (collection == null)
                return null;

            var assetProperty = collection.GetType().GetProperty("asset");
            if (assetProperty != null)
            {
                var asset = assetProperty.GetValue(collection, null) as InputActionAsset;
                if (asset != null)
                    return asset;
            }

            foreach (var action in collection)
            {
                if (action != null && action.actionMap != null && action.actionMap.asset != null)
                    return action.actionMap.asset;
            }

            return null;
        }

        private static string GetActionName(InputAction action)
        {
            if (action == null)
                return string.Empty;
            if (action.actionMap != null && !string.IsNullOrEmpty(action.actionMap.name))
                return action.actionMap.name + "/" + action.name;

            return action.name;
        }

        private static float ReadActionValue(InputAction action)
        {
            if (action == null)
                return 0f;

            try
            {
                if (action.type == InputActionType.Button)
                    return action.IsPressed() ? 1f : 0f;
            }
            catch (InvalidOperationException)
            {
                return 0f;
            }

            try
            {
                return Mathf.Abs(action.ReadValue<float>());
            }
            catch (InvalidOperationException)
            {
            }

            try
            {
                return Mathf.Clamp01(action.ReadValue<Vector2>().magnitude);
            }
            catch (InvalidOperationException)
            {
            }

            try
            {
                return Mathf.Clamp01(action.ReadValue<Vector3>().magnitude);
            }
            catch (InvalidOperationException)
            {
            }

            return SafeIsPressed(action) ? 1f : 0f;
        }

        private static bool IsActionPressed(InputAction action, float value)
        {
            return SafeIsPressed(action) || value > 0.0001f;
        }

        private static bool SafeIsPressed(InputAction action)
        {
            try
            {
                return action != null && action.IsPressed();
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
#else
    /// <summary>
    /// 未安装 Unity Input System 时的 legacy 输入后端，仅作为兼容兜底。
    /// </summary>
    public sealed class UnityInputBackend : IInputBackend, IDisposable
    {
        private readonly Dictionary<string, KeyCode> mButtonBindings = new Dictionary<string, KeyCode>();

        public string BackendName
        {
            get { return "Unity.InputLegacy"; }
        }

        public InputDeviceType CurrentDeviceType
        {
            get
            {
                if (TryGetTouchActive())
                    return InputDeviceType.Touch;

                if (IsGamepadConnected)
                    return InputDeviceType.Gamepad;

                return InputDeviceType.KeyboardMouse;
            }
        }

        public bool IsGamepadConnected
        {
            get
            {
                var joystickNames = TryGetJoystickNames();
                if (joystickNames == null)
                    return false;

                for (var i = 0; i < joystickNames.Length; i++)
                {
                    if (!string.IsNullOrEmpty(joystickNames[i]))
                        return true;
                }

                return false;
            }
        }

        public void BindButton(string actionName, KeyCode keyCode)
        {
            if (string.IsNullOrEmpty(actionName))
                return;

            mButtonBindings[actionName] = keyCode;
        }

        public void UnbindButton(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
                return;

            mButtonBindings.Remove(actionName);
        }

        public void ClearBindings()
        {
            mButtonBindings.Clear();
        }

        public void Poll(IInputStateWriter writer)
        {
            foreach (var binding in mButtonBindings)
            {
                var isPressed = TryGetKey(binding.Value);
                writer.SetAction(binding.Key, isPressed, isPressed ? 1f : 0f);
            }
        }

        public void SetEnabledActionMaps(IReadOnlyList<string> actionMapNames)
        {
        }

        public void Dispose()
        {
            mButtonBindings.Clear();
        }

        private static bool TryGetTouchActive()
        {
            try
            {
                return Input.touchSupported && Input.touchCount > 0;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static string[] TryGetJoystickNames()
        {
            try
            {
                return Input.GetJoystickNames();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static bool TryGetKey(KeyCode keyCode)
        {
            try
            {
                return Input.GetKey(keyCode);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
#endif
}
#endif
