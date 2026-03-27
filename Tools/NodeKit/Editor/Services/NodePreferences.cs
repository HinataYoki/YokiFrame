using UnityEditor;
using UnityEngine;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// NodeKit 偏好设置
    /// </summary>
    public static class NodePreferences
    {
        private const string KEY_PREFIX = "NodeKit_";

        public static float MinZoom
        {
            get => EditorPrefs.GetFloat(KEY_PREFIX + "MinZoom", 0.25f);
            set => EditorPrefs.SetFloat(KEY_PREFIX + "MinZoom", value);
        }

        public static float MaxZoom
        {
            get => EditorPrefs.GetFloat(KEY_PREFIX + "MaxZoom", 5f);
            set => EditorPrefs.SetFloat(KEY_PREFIX + "MaxZoom", value);
        }

        public static float NoodleThickness
        {
            get => EditorPrefs.GetFloat(KEY_PREFIX + "NoodleThickness", 3f);
            set => EditorPrefs.SetFloat(KEY_PREFIX + "NoodleThickness", value);
        }

        public static NoodlePath NoodlePath
        {
            get => (NoodlePath)EditorPrefs.GetInt(KEY_PREFIX + "NoodlePath", 0);
            set => EditorPrefs.SetInt(KEY_PREFIX + "NoodlePath", (int)value);
        }

        public static NoodleStroke NoodleStroke
        {
            get => (NoodleStroke)EditorPrefs.GetInt(KEY_PREFIX + "NoodleStroke", 0);
            set => EditorPrefs.SetInt(KEY_PREFIX + "NoodleStroke", (int)value);
        }

        public static Color HighlightColor
        {
            get
            {
                var hex = EditorPrefs.GetString(KEY_PREFIX + "HighlightColor", "#44BFFF");
                return ColorUtility.TryParseHtmlString(hex, out var c) ? c : new Color(0.27f, 0.75f, 1f);
            }
            set => EditorPrefs.SetString(KEY_PREFIX + "HighlightColor", "#" + ColorUtility.ToHtmlStringRGB(value));
        }

        public static bool AutoSave
        {
            get => EditorPrefs.GetBool(KEY_PREFIX + "AutoSave", true);
            set => EditorPrefs.SetBool(KEY_PREFIX + "AutoSave", value);
        }

        public static bool PortTooltips
        {
            get => EditorPrefs.GetBool(KEY_PREFIX + "PortTooltips", true);
            set => EditorPrefs.SetBool(KEY_PREFIX + "PortTooltips", value);
        }
    }

    public enum NoodlePath { Curvy, Straight, Angled }
    public enum NoodleStroke { Full, Dashed }
}
