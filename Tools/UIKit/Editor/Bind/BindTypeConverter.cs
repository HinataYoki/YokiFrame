using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 绑定类型转换器 - 处理 Member/Element/Component 之间的类型转换
    /// </summary>
    public static class BindTypeConverter
    {
        #region 公共方法

        /// <summary>
        /// 检查转换是否可行
        /// </summary>
        /// <param name="bind">目标 Bind 组件</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="reason">不可行的原因（输出）</param>
        /// <returns>是否可以转换</returns>
        public static bool CanConvert(AbstractBind bind, BindType targetType, out string reason)
        {
            reason = null;

            if (bind == null)
            {
                reason = "Bind 组件为空";
                return false;
            }

            // 相同类型无需转换
            if (bind.Bind == targetType)
            {
                reason = "源类型与目标类型相同";
                return false;
            }

            // 检查名称是否有效
            if (string.IsNullOrEmpty(bind.Name))
            {
                reason = "绑定名称为空";
                return false;
            }

            if (!BindValidator.IsValidIdentifier(bind.Name))
            {
                reason = $"绑定名称 '{bind.Name}' 不是有效的 C# 标识符";
                return false;
            }

            // 使用策略检查是否支持转换
            var sourceStrategy = BindStrategyRegistry.Get(bind.Bind);
            var targetStrategy = BindStrategyRegistry.Get(targetType);

            if (sourceStrategy != null && !sourceStrategy.SupportsConversion)
            {
                reason = $"{sourceStrategy.DisplayName} 类型不支持转换";
                return false;
            }

            if (targetStrategy != null && !targetStrategy.SupportsConversion)
            {
                reason = $"{targetStrategy.DisplayName} 类型不支持转换";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 预览类型转换影响
        /// </summary>
        /// <param name="bind">目标 Bind 组件</param>
        /// <param name="targetType">目标类型</param>
        /// <returns>转换结果（预览模式）</returns>
        public static TypeConvertResult Preview(AbstractBind bind, BindType targetType)
        {
            if (!CanConvert(bind, targetType, out string reason))
            {
                return TypeConvertResult.Failed(reason, bind?.Bind ?? BindType.Member, targetType);
            }

            var result = TypeConvertResult.Successful(bind.Bind, targetType);
            AnalyzeConversionImpact(bind, targetType, result);
            return result;
        }

        /// <summary>
        /// 执行类型转换
        /// </summary>
        /// <param name="bind">目标 Bind 组件</param>
        /// <param name="targetType">目标类型</param>
        /// <returns>转换结果</returns>
        public static TypeConvertResult Execute(AbstractBind bind, BindType targetType)
        {
            // 先预览
            var result = Preview(bind, targetType);
            if (!result.CanExecute)
            {
                return result;
            }

            try
            {
                // 执行转换
                ExecuteConversion(bind, targetType, result);
                result.Success = true;
            }
            catch (System.Exception e)
            {
                result.Success = false;
                result.ErrorMessage = $"转换执行失败: {e.Message}";
                Debug.LogError($"[BindTypeConverter] {result.ErrorMessage}\n{e.StackTrace}");
            }

            return result;
        }

        #endregion

        #region 影响分析

        /// <summary>
        /// 分析转换影响
        /// </summary>
        private static void AnalyzeConversionImpact(
            AbstractBind bind,
            BindType targetType,
            TypeConvertResult result)
        {
            var sourceType = bind.Bind;
            string bindName = bind.Name;
            string panelName = GetPanelName(bind);

            // 创建代码生成上下文
            var context = UICodeGenContext.Create(panelName);

            // 分析源类型影响（需要删除的文件）
            AnalyzeSourceTypeImpact(sourceType, bindName, context, result);

            // 分析目标类型影响（需要创建的文件）
            AnalyzeTargetTypeImpact(targetType, bindName, context, result);

            // 检查命名冲突
            CheckNameConflicts(bindName, targetType, context, result);
        }

        /// <summary>
        /// 分析源类型影响（需要删除的文件）
        /// </summary>
        private static void AnalyzeSourceTypeImpact(
            BindType sourceType,
            string bindName,
            UICodeGenContext context,
            TypeConvertResult result)
        {
            var strategy = BindStrategyRegistry.Get(sourceType);
            if (strategy == null || !strategy.RequiresClassFile) return;

            var bindInfo = new BindCodeInfo { Type = bindName, Bind = sourceType };

            // 获取需要删除的文件路径
            var scriptPath = strategy.GetScriptPath(bindInfo, context, false);
            var designerPath = strategy.GetScriptPath(bindInfo, context, true);

            if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
                result.AddFileToDelete(scriptPath);
            if (!string.IsNullOrEmpty(designerPath) && File.Exists(designerPath))
                result.AddFileToDelete(designerPath);

            // 需要修改 Panel 的 Designer 文件
            var panelDesignerPath = $"{context.ScriptRootPath}/{context.PanelName}/{context.PanelName}.Designer.cs";
            result.AddFileToModify(panelDesignerPath);
        }

        /// <summary>
        /// 分析目标类型影响（需要创建的文件）
        /// </summary>
        private static void AnalyzeTargetTypeImpact(
            BindType targetType,
            string bindName,
            UICodeGenContext context,
            TypeConvertResult result)
        {
            var strategy = BindStrategyRegistry.Get(targetType);
            if (strategy == null || !strategy.RequiresClassFile) return;

            var bindInfo = new BindCodeInfo { Type = bindName, Bind = targetType };

            // 获取需要创建的文件路径
            var scriptPath = strategy.GetScriptPath(bindInfo, context, false);
            var designerPath = strategy.GetScriptPath(bindInfo, context, true);

            if (!string.IsNullOrEmpty(scriptPath))
                result.AddFileToCreate(scriptPath);
            if (!string.IsNullOrEmpty(designerPath))
                result.AddFileToCreate(designerPath);

            // 需要修改 Panel 的 Designer 文件
            var panelDesignerPath = $"{context.ScriptRootPath}/{context.PanelName}/{context.PanelName}.Designer.cs";
            result.AddFileToModify(panelDesignerPath);
        }

        /// <summary>
        /// 检查命名冲突
        /// </summary>
        private static void CheckNameConflicts(
            string bindName,
            BindType targetType,
            UICodeGenContext context,
            TypeConvertResult result)
        {
            var strategy = BindStrategyRegistry.Get(targetType);
            if (strategy == null || !strategy.RequiresClassFile) return;

            var bindInfo = new BindCodeInfo { Type = bindName, Bind = targetType };
            var conflictPath = strategy.GetScriptPath(bindInfo, context, false);

            if (!string.IsNullOrEmpty(conflictPath) && File.Exists(conflictPath))
            {
                result.HasNameConflict = true;
                result.ConflictFilePath = conflictPath;
            }
        }

        #endregion

        #region 执行转换

        /// <summary>
        /// 执行转换操作
        /// </summary>
        private static void ExecuteConversion(
            AbstractBind bind,
            BindType targetType,
            TypeConvertResult result)
        {
            // 1. 更新 Bind 组件的类型
            Undo.RecordObject(bind, "Convert Bind Type");
            bind.bind = targetType;
            EditorUtility.SetDirty(bind);

            // 2. 删除需要删除的文件
            foreach (var filePath in result.FilesToDelete)
            {
                if (File.Exists(filePath))
                {
                    AssetDatabase.DeleteAsset(filePath);
                }
            }

            // 3. 创建需要创建的文件（这里只是标记，实际创建由代码生成器完成）
            // 文件创建将在下次代码生成时自动完成

            // 4. 保存 Prefab
            var prefabRoot = GetPrefabRoot(bind.gameObject);
            if (prefabRoot != null)
            {
                PrefabUtility.SavePrefabAsset(prefabRoot);
            }

            AssetDatabase.Refresh();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取 Bind 所属的 Panel 名称
        /// </summary>
        private static string GetPanelName(AbstractBind bind)
        {
            if (bind == null)
                return "Unknown";

            // 向上查找 UIPanel 组件
            var current = bind.transform;
            while (current != null)
            {
                var panel = current.GetComponent<UIPanel>();
                if (panel != null)
                {
                    return current.name;
                }
                current = current.parent;
            }

            // 如果没找到 UIPanel，使用根节点名称
            var root = bind.transform.root;
            return root != null ? root.name : "Unknown";
        }

        /// <summary>
        /// 获取 Prefab 根节点
        /// </summary>
        private static GameObject GetPrefabRoot(GameObject obj)
        {
            if (obj == null)
                return null;

            // 检查是否是 Prefab 实例
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                return prefabStage.prefabContentsRoot;
            }

            // 检查是否是 Prefab 资产
            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(assetPath))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }

            return null;
        }

        /// <summary>
        /// 获取转换的描述文本
        /// </summary>
        public static string GetConversionDescription(BindType sourceType, BindType targetType)
        {
            var sourceName = BindStrategyRegistry.GetDisplayName(sourceType);
            var targetName = BindStrategyRegistry.GetDisplayName(targetType);
            return $"{sourceName} → {targetName}";
        }

        /// <summary>
        /// 获取类型的显示名称
        /// </summary>
        private static string GetTypeDisplayName(BindType type)
        {
            return BindStrategyRegistry.GetDisplayName(type);
        }

        #endregion
    }
}
