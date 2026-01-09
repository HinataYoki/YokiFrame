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
            
            mStylesInitialized = true;
        }

        private void DrawOverlayContent()
        {
            // 标题
            GUILayout.Label("UIKit Debug", mHeaderStyle);
            GUILayout.Space(5);
            
            // 面板数量
            DrawInfoLine("活动面板", mCachedPanelCount.ToString());
            
            // 栈深度
            DrawInfoLine("栈深度", mCachedStackDepth.ToString());
            
            // 缓存数量
            DrawInfoLine("缓存面板", mCachedCacheCount.ToString());
            
            // 当前焦点
            DrawInfoLine("当前焦点", mCachedFocusName);
            
            // 输入模式
            DrawInfoLine("输入模式", mCachedInputMode);
            
            GUILayout.FlexibleSpace();
            
            // 提示
            var hotkeyText = mRequireShift ? $"Shift+{mToggleKey}" : mToggleKey.ToString();
            GUILayout.Label($"按 {hotkeyText} 关闭", new GUIStyle(mLabelStyle) { fontSize = 10, normal = { textColor = Color.gray } });
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
            // 活动面板数量
            mCachedPanelCount = 0;
            if (UIRoot.Instance != null)
            {
                var panels = UIRoot.Instance.GetComponentsInChildren<UIPanel>(true);
                for (int i = 0; i < panels.Length; i++)
                {
                    if (panels[i].State != PanelState.Close)
                    {
                        mCachedPanelCount++;
                    }
                }
            }
            
            // 栈深度
            mCachedStackDepth = UIKit.GetStackDepth();
            
            // 缓存数量
            mCachedCacheCount = UIKit.GetCachedPanels().Count;
            
            // 当前焦点
            var focusSystem = UIFocusSystem.Instance;
            if (focusSystem != null && focusSystem.CurrentFocus != null)
            {
                mCachedFocusName = focusSystem.CurrentFocus.name;
                if (mCachedFocusName.Length > 15)
                {
                    mCachedFocusName = mCachedFocusName.Substring(0, 12) + "...";
                }
            }
            else
            {
                mCachedFocusName = "无";
            }
            
            // 输入模式
            mCachedInputMode = focusSystem?.CurrentInputMode.ToString() ?? "未知";
        }

        #endregion
    }
}
