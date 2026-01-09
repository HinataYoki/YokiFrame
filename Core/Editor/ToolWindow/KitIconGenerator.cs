#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Kit 图标生成器
    /// 在编辑器中动态生成矢量风格图标，避免 Emoji 字体兼容性问题
    /// </summary>
    [InitializeOnLoad]
    public static partial class KitIconGenerator
    {
        private static readonly Dictionary<string, Texture2D> sIconCache = new();
        private const int ICON_SIZE = 32;

        static KitIconGenerator()
        {
            // 编辑器加载时预生成图标
            EditorApplication.delayCall += GenerateAllIcons;
        }

        /// <summary>
        /// 获取图标纹理
        /// </summary>
        public static Texture2D GetIcon(string iconId)
        {
            if (sIconCache.TryGetValue(iconId, out var cached) && cached != null)
                return cached;

            var icon = GenerateIcon(iconId);
            if (icon != null)
                sIconCache[iconId] = icon;
            return icon;
        }

        /// <summary>
        /// 预生成所有图标
        /// </summary>
        private static void GenerateAllIcons()
        {
            // Core
            GenerateIcon(KitIcons.ARCHITECTURE);
            GenerateIcon(KitIcons.RESKIT);
            GenerateIcon(KitIcons.KITLOGGER);
            GenerateIcon(KitIcons.CODEGEN);
            // Core Kit
            GenerateIcon(KitIcons.EVENTKIT);
            GenerateIcon(KitIcons.FSMKIT);
            GenerateIcon(KitIcons.POOLKIT);
            GenerateIcon(KitIcons.SINGLETON);
            GenerateIcon(KitIcons.FLUENTAPI);
            GenerateIcon(KitIcons.TOOLCLASS);
            // Tools
            GenerateIcon(KitIcons.ACTIONKIT);
            GenerateIcon(KitIcons.AUDIOKIT);
            GenerateIcon(KitIcons.BUFFKIT);
            GenerateIcon(KitIcons.LOCALIZATIONKIT);
            GenerateIcon(KitIcons.SAVEKIT);
            GenerateIcon(KitIcons.SCENEKIT);
            GenerateIcon(KitIcons.TABLEKIT);
            GenerateIcon(KitIcons.UIKIT);
            // Special
            GenerateIcon(KitIcons.DOCUMENTATION);
            // UI 操作图标
            GenerateIcon(KitIcons.POPOUT);
            GenerateIcon(KitIcons.FOLDER_DOCS);
            GenerateIcon(KitIcons.FOLDER_TOOLS);
            GenerateIcon(KitIcons.TIP);
            // 分类图标
            GenerateIcon(KitIcons.CATEGORY_CORE);
            GenerateIcon(KitIcons.CATEGORY_COREKIT);
            GenerateIcon(KitIcons.CATEGORY_TOOLS);
            // 链接图标
            GenerateIcon(KitIcons.PACKAGE);
            GenerateIcon(KitIcons.GITHUB);
            // 通用操作图标
            GenerateIcon(KitIcons.REFRESH);
            GenerateIcon(KitIcons.COPY);
            GenerateIcon(KitIcons.DELETE);
            GenerateIcon(KitIcons.PLAY);
            GenerateIcon(KitIcons.PAUSE);
            GenerateIcon(KitIcons.STOP);
            GenerateIcon(KitIcons.EXPAND);
            // 状态图标
            GenerateIcon(KitIcons.SUCCESS);
            GenerateIcon(KitIcons.WARNING);
            GenerateIcon(KitIcons.ERROR);
            GenerateIcon(KitIcons.INFO);
            // 箭头图标
            GenerateIcon(KitIcons.ARROW_RIGHT);
            GenerateIcon(KitIcons.ARROW_DOWN);
            GenerateIcon(KitIcons.ARROW_LEFT);
            GenerateIcon(KitIcons.CHEVRON_RIGHT);
            GenerateIcon(KitIcons.CHEVRON_DOWN);
            // 数据流图标
            GenerateIcon(KitIcons.SEND);
            GenerateIcon(KitIcons.RECEIVE);
            GenerateIcon(KitIcons.EVENT);
            // 其他图标
            GenerateIcon(KitIcons.CLIPBOARD);
            GenerateIcon(KitIcons.STACK);
            GenerateIcon(KitIcons.TARGET);
            GenerateIcon(KitIcons.CACHE);
            GenerateIcon(KitIcons.CHART);
            GenerateIcon(KitIcons.MUSIC);
            GenerateIcon(KitIcons.VOLUME);
            GenerateIcon(KitIcons.TIMELINE);
            GenerateIcon(KitIcons.DOT);
            GenerateIcon(KitIcons.SCROLL);
            GenerateIcon(KitIcons.LISTENER);
            GenerateIcon(KitIcons.DOCUMENT);
            GenerateIcon(KitIcons.SETTINGS);
            GenerateIcon(KitIcons.RESET);
            GenerateIcon(KitIcons.LOCATION);
            GenerateIcon(KitIcons.CODE);
            GenerateIcon(KitIcons.FOLDER);
            GenerateIcon(KitIcons.CLOCK);
            GenerateIcon(KitIcons.CHECK);
            GenerateIcon(KitIcons.DOT_FILLED);
            GenerateIcon(KitIcons.DOT_EMPTY);
            GenerateIcon(KitIcons.DOT_HALF);
            GenerateIcon(KitIcons.DIAMOND);
            GenerateIcon(KitIcons.TRIANGLE_UP);
            GenerateIcon(KitIcons.TRIANGLE_DOWN);
        }

        private static Texture2D GenerateIcon(string iconId)
        {
            if (sIconCache.TryGetValue(iconId, out var cached) && cached != null)
                return cached;

            var tex = new Texture2D(ICON_SIZE, ICON_SIZE, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            // 清空为透明
            var pixels = new Color32[ICON_SIZE * ICON_SIZE];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);
            tex.SetPixels32(pixels);

            // 根据图标 ID 绘制
            DrawIconShape(tex, iconId);

            tex.Apply();
            sIconCache[iconId] = tex;
            return tex;
        }

        /// <summary>
        /// 根据图标 ID 绘制对应形状（路由到具体绘制方法）
        /// </summary>
        private static void DrawIconShape(Texture2D tex, string iconId)
        {
            // 定义颜色
            var blue = new Color32(66, 133, 244, 255);
            var green = new Color32(52, 168, 83, 255);
            var orange = new Color32(251, 188, 4, 255);
            var red = new Color32(234, 67, 53, 255);
            var purple = new Color32(156, 39, 176, 255);
            var cyan = new Color32(0, 188, 212, 255);
            var teal = new Color32(0, 150, 136, 255);
            var pink = new Color32(233, 30, 99, 255);

            switch (iconId)
            {
                // Core
                case KitIcons.ARCHITECTURE:
                    DrawArchitectureIcon(tex, blue);
                    break;
                case KitIcons.RESKIT:
                    DrawBoxIcon(tex, orange);
                    break;
                case KitIcons.KITLOGGER:
                    DrawDocumentIcon(tex, green);
                    break;
                case KitIcons.CODEGEN:
                    DrawGearIcon(tex, purple);
                    break;

                // Core Kit
                case KitIcons.EVENTKIT:
                    DrawSignalIcon(tex, cyan);
                    break;
                case KitIcons.FSMKIT:
                    DrawCycleIcon(tex, blue);
                    break;
                case KitIcons.POOLKIT:
                    DrawRecycleIcon(tex, green);
                    break;
                case KitIcons.SINGLETON:
                    DrawTargetIcon(tex, red);
                    break;
                case KitIcons.FLUENTAPI:
                    DrawChainIcon(tex, purple);
                    break;
                case KitIcons.TOOLCLASS:
                    DrawToolboxIcon(tex, orange);
                    break;

                // Tools
                case KitIcons.ACTIONKIT:
                    DrawLightningIcon(tex, orange);
                    break;
                case KitIcons.AUDIOKIT:
                    DrawSpeakerIcon(tex, blue);
                    break;
                case KitIcons.BUFFKIT:
                    DrawSparkleIcon(tex, pink);
                    break;
                case KitIcons.LOCALIZATIONKIT:
                    DrawGlobeIcon(tex, cyan);
                    break;
                case KitIcons.SAVEKIT:
                    DrawDiskIcon(tex, blue);
                    break;
                case KitIcons.SCENEKIT:
                    DrawClapperIcon(tex, red);
                    break;
                case KitIcons.TABLEKIT:
                    DrawChartIcon(tex, green);
                    break;
                case KitIcons.UIKIT:
                    DrawFrameIcon(tex, teal);
                    break;

                // Special
                case KitIcons.DOCUMENTATION:
                    DrawBookIcon(tex, purple);
                    break;

                // UI 操作图标
                case KitIcons.POPOUT:
                    DrawPopoutIcon(tex, new Color32(180, 180, 180, 255));
                    break;
                case KitIcons.FOLDER_DOCS:
                    DrawFolderDocsIcon(tex, new Color32(100, 149, 237, 255));
                    break;
                case KitIcons.FOLDER_TOOLS:
                    DrawFolderToolsIcon(tex, new Color32(255, 165, 0, 255));
                    break;
                case KitIcons.TIP:
                    DrawTipIcon(tex, new Color32(255, 200, 50, 255));
                    break;
                case KitIcons.CATEGORY_CORE:
                    DrawCategoryIcon(tex, new Color32(100, 149, 237, 255), "C");
                    break;
                case KitIcons.CATEGORY_COREKIT:
                    DrawCategoryIcon(tex, new Color32(147, 112, 219, 255), "K");
                    break;
                case KitIcons.CATEGORY_TOOLS:
                    DrawCategoryIcon(tex, new Color32(255, 165, 0, 255), "T");
                    break;
                case KitIcons.PACKAGE:
                    DrawPackageIcon(tex, new Color32(150, 150, 160, 255));
                    break;
                case KitIcons.GITHUB:
                    DrawGitHubIcon(tex, new Color32(140, 140, 150, 255));
                    break;

                // 通用操作图标
                case KitIcons.REFRESH:
                    DrawRefreshIcon(tex, new Color32(100, 180, 100, 255));
                    break;
                case KitIcons.COPY:
                    DrawCopyIcon(tex, new Color32(150, 150, 160, 255));
                    break;
                case KitIcons.DELETE:
                    DrawDeleteIcon(tex, new Color32(220, 80, 80, 255));
                    break;
                case KitIcons.PLAY:
                    DrawPlayIcon(tex, new Color32(100, 200, 100, 255));
                    break;
                case KitIcons.PAUSE:
                    DrawPauseIcon(tex, new Color32(200, 180, 80, 255));
                    break;
                case KitIcons.STOP:
                    DrawStopIcon(tex, new Color32(200, 80, 80, 255));
                    break;
                case KitIcons.EXPAND:
                    DrawExpandIcon(tex, new Color32(150, 150, 160, 255));
                    break;

                // 状态图标
                case KitIcons.SUCCESS:
                    DrawSuccessIcon(tex, new Color32(100, 200, 100, 255));
                    break;
                case KitIcons.WARNING:
                    DrawWarningIcon(tex, new Color32(255, 180, 50, 255));
                    break;
                case KitIcons.ERROR:
                    DrawErrorIcon(tex, new Color32(220, 80, 80, 255));
                    break;
                case KitIcons.INFO:
                    DrawInfoIcon(tex, new Color32(100, 150, 220, 255));
                    break;

                // 箭头图标
                case KitIcons.ARROW_RIGHT:
                    DrawArrowIcon(tex, new Color32(150, 150, 160, 255), 0);
                    break;
                case KitIcons.ARROW_DOWN:
                    DrawArrowIcon(tex, new Color32(150, 150, 160, 255), 1);
                    break;
                case KitIcons.ARROW_LEFT:
                    DrawArrowIcon(tex, new Color32(150, 150, 160, 255), 2);
                    break;
                case KitIcons.CHEVRON_RIGHT:
                    DrawChevronIcon(tex, new Color32(150, 150, 160, 255), 0);
                    break;
                case KitIcons.CHEVRON_DOWN:
                    DrawChevronIcon(tex, new Color32(150, 150, 160, 255), 1);
                    break;

                // 数据流图标
                case KitIcons.SEND:
                    DrawSendIcon(tex, new Color32(255, 120, 100, 255));
                    break;
                case KitIcons.RECEIVE:
                    DrawReceiveIcon(tex, new Color32(100, 220, 150, 255));
                    break;
                case KitIcons.EVENT:
                    DrawEventIcon(tex, new Color32(255, 200, 80, 255));
                    break;

                // 其他图标
                case KitIcons.CLIPBOARD:
                    DrawClipboardIcon(tex, new Color32(150, 150, 160, 255));
                    break;
                case KitIcons.STACK:
                    DrawStackIcon(tex, purple);
                    break;
                case KitIcons.TARGET:
                    DrawTargetIcon(tex, red);
                    break;
                case KitIcons.CACHE:
                    DrawCacheIcon(tex, blue);
                    break;
                case KitIcons.CHART:
                    DrawChartIcon(tex, green);
                    break;
                case KitIcons.MUSIC:
                    DrawMusicIcon(tex, purple);
                    break;
                case KitIcons.VOLUME:
                    DrawVolumeIcon(tex, blue);
                    break;
                case KitIcons.TIMELINE:
                    DrawTimelineIcon(tex, cyan);
                    break;
                case KitIcons.DOT:
                    DrawDotIcon(tex, green);
                    break;
                case KitIcons.SCROLL:
                    DrawScrollIcon(tex, cyan);
                    break;
                case KitIcons.LISTENER:
                    DrawListenerIcon(tex, green);
                    break;
                case KitIcons.DOCUMENT:
                    DrawDocumentIcon(tex, blue);
                    break;
                case KitIcons.SETTINGS:
                    DrawSettingsIcon(tex, new Color32(150, 150, 160, 255));
                    break;
                case KitIcons.RESET:
                    DrawResetIcon(tex, new Color32(150, 150, 160, 255));
                    break;
                case KitIcons.LOCATION:
                    DrawLocationIcon(tex, new Color32(220, 80, 80, 255));
                    break;
                case KitIcons.CODE:
                    DrawCodeIcon(tex, new Color32(100, 150, 220, 255));
                    break;
                case KitIcons.FOLDER:
                    DrawFolderIcon(tex, new Color32(255, 200, 80, 255));
                    break;
                case KitIcons.CLOCK:
                    DrawClockIcon(tex, new Color32(150, 150, 160, 255));
                    break;
                case KitIcons.CHECK:
                    DrawCheckIcon(tex, new Color32(100, 200, 100, 255));
                    break;
                case KitIcons.DOT_FILLED:
                    DrawDotFilledIcon(tex, new Color32(150, 150, 160, 255));
                    break;
                case KitIcons.DOT_EMPTY:
                    DrawDotEmptyIcon(tex, new Color32(150, 150, 160, 255));
                    break;
                case KitIcons.DOT_HALF:
                    DrawDotHalfIcon(tex, new Color32(150, 150, 160, 255));
                    break;
                case KitIcons.DIAMOND:
                    DrawDiamondIcon(tex, new Color32(150, 150, 160, 255));
                    break;
                case KitIcons.TRIANGLE_UP:
                    DrawTriangleUpIcon(tex, new Color32(150, 150, 160, 255));
                    break;
                case KitIcons.TRIANGLE_DOWN:
                    DrawTriangleDownIcon(tex, new Color32(150, 150, 160, 255));
                    break;

                default:
                    DrawDefaultIcon(tex, blue);
                    break;
            }
        }
    }
}
#endif
