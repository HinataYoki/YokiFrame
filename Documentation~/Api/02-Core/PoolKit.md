# PoolKit

> 面向读者：需要复用普通 C# 对象并控制归还生命周期的 Runtime 开发者
>
> 主要入口：`PoolKit`、`PoolKit.Shared`
>
> 运行边界：跨宿主 Runtime；池诊断只在 Editor/Tools 编译
>
> 状态来源：`Documentation~/Api/00-GettingStarted/Kit_Status.md`

## 适用场景

PoolKit 是 YokiFrame 唯一的对象池入口。对象需要高频创建和销毁、且可以明确重置状态时使用它。局部池适合一个系统独占；`PoolKit.Shared` 适合按类型注册的全局共享池。池只管理引用类型，不负责 Unity `GameObject` 的场景归属。

## 入口与当前状态

| 项目 | 当前值 |
|---|---|
| Runtime | 已实现，位于 `Core/Runtime/PoolKit` |
| 程序集 | Core Runtime 编入 `YokiFrame`，无宿主引用 |
| Interaction | 已实现，Provider 位于 `Core/Editor/PoolKit` |
| Workbench | 已实现，支持压力、对象明细、事件和疑似未归还检查 |
| 状态入口 | `PoolKit/state` |

## 快速上手

```csharp
using YokiFrame;

public sealed class Bullet
{
    public int Damage;
    public void Reset() { Damage = 0; }
}

ObjectPool<Bullet> pool = PoolKit.Create<Bullet>(
    static () => new Bullet(),
    onRecycled: static bullet => bullet.Reset(),
    options: new PoolOptions(initialCount: 8, maxRetained: 32));

Bullet bullet = pool.Allocate();
bullet.Damage = 10;
pool.Recycle(bullet);
pool.Dispose();
```

如果类型本身可控，推荐实现 `IPoolable` 并使用约定重载。

## 核心 API

### `PoolKit`、`ObjectPool<T>` 和 `PoolOptions`

`PoolOptions` 是不可变 `readonly struct`。显式创建容量配置不会产生托管堆分配；省略 `options`、`default(PoolOptions)` 与 `new PoolOptions()` 都表示零预热、无限缓存。

| API | 说明 |
|---|---|
| `PoolKit.Create<T>(Func<T>, Action<T>, Action<T>, PoolOptions options = default)` | 创建调用方独占池；factory 不能返回 `null`。显式委托不会自动绑定 `IPoolable`。 |
| `PoolKit.Create<T>(PoolOptions options = default)` | 创建 `T : class, IPoolable, new()` 的标准池，并绑定 `OnAllocated`/`OnRecycled`。 |
| `PoolKit.Shared` | 全局 `SharedPoolRegistry`。 |
| `ObjectPool<T>.CurCount` | 当前缓存对象数，不包含借出对象。 |
| `ObjectPool<T>.Allocate()` | 从缓存取对象，缓存为空时调用 factory，再执行借出回调。 |
| `ObjectPool<T>.Recycle(T obj)` | 回收对象；进入缓存返回 `true`，null、重复回收或容量已满返回 `false`。 |
| `ObjectPool<T>.Clear()` | 释放缓存对象，池仍可继续使用。 |
| `ObjectPool<T>.Dispose()` | 释放缓存并注销池；之后 `Allocate`/`Recycle` 会抛 `ObjectDisposedException`。 |
| `readonly struct PoolOptions(int initialCount = 0, int maxRetained = -1)` | 零分配容量配置；负预热或预热超过有限上限会被拒绝。 |
| `PoolOptions.InitialCount` / `MaxRetained` | 读取预热数和最大缓存数。 |
| `PoolOptions.UNBOUNDED` / `PoolOptions.Default` | `-1` 表示不限制缓存；默认不预热且不限制。 |

`Recycle` 在容量已满时仍会先执行 `onRecycled`，然后不缓存对象；如果对象实现 `IDisposable`，离开缓存时会调用 `Dispose()`。null、重复回收和已释放池是不同情况：前两者返回 `false`，已释放池抛 `ObjectDisposedException`。

### 配置值语义与运行时流程

`PoolOptions` 保留为一个值对象，而不是把 `initialCount`、`maxRetained` 展开为两个 `int` 参数。两者存在构造期约束：预热数不能为负，有限缓存上限不能小于预热数。单一 `readonly struct` 让调用点保持语义清晰、集中校验，并且显式配置不产生托管堆分配。它只在建池时读取，`Allocate` / `Recycle` 不保存或访问该结构。

```text
PoolKit.Create / Shared.Register
  -> PoolOptions 校验和初始容量计算
  -> ObjectPool<T>
       Stack<T>       : 可复用对象
       HashSet<T>     : 引用相等的重复回收保护和回收保留
       factory/callback: 业务创建、借出、回收生命周期
```

`ObjectPool<T>` 的关键路径如下：

1. `Allocate` 优先从 `Stack<T>` 弹出，缓存为空才在锁外调用 factory。
2. 借出回调成功后才把对象交给调用方；回调抛异常时，尚未交付的 `IDisposable` 对象会被释放，原始业务异常仍然抛出。
3. `Recycle` 先用引用相等 `HashSet<T>` 预留对象，防止回收回调重入或重复回收。
4. 回收回调成功后，容量允许则入栈；容量已满时不缓存，必要时释放对象。
5. `maxRetained = 0` 不再预分配默认的 16 个栈槽位，适合只复用生命周期回调而不保留对象的场景。

`default(PoolOptions)` 是 C# 9 值类型默认值，无法执行参数化构造；因此它被明确解释为零预热、无限缓存，而不是零缓存上限。

### `IPool<T>` 与 `IPoolable`

| 类型/API | 说明 |
|---|---|
| `IPool<T>.Allocate()` / `Recycle(T)` | 最小分配和回收契约。 |
| `IPoolable.OnAllocated()` | 对象借出后恢复可用状态。 |
| `IPoolable.OnRecycled()` | 对象归还前清理业务引用。 |

生命周期回调必须可重复执行。建议在 `OnRecycled` 清理订阅、外部引用、临时列表和上一次请求状态。

### `SharedPoolRegistry`

| API | 说明 |
|---|---|
| `Count` | 已注册共享池数量。 |
| `Register<T>(Func<T>, Action<T>, Action<T>, PoolOptions options = default)` | 注册普通引用类型共享池。 |
| `Register<T>(PoolOptions options = default)` | 注册 `IPoolable, new()` 类型共享池。 |
| `Get<T>()` | 获取已注册池；未注册抛 `InvalidOperationException`。 |
| `TryGet<T>(out ObjectPool<T>)` | 查询而不抛异常。 |
| `Remove<T>()` | 移除并释放指定类型池，返回是否找到。 |
| `Clear()` | 移除并释放全部共享池。 |

同一类型只能注册一个共享池。重复注册会释放新建池并抛出 `InvalidOperationException`。共享池的注册 owner 应固定在初始化阶段，测试结束使用 `Remove` 或 `Clear`。

## 宿主与工具入口

`PoolDebugger` 只在 Unity Editor 或 Godot Tools 编译，提供 `EnableTracking`、`EnableEventHistory`、`EnableStackTrace`、`GetAllPools`、`GetEventHistory`、`ForceReturn`、`ClearEventHistory` 和 `Clear`。诊断模型包含总量、借出数、缓存数、峰值、上限、对象来源和有限事件历史。

```text
ObjectPool<T> --(UNITY_EDITOR / GODOT+TOOLS)--> PoolEditorHook（Runtime 内部单向桥接）
PoolEditorHook --> Core/Editor/PoolKit 的 PoolDebugger
PoolDebugger --按需有界快照--> Core/Editor/PoolKit Snapshot + JSON
Snapshot --> Interaction Provider --> Host Telemetry/FileBridge
Host --> Tooling.Application 强类型模型 --> Avalonia Workbench
```

边界规则：`Core/Editor/PoolKit` 保存完整 `PoolDebugger`、诊断对象/事件模型、堆栈解析、JSON、命令、Interaction、快照组装和 Workbench 协议模型；这些文件不会进入 Player。`Core/Runtime/PoolKit/Diagnostics` 只保留 `PoolEditorHook` 单向桥接，`Core/Runtime/PoolKit/Contracts` 只保留内部快照/强制归还契约；它们每个文件整体受 `UNITY_EDITOR || (GODOT && TOOLS)` 包裹。Core Runtime 不反向依赖 `YokiFrame.Editor`，Player 和无 `TOOLS` 的 Godot 导出不含 `PoolDebugger`、诊断对象、事件历史、Provider 或通信类型。

性能与正确性规则：

- Player 的借还路径不包含任何诊断分支；诊断宏在编译时剔除。
- Editor/Tools 关闭跟踪时只保留池登记，不记录借出对象、事件或堆栈；堆栈开启会同时启用跟踪和事件历史，关闭跟踪会清除旧借出映射。
- 开启跟踪后，借还路径不再复制整池 `object[]`。缓存对象只在 `GetAllPools` 或 Workbench 状态读取时按预算复制。
- `PoolKit/state` 最多发布 24 个池、32 条对象明细、24 条事件和 24 条泄漏候选；对象池统计和泄漏扫描仍覆盖全部已登记池。超过预算时以 `poolTotal`、对象总数、`total` 和 `truncated` 显式说明覆盖范围。
- 每个局部池在当前诊断会话中有 `poolId`。Workbench 以它关联池、事件和泄漏候选；它不跨 Domain Reload 或新会话持久化。

Workbench/CLI 使用 `PoolKit/state` 和以下 action：

```powershell
yoki command send --engine <engineId> --kit PoolKit --action get_workbench_snapshot --project <projectRoot>
yoki command send --engine <engineId> --kit PoolKit --action check_leak --project <projectRoot>
```

`set_tracking` 需要完整的三个布尔字段，`clear_history` 只清理诊断事件，不改对象池。`check_leak` 只表示当前仍有借出对象，是排查线索，不是内存泄漏定论；Workbench 会选中首个可见候选池，已记录的借出位置可点击并通过宿主代码编辑器打开。堆栈采集成本最高，定位结束后应关闭。

## 生命周期与错误边界

- 池的 owner 必须负责 `Dispose`；全局共享池由注册表 owner 负责 `Remove` 或 `Clear`。
- 不要把同一对象同时交给两个池；池使用引用相等判断重复回收。
- 工厂返回 null、生命周期回调抛异常或池已释放都会中断当前操作，不应在业务层静默吞掉异常。
- Unity 对象的 `Destroy`、场景归属和组件生命周期仍由宿主 Adapter 或业务 owner 管理。

## 限制与相关资料

- PoolKit 只管理引用类型；Unity `GameObject` 的创建、销毁和场景归属不由它接管
- `check_leak` 只报告仍有借出对象的候选，不能替代业务生命周期判断
- 追踪和堆栈只用于短时排查，结束后应关闭以避免 Editor/Tools 诊断成本
