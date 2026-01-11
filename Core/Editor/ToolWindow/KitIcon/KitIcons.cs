#if UNITY_EDITOR
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 各 Kit 的统一图标定义
    /// 使用字符串 ID 标识图标，通过 KitIconGenerator 生成实际纹理
    /// </summary>
    public static class KitIcons
    {
        #region Kit 图标

        // Core
        public const string ARCHITECTURE = "architecture";
        public const string RESKIT = "reskit";
        public const string KITLOGGER = "kitlogger";
        public const string CODEGEN = "codegen";
        
        // Core Kit
        public const string EVENTKIT = "eventkit";
        public const string FSMKIT = "fsmkit";
        public const string POOLKIT = "poolkit";
        public const string SINGLETON = "singleton";
        public const string FLUENTAPI = "fluentapi";
        public const string TOOLCLASS = "toolclass";
        
        // Tools
        public const string ACTIONKIT = "actionkit";
        public const string AUDIOKIT = "audiokit";
        public const string BUFFKIT = "buffkit";
        public const string INPUTKIT = "inputkit";
        public const string LOCALIZATIONKIT = "localizationkit";
        public const string SAVEKIT = "savekit";
        public const string SCENEKIT = "scenekit";
        public const string TABLEKIT = "tablekit";
        public const string UIKIT = "uikit";
        
        // Special
        public const string DOCUMENTATION = "documentation";

        #endregion

        #region UI 操作图标

        public const string POPOUT = "popout";
        public const string FOLDER_DOCS = "folder_docs";
        public const string FOLDER_TOOLS = "folder_tools";
        public const string TIP = "tip";
        public const string PACKAGE = "package";
        public const string GITHUB = "github";
        
        // 分类图标
        public const string CATEGORY_CORE = "category_core";
        public const string CATEGORY_COREKIT = "category_corekit";
        public const string CATEGORY_TOOLS = "category_tools";

        #endregion

        #region 通用操作图标

        // 操作
        public const string REFRESH = "refresh";
        public const string COPY = "copy";
        public const string DELETE = "delete";
        public const string PLAY = "play";
        public const string PAUSE = "pause";
        public const string STOP = "stop";
        public const string EXPAND = "expand";
        
        // 状态
        public const string SUCCESS = "success";
        public const string WARNING = "warning";
        public const string ERROR = "error";
        public const string INFO = "info";
        
        // 箭头
        public const string ARROW_RIGHT = "arrow_right";
        public const string ARROW_DOWN = "arrow_down";
        public const string ARROW_LEFT = "arrow_left";
        public const string CHEVRON_RIGHT = "chevron_right";
        public const string CHEVRON_DOWN = "chevron_down";
        
        // 数据流
        public const string SEND = "send";
        public const string RECEIVE = "receive";
        public const string EVENT = "event";
        
        // 其他
        public const string CLIPBOARD = "clipboard";
        public const string STACK = "stack";
        public const string TARGET = "target";
        public const string CACHE = "cache";
        public const string CHART = "chart";
        public const string MUSIC = "music";
        public const string VOLUME = "volume";
        public const string TIMELINE = "timeline";
        public const string DOT = "dot";
        public const string SCROLL = "scroll";
        public const string LISTENER = "listener";
        public const string DOCUMENT = "document";
        public const string SETTINGS = "settings";
        public const string RESET = "reset";
        public const string LOCATION = "location";
        public const string CODE = "code";
        public const string FOLDER = "folder";
        public const string CLOCK = "clock";
        public const string CHECK = "check";
        public const string GAMEPAD = "gamepad";
        public const string KEYBOARD = "keyboard";
        public const string TOUCH = "touch";
        
        // 状态点图标
        public const string DOT_FILLED = "dot_filled";
        public const string DOT_EMPTY = "dot_empty";
        public const string DOT_HALF = "dot_half";
        public const string DIAMOND = "diamond";
        public const string TRIANGLE_UP = "triangle_up";
        public const string TRIANGLE_DOWN = "triangle_down";

        #endregion

        /// <summary>
        /// 获取图标纹理
        /// </summary>
        public static Texture2D GetTexture(string iconId) => KitIconGenerator.GetIcon(iconId);
    }
}
#endif
