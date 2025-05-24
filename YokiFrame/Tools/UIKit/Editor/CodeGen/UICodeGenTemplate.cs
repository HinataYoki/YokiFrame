using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace YokiFrame
{
    public class UICodeGenTemplate
    {
        #region PanelCodeTemplate
        /// <summary>
        /// UI面板代码模板
        /// </summary>
        /// <param name="name">面板名称</param>
        /// <param name="scriptFilePath">代码路径</param>
        /// <param name="scriptNamespace">命名空间</param>
        public static void WritePanel(string name, string scriptFilePath, string scriptNamespace)
        {
            if (File.Exists(scriptFilePath)) return;

            var writer = File.CreateText(scriptFilePath);
            var codeWriter = new FileCodeWriteKit(writer);
            var rootCode = new RootCode()
                .Using(nameof(UnityEngine))
                .Using("UnityEngine.UI")
                .Using(nameof(YokiFrame))
                .EmptyLine()
                .Namespace(scriptNamespace, scope =>
                {
                    var DataName = $"{name}Data";
                    scope
                    .Class(DataName, nameof(IUIData), false, false, classScope => { })
                    .Class(name, nameof(UIPanel), true, false, classScope =>
                    {
                        classScope
                        .CustomScope($"protected override void OnInit({nameof(IUIData)} uiData = null)", false, function =>
                        {
                            function
                            .Custom($"mData = uiData as {DataName} ?? new {DataName}();")
                            .Custom("// please add init code here");
                        })
                        .EmptyLine()
                        .CustomScope("protected override void OnOpen()", false, function => { })
                        .EmptyLine()
                        .CustomScope("protected override void OnShow()", false, function => { })
                        .EmptyLine()
                        .CustomScope("protected override void OnHide()", false, function => { })
                        .EmptyLine()
                        .CustomScope("protected override void OnClose()", false, function => { });
                    });
                });
            rootCode.Gen(codeWriter);
            codeWriter.Dispose();
        }
        /// <summary>
        /// 面板定义代码模板
        /// </summary>
        /// <param name="name">面板名称</param>
        /// <param name="designerPath">定义路径</param>
        /// <param name="scriptNamespace">命名空间</param>
        /// <param name="panelCodeInfo">绑定信息</param>
        public static void WritePanelDesigner(string name, string designerPath, string scriptNamespace, BindCodeInfo panelCodeInfo)
        {
            var codeContext = new UIGenCodeContext
            {
                PanelName = name,
                ElementPath = $"{UIKitCreateConfig.Instance.ScriptGeneratePath}/{name}/{nameof(UIElement)}",
                ComponentPath = $"{UIKitCreateConfig.Instance.ScriptGeneratePath}/{nameof(UIComponent)}",
                ScriptNamespace = scriptNamespace,
            };

            var writer = File.CreateText(designerPath);
            var codeWriter = new FileCodeWriteKit(writer);
            var root = new RootCode()
                .Using(nameof(System))
                .Using(nameof(UnityEngine))
                .Using("UnityEngine.UI")
                .Using($"{scriptNamespace}.{name}{nameof(UIElement)}")
                .Using(nameof(YokiFrame))
                .EmptyLine()
                .Namespace(scriptNamespace, scope =>
                {
                    scope.Custom($"// Generate Id:{Guid.NewGuid()}");
                    scope.Class(name, string.Empty, true, false, classScope =>
                    {
                        foreach (var bindInfo in panelCodeInfo.MemberDic.Values)
                        {
                            if (!string.IsNullOrEmpty(bindInfo.Comment))
                            {
                                classScope.Custom("/// <summary>");
                                classScope.Custom("/// " + bindInfo.Comment);
                                classScope.Custom("/// </summary>");
                            }
                            classScope.Custom("[SerializeField]");
                            classScope.Custom($"public {bindInfo.TypeName} m{bindInfo.Name};");

                            RecursionGen(bindInfo, codeContext);
                        }
                        classScope.EmptyLine();

                        classScope.CustomScope("protected override void ClearUIComponents()", false, (function) =>
                        {
                            foreach (var bindInfo in panelCodeInfo.MemberDic.Values)
                            {
                                function.Custom($"m{bindInfo.Name} = default;");
                            }

                            function.EmptyLine();
                            function.Custom("mData = null;");
                        });

                        classScope.EmptyLine();
                        classScope.Custom($"{name}Data mData;");
                        classScope.CustomScope($"public {name}Data Data", false, (property) =>
                        {
                            property.CustomScope("get", false, (getter) => { getter.Custom("return mData;"); });
                        });
                    });
                });
            root.Gen(codeWriter);
            codeWriter.Dispose();
        }
        #endregion

        #region ElementCodeTemplate
        /// <summary>
        /// UI组件元素代码模板
        /// </summary>
        public static void WriteElement(BindCodeInfo bindCodeInfo, UIGenCodeContext codeContext)
        {
            var name = bindCodeInfo.TypeName;
            var scriptFilePath = codeContext.ElementPath + $"/{name}.cs";
            if (!File.Exists(scriptFilePath))
            {
                Directory.CreateDirectory(PathUtils.GetDirectoryPath(scriptFilePath));
                var writer = File.CreateText(scriptFilePath);
                var codeWriter = new FileCodeWriteKit(writer);
                var rootCode = new RootCode()
                    .Custom("/****************************************************************************")
                    .Custom($"{DateTime.Now.Year}.{DateTime.Now.Month} {SystemInfo.deviceName}")
                    .Custom("****************************************************************************/")
                    .EmptyLine()
                    .Using(nameof(UnityEngine))
                    .Using("UnityEngine.UI")
                    .Using(nameof(YokiFrame))
                    .EmptyLine()
                    .Namespace($"{codeContext.ScriptNamespace}.{codeContext.PanelName}{nameof(UIElement)}", scope =>
                    {
                        scope.Class(name, nameof(UIElement), true, false, classScope =>
                        {

                        });
                    });

                rootCode.Gen(codeWriter);
                codeWriter.Dispose();
            }
            WriteElementDesigner(bindCodeInfo, codeContext);
        }
        /// <summary>
        /// UI元素定义代码模板
        /// </summary>
        public static void WriteElementDesigner(BindCodeInfo bindCodeInfo, UIGenCodeContext codeContext)
        {
            var name = bindCodeInfo.TypeName;
            var scriptFilePath = codeContext.ElementPath + $"/{bindCodeInfo.TypeName}.Designer.cs";
            Directory.CreateDirectory(PathUtils.GetDirectoryPath(scriptFilePath));
            var writer = File.CreateText(scriptFilePath);
            var codeWriter = new FileCodeWriteKit(writer);
            var rootCode = new RootCode()
                .Custom("/****************************************************************************")
                .Custom($"{DateTime.Now.Year}.{DateTime.Now.Month} {SystemInfo.deviceName}")
                .Custom("****************************************************************************/")
                .EmptyLine()
                .Using(nameof(UnityEngine))
                .Using("UnityEngine.UI")
                .Using(nameof(YokiFrame))
                .EmptyLine()
                .Namespace($"{codeContext.ScriptNamespace}.{codeContext.PanelName}{nameof(UIElement)}", scope =>
                {
                    scope.Class(name, string.Empty, true, false, classScope =>
                    {
                        foreach (var bindInfo in bindCodeInfo.MemberDic.Values)
                        {
                            if (!string.IsNullOrEmpty(bindInfo.Comment))
                            {
                                classScope.Custom("/// <summary>");
                                classScope.Custom("/// " + bindInfo.Comment);
                                classScope.Custom("/// </summary>");
                            }

                            classScope.Custom("[SerializeField]");
                            classScope.Custom($"public {bindInfo.TypeName} m{bindInfo.Name};");

                        }

                        classScope.EmptyLine();
                        classScope.Custom($"public override string Name => \"{bindCodeInfo.Name}\";");
                        classScope.Custom($"public override string TypeName => \"{bindCodeInfo.TypeName}\";");
                        classScope.Custom($"public override string Comment => \"{bindCodeInfo.Comment}\";");

                        classScope.EmptyLine();
                        classScope.CustomScope("public void Clear()", false, (property) =>
                        {
                            foreach (var bindInfo in bindCodeInfo.MemberDic.Values)
                            {
                                property.Custom($"m{bindInfo.Name} = default;");
                            }
                        });
                    });
                });

            rootCode.Gen(codeWriter);
            codeWriter.Dispose();
        }
        #endregion

        #region ComponentCodeTemplate
        /// <summary>
        /// UI组件代码模板
        /// </summary>
        public static void WriteComponent(BindCodeInfo bindCodeInfo, UIGenCodeContext codeContext)
        {
            var name = bindCodeInfo.TypeName;
            var scriptFilePath = codeContext.ComponentPath + $"/{name}.cs";
            if (!File.Exists(scriptFilePath))
            {
                Directory.CreateDirectory(PathUtils.GetDirectoryPath(scriptFilePath));
                var writer = File.CreateText(scriptFilePath);
                var codeWriter = new FileCodeWriteKit(writer);
                var rootCode = new RootCode()
                    .Custom("/****************************************************************************")
                    .Custom($"{DateTime.Now.Year}.{DateTime.Now.Month} {SystemInfo.deviceName}")
                    .Custom("****************************************************************************/")
                    .EmptyLine()
                    .Using(nameof(UnityEngine))
                    .Using("UnityEngine.UI")
                    .Using(nameof(YokiFrame))
                    .EmptyLine()
                    .Namespace(codeContext.ScriptNamespace, scope =>
                    {
                        scope.Class(name, nameof(UIComponent), true, false, fatherClass =>
                        {
                        });
                    });

                rootCode.Gen(codeWriter);
                codeWriter.Dispose();
            }
            WriteComponentDesigner(bindCodeInfo, codeContext);
        }
        /// <summary>
        /// UI组件定义代码模板
        /// </summary>
        public static void WriteComponentDesigner(BindCodeInfo bindCodeInfo, UIGenCodeContext codeContext)
        {
            var name = bindCodeInfo.TypeName;
            var scriptFilePath = codeContext.ComponentPath + $"/{bindCodeInfo.TypeName}.Designer.cs";

            Directory.CreateDirectory(PathUtils.GetDirectoryPath(scriptFilePath));
            var writer = File.CreateText(scriptFilePath);
            var codeWriter = new FileCodeWriteKit(writer);
            var rootCode = new RootCode()
                .Custom("/****************************************************************************")
                .Custom($"{DateTime.Now.Year}.{DateTime.Now.Month} {SystemInfo.deviceName}")
                .Custom("****************************************************************************/")
                .EmptyLine()
                .Using(nameof(UnityEngine))
                .Using("UnityEngine.UI")
                .Using(nameof(YokiFrame))
                .EmptyLine()
                .Namespace(codeContext.ScriptNamespace, scope =>
                {
                    scope.Class(name, string.Empty, true, false, classScope =>
                    {
                        foreach (var bindInfo in bindCodeInfo.MemberDic.Values)
                        {
                            if (!string.IsNullOrEmpty(bindInfo.Comment))
                            {
                                classScope.Custom("/// <summary>");
                                classScope.Custom("/// " + bindInfo.Comment);
                                classScope.Custom("/// </summary>");
                            }

                            classScope.Custom("[SerializeField]");
                            classScope.Custom($"public {bindInfo.TypeName} m{bindInfo.Name};");

                            RecursionGen(bindInfo, codeContext);
                        }

                        classScope.EmptyLine();
                        classScope.Custom($"public override string Name => \"{bindCodeInfo.Name}\";");
                        classScope.Custom($"public override string TypeName => \"{bindCodeInfo.TypeName}\";");
                        classScope.Custom($"public override string Comment => \"{bindCodeInfo.Comment}\";");

                        classScope.EmptyLine();
                        classScope.CustomScope("public void Clear()", false, (property) =>
                        {
                            foreach (var bindInfo in bindCodeInfo.MemberDic.Values)
                            {
                                property.Custom($"m{bindInfo.Name} = default;");
                            }
                        });
                    });
                });

            rootCode.Gen(codeWriter);
            codeWriter.Dispose();
        }
        #endregion

        /// <summary>
        /// 递归生成元素和组件代码
        /// </summary>
        /// <param name="bindInfo"></param>
        /// <param name="codeContext"></param>
        private static void RecursionGen(BindCodeInfo bindInfo, UIGenCodeContext codeContext)
        {
            if (bindInfo.BindScript.Bind is BindType.Element)
            {
                if (!codeContext.AlreadyElementSet.Contains(bindInfo.TypeName))
                {
                    codeContext.AlreadyElementSet.Add(bindInfo.TypeName);
                    WriteElement(bindInfo, codeContext);
                }
            }
            else if (bindInfo.BindScript.Bind is BindType.Component)
            {
                if (!codeContext.AlreadyElementSet.Contains(bindInfo.TypeName))
                {
                    codeContext.AlreadyElementSet.Add(bindInfo.TypeName);
                    WriteComponent(bindInfo, codeContext);
                }
            }
        }
    }

    /// <summary>
    /// UI代码生成上下文
    /// </summary>
    public class UIGenCodeContext
    {
        /// <summary>
        /// 面板名称
        /// </summary>
        public string PanelName;
        /// <summary>
        /// 元素路径
        /// </summary>
        public string ElementPath;
        /// <summary>
        /// 组件路径
        /// </summary>
        public string ComponentPath;
        /// <summary>
        /// 命名空间
        /// </summary>
        public string ScriptNamespace;
        /// <summary>
        /// 已生成的元素和组件集合
        /// </summary>
        public HashSet<string> AlreadyElementSet = new();
    }
}