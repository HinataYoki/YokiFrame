using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    public class UIKitCreateConfig : ScriptableObject
    {
        private static UIKitCreateConfig mInstance;
        public static UIKitCreateConfig Instance
        {
            get
            {
                if (mInstance == null)
                {
                    var guids = AssetDatabase.FindAssets(nameof(UIKitCreateConfig));
                    if (guids != null && guids.Length > 0)
                    {
                        var guid = guids[0];
                        if (guid != null)
                        {
                            var path = AssetDatabase.GUIDToAssetPath(guid);
                            mInstance = AssetDatabase.LoadAssetAtPath<UIKitCreateConfig>(path);
                        }

                        if (mInstance == null)
                        {
                            LogKit.Error<UIKitCreateConfig>("UIKit配置文件查询失败，请检查是否有同名或丢失文件");
                        }
                    }
                }
                return mInstance;
            }
        }

        public string PrefabGeneratePath = "Assets/Art/UIPrefab";
        public string ScriptGeneratePath = "Assets/Scripts/UI";
        public string ScriptNamespace = "GameUI";
        /// <summary>
        /// UI脚本所在的程序集名称
        /// </summary>
        public string AssemblyName = "Assembly-CSharp";

        public List<string> BindPrefabPathList = new();

        public readonly static string GeneratePrePath = $"{Application.dataPath.Replace("Assets", string.Empty)}";
    }
}