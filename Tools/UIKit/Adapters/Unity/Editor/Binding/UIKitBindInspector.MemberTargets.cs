#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.Unity.Inspector;

namespace YokiFrame
{
    internal sealed partial class UIKitBindInspector
    {
        /// <summary>按当前序列化值重建已选组件列表和按需添加入口。</summary>
        private void RefreshMemberTargetEditor()
        {
            if (mMemberTargetsContainer == null)
                return;
            serializedObject.Update();
            InspectorSelectionListOptions<Component> options = new()
            {
                EmptyLabel = "尚未选择组件",
                AddLabel = "+ 添加组件",
                MinimumCount = 1,
                RefreshAfterChange = false,
                SelectedItemsProvider = GetSelectedMemberComponents,
                AvailableItemsProvider = GetAvailableMemberComponents,
                DisplayNameFactory = GetComponentDisplayName,
                TooltipFactory = component => component.GetType().FullName,
                DetailFactory = CreateMemberTargetDetail,
                AddItem = component => SetMemberSelected(component, true),
                RemoveItem = component => SetMemberSelected(component, false),
            };
            InspectorKitUi.Refresh(mMemberTargetsContainer, body =>
            {
                body.Add(InspectorKitUi.CreateSelectionList(options));
            });
        }

        /// <summary>读取当前已选组件，保持序列化列表的原始顺序。</summary>
        private IReadOnlyList<Component> GetSelectedMemberComponents()
        {
            List<MemberTargetValue> values = ReadMemberTargetValues();
            List<Component> selected = new(values.Count);
            for (var index = 0; index < values.Count; index++)
            {
                Component component = values[index].Target;
                if (component != default && !selected.Contains(component))
                    selected.Add(component);
            }
            return selected;
        }

        /// <summary>返回尚未绑定的同节点非 Bind 组件，供添加菜单按需读取。</summary>
        private IReadOnlyList<Component> GetAvailableMemberComponents()
        {
            List<MemberTargetValue> values = ReadMemberTargetValues();
            List<Component> available = new();
            for (var index = 0; index < mComponents.Count; index++)
            {
                Component component = mComponents[index];
                if (FindMemberTargetIndex(values, component) < 0)
                    available.Add(component);
            }
            return available;
        }

        /// <summary>为已选组件创建独立字段名输入，编辑时只刷新列表内容。</summary>
        private VisualElement CreateMemberTargetDetail(Component component)
        {
            MemberTargetValue selected = FindMemberTarget(ReadMemberTargetValues(), component);
            if (selected == null)
                return default;
            TextField nameField = new() { value = selected.Name };
            nameField.RegisterValueChangedCallback(evt => WriteMemberName(component, evt.newValue));
            return InspectorKitUi.CreateFieldRow("字段名称", nameField);
        }

        /// <summary>切换一个组件的选择状态，并保持至少一个 Member 目标。</summary>
        private void SetMemberSelected(Component component, bool selected)
        {
            serializedObject.Update();
            List<MemberTargetValue> values = ReadMemberTargetValues();
            int selectedIndex = FindMemberTargetIndex(values, component);
            if (selected && selectedIndex < 0)
                AddMemberTarget(values, component);
            else if (!selected && selectedIndex >= 0 && values.Count > 1)
                values.RemoveAt(selectedIndex);
            else
            {
                RefreshMemberTargetEditor();
                return;
            }

            WriteMemberTargetValues(values);
            RefreshInspectorState();
        }

        /// <summary>向当前选择追加组件，并按节点顺序生成稳定默认字段名。</summary>
        private void AddMemberTarget(List<MemberTargetValue> values, Component component)
        {
            List<BindMemberTarget> targets = new(values.Count + 1);
            for (var index = 0; index < values.Count; index++)
            {
                targets.Add(new BindMemberTarget
                {
                    Target = values[index].Target,
                    Name = values[index].Name,
                });
            }
            targets.Add(new BindMemberTarget { Target = component });
            AbstractBind bind = target as AbstractBind;
            string fieldName = UIKitBindMemberNaming.CreateDefaultName(
                bind,
                component,
                targets,
                targets.Count - 1);
            values.Add(new MemberTargetValue { Target = component, Name = fieldName });
        }

        /// <summary>读取显式多目标；列表为空时把旧 Target 临时视为唯一首项。</summary>
        private List<MemberTargetValue> ReadMemberTargetValues()
        {
            List<MemberTargetValue> values = new();
            for (var index = 0; index < mMemberTargets.arraySize; index++)
            {
                SerializedProperty item = mMemberTargets.GetArrayElementAtIndex(index);
                values.Add(new MemberTargetValue
                {
                    Target = item.FindPropertyRelative("mTarget").objectReferenceValue as Component,
                    Name = item.FindPropertyRelative("mName").stringValue,
                });
            }

            Component legacyTarget = mTarget.objectReferenceValue as Component;
            if (values.Count == 0 && legacyTarget != default)
                values.Add(new MemberTargetValue { Target = legacyTarget, Name = mName.stringValue });
            return values;
        }

        /// <summary>写入多目标列表；只剩一项时折叠回旧字段避免无意义升级。</summary>
        private void WriteMemberTargetValues(List<MemberTargetValue> values)
        {
            serializedObject.Update();
            bool storeList = values.Count > 1;
            mMemberTargets.arraySize = storeList ? values.Count : 0;
            if (storeList)
            {
                for (var index = 0; index < values.Count; index++)
                    WriteMemberTargetElement(index, values[index]);
            }
            if (values.Count > 0)
                SyncLegacyMemberTarget(values[0]);
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }

        /// <summary>写入列表中的一个组件引用和字段名。</summary>
        private void WriteMemberTargetElement(int index, MemberTargetValue value)
        {
            SerializedProperty item = mMemberTargets.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("mTarget").objectReferenceValue = value.Target;
            item.FindPropertyRelative("mName").stringValue = value.Name ?? string.Empty;
        }

        /// <summary>把首项同步到旧 Target、Name 和类型字段。</summary>
        private void SyncLegacyMemberTarget(MemberTargetValue primary)
        {
            string typeName = primary.Target == default
                ? string.Empty
                : primary.Target.GetType().FullName;
            mTarget.objectReferenceValue = primary.Target;
            mName.stringValue = primary.Name ?? string.Empty;
            mAutoType.stringValue = typeName;
            if (CurrentBindType() == BindType.Member)
                mType.stringValue = typeName;
        }

        /// <summary>修改一个已选组件的字段名，并在首项变化时同步旧 Name。</summary>
        private void WriteMemberName(Component component, string value)
        {
            serializedObject.Update();
            List<MemberTargetValue> values = ReadMemberTargetValues();
            int index = FindMemberTargetIndex(values, component);
            if (index < 0)
                return;
            values[index].Name = value ?? string.Empty;
            WriteMemberTargetValues(values);
            if (index == 0)
                mNameField.SetValueWithoutNotify(values[index].Name);
            RefreshInspectorState(false);
        }

        /// <summary>修改旧 Name，并在多目标模式同步首项字段名。</summary>
        private void WritePrimaryName(string value)
        {
            serializedObject.Update();
            string normalized = value ?? string.Empty;
            mName.stringValue = normalized;
            if (mMemberTargets.arraySize > 0)
            {
                SerializedProperty first = mMemberTargets.GetArrayElementAtIndex(0);
                first.FindPropertyRelative("mName").stringValue = normalized;
            }
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>在已有多目标数据上恢复旧字段，供旧消费者继续读取首项。</summary>
        private bool EnsureLegacyMemberTarget()
        {
            SerializedProperty first = mMemberTargets.GetArrayElementAtIndex(0);
            Component component = first.FindPropertyRelative("mTarget").objectReferenceValue as Component;
            if (component == default)
                return false;
            SerializedProperty name = first.FindPropertyRelative("mName");
            bool changed = false;
            if (string.IsNullOrWhiteSpace(name.stringValue))
            {
                AbstractBind bind = target as AbstractBind;
                name.stringValue = UIKitBindMemberNaming.CreateDefaultName(
                    bind,
                    component,
                    bind.MemberTargets,
                    0);
                changed = true;
            }
            changed |= mTarget.objectReferenceValue != component;
            mTarget.objectReferenceValue = component;
            changed |= SetStringIfDifferent(mName, name.stringValue);
            changed |= SetStringIfDifferent(mAutoType, component.GetType().FullName);
            changed |= SetStringIfDifferent(mType, component.GetType().FullName);
            return changed;
        }

        /// <summary>判断当前配置是否使用内置 Member 的多目标列表。</summary>
        private bool AllowsMultipleMemberSelection()
        {
            AbstractBind bind = target as AbstractBind;
            return bind != default
                && CurrentBindType() == BindType.Member
                && UIKitBindStrategyRegistry.TryGetBuiltIn(BindType.Member, out _);
        }

        /// <summary>判断当前内置 Member 是否已经保存显式多目标数据。</summary>
        private bool IsBuiltInMultiMember()
        {
            return mMemberTargets != null
                && mMemberTargets.arraySize > 0
                && AllowsMultipleMemberSelection();
        }

        /// <summary>校验 Inspector 中每个多目标的节点归属、唯一性和字段名。</summary>
        private bool TryValidateMemberTargets(out string error)
        {
            AbstractBind bind = target as AbstractBind;
            List<MemberTargetValue> values = ReadMemberTargetValues();
            HashSet<Component> targets = new();
            HashSet<string> names = new(StringComparer.Ordinal);
            for (var index = 0; index < values.Count; index++)
            {
                MemberTargetValue value = values[index];
                if (value.Target == default
                    || value.Target is AbstractBind
                    || value.Target.gameObject != bind.gameObject)
                    return FailMemberValidation("Member 目标必须位于 Bind 所在 GameObject。", out error);
                if (!targets.Add(value.Target))
                    return FailMemberValidation("同一组件不能重复绑定。", out error);
                try
                {
                    CodeGenKit.RequireIdentifier(value.Name, nameof(value.Name));
                }
                catch (ArgumentException exception)
                {
                    return FailMemberValidation(exception.Message, out error);
                }
                if (!names.Add(value.Name))
                    return FailMemberValidation("字段名重复: " + value.Name, out error);
            }
            error = string.Empty;
            return true;
        }

        /// <summary>生成多目标 Designer 字段预览。</summary>
        private string BuildMultiMemberCodePreview()
        {
            List<MemberTargetValue> values = ReadMemberTargetValues();
            StringBuilder builder = new(values.Count * 64);
            for (var index = 0; index < values.Count; index++)
            {
                if (index > 0)
                    builder.AppendLine();
                AppendPreviewComment(builder);
                string typeName = values[index].Target == default
                    ? "GameObject"
                    : FormatTypeName(values[index].Target.GetType().FullName);
                builder.Append("public ").Append(typeName).Append(' ')
                    .Append(values[index].Name).Append(';');
            }
            return builder.ToString();
        }

        /// <summary>向字段预览追加当前 Bind 的可选 XML 注释。</summary>
        private void AppendPreviewComment(StringBuilder builder)
        {
            if (string.IsNullOrWhiteSpace(mComment.stringValue))
                return;
            builder.AppendLine("/// <summary>");
            builder.Append("/// ").AppendLine(mComment.stringValue);
            builder.AppendLine("/// </summary>");
        }

        /// <summary>按组件引用查找已选值。</summary>
        private static MemberTargetValue FindMemberTarget(
            List<MemberTargetValue> values,
            Component component)
        {
            int index = FindMemberTargetIndex(values, component);
            return index < 0 ? null : values[index];
        }

        /// <summary>按组件引用查找已选值索引。</summary>
        private static int FindMemberTargetIndex(
            List<MemberTargetValue> values,
            Component component)
        {
            for (var index = 0; index < values.Count; index++)
            {
                if (values[index].Target == component)
                    return index;
            }
            return -1;
        }

        /// <summary>为重复组件类型追加稳定序号，便于用户区分列表和候选项。</summary>
        private string GetComponentDisplayName(Component component)
        {
            int componentIndex = mComponents.IndexOf(component);
            Type type = component.GetType();
            if (componentIndex < 0)
                return type.Name;
            int typeIndex = 1;
            for (var index = 0; index < componentIndex; index++)
            {
                if (mComponents[index].GetType() == type)
                    typeIndex++;
            }
            return typeIndex == 1 ? type.Name : type.Name + " #" + typeIndex;
        }

        /// <summary>返回统一的 Member 校验失败结果。</summary>
        private static bool FailMemberValidation(string message, out string error)
        {
            error = message;
            return false;
        }

        /// <summary>保存 Inspector 编辑过程中的一个组件引用与字段名。</summary>
        private sealed class MemberTargetValue
        {
            internal Component Target;
            internal string Name;
        }
    }
}
#endif
