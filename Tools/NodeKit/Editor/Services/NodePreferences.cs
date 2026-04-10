using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.NodeKit.Editor
{
    public static class NodePreferences
    {
        private const string KeyPrefix = "NodeKit_";
        private static readonly Dictionary<string, Color> sDefaultTypeColors = new()
        {
            { "float", new Color(0.5f, 0.8f, 0.5f) },
            { "int", new Color(0.5f, 0.7f, 0.9f) },
            { "bool", new Color(0.9f, 0.5f, 0.5f) },
            { "string", new Color(0.9f, 0.7f, 0.5f) },
            { "Vector2", new Color(0.9f, 0.9f, 0.5f) },
            { "Vector3", new Color(0.9f, 0.9f, 0.5f) },
            { "Vector4", new Color(0.9f, 0.9f, 0.5f) },
            { "Color", new Color(0.9f, 0.5f, 0.9f) },
            { "GameObject", new Color(0.5f, 0.9f, 0.9f) },
            { "Object", new Color(0.7f, 0.7f, 0.7f) },
        };

        public static float MinZoom
        {
            get => EditorPrefs.GetFloat(KeyPrefix + "MinZoom", 0.25f);
            set => EditorPrefs.SetFloat(KeyPrefix + "MinZoom", value);
        }

        public static float MaxZoom
        {
            get => EditorPrefs.GetFloat(KeyPrefix + "MaxZoom", 5f);
            set => EditorPrefs.SetFloat(KeyPrefix + "MaxZoom", value);
        }

        public static float NoodleThickness
        {
            get => EditorPrefs.GetFloat(KeyPrefix + "NoodleThickness", 3f);
            set => EditorPrefs.SetFloat(KeyPrefix + "NoodleThickness", value);
        }

        public static NoodlePath NoodlePath
        {
            get => (NoodlePath)EditorPrefs.GetInt(KeyPrefix + "NoodlePath", (int)NoodlePath.Curvy);
            set => EditorPrefs.SetInt(KeyPrefix + "NoodlePath", (int)value);
        }

        public static NoodleStroke NoodleStroke
        {
            get => (NoodleStroke)EditorPrefs.GetInt(KeyPrefix + "NoodleStroke", (int)NoodleStroke.Full);
            set => EditorPrefs.SetInt(KeyPrefix + "NoodleStroke", (int)value);
        }

        public static Color HighlightColor
        {
            get => GetColor("HighlightColor", new Color(0.27f, 0.75f, 1f));
            set => SetColor("HighlightColor", value);
        }

        public static Color TintColor
        {
            get => GetColor("TintColor", new Color(0.35f, 0.35f, 0.35f));
            set => SetColor("TintColor", value);
        }

        public static Color GridLineColor
        {
            get => GetColor("GridLineColor", new Color(1f, 1f, 1f, 0.05f));
            set => SetColor("GridLineColor", value);
        }

        public static Color GridBgColor
        {
            get => GetColor("GridBgColor", new Color(0.12f, 0.12f, 0.12f));
            set => SetColor("GridBgColor", value);
        }

        public static Color GridMajorLineColor
        {
            get => GetColor("GridMajorLineColor", new Color(1f, 1f, 1f, 0.10f));
            set => SetColor("GridMajorLineColor", value);
        }

        public static bool AutoSave
        {
            get => EditorPrefs.GetBool(KeyPrefix + "AutoSave", true);
            set => EditorPrefs.SetBool(KeyPrefix + "AutoSave", value);
        }

        public static bool PortTooltips
        {
            get => EditorPrefs.GetBool(KeyPrefix + "PortTooltips", true);
            set => EditorPrefs.SetBool(KeyPrefix + "PortTooltips", value);
        }

        public static bool OpenOnCreate
        {
            get => EditorPrefs.GetBool(KeyPrefix + "OpenOnCreate", true);
            set => EditorPrefs.SetBool(KeyPrefix + "OpenOnCreate", value);
        }

        public static bool DragToCreate
        {
            get => EditorPrefs.GetBool(KeyPrefix + "DragToCreate", true);
            set => EditorPrefs.SetBool(KeyPrefix + "DragToCreate", value);
        }

        public static bool CreateFilter
        {
            get => EditorPrefs.GetBool(KeyPrefix + "CreateFilter", true);
            set => EditorPrefs.SetBool(KeyPrefix + "CreateFilter", value);
        }

        public static bool GridSnap
        {
            get => EditorPrefs.GetBool(KeyPrefix + "GridSnap", true);
            set => EditorPrefs.SetBool(KeyPrefix + "GridSnap", value);
        }

        public static bool ZoomToMouse
        {
            get => EditorPrefs.GetBool(KeyPrefix + "ZoomToMouse", true);
            set => EditorPrefs.SetBool(KeyPrefix + "ZoomToMouse", value);
        }

        public static float GridSnapSize
        {
            get => EditorPrefs.GetFloat(KeyPrefix + "GridSnapSize", 20f);
            set => EditorPrefs.SetFloat(KeyPrefix + "GridSnapSize", Mathf.Max(1f, value));
        }

        public static bool TryGetTypeColor(string typeName, out Color color)
        {
            string key = GetTypeColorKey(typeName);
            if (EditorPrefs.HasKey(key))
            {
                color = GetColor("TypeColor_" + typeName, Color.white);
                return true;
            }

            if (sDefaultTypeColors.TryGetValue(typeName, out color))
                return true;

            color = default;
            return false;
        }

        public static void SetTypeColor(string typeName, Color value)
        {
            EditorPrefs.SetString(GetTypeColorKey(typeName), "#" + ColorUtility.ToHtmlStringRGBA(value));
        }

        public static IEnumerable<KeyValuePair<string, Color>> GetRegisteredTypeColors()
        {
            foreach (var pair in sDefaultTypeColors)
                yield return new KeyValuePair<string, Color>(pair.Key, GetColor("TypeColor_" + pair.Key, pair.Value));
        }

        public static void ResetAll()
        {
            EditorPrefs.DeleteKey(KeyPrefix + "MinZoom");
            EditorPrefs.DeleteKey(KeyPrefix + "MaxZoom");
            EditorPrefs.DeleteKey(KeyPrefix + "NoodleThickness");
            EditorPrefs.DeleteKey(KeyPrefix + "NoodlePath");
            EditorPrefs.DeleteKey(KeyPrefix + "NoodleStroke");
            EditorPrefs.DeleteKey(KeyPrefix + "HighlightColor");
            EditorPrefs.DeleteKey(KeyPrefix + "TintColor");
            EditorPrefs.DeleteKey(KeyPrefix + "GridLineColor");
            EditorPrefs.DeleteKey(KeyPrefix + "GridBgColor");
            EditorPrefs.DeleteKey(KeyPrefix + "GridMajorLineColor");
            EditorPrefs.DeleteKey(KeyPrefix + "AutoSave");
            EditorPrefs.DeleteKey(KeyPrefix + "PortTooltips");
            EditorPrefs.DeleteKey(KeyPrefix + "OpenOnCreate");
            EditorPrefs.DeleteKey(KeyPrefix + "DragToCreate");
            EditorPrefs.DeleteKey(KeyPrefix + "CreateFilter");
            EditorPrefs.DeleteKey(KeyPrefix + "GridSnap");
            EditorPrefs.DeleteKey(KeyPrefix + "ZoomToMouse");
            EditorPrefs.DeleteKey(KeyPrefix + "GridSnapSize");

            foreach (var pair in sDefaultTypeColors)
                EditorPrefs.DeleteKey(GetTypeColorKey(pair.Key));
        }

        private static Color GetColor(string keySuffix, Color fallback)
        {
            var hex = EditorPrefs.GetString(KeyPrefix + keySuffix, "#" + ColorUtility.ToHtmlStringRGBA(fallback));
            return ColorUtility.TryParseHtmlString(hex, out var color) ? color : fallback;
        }

        private static void SetColor(string keySuffix, Color value)
        {
            EditorPrefs.SetString(KeyPrefix + keySuffix, "#" + ColorUtility.ToHtmlStringRGB(value));
        }

#if UNITY_2019_1_OR_NEWER
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Preferences/NodeKit", SettingsScope.User)
            {
                guiHandler = _ => DrawPreferencesGUI(),
                keywords = new HashSet<string>(new[] { "NodeKit", "node", "graph", "ports", "zoom" })
            };
        }
#endif

        private static void DrawPreferencesGUI()
        {
            EditorGUILayout.LabelField("Node", EditorStyles.boldLabel);
            TintColor = EditorGUILayout.ColorField("Tint", TintColor);
            HighlightColor = EditorGUILayout.ColorField("Selection", HighlightColor);
            NoodlePath = (NoodlePath)EditorGUILayout.EnumPopup("Noodle Path", NoodlePath);
            NoodleStroke = (NoodleStroke)EditorGUILayout.EnumPopup("Noodle Stroke", NoodleStroke);
            NoodleThickness = EditorGUILayout.FloatField("Noodle Thickness", NoodleThickness);
            PortTooltips = EditorGUILayout.Toggle("Port Tooltips", PortTooltips);
            DragToCreate = EditorGUILayout.Toggle("Drag To Create", DragToCreate);
            CreateFilter = EditorGUILayout.Toggle("Create Filter", CreateFilter);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
            GridSnap = EditorGUILayout.Toggle("Snap", GridSnap);
            GridSnapSize = EditorGUILayout.FloatField("Snap Size", GridSnapSize);
            ZoomToMouse = EditorGUILayout.Toggle("Zoom To Mouse", ZoomToMouse);
            MinZoom = EditorGUILayout.FloatField("Min Zoom", MinZoom);
            MaxZoom = EditorGUILayout.FloatField("Max Zoom", MaxZoom);
            GridLineColor = EditorGUILayout.ColorField("Line Color", GridLineColor);
            GridMajorLineColor = EditorGUILayout.ColorField("Major Line", GridMajorLineColor);
            GridBgColor = EditorGUILayout.ColorField("Background", GridBgColor);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("System", EditorStyles.boldLabel);
            AutoSave = EditorGUILayout.Toggle("Auto Save", AutoSave);
            OpenOnCreate = EditorGUILayout.Toggle("Open On Create", OpenOnCreate);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Types", EditorStyles.boldLabel);
            foreach (var pair in GetRegisteredTypeColors())
            {
                SetTypeColor(pair.Key, EditorGUILayout.ColorField(pair.Key, pair.Value));
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Reset Defaults", GUILayout.Width(120)))
                ResetAll();

            if (GUI.changed)
                NodeGraphWindow.RepaintAll();
        }

        private static string GetTypeColorKey(string typeName) => KeyPrefix + "TypeColor_" + typeName;
    }

    public enum NoodlePath
    {
        Curvy,
        Straight,
        Angled
    }

    public enum NoodleStroke
    {
        Full,
        Dashed
    }
}
