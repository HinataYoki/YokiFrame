#if GODOT && TOOLS
using System;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// 作为正式 Godot Editor Adapter，统一拥有 Workbench 菜单与 `godot-editor` Host 生命周期。
    /// </summary>
    [Tool]
    public partial class GodotEditorPlugin : EditorPlugin
    {
        private const string MENU_ITEM = "YokiFrame";
        private const int WORKBENCH_SHORTCUT_ID = 1;
        private const double COMMAND_POLL_INTERVAL_SECONDS = 0.1d;
        // 心跳仅承担低频 FileBridge 存活证明；Runtime Telemetry 不经过磁盘。
        private const double HEARTBEAT_INTERVAL_SECONDS = 5d;

        private GodotEditorFileBridgeHost mFileBridgeHost;
        private PopupMenu mWorkbenchShortcutMenu;
        private double mCommandPollElapsed;
        private double mHeartbeatElapsed;

        /// <summary>
        /// 插件进入 Godot Editor tree 时注册菜单并建立独占 Editor Host 会话。
        /// </summary>
        public override void _EnterTree()
        {
            RegisterWorkbenchMenu();
            StartEditorHost();
            SetProcess(true);
        }

        /// <summary>
        /// 在编辑器帧循环中按 100ms 消费命令，并按 5 秒刷新在线心跳。
        /// </summary>
        /// <param name="delta">当前 Editor 帧间隔秒数。</param>
        public override void _Process(double delta)
        {
            mCommandPollElapsed += delta;
            mHeartbeatElapsed += delta;
            if (mCommandPollElapsed >= COMMAND_POLL_INTERVAL_SECONDS)
            {
                mCommandPollElapsed = 0d;
                ProcessPendingCommands();
            }

            if (mHeartbeatElapsed >= HEARTBEAT_INTERVAL_SECONDS)
            {
                mHeartbeatElapsed = 0d;
                RefreshHeartbeat();
            }
        }

        /// <summary>
        /// 插件退出 Editor tree 时停止轮询、释放在线身份并移除菜单资源。
        /// </summary>
        public override void _ExitTree()
        {
            SetProcess(false);
            StopEditorHost();
            UnregisterWorkbenchMenu();
        }

        /// <summary>
        /// 注册 Project/Tools/YokiFrame 子菜单及 Ctrl+E、Ctrl+Alt+E 两个全局快捷键。
        /// </summary>
        private void RegisterWorkbenchMenu()
        {
            mWorkbenchShortcutMenu = new PopupMenu { Name = "YokiFrameWorkbenchShortcutMenu" };
            var primaryEvent = CreateShortcutEvent(altPressed: false);
            var fallbackEvent = CreateShortcutEvent(altPressed: true);
            Shortcut shortcut = new Shortcut
            {
                ResourceName = "Open Workbench (Ctrl+E / Ctrl+Alt+E)",
                Events = new Godot.Collections.Array(new GodotObject[] { primaryEvent, fallbackEvent })
            };
            mWorkbenchShortcutMenu.AddShortcut(shortcut, WORKBENCH_SHORTCUT_ID, true);
            mWorkbenchShortcutMenu.IdPressed += OnWorkbenchShortcutPressed;
            AddToolSubmenuItem(MENU_ITEM, mWorkbenchShortcutMenu);
        }

        /// <summary>
        /// 创建共享 E 键与 Ctrl 修饰符，并按参数添加 Alt 兜底修饰符。
        /// </summary>
        /// <param name="altPressed">是否添加 Alt 修饰符。</param>
        /// <returns>快捷键输入事件。</returns>
        private static InputEventKey CreateShortcutEvent(bool altPressed)
        {
            return new InputEventKey
            {
                Keycode = Key.E,
                CtrlPressed = true,
                AltPressed = altPressed
            };
        }

        /// <summary>
        /// 移除工具菜单并清空托管引用；PopupMenu 原生生命周期由 Godot EditorPlugin 拥有。
        /// </summary>
        private void UnregisterWorkbenchMenu()
        {
            if (mWorkbenchShortcutMenu == null)
            {
                return;
            }

            mWorkbenchShortcutMenu = null;
            RemoveToolMenuItem(MENU_ITEM);
        }

        /// <summary>
        /// 只响应当前插件注册的 Workbench shortcut ID。
        /// </summary>
        /// <param name="itemId">Godot PopupMenu item ID。</param>
        private void OnWorkbenchShortcutPressed(long itemId)
        {
            if (itemId == WORKBENCH_SHORTCUT_ID)
            {
                OpenWorkbench();
            }
        }

        /// <summary>
        /// 解析受控 Runtime manifest 并启动绑定当前项目的 Workbench。
        /// </summary>
        private void OpenWorkbench()
        {
            var projectRoot = ProjectSettings.GlobalizePath("res://");
            var ownerHandle = ResolveOwnerHandle();
            var processId = GodotWorkbenchLauncher.Launch(projectRoot, ownerHandle, out var errorMessage);
            if (processId <= 0)
            {
                GD.PushError("[YokiFrame] " + errorMessage);
                return;
            }

            GD.Print("[YokiFrame] Workbench launched. PID: " + processId);
        }

        /// <summary>
        /// 在 Windows 返回 Godot 主窗口句柄供 Workbench 建立 owner 关系，其它平台返回 0。
        /// </summary>
        /// <returns>原生主窗口句柄或 0。</returns>
        private static long ResolveOwnerHandle()
        {
            return OS.GetName() == "Windows"
                ? DisplayServer.WindowGetNativeHandle(
                    DisplayServer.HandleType.WindowHandle,
                    (int)DisplayServer.MainWindowId)
                : 0L;
        }

        /// <summary>
        /// 创建并启动当前项目的 Editor Host；失败时清理半初始化状态并保留菜单可用。
        /// </summary>
        private void StartEditorHost()
        {
            StopEditorHost();
            try
            {
                var projectRoot = ProjectSettings.GlobalizePath("res://");
                mFileBridgeHost = new GodotEditorFileBridgeHost(projectRoot, ResolveEngineVersion());
                mFileBridgeHost.Start();
            }
            catch (Exception exception)
            {
                StopEditorHost();
                GD.PushError("[YokiFrame] Failed to start godot-editor Host: " + exception.Message);
            }
        }

        /// <summary>
        /// 读取 Godot 版本字典中的规范 string 字段，缺失时回落到 unknown。
        /// </summary>
        /// <returns>Godot 编辑器版本。</returns>
        private static string ResolveEngineVersion()
        {
            var versionInfo = Engine.GetVersionInfo();
            return versionInfo.TryGetValue("string", out var value)
                ? value.AsString()
                : "unknown";
        }

        /// <summary>
        /// 尝试消费当前 Editor Host 的 FileBridge 命令；宿主未启动时保持静默。
        /// </summary>
        private void ProcessPendingCommands()
        {
            var host = mFileBridgeHost;
            if (host == null || !host.IsRunning)
            {
                return;
            }

            try
            {
                host.ProcessPendingCommands();
            }
            catch (Exception exception)
            {
                GD.PushError("[YokiFrame] godot-editor command pump failed: " + exception.Message);
            }
        }

        /// <summary>
        /// 尝试刷新当前 Editor Host heartbeat；宿主未启动时保持静默。
        /// </summary>
        private void RefreshHeartbeat()
        {
            var host = mFileBridgeHost;
            if (host == null || !host.IsRunning)
            {
                return;
            }

            try
            {
                host.RefreshHeartbeat();
            }
            catch (Exception exception)
            {
                GD.PushError("[YokiFrame] godot-editor heartbeat failed: " + exception.Message);
            }
        }

        /// <summary>
        /// 幂等停止并释放 Editor Host，使 registry 与 heartbeat 不残留为在线状态。
        /// </summary>
        private void StopEditorHost()
        {
            var host = mFileBridgeHost;
            mFileBridgeHost = null;
            if (host == null)
            {
                return;
            }

            try
            {
                host.Dispose();
            }
            catch (Exception exception)
            {
                GD.PushError("[YokiFrame] Failed to stop godot-editor Host: " + exception.Message);
            }
        }
    }
}
#endif
