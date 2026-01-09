#if UNITY_EDITOR
using System.Linq;

namespace YokiFrame
{
    /// <summary>
    /// DefaultUICodeGenTemplate - 字段和方法生成
    /// </summary>
    public partial class DefaultUICodeGenTemplate
    {
        #region 字段生成

        /// <summary>
        /// 生成绑定字段（Panel Designer 用）
        /// </summary>
        protected virtual void WriteBindFields(ClassCodeScope cls, BindCodeInfo bindCodeInfo, UICodeGenContext context)
        {
            var sortList = bindCodeInfo.MemberDic.Values.OrderBy(info => info.order).ToList();

            foreach (var bindInfo in sortList)
            {
                if (bindInfo.RepeatElement) continue;

                // 使用策略获取完整类型名
                var strategy = BindStrategyRegistry.Get(bindInfo.Bind);
                var typeName = strategy?.GetFullTypeName(bindInfo, context) ?? bindInfo.Type;

                // 使用 Fluent API 生成字段
                cls.SerializeField(typeName, bindInfo.Name, bindInfo.Comment);

                // 递归生成子元素/组件
                WriteBindTypeCode(bindInfo, context);
            }
        }

        /// <summary>
        /// 生成绑定字段（Element/Component Designer 用）
        /// </summary>
        protected virtual void WriteBindFieldsForDesigner(ClassCodeScope cls, BindCodeInfo bindCodeInfo, UICodeGenContext context, bool isComponent)
        {
            var sortList = bindCodeInfo.MemberDic.Values.OrderBy(info => info.order).ToList();

            foreach (var bindInfo in sortList)
            {
                if (bindInfo.RepeatElement) continue;

                cls.SerializeField(bindInfo.Type, bindInfo.Name, bindInfo.Comment);

                // 递归生成子类型（Component 下不生成 Element）
                if (!isComponent || bindInfo.Bind != BindType.Element)
                {
                    WriteBindTypeCode(bindInfo, context);
                }
            }
        }

        #endregion

        #region 方法生成

        /// <summary>
        /// 生成 Clear 方法
        /// </summary>
        protected virtual void WriteClearMethod(ClassCodeScope cls, BindCodeInfo bindCodeInfo, string methodName, bool isOverride)
        {
            if (isOverride)
            {
                cls.ProtectedOverrideVoid(methodName, method =>
                {
                    method.WithBody(body => WriteClearMethodBody(body, bindCodeInfo, true));
                });
            }
            else
            {
                cls.VoidMethod(methodName, method =>
                {
                    method.WithBody(body => WriteClearMethodBody(body, bindCodeInfo, false));
                });
            }
        }

        /// <summary>
        /// 写入 Clear 方法体
        /// </summary>
        protected virtual void WriteClearMethodBody(ICodeScope body, BindCodeInfo bindCodeInfo, bool clearData)
        {
            foreach (var bindInfo in bindCodeInfo.MemberDic.Values)
            {
                if (bindInfo.RepeatElement) continue;

                var strategy = BindStrategyRegistry.Get(bindInfo.Bind);
                if (strategy != null && strategy.RequiresClassFile)
                {
                    body.Custom($"{bindInfo.Name}.Clear();");
                }
                body.Custom($"{bindInfo.Name} = default;");
            }

            if (clearData)
            {
                body.EmptyLine();
                body.Custom("mData = null;");
            }
        }

        /// <summary>
        /// 生成 Data 属性
        /// </summary>
        protected virtual void WriteDataProperty(ClassCodeScope cls, string panelName)
        {
            var dataTypeName = $"{panelName}Data";

            cls.PrivateField(dataTypeName, "mData");
            cls.Property(dataTypeName, "Data", prop =>
            {
                prop.WithGetter(getter =>
                {
                    getter.Custom("return mData;");
                });
            });
        }

        #endregion

        #region Panel 生命周期方法

        /// <summary>
        /// 生成 Panel 基础生命周期方法
        /// </summary>
        protected virtual void WritePanelLifecycleMethods(ClassCodeScope cls, string dataName)
        {
            // OnInit
            cls.ProtectedOverrideVoid("OnInit", method =>
            {
                method.WithParameter(nameof(IUIData), "uiData", "null");
                method.WithBody(body =>
                {
                    body.Custom($"mData = uiData as {dataName} ?? new {dataName}();");
                    body.Custom("// 在此添加初始化代码");
                });
            });
            cls.EmptyLine();

            // OnOpen
            cls.ProtectedOverrideVoid("OnOpen", method =>
            {
                method.WithParameter(nameof(IUIData), "uiData", "null");
                method.WithBody(body =>
                {
                    body.Custom($"mData = uiData as {dataName} ?? mData;");
                });
            });
            cls.EmptyLine();

            // OnShow
            cls.ProtectedOverrideVoid("OnShow", method =>
            {
                method.WithBody(_ => { });
            });
            cls.EmptyLine();

            // OnHide
            cls.ProtectedOverrideVoid("OnHide", method =>
            {
                method.WithBody(_ => { });
            });
            cls.EmptyLine();

            // OnClose
            cls.ProtectedOverrideVoid("OnClose", method =>
            {
                method.WithBody(_ => { });
            });
        }

        /// <summary>
        /// 生成 Panel 扩展生命周期钩子
        /// </summary>
        protected virtual void WritePanelLifecycleHooks(ClassCodeScope cls)
        {
            cls.EmptyLine();
            cls.Custom("#region 生命周期钩子");
            cls.EmptyLine();

            cls.ProtectedOverrideVoid("OnWillShow", method =>
            {
                method.WithBody(body => body.Comment("面板即将显示时调用"));
            });
            cls.EmptyLine();

            cls.ProtectedOverrideVoid("OnDidShow", method =>
            {
                method.WithBody(body => body.Comment("面板显示完成后调用"));
            });
            cls.EmptyLine();

            cls.ProtectedOverrideVoid("OnWillHide", method =>
            {
                method.WithBody(body => body.Comment("面板即将隐藏时调用"));
            });
            cls.EmptyLine();

            cls.ProtectedOverrideVoid("OnDidHide", method =>
            {
                method.WithBody(body => body.Comment("面板隐藏完成后调用"));
            });

            cls.EmptyLine();
            cls.Custom("#endregion");
        }

        /// <summary>
        /// 生成 Panel 焦点支持
        /// </summary>
        protected virtual void WritePanelFocusSupport(ClassCodeScope cls)
        {
            cls.EmptyLine();
            cls.Custom("#region 焦点导航");
            cls.EmptyLine();

            cls.OverrideMethod("GameObject", "GetDefaultFocusTarget", method =>
            {
                method.WithBody(body =>
                {
                    body.Comment("返回默认焦点目标，返回 null 则不自动设置焦点");
                    body.Custom("return null;");
                });
            });

            cls.EmptyLine();
            cls.Custom("#endregion");
        }

        #endregion
    }
}
#endif
