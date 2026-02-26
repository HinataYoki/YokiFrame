#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SingletonKit 路径属性文档
    /// </summary>
    internal static class SingletonKitDocPath
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "MonoSingletonPath 路径属性",
                Description = "为 MonoBehaviour 单例指定自动创建的层级路径。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基础用法",
                        Code = @"// 指定单例在 Hierarchy 中的路径
[MonoSingletonPath(""Managers/Audio"")]
public class AudioManager : MonoBehaviour, ISingleton
{
    public static AudioManager Instance => 
        SingletonKit<AudioManager>.Instance;
    
    public void OnSingletonInit()
    {
        DontDestroyOnLoad(gameObject);
    }
}

// 访问时自动创建层级结构：
// Managers (GameObject)
//   └── Audio (AudioManager)
var audio = AudioManager.Instance;",
                        Explanation = "路径使用 / 分隔，自动创建不存在的父级 GameObject。"
                    },
                    new()
                    {
                        Title = "UI 单例（RectTransform）",
                        Code = @"// 创建 RectTransform 类型的单例
[MonoSingletonPath(""UI/Managers/Dialog"", isRectTransform: true)]
public class DialogManager : MonoBehaviour, ISingleton
{
    public static DialogManager Instance => 
        SingletonKit<DialogManager>.Instance;
    
    public void OnSingletonInit()
    {
        // RectTransform 已自动创建
        var rect = GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
    }
}",
                        Explanation = "isRectTransform 参数为 true 时，创建带 RectTransform 的 GameObject。"
                    },
                    new()
                    {
                        Title = "路径属性详解",
                        Code = @"// MonoSingletonPathAttribute 定义
public class MonoSingletonPathAttribute : Attribute
{
    /// <summary>
    /// 层级路径（如 ""Root/Manager""）
    /// </summary>
    public string PathInHierarchy { get; }
    
    /// <summary>
    /// 是否创建 RectTransform（用于 UI）
    /// </summary>
    public bool IsRectTransform { get; }
}

// 无路径属性时的默认行为
public class SimpleManager : MonoBehaviour, ISingleton
{
    // 直接在根层级创建名为 ""SimpleManager"" 的 GameObject
    public static SimpleManager Instance => 
        SingletonKit<SimpleManager>.Instance;
    
    public void OnSingletonInit() { }
}",
                        Explanation = "不使用路径属性时，单例直接创建在 Hierarchy 根层级。"
                    }
                }
            };
        }
    }
}
#endif
