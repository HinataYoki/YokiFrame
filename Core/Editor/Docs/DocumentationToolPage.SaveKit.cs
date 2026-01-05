#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    public partial class DocumentationToolPage
    {
        private DocModule CreateSaveKitDoc()
        {
            return new DocModule
            {
                Name = "SaveKit",
                Icon = "💾",
                Category = "TOOLS",
                Description = "存档系统工具，提供多槽位存档、版本迁移、加密、自动保存等功能。支持与 Architecture 集成。",
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "基本存档操作",
                        Description = "SaveKit 提供简洁的存档读写 API。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "保存数据",
                                Code = @"// 创建存档数据
var saveData = SaveKit.CreateSaveData();

// 保存模块数据（使用 int key，避免魔法字符串）
saveData.SetModule(ModuleId.Player, playerData);
saveData.SetModule(ModuleId.Inventory, inventoryData);

// 保存到槽位
bool success = SaveKit.Save(slotId: 0, saveData);",
                                Explanation = "每个模块使用唯一的 int ID 作为 key，推荐使用枚举或常量定义。"
                            },
                            new()
                            {
                                Title = "加载数据",
                                Code = @"// 从槽位加载
var saveData = SaveKit.Load(slotId: 0);

if (saveData != null)
{
    // 获取模块数据
    var playerData = saveData.GetModule<PlayerData>(ModuleId.Player);
    var inventoryData = saveData.GetModule<InventoryData>(ModuleId.Inventory);
}

// 检查槽位是否存在
if (SaveKit.Exists(slotId: 0))
{
    // 存档存在
}"
                            },
                            new()
                            {
                                Title = "删除存档",
                                Code = @"// 删除指定槽位
SaveKit.Delete(slotId: 0);

// 获取所有存档元数据
var allSlots = SaveKit.GetAllSlots();
foreach (var meta in allSlots)
{
    Debug.Log($""槽位 {meta.SlotId}: {meta.SaveTime}"");
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "异步存档",
                        Description = "异步操作避免阻塞主线程。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "异步保存/加载",
                                Code = @"// 回调方式保存
SaveKit.SaveAsync(slotId, saveData, success =>
{
    if (success) Debug.Log(""保存成功"");
});

// 回调方式加载
SaveKit.LoadAsync(slotId, data =>
{
    if (data != null)
    {
        // 处理加载的数据
    }
});

// UniTask 方式（推荐）
bool success = await SaveKit.SaveUniTaskAsync(slotId, saveData);
var data = await SaveKit.LoadUniTaskAsync(slotId);",
                                Explanation = "异步操作在后台线程执行文件 IO，不阻塞主线程。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "自动保存",
                        Description = "定时自动保存功能。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "启用自动保存",
                                Code = @"// 启用自动保存（每60秒）
SaveKit.EnableAutoSave(
    slotId: 0,
    data: saveData,
    intervalSeconds: 60f,
    onBeforeSave: () =>
    {
        // 保存前更新数据
        CollectGameData(saveData);
    }
);

// UniTask 版本（推荐）
SaveKit.EnableAutoSaveUniTask(slotId, saveData, 60f, OnBeforeSave);

// 禁用自动保存
SaveKit.DisableAutoSave();

// 检查状态
if (SaveKit.IsAutoSaveEnabled)
{
    // 自动保存已启用
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "版本迁移",
                        Description = "处理存档数据结构变更。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "注册迁移器",
                                Code = @"// 设置当前版本
SaveKit.SetCurrentVersion(3);

// 注册迁移器
SaveKit.RegisterMigrator(new V1ToV2Migrator());
SaveKit.RegisterMigrator(new V2ToV3Migrator());

// 实现迁移器
public class V1ToV2Migrator : ISaveMigrator
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public SaveData Migrate(SaveData data)
    {
        var oldPlayer = data.GetModule<OldPlayerData>(ModuleId.Player);
        var newPlayer = new PlayerData
        {
            Name = oldPlayer.Name,
            Level = oldPlayer.Lv,
            Exp = 0
        };
        data.SetModule(ModuleId.Player, newPlayer);
        return data;
    }
}",
                                Explanation = "加载旧版本存档时会自动按顺序执行迁移器。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "加密与序列化",
                        Description = "自定义加密和序列化方式。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "配置加密",
                                Code = @"// 使用 AES 加密
var encryptor = new AesSaveEncryptor(""your-secret-key"");
SaveKit.SetEncryptor(encryptor);

// 禁用加密
SaveKit.SetEncryptor(null);

// 自定义序列化器
SaveKit.SetSerializer(new CustomSerializer());"
                            },
                            new()
                            {
                                Title = "配置路径",
                                Code = @"// 设置存档路径
SaveKit.SetSavePath(Path.Combine(
    Application.persistentDataPath, ""MySaves""));

// 设置最大槽位数
SaveKit.SetMaxSlots(20);"
                            }
                        }
                    },
                    new()
                    {
                        Title = "Architecture 集成",
                        Description = "与 YokiFrame Architecture 的 IModel 集成。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "收集与应用",
                                Code = @"// 从 Architecture 收集所有 IModel 数据
var saveData = SaveKit.CreateSaveData();
SaveKit.CollectFromArchitecture<GameArchitecture>(saveData);
SaveKit.Save(slotId, saveData);

// 将存档数据应用到 Architecture
var loadedData = SaveKit.Load(slotId);
if (loadedData != null)
{
    SaveKit.ApplyToArchitecture<GameArchitecture>(loadedData);
}",
                                Explanation = "自动序列化/反序列化所有注册的 IModel 服务。"
                            }
                        }
                    }
                }
            };
        }
    }
}
#endif
