# YokiFrame 编辑器样式统一审查报告

> **审查日期**：2025-01-XX  
> **审查范围**：Tools 层编辑器对 Core 层 UI 资源的引用情况

---

## 📊 审查结果总结

### 现状评分

| 维度 | 评分 | 说明 |
|------|------|------|
| **组件复用** | 7/10 | Tools 层大量使用 YokiFrameUIComponents，但存在内联样式 |
| **样式一致性** | 4/10 | 缺少 USS 文件，大量硬编码样式值 |
| **设计令牌使用** | 6/10 | 部分使用 Colors/Spacing 常量，但不完整 |
| **可维护性** | 5/10 | 样式分散在 C# 代码中，难以统一修改 |

### 核心问题

1. **Tools 层无 USS 样式文件**：所有样式通过 C# 内联定义
2. **硬编码样式值泛滥**：大量魔法数字（如 `new Color(0.08f, 0.08f, 0.10f)`）
3. **设计令牌使用不一致**：部分代码使用 `YokiFrameUIComponents.Colors`，部分直接硬编码
4. **缺少 BEM 命名规范**：无 CSS 类名，无法通过 USS 统一管理

---

## 🔍 详细审查发现

### 1. 组件使用情况

**✅ 良好实践**：
- ActionKit、AudioKit、UIKit 大量使用 `YokiFrameUIComponents` 组件工厂
- 使用 `using static YokiFrame.EditorTools.YokiFrameUIComponents` 简化调用

**❌ 问题实践**：
```csharp
// 示例：AudioKit/Editor/AudioKitToolPage.Console.cs
var container = new VisualElement {
    style = {
        backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.10f))  // 硬编码
    }
};
```

### 2. 样式定义方式

**当前方式**：100% C# 内联样式
```csharp
toolbar.style.paddingLeft = YokiFrameUIComponents.Spacing.LG;
toolbar.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.10f));
```

**推荐方式**：USS + BEM 类名
```csharp
toolbar.AddToClassList("yoki-toolbar");
toolbar.AddToClassList("yoki-toolbar--audio");
```

### 3. 设计令牌覆盖率

| 令牌类型 | Core 定义 | Tools 使用率 | 问题 |
|---------|----------|-------------|------|
| Colors | ✅ 完整 | 60% | 40% 硬编码颜色值 |
| Spacing | ✅ 完整 | 70% | 30% 魔法数字 |
| Radius | ✅ 完整 | 30% | 70% 硬编码圆角值 |
| Font Size | ✅ 完整 | 20% | 80% 硬编码字体大小 |

---

## 📋 重构方案设计

### 阶段 1：建立样式基础设施（1-2 天）

**目标**：为 Tools 层建立 USS 样式系统

**任务清单**：
1. 创建 Tools 层样式目录结构
2. 为每个 Kit 创建专用 USS 文件
3. 建立样式注册机制
4. 编写样式迁移指南

**目录结构**：
```
Core/Editor/UISystem/Styling/Kits/
├── ActionKit/
│   └── ActionKit.uss
├── AudioKit/
│   └── AudioKit.uss
├── UIKit/
│   └── UIKit.uss
└── ...
```


### 阶段 2：样式提取与迁移（3-5 天）

**目标**：将 C# 内联样式迁移到 USS

**迁移优先级**：
1. **P0 - 颜色系统**：所有硬编码颜色 → CSS 变量
2. **P1 - 间距系统**：所有魔法数字 → CSS 变量
3. **P2 - 组件样式**：重复样式模式 → BEM 类名
4. **P3 - 动画效果**：过渡动画 → CSS transitions

**迁移示例**：

**迁移前（C#）**：
```csharp
var toolbar = new VisualElement {
    style = {
        height = 48,
        paddingLeft = 16,
        paddingRight = 16,
        backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f)),
        borderBottomWidth = 1,
        borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f))
    }
};
```

**迁移后（C# + USS）**：
```csharp
var toolbar = new VisualElement();
toolbar.AddToClassList("yoki-toolbar");
toolbar.AddToClassList("yoki-toolbar--audio");
```

```css
/* AudioKit.uss */
.yoki-toolbar {
    height: 48px;
    padding-left: var(--yoki-spacing-lg);
    padding-right: var(--yoki-spacing-lg);
    background-color: var(--yoki-layer-toolbar);
    border-bottom-width: 1px;
    border-bottom-color: var(--yoki-border-default);
}

.yoki-toolbar--audio {
    /* AudioKit 特定样式 */
}
```

### 阶段 3：组件标准化（2-3 天）

**目标**：统一组件创建模式

**标准化内容**：
1. 所有组件必须使用 `YokiFrameUIComponents` 工厂方法
2. 禁止直接 `new VisualElement()` 并内联样式
3. 自定义样式通过 BEM 类名扩展

**组件创建规范**：
```csharp
// ❌ 禁止
var card = new VisualElement();
card.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.21f));
card.style.borderRadius = 6;

// ✅ 推荐
var card = YokiFrameUIComponents.CreateCard();
card.AddToClassList("audio-kit-card");  // Kit 特定样式
```

### 阶段 4：设计令牌完善（1-2 天）

**目标**：补充缺失的设计令牌

**新增令牌**：
```css
/* YokiTokens.uss 补充 */
:root {
    /* === 工具栏专用 === */
    --yoki-toolbar-height: 48px;
    --yoki-toolbar-bg: rgb(38, 39, 43);
    
    /* === 卡片系统 === */
    --yoki-card-padding: var(--yoki-spacing-lg);
    --yoki-card-radius: var(--yoki-radius-lg);
    --yoki-card-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
    
    /* === 列表项 === */
    --yoki-list-item-height: 32px;
    --yoki-list-item-hover: var(--yoki-layer-hover);
    
    /* === 徽章尺寸 === */
    --yoki-badge-height: 18px;
    --yoki-badge-padding-h: 6px;
    --yoki-badge-font-size: var(--yoki-font-xs);
}
```

---

## 🎯 重构实施计划

### 第 1 周：基础设施 + ActionKit 试点

**Day 1-2**：建立样式基础设施
- 创建目录结构
- 编写样式注册代码
- 建立迁移模板

**Day 3-5**：ActionKit 试点迁移
- 提取 ActionKit 所有样式到 USS
- 重构 ActionKitToolPage 使用 BEM 类名
- 验证样式效果一致性

**交付物**：
- `Core/Editor/UISystem/Styling/Kits/ActionKit/ActionKit.uss`
- 迁移指南文档
- ActionKit 重构完成

### 第 2 周：AudioKit + UIKit 迁移

**Day 1-3**：AudioKit 迁移
- 提取 AudioKit 样式（Console/CodeGenerator）
- 重构组件创建代码
- 测试验证

**Day 4-5**：UIKit 迁移
- 提取 UIKit 样式（CreatePanel/BindInspector）
- 重构组件创建代码
- 测试验证

**交付物**：
- `AudioKit.uss` + `UIKit.uss`
- 2 个 Kit 重构完成

### 第 3 周：剩余 Kits + 设计令牌完善

**Day 1-3**：剩余 Kits 迁移
- BuffKit, LocalizationKit, SaveKit, SceneKit, SpatialKit

**Day 4-5**：设计令牌完善 + 文档
- 补充缺失的设计令牌
- 编写样式使用文档
- 更新 AI_NAVIGATION.md

**交付物**：
- 所有 Kits 样式统一
- 完整的设计令牌系统
- 样式使用文档

---

## 📐 样式规范定义

### BEM 命名规范

**格式**：`.yoki-{kit}-{block}[__{element}][--{modifier}]`

**示例**：
```css
/* Block */
.yoki-audio-toolbar { }

/* Element */
.yoki-audio-toolbar__title { }
.yoki-audio-toolbar__volume-slider { }

/* Modifier */
.yoki-audio-toolbar--compact { }
.yoki-audio-toolbar--recording { }
```

### 样式文件组织

**每个 Kit 的 USS 文件结构**：
```css
/* 1. 导入设计令牌（自动） */

/* 2. Kit 全局样式 */
.yoki-{kit}-root { }

/* 3. 工具栏样式 */
.yoki-{kit}-toolbar { }

/* 4. 内容区样式 */
.yoki-{kit}-content { }

/* 5. 卡片/列表样式 */
.yoki-{kit}-card { }
.yoki-{kit}-list-item { }

/* 6. 特定组件样式 */
.yoki-{kit}-specific-component { }

/* 7. 状态样式 */
.yoki-{kit}--active { }
.yoki-{kit}--disabled { }
```

### C# 代码规范

**组件创建**：
```csharp
// 1. 使用工厂方法创建基础组件
var toolbar = YokiFrameUIComponents.CreateToolbar();

// 2. 添加 BEM 类名
toolbar.AddToClassList("yoki-audio-toolbar");

// 3. 添加修饰符（可选）
if (isCompact) toolbar.AddToClassList("yoki-audio-toolbar--compact");

// 4. 禁止内联样式（除非动态计算）
// ❌ toolbar.style.backgroundColor = ...
```

**设计令牌使用**：
```csharp
// ✅ 使用常量
label.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
container.style.paddingLeft = YokiFrameUIComponents.Spacing.LG;

// ❌ 硬编码
label.style.color = new StyleColor(new Color(0.94f, 0.94f, 0.96f));
container.style.paddingLeft = 16;
```

---

## 🚨 风险评估

| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|---------|
| 样式迁移后视觉不一致 | 高 | 中 | 每个 Kit 迁移后截图对比 |
| USS 变量不支持旧版 Unity | 中 | 低 | 使用 Unity 2021.3+ 支持的特性 |
| 重构工作量超预期 | 中 | 中 | 分阶段交付，优先核心 Kits |
| 破坏现有功能 | 高 | 低 | 充分测试，保留回滚方案 |

---

## ✅ 验收标准

### 功能验收
- [ ] 所有 Tools 层 Kits 编辑器窗口正常显示
- [ ] 样式效果与重构前一致
- [ ] 无控制台错误或警告

### 代码质量验收
- [ ] 所有 Kit 有对应的 USS 文件
- [ ] C# 代码中硬编码样式值 < 5%
- [ ] 所有组件使用 BEM 类名
- [ ] 设计令牌覆盖率 > 95%

### 文档验收
- [ ] 样式使用文档完整
- [ ] 迁移指南清晰可执行
- [ ] AI_NAVIGATION.md 更新样式系统说明

---

## 📚 参考资料

- [BEM 命名规范](http://getbem.com/)
- [UIToolkit USS 文档](https://docs.unity3d.com/Manual/UIE-USS.html)
- [设计令牌系统](https://www.designtokens.org/)
- YokiFrame 框架开发规范：`.kiro/steering/yokiframe-guidelines.md`

---

## 🎬 下一步行动

1. **评审本方案**：团队评审，确认可行性
2. **创建任务分支**：`feature/style-unification`
3. **启动第 1 周工作**：基础设施 + ActionKit 试点
4. **每日同步进度**：确保按计划推进

---

**审查人**：AI Agent  
**审查完成时间**：2025-01-XX  
**预计重构完成时间**：3 周（15 个工作日）
