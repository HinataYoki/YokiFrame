#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 原始文件加载器接口文档
    /// </summary>
    internal static class ResKitDocRawFileInterface
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "原始文件加载器接口",
                Description = "实现 IRawFileLoader 和 IRawFileLoaderPool 接口可自定义原始文件加载方式。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "接口定义",
                        Code = @"// 原始文件加载器接口
public interface IRawFileLoader
{
    string LoadRawFileText(string path);
    byte[] LoadRawFileData(string path);
    void LoadRawFileTextAsync(string path, Action<string> onComplete);
    void LoadRawFileDataAsync(string path, Action<byte[]> onComplete);
    string GetRawFilePath(string path);
    void UnloadAndRecycle();
}

// 原始文件加载池接口
public interface IRawFileLoaderPool
{
    IRawFileLoader Allocate();
    void Recycle(IRawFileLoader loader);
}

#if YOKIFRAME_UNITASK_SUPPORT
// UniTask 扩展接口
public interface IRawFileLoaderUniTask : IRawFileLoader
{
    UniTask<string> LoadRawFileTextUniTaskAsync(string path, CancellationToken ct = default);
    UniTask<byte[]> LoadRawFileDataUniTaskAsync(string path, CancellationToken ct = default);
}
#endif",
                        Explanation = "通过实现这些接口，可以支持 Addressables、自定义文件系统等加载方式。"
                    }
                }
            };
        }
    }
}
#endif
