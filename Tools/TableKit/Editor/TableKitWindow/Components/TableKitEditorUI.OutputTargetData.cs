#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - 多目标输出数据与持久化
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region 输出目标数据结构

        /// <summary>
        /// 额外输出目标配置
        /// </summary>
        [Serializable]
        private class ExtraOutputTarget
        {
            /// <summary>目标名称</summary>
            public string name = "服务端";
            /// <summary>导出目标（client/server/all），决定导出哪些字段分组</summary>
            public string target = "server";
            /// <summary>数据格式（bin/json/lua）</summary>
            public string dataTarget = "json";
            /// <summary>数据输出目录</summary>
            public string dataDir = "";
            /// <summary>代码生成器类型</summary>
            public string codeTarget = "java-json";
            /// <summary>代码输出目录</summary>
            public string codeDir = "";
            /// <summary>是否启用</summary>
            public bool enabled = true;
        }

        [Serializable]
        private class ExtraOutputTargetListWrapper
        {
            public List<ExtraOutputTarget> targets = new();
        }

        #endregion

        #region 多目标输出字段

        private List<ExtraOutputTarget> mExtraOutputTargets = new();
        private const string PREF_EXTRA_OUTPUT_TARGETS = "TableKit_ExtraOutputTargets";

        // 所有可用的代码生成器选项（包含客户端和服务端）
        private static readonly string[] ALL_CODE_TARGET_OPTIONS =
        {
            // Unity 客户端
            "cs-bin",
            "cs-simple-json",
            "cs-newtonsoft-json",
            // .NET 服务端
            "cs-dotnet-json",
            // Java
            "java-bin",
            "java-json",
            // Go
            "go-bin",
            "go-json",
            // Python
            "python-json",
            // C++
            "cpp-bin",
            // Rust
            "rust-bin",
            "rust-json",
            // Lua
            "lua-lua",
            "lua-bin",
            // TypeScript
            "typescript-bin",
            "typescript-json"
        };

        #endregion

        #region 多目标输出持久化

        private void LoadExtraOutputTargets()
        {
            var json = EditorPrefs.GetString(PREF_EXTRA_OUTPUT_TARGETS, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<ExtraOutputTargetListWrapper>(json);
                    mExtraOutputTargets = wrapper?.targets ?? new List<ExtraOutputTarget>();
                }
                catch
                {
                    mExtraOutputTargets = new List<ExtraOutputTarget>();
                }
            }
            else
            {
                mExtraOutputTargets = new List<ExtraOutputTarget>();
            }
        }

        private void SaveExtraOutputTargets()
        {
            var wrapper = new ExtraOutputTargetListWrapper { targets = mExtraOutputTargets };
            var json = JsonUtility.ToJson(wrapper);
            EditorPrefs.SetString(PREF_EXTRA_OUTPUT_TARGETS, json);
        }

        #endregion

        #region 数据格式与代码类型同步

        /// <summary>
        /// 根据数据格式获取匹配的代码类型
        /// </summary>
        private static string GetMatchingCodeTarget(string currentCodeTarget, string dataTarget)
        {
            if (string.IsNullOrEmpty(currentCodeTarget))
            {
                return dataTarget switch
                {
                    "bin" => "java-bin",
                    "lua" => "lua-lua",
                    _ => "java-json"
                };
            }

            var dashIndex = currentCodeTarget.LastIndexOf('-');
            if (dashIndex <= 0) return currentCodeTarget;

            var prefix = currentCodeTarget.Substring(0, dashIndex);
            
            string targetSuffix;
            if (dataTarget == "bin")
            {
                targetSuffix = "-bin";
            }
            else if (dataTarget == "lua")
            {
                if (prefix == "lua")
                    targetSuffix = "-lua";
                else
                    return "lua-lua";
            }
            else
            {
                targetSuffix = prefix == "lua" ? "-lua" : "-json";
            }

            var newCodeTarget = prefix + targetSuffix;

            foreach (var option in ALL_CODE_TARGET_OPTIONS)
            {
                if (option == newCodeTarget) return newCodeTarget;
            }

            return currentCodeTarget;
        }

        /// <summary>
        /// 根据代码类型获取匹配的数据格式
        /// </summary>
        private static string GetMatchingDataTarget(string codeTarget)
        {
            if (string.IsNullOrEmpty(codeTarget)) return "json";

            if (codeTarget.EndsWith("-bin"))
                return "bin";
            
            if (codeTarget == "lua-lua")
                return "lua";

            return "json";
        }

        #endregion
    }
}
#endif
