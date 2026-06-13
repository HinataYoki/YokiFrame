#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 同步加载文档
    /// </summary>
    internal static class ResKitDocSync
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "同步加载",
                Description = "同步加载资源，适合小资源或加载界面。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基本加载",
                        Code = @"// 加载资源
var prefab = ResKit.Load<GameObject>(""Prefabs/Player"");
var sprite = ResKit.Load<Sprite>(""Sprites/Icon"");
var clip = ResKit.Load<AudioClip>(""Audio/BGM"");

// 加载并实例化
var player = ResKit.Instantiate(""Prefabs/Player"", parent);

// 获取句柄 — LoadAsset 内部已自动 Retain（RefCount=1）
var handler = ResKit.LoadAsset<GameObject>(""Prefabs/Enemy"");
// 需要更长时间持有时可额外 Retain（RefCount+1）
handler.Retain();
// 每次 Release 使 RefCount-1，归零时自动卸载
handler.Release();"
                    }
                }
            };
        }
    }
}
#endif
