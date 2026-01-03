using System.Collections.Generic;
using UnityEditor;

namespace YokiFrame
{
    [FilePath("ProjectSettings/UIKitCreateConfig.asset", FilePathAttribute.Location.ProjectFolder)]
    public class UIKitCreateConfig : ScriptableSingleton<UIKitCreateConfig>
    {
        public static UIKitCreateConfig Instance => instance;

        public string PrefabGeneratePath = "Assets/Resources/Art/UIPrefab";
        public string ScriptGeneratePath = "Assets/Scripts/UI";
        public string ScriptNamespace = "GameUI";
        /// <summary>
        /// UI脚本所在的程序集名称
        /// </summary>
        public string AssemblyName = "Assembly-CSharp";
        /// <summary>
        /// 需要绑定的Prefab列表
        /// </summary>
        public List<string> BindPrefabPathList = new();
    }
}