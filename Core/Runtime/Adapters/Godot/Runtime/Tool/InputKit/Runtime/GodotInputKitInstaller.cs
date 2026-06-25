#if GODOT
using Godot;
using System;
using System.Collections.Generic;
using YokiFrame;
using InputKitApi = YokiFrame.InputKit;

namespace YokiFrame.Godot
{
    /// <summary>
    /// 将 Godot 输入后端注入 InputKit，保持 Unity/Godot 共用静态入口。
    /// </summary>
    public static class GodotInputKitInstaller
    {
        private static GodotInputBackend sBackend;

        public static void Install(IResourceProvider provider)
        {
            sBackend = new GodotInputBackend();
            InputKitApi.SetBackend(sBackend);
        }

        public static bool Tick(float deltaSeconds)
        {
            InputKitApi.Update(Time.GetTicksMsec() / 1000.0f);
            return true;
        }

        public static GodotInputBackend GetBackend()
        {
            return sBackend;
        }
    }

    /// <summary>
    /// InputKit 的 Godot InputMap 后端。
    /// </summary>
    public sealed class GodotInputBackend : IInputBackend
    {
        private readonly List<string> mActions = new List<string>(64);
        private readonly HashSet<string> mEnabledActionMaps = new HashSet<string>(StringComparer.Ordinal);
        private bool mUseActionMapFilter;
        private InputDeviceType mCurrentDeviceType = InputDeviceType.Unknown;

        public GodotInputBackend()
        {
            RefreshActions();
        }

        public string BackendName
        {
            get { return "Godot.InputMap"; }
        }

        public InputDeviceType CurrentDeviceType
        {
            get { return mCurrentDeviceType; }
        }

        public bool IsGamepadConnected
        {
            get { return Input.GetConnectedJoypads().Count > 0; }
        }

        public void RefreshActions()
        {
            mActions.Clear();
            var actions = InputMap.GetActions();
            for (var i = 0; i < actions.Count; i++)
            {
                var actionName = actions[i].ToString();
                if (!string.IsNullOrEmpty(actionName))
                    mActions.Add(actionName);
            }
        }

        public void Poll(IInputStateWriter writer)
        {
            if (writer == null)
                return;

            if (mActions.Count == 0)
                RefreshActions();

            var anyPressed = false;
            for (var i = 0; i < mActions.Count; i++)
            {
                var actionName = mActions[i];
                if (!IsActionMapEnabled(actionName))
                    continue;

                var pressed = Input.IsActionPressed(actionName);
                var strength = Input.GetActionStrength(actionName);
                writer.SetAction(actionName, pressed, strength);
                anyPressed |= pressed || strength > 0.0001f;
            }

            UpdateCurrentDevice(anyPressed);
        }

        public void SetEnabledActionMaps(IReadOnlyList<string> actionMapNames)
        {
            mUseActionMapFilter = true;
            mEnabledActionMaps.Clear();

            if (actionMapNames == null)
                return;

            for (var i = 0; i < actionMapNames.Count; i++)
            {
                if (!string.IsNullOrEmpty(actionMapNames[i]))
                    mEnabledActionMaps.Add(actionMapNames[i]);
            }
        }

        private bool IsActionMapEnabled(string actionName)
        {
            if (!mUseActionMapFilter)
                return true;

            foreach (var mapName in mEnabledActionMaps)
            {
                if (IsActionInMap(actionName, mapName))
                    return true;
            }

            return false;
        }

        private static bool IsActionInMap(string actionName, string mapName)
        {
            if (string.IsNullOrEmpty(actionName) || string.IsNullOrEmpty(mapName))
                return false;

            return actionName.StartsWith(mapName + "/", StringComparison.Ordinal)
                || actionName.StartsWith(mapName + ".", StringComparison.Ordinal)
                || string.Equals(actionName, mapName, StringComparison.Ordinal);
        }

        private void UpdateCurrentDevice(bool anyPressed)
        {
            if (IsGamepadConnected)
            {
                mCurrentDeviceType = InputDeviceType.Gamepad;
                return;
            }

            mCurrentDeviceType = anyPressed ? InputDeviceType.KeyboardMouse : InputDeviceType.Unknown;
        }
    }
}
#endif
