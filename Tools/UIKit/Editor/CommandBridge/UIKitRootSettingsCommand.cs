#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// Tauri 工作台读写 UIKitSettings 的 Unity Editor 命令。
    /// </summary>
    internal static class UIKitRootSettingsCommand
    {
        [Serializable]
        private sealed class SettingsPayload
        {
            public string RenderMode;
            public int SortOrder;
            public int TargetDisplay;
            public bool PixelPerfect;
            public string ScaleMode;
            public float ReferenceResolutionX;
            public float ReferenceResolutionY;
            public string ScreenMatchMode;
            public float MatchWidthOrHeight;
            public float ReferencePixelsPerUnit;
            public string PhysicalUnit;
            public float FallbackScreenDPI;
            public float DefaultSpriteDPI;
            public float DynamicPixelsPerUnit;
            public bool IgnoreReversedGraphics;
            public string BlockingObjects;
            public int BlockingMask;
        }

        public static string BuildSettingsJson(string message = null)
        {
            return BuildResponseJson(UIKitSettings.Instance, message);
        }

        public static string SaveSettings(string payloadJson)
        {
            var payload = FromJson(payloadJson);
            var settings = UIKitSettings.Instance;
            Undo.RecordObject(settings, "Update UIKit Settings");
            ApplyPayload(settings, payload);
            SaveAsset(settings);
            return BuildResponseJson(settings, "UIKit 配置已保存，下次 UIRoot 初始化时生效。");
        }

        public static string ResetSettings()
        {
            var settings = UIKitSettings.Instance;
            Undo.RecordObject(settings, "Reset UIKit Settings");
            settings.ResetToDefault();
            SaveAsset(settings);
            return BuildResponseJson(settings, "UIKit 配置已重置为默认值。");
        }

        private static SettingsPayload FromJson(string payloadJson)
        {
            if (string.IsNullOrEmpty(payloadJson) || payloadJson.Trim() == "{}")
                throw new InvalidOperationException("缺少 UIKit UIRoot 设置 payload。");

            var payload = new SettingsPayload();
            JsonUtility.FromJsonOverwrite(payloadJson, payload);
            return payload;
        }

        private static void ApplyPayload(UIKitSettings settings, SettingsPayload payload)
        {
            settings.RenderMode = ParseEnum(payload.RenderMode, settings.RenderMode);
            settings.SortOrder = payload.SortOrder;
            settings.TargetDisplay = Math.Max(0, payload.TargetDisplay);
            settings.PixelPerfect = payload.PixelPerfect;

            settings.ScaleMode = ParseEnum(payload.ScaleMode, settings.ScaleMode);
            settings.ReferenceResolution = new Vector2(
                SanitizePositive(payload.ReferenceResolutionX, settings.ReferenceResolution.x),
                SanitizePositive(payload.ReferenceResolutionY, settings.ReferenceResolution.y));
            settings.ScreenMatchMode = ParseEnum(payload.ScreenMatchMode, settings.ScreenMatchMode);
            settings.MatchWidthOrHeight = Mathf.Clamp01(SanitizeFinite(payload.MatchWidthOrHeight, settings.MatchWidthOrHeight));
            settings.ReferencePixelsPerUnit = SanitizePositive(payload.ReferencePixelsPerUnit, settings.ReferencePixelsPerUnit);
            settings.PhysicalUnit = ParseEnum(payload.PhysicalUnit, settings.PhysicalUnit);
            settings.FallbackScreenDPI = SanitizePositive(payload.FallbackScreenDPI, settings.FallbackScreenDPI);
            settings.DefaultSpriteDPI = SanitizePositive(payload.DefaultSpriteDPI, settings.DefaultSpriteDPI);
            settings.DynamicPixelsPerUnit = SanitizePositive(payload.DynamicPixelsPerUnit, settings.DynamicPixelsPerUnit);

            settings.IgnoreReversedGraphics = payload.IgnoreReversedGraphics;
            settings.BlockingObjects = ParseEnum(payload.BlockingObjects, settings.BlockingObjects);
            settings.BlockingMask = payload.BlockingMask;
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            if (string.IsNullOrEmpty(value))
                return fallback;

            T parsed;
            return Enum.TryParse(value, true, out parsed) ? parsed : fallback;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        private static float SanitizePositive(float value, float fallback)
        {
            var finite = SanitizeFinite(value, fallback);
            return finite > 0f ? finite : fallback;
        }

        private static void SaveAsset(UIKitSettings settings)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static string BuildResponseJson(UIKitSettings settings, string message)
        {
            var assetPath = AssetDatabase.GetAssetPath(settings);
            var sb = new StringBuilder(512);
            sb.Append("{\"available\":true,\"assetPath\":\"");
            sb.Append(JsonHelper.EscapeString(assetPath));
            sb.Append("\",\"settings\":");
            AppendSettingsJson(sb, settings);
            if (!string.IsNullOrEmpty(message))
            {
                sb.Append(",\"message\":\"");
                sb.Append(JsonHelper.EscapeString(message));
                sb.Append('"');
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendSettingsJson(StringBuilder sb, UIKitSettings settings)
        {
            sb.Append("{\"renderMode\":\"");
            sb.Append(JsonHelper.EscapeString(settings.RenderMode.ToString()));
            sb.Append("\",\"sortOrder\":");
            sb.Append(settings.SortOrder);
            sb.Append(",\"targetDisplay\":");
            sb.Append(settings.TargetDisplay);
            sb.Append(",\"pixelPerfect\":");
            sb.Append(settings.PixelPerfect ? "true" : "false");
            sb.Append(",\"scaleMode\":\"");
            sb.Append(JsonHelper.EscapeString(settings.ScaleMode.ToString()));
            sb.Append("\",\"referenceResolutionX\":");
            AppendFloat(sb, settings.ReferenceResolution.x);
            sb.Append(",\"referenceResolutionY\":");
            AppendFloat(sb, settings.ReferenceResolution.y);
            sb.Append(",\"screenMatchMode\":\"");
            sb.Append(JsonHelper.EscapeString(settings.ScreenMatchMode.ToString()));
            sb.Append("\",\"matchWidthOrHeight\":");
            AppendFloat(sb, settings.MatchWidthOrHeight);
            sb.Append(",\"referencePixelsPerUnit\":");
            AppendFloat(sb, settings.ReferencePixelsPerUnit);
            sb.Append(",\"physicalUnit\":\"");
            sb.Append(JsonHelper.EscapeString(settings.PhysicalUnit.ToString()));
            sb.Append("\",\"fallbackScreenDPI\":");
            AppendFloat(sb, settings.FallbackScreenDPI);
            sb.Append(",\"defaultSpriteDPI\":");
            AppendFloat(sb, settings.DefaultSpriteDPI);
            sb.Append(",\"dynamicPixelsPerUnit\":");
            AppendFloat(sb, settings.DynamicPixelsPerUnit);
            sb.Append(",\"ignoreReversedGraphics\":");
            sb.Append(settings.IgnoreReversedGraphics ? "true" : "false");
            sb.Append(",\"blockingObjects\":\"");
            sb.Append(JsonHelper.EscapeString(settings.BlockingObjects.ToString()));
            sb.Append("\",\"blockingMask\":");
            sb.Append(settings.BlockingMask.value);
            sb.Append('}');
        }

        private static void AppendFloat(StringBuilder sb, float value)
        {
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
#endif
