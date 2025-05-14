using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace YokiFrame
{
    public class UIPanelTemplate
    {
        public static void Write(string name, string scriptFilePath, string scriptNamespace)
        {
            if (File.Exists(scriptFilePath)) return;

            var writer = File.CreateText(scriptFilePath);
            var codeWriter = new FileCodeWriter(writer);
            var rootCode = new RootCode()
                .Using("UnityEngine")
                .Using("UnityEngine.UI")
                .Using(nameof(YokiFrame))
                .EmptyLine()
                .Namespace(scriptNamespace, scope =>
                {
                    scope.Class(name + "Data", "IUIData", false, false, classScope => { });

                    scope.Class(name, "UIPanel", true, false, classScope =>
                    {
                        classScope.CustomScope("protected override void OnInit(IUIData uiData = null)", false,
                            function =>
                            {
                                function.Custom(string.Format("mData = uiData as {0} ?? new {0}();", (name + "Data")));
                                function.Custom("// please add init code here");
                            });

                        classScope.EmptyLine();
                        classScope.CustomScope("protected override void OnOpen()", false,
                            function => { });

                        classScope.EmptyLine();
                        classScope.CustomScope("protected override void OnShow()", false,
                            function => { });
                        classScope.EmptyLine();
                        classScope.CustomScope("protected override void OnHide()", false,
                            function => { });

                        classScope.EmptyLine();
                        classScope.CustomScope("protected override void OnClose()", false,
                            function => { });
                    });
                });

            rootCode.Gen(codeWriter);
            codeWriter.Dispose();
        }

        public static void WriteDesigner(string name, string scriptFilePath, string scriptNamespace, PanelCodeInfo panelCodeInfo)
        {
            var writer = File.CreateText(scriptFilePath);

            var codeWriter = new FileCodeWriter(writer);
            var root = new RootCode()
                .Using("System")
                .Using("UnityEngine")
                .Using("UnityEngine.UI")
                .Using(nameof(YokiFrame))
                .EmptyLine()
                .Namespace(scriptNamespace, scope =>
                    {
                        scope.Custom(string.Format("// Generate Id:{0}", Guid.NewGuid().ToString()));
                        scope.Class(name, null, true, false, classScope =>
                        {
                            classScope.Custom("public const string Name = \"" + name + "\";");
                            classScope.EmptyLine();

                            foreach (var bindInfo in panelCodeInfo.BindInfos)
                            {
                                if (!string.IsNullOrEmpty(bindInfo.BindScript.Comment))
                                {
                                    classScope.Custom("/// <summary>");
                                    classScope.Custom("/// " + bindInfo.BindScript.Comment);
                                    classScope.Custom("/// </summary>");
                                }

                                classScope.Custom("[SerializeField]");
                                classScope.Custom("public " + bindInfo.BindScript.TypeName + " " + bindInfo.TypeName + ";");
                            }

                            classScope.EmptyLine();
                            classScope.Custom("private " + name + "Data mPrivateData = null;");

                            classScope.EmptyLine();

                            classScope.CustomScope("protected override void ClearUIComponents()", false, (function) =>
                            {
                                foreach (var bindInfo in panelCodeInfo.BindInfos)
                                {
                                    function.Custom(bindInfo.TypeName + " = null;");
                                }

                                function.EmptyLine();
                                function.Custom("mData = null;");
                            });

                            classScope.EmptyLine();

                            classScope.CustomScope("public " + name + "Data Data", false,
                                (property) =>
                                {
                                    property.CustomScope("get", false, (getter) => { getter.Custom("return mData;"); });
                                });

                            classScope.EmptyLine();


                            classScope.CustomScope(name + "Data mData", false, (property) =>
                            {
                                property.CustomScope("get", false,
                                    (getter) =>
                                    {
                                        getter.Custom("return mPrivateData ?? (mPrivateData = new " + name + "Data());");
                                    });

                                property.CustomScope("set", false, (setter) =>
                                {
                                    setter.Custom("mUIData = value;");
                                    setter.Custom("mPrivateData = value;");
                                });
                            });
                        });
                    });
            root.Gen(codeWriter);
            codeWriter.Dispose();
        }

        public static void WriteElement(string generateFilePath, string behaviourName, string nameSpace, ElementCodeInfo elementCodeInfo)
        {
            var sw = new StreamWriter(generateFilePath, false, new UTF8Encoding(false));
            var strBuilder = new StringBuilder();

            var markType = elementCodeInfo.BindInfo.BindScript.GetBindType();

            strBuilder.AppendLine("/****************************************************************************");
            strBuilder.AppendFormat(" * {0}.{1} {2}\n", DateTime.Now.Year, DateTime.Now.Month, SystemInfo.deviceName);
            strBuilder.AppendLine(" ****************************************************************************/");
            strBuilder.AppendLine();

            strBuilder.AppendLine("using System;");
            strBuilder.AppendLine("using System.Collections.Generic;");
            strBuilder.AppendLine("using UnityEngine;");
            strBuilder.AppendLine("using UnityEngine.UI;");
            strBuilder.AppendLine("using QFramework;").AppendLine();

            strBuilder.AppendLine("namespace " + nameSpace);
            strBuilder.AppendLine("{");
            strBuilder.AppendFormat("\tpublic partial class {0} : {1}", behaviourName,
                markType == BindType.Component ? "UIComponent" : "UIElement");
            strBuilder.AppendLine();
            strBuilder.AppendLine("\t{");
            strBuilder.Append("\t\t").AppendLine("private void Awake()");
            strBuilder.Append("\t\t").AppendLine("{");
            strBuilder.Append("\t\t").AppendLine("}");
            strBuilder.AppendLine();
            strBuilder.Append("\t\t").AppendLine("protected override void OnBeforeDestroy()");
            strBuilder.Append("\t\t").AppendLine("{");
            strBuilder.Append("\t\t").AppendLine("}");
            strBuilder.AppendLine("\t}");
            strBuilder.Append("}");

            sw.Write(strBuilder);
            sw.Flush();
            sw.Close();
        }

        public static void WriteElementComponent(string generateFilePath, string behaviourName, string nameSpace, ElementCodeInfo elementCodeInfo)
        {
            var sw = new StreamWriter(generateFilePath, false, Encoding.UTF8);
            var strBuilder = new StringBuilder();

            strBuilder.AppendLine("/****************************************************************************");
            strBuilder.AppendFormat(" * {0}.{1} {2}\n", DateTime.Now.Year, DateTime.Now.Month, SystemInfo.deviceName);
            strBuilder.AppendLine(" ****************************************************************************/");
            strBuilder.AppendLine();
            strBuilder.AppendLine("using UnityEngine;");
            strBuilder.AppendLine("using UnityEngine.UI;");
            strBuilder.AppendLine("using QFramework;");
            strBuilder.AppendLine();
            strBuilder.AppendLine("namespace " + nameSpace);
            strBuilder.AppendLine("{");
            strBuilder.AppendFormat("\tpublic partial class {0}", behaviourName);
            strBuilder.AppendLine();
            strBuilder.AppendLine("\t{");

            foreach (var markInfo in elementCodeInfo.BindInfos)
            {
                var strUIType = markInfo.BindScript.TypeName;
                strBuilder.AppendFormat("\t\t[SerializeField] public {0} {1};\r\n",
                    strUIType, markInfo.TypeName);
            }

            strBuilder.AppendLine();

            strBuilder.Append("\t\t").AppendLine("public void Clear()");
            strBuilder.Append("\t\t").AppendLine("{");
            foreach (var markInfo in elementCodeInfo.BindInfos)
            {
                strBuilder.AppendFormat("\t\t\t{0} = null;\r\n",
                    markInfo.TypeName);
            }

            strBuilder.Append("\t\t").AppendLine("}");
            strBuilder.AppendLine();

            strBuilder.Append("\t\t").AppendLine("public override string ComponentName");
            strBuilder.Append("\t\t").AppendLine("{");
            strBuilder.Append("\t\t\t");
            strBuilder.AppendLine("get { return \"" + elementCodeInfo.BindInfo.BindScript.TypeName + "\";}");
            strBuilder.Append("\t\t").AppendLine("}");
            strBuilder.AppendLine("\t}");
            strBuilder.AppendLine("}");
            sw.Write(strBuilder);
            sw.Flush();
            sw.Close();
        }
    }
}