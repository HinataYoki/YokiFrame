#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// TableKitEditorUI - 项目级用户配置持久化
    /// 配置存储于 UserSettings/TableKitPrefs.json，项目隔离，适合纳入 .gitignore
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region 持久化数据结构

        [Serializable]
        private class UserPrefsData
        {
            public string editorDataPath = "Assets/Resources/Art/Table/";
            public string runtimePathPattern = "Art/Table/{0}";
            public string lubanWorkDir = "Luban/MiniTemplate";
            public string lubanDllPath = "Luban/Tools/Luban/Luban.dll";
            public string target = "client";
            public string codeTarget = "cs-bin";
            public string dataTarget = "bin";
            public string outputDataDir = "Assets/Resources/Art/Table/";
            public string outputCodeDir = "Assets/Scripts/TableKit/";
            public bool useAssemblyDefinition;
            public string assemblyName = "YokiFrame.TableKit";
            public bool generateExternalTypeUtil;
            public bool useAsyncLoading;
            public bool customEditorDataPath;
            public List<ExtraOutputTarget> extraOutputTargets = new();
        }

        private UserPrefsData mUserPrefs;

        #endregion

        #region 旧 EditorPrefs 键（仅用于一次性迁移）

        private const string LEGACY_PREF_EDITOR_DATA_PATH = "TableKit_EditorDataPath";
        private const string LEGACY_PREF_RUNTIME_PATH_PATTERN = "TableKit_RuntimePathPattern";
        private const string LEGACY_PREF_LUBAN_WORK_DIR = "TableKit_LubanWorkDir";
        private const string LEGACY_PREF_LUBAN_DLL_PATH = "TableKit_LubanDllPath";
        private const string LEGACY_PREF_TARGET = "TableKit_Target";
        private const string LEGACY_PREF_CODE_TARGET = "TableKit_CodeTarget";
        private const string LEGACY_PREF_DATA_TARGET = "TableKit_DataTarget";
        private const string LEGACY_PREF_OUTPUT_DATA_DIR = "TableKit_OutputDataDir";
        private const string LEGACY_PREF_OUTPUT_CODE_DIR = "TableKit_OutputCodeDir";
        private const string LEGACY_PREF_USE_ASSEMBLY = "TableKit_UseAssembly";
        private const string LEGACY_PREF_ASSEMBLY_NAME = "TableKit_AssemblyName";
        private const string LEGACY_PREF_GENERATE_EXTERNAL_TYPE_UTIL = "TableKit_GenerateExternalTypeUtil";
        private const string LEGACY_PREF_USE_ASYNC_LOADING = "TableKit_UseAsyncLoading";
        private const string LEGACY_PREF_CUSTOM_EDITOR_DATA_PATH = "TableKit_CustomEditorDataPath";
        private const string LEGACY_PREF_EXTRA_OUTPUT_TARGETS = "TableKit_ExtraOutputTargets";

        #endregion

        #region 文件读写

        private static string GetUserPrefsPath()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return string.IsNullOrEmpty(projectRoot)
                ? string.Empty
                : Path.Combine(projectRoot, "UserSettings", "TableKitPrefs.json");
        }

        private void LoadUserPrefs()
        {
            var path = GetUserPrefsPath();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var data = JsonUtility.FromJson<UserPrefsData>(json);
                    if (data != null)
                    {
                        data.extraOutputTargets ??= new List<ExtraOutputTarget>();
                        mUserPrefs = data;
                        return;
                    }
                }
                catch (Exception e)
                {
                    LogKit.Error($"[TableKit] 用户配置读取失败: {e.Message}");
                }
            }

            // 首次加载：尝试从旧 EditorPrefs 迁移为初值，并立刻落盘
            mUserPrefs = MigrateFromLegacyEditorPrefs();
            SaveUserPrefs();
        }

        private void SaveUserPrefs()
        {
            if (mUserPrefs == default) return;
            var path = GetUserPrefsPath();
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var json = JsonUtility.ToJson(mUserPrefs, true);
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                LogKit.Error($"[TableKit] 用户配置写入失败: {e.Message}");
            }
        }

        #endregion

        #region 旧配置迁移

        private static UserPrefsData MigrateFromLegacyEditorPrefs()
        {
            var data = new UserPrefsData
            {
                editorDataPath = EditorPrefs.GetString(LEGACY_PREF_EDITOR_DATA_PATH, "Assets/Resources/Art/Table/"),
                runtimePathPattern = EditorPrefs.GetString(LEGACY_PREF_RUNTIME_PATH_PATTERN, "Art/Table/{0}"),
                lubanWorkDir = EditorPrefs.GetString(LEGACY_PREF_LUBAN_WORK_DIR, "Luban/MiniTemplate"),
                lubanDllPath = EditorPrefs.GetString(LEGACY_PREF_LUBAN_DLL_PATH, "Luban/Tools/Luban/Luban.dll"),
                target = EditorPrefs.GetString(LEGACY_PREF_TARGET, "client"),
                codeTarget = EditorPrefs.GetString(LEGACY_PREF_CODE_TARGET, "cs-bin"),
                dataTarget = EditorPrefs.GetString(LEGACY_PREF_DATA_TARGET, "bin"),
                outputDataDir = EditorPrefs.GetString(LEGACY_PREF_OUTPUT_DATA_DIR, "Assets/Resources/Art/Table/"),
                outputCodeDir = EditorPrefs.GetString(LEGACY_PREF_OUTPUT_CODE_DIR, "Assets/Scripts/TableKit/"),
                useAssemblyDefinition = EditorPrefs.GetBool(LEGACY_PREF_USE_ASSEMBLY, false),
                assemblyName = EditorPrefs.GetString(LEGACY_PREF_ASSEMBLY_NAME, "YokiFrame.TableKit"),
                generateExternalTypeUtil = EditorPrefs.GetBool(LEGACY_PREF_GENERATE_EXTERNAL_TYPE_UTIL, false),
                useAsyncLoading = EditorPrefs.GetBool(LEGACY_PREF_USE_ASYNC_LOADING, false),
                customEditorDataPath = EditorPrefs.GetBool(LEGACY_PREF_CUSTOM_EDITOR_DATA_PATH, false),
            };

            var extraJson = EditorPrefs.GetString(LEGACY_PREF_EXTRA_OUTPUT_TARGETS, "");
            if (!string.IsNullOrEmpty(extraJson))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<ExtraOutputTargetListWrapper>(extraJson);
                    data.extraOutputTargets = wrapper?.targets ?? new List<ExtraOutputTarget>();
                }
                catch
                {
                    data.extraOutputTargets = new List<ExtraOutputTarget>();
                }
            }

            return data;
        }

        #endregion
    }
}
#endif
