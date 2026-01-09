#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SaveKit 基本存档操作文档
    /// </summary>
    internal static class SaveKitDocBasic
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "基本存档操作",
                Description = "SaveKit 使用延迟序列化：注册模块引用后，保存时才在线程池执行序列化。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "注册模块并保存",
                        Code = @"// 创建存档数据
var saveData = SaveKit.CreateSaveData();

// 注册模块引用（只存引用，不序列化）
saveData.RegisterModule(playerData);
saveData.RegisterModule(inventoryData);
saveData.RegisterModule(questData);

// 异步保存（推荐）- 序列化在线程池执行，主线程零阻塞
await SaveKit.SaveUniTaskAsync(slotId: 0, saveData);

// 同步保存（会阻塞主线程）
bool success = SaveKit.Save(slotId: 0, saveData);",
                        Explanation = "RegisterModule 只存储对象引用，保存时才执行序列化。模块类型作为唯一标识，无需手动指定 key。"
                    },
                    new()
                    {
                        Title = "加载数据",
                        Code = @"// 异步加载（推荐）
var saveData = await SaveKit.LoadUniTaskAsync(slotId: 0);

// 同步加载
var saveData = SaveKit.Load(slotId: 0);

if (saveData != null)
{
    // 获取模块数据（从字节反序列化）
    var playerData = saveData.GetModule<PlayerData>();
    var inventoryData = saveData.GetModule<InventoryData>();
    
    // 重新注册引用以便后续保存
    saveData.RegisterModule(playerData);
    saveData.RegisterModule(inventoryData);
}"
                    },
                    new()
                    {
                        Title = "检查与删除",
                        Code = @"// 检查槽位是否存在（通过魔数验证）
if (SaveKit.Exists(slotId: 0))
{
    // 获取存档元数据（只读取文件头，不加载全部数据）
    var meta = SaveKit.GetMeta(slotId: 0);
    Debug.Log($""创建时间: {meta.GetCreatedDateTime()}"");
    Debug.Log($""最后保存: {meta.GetLastSavedDateTime()}"");
}

// 删除指定槽位
SaveKit.Delete(slotId: 0);

// 获取所有有效存档
var allSlots = SaveKit.GetAllSlots();

// 扫描目录下所有存档文件
var allSaves = SaveKit.ScanAllSaves();"
                    }
                }
            };
        }
    }
}
#endif
