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
        /// UI 脚本所在的程序集名称
        /// 用于序列化时通过反射获取生成的 UI 类型，以便绑定 Prefab 组件引用
        /// 如果 UI 脚本使用了 Assembly Definition，需要指定对应的程序集名称
        /// </summary>
        public string AssemblyName = "Assembly-CSharp";
        /// <summary>
        /// 代码生成模板名称
        /// </summary>
        public string CodeGenTemplateName = UICodeGenTemplateRegistry.DEFAULT_TEMPLATE_NAME;
        /// <summary>
        /// 需要绑定的Prefab列表
        /// </summary>
        public List<string> BindPrefabPathList = new();

        /// <summary>
        /// 保存配置
        /// </summary>
        public void SaveConfig()
        {
            Save(true);
        }
    }
}
