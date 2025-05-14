using System.Linq;
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
                    var guid = AssetDatabase.FindAssets(nameof(UIKitCreateConfig)).First();
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
                return mInstance;
            }
        }

        public string PrefabGeneratePath = "Assets/Art/UIPrefab";
        public string ScriptGeneratePath = "Assets/Scripts/UI";
        public string ScriptNamespace = "GameUI";

        public readonly static string GeneratePrePath = $"{Application.dataPath.Replace("Assets", string.Empty)}";
    }
}