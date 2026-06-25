#if !GODOT
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UI 运行时调试覆盖层 - IMGUI 绘制与数据更新
    /// </summary>
    public partial class UIDebugOverlay
    {
        #region 绘制方法

        private void InitializeStyles()
        {
            if (mStylesInitialized) return;

            // 背景样式
            mBoxStyle = new GUIStyle(GUI.skin.box);
            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, mBackgroundAlpha));
            bgTex.Apply();
            mBoxStyle.normal.background = bgTex;

            // 标签样式
            mLabelStyle = new GUIStyle(GUI.skin.label);
            mLabelStyle.fontSize = mFontSize;
            mLabelStyle.normal.textColor = Color.white;

            // 标题样式
            mHeaderStyle = new GUIStyle(mLabelStyle);
            mHeaderStyle.fontStyle = FontStyle.Bold;
            mHeaderStyle.normal.textColor = new Color(0.3f, 0.8f, 1f);

            // 提示样式
            mHintStyle = new GUIStyle(mLabelStyle);
            mHintStyle.fontSize = 10;
            mHintStyle.normal.textColor = Color.gray;

            mStylesInitialized = true;
        }

        private void DrawOverlayContent()
        {
            // 标题
            GUILayout.Label("UIKit Debug", mHeaderStyle);
            GUILayout.Space(5);

            // 面板数量
            DrawInfoLine("活动面板", mCachedPanelCountStr);

            // 栈深度
            DrawInfoLine("栈深度", mCachedStackDepthStr);

            // 缓存数量
            DrawInfoLine("缓存面板", mCachedCacheCountStr);

            // 当前焦点
            DrawInfoLine("当前焦点", mCachedFocusName);

            // 输入模式
            DrawInfoLine("输入模式", mCachedInputMode);

            GUILayout.FlexibleSpace();

            // 提示
            GUILayout.Label(mCachedHotkeyText, mHintStyle);
        }

        private void DrawInfoLine(string label, string value)
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label($"{label}:", mLabelStyle, GUILayout.Width(80));
                GUILayout.Label(value, mLabelStyle);
            }
            GUILayout.EndHorizontal();
        }

        #endregion

        #region 数据更新

        private void UpdateCachedData()
        {
            // 活动面板数量（非分配 GetComponentsInChildren）
            mCachedPanelCount = 0;
            if (UIRoot.Instance != default)
            {
                UIRoot.Instance.GetComponentsInChildren(true, sPanelBuffer);
                for (int i = 0; i < sPanelBuffer.Count; i++)
                {
                    if (sPanelBuffer[i].State != PanelState.Close)
                    {
                        mCachedPanelCount++;
                    }
                }
            }

            // 栈深度
            mCachedStackDepth = UIKit.GetStackDepth();

            // 缓存数量
            mCachedCacheCount = UIKit.GetCachedPanels().Count;

            // 缓存 ToString 结果
            mCachedPanelCountStr = mCachedPanelCount.ToString();
            mCachedStackDepthStr = mCachedStackDepth.ToString();
            mCachedCacheCountStr = mCachedCacheCount.ToString();

            // 当前焦点（Substring 替换为 AsSpan 截断，避免分配）
            if (UIRoot.Instance != default)
            {
                var currentFocus = UIRoot.Instance.CurrentFocus;
                if (currentFocus != default)
                {
                    var name = currentFocus.name;
                    mCachedFocusName = name.Length > 15
                        ? name.Substring(0, 12) + "..."
                        : name;
                }
                else
                {
                    mCachedFocusName = "无";
                }

                // 输入模式
                mCachedInputMode = UIRoot.Instance.CurrentInputMode.ToString();
            }
            else
            {
                mCachedFocusName = "无";
                mCachedInputMode = "Unknown";
            }

            // 热键提示文本
            mCachedHotkeyText = mRequireShift
                ? string.Concat("按 Shift+", mToggleKey.ToString(), " 关闭")
                : string.Concat("按 ", mToggleKey.ToString(), " 关闭");
        }

        #endregion
    }
}
#endif
