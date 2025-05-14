using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    [CustomEditor(typeof(AbstractBind), true)]
    [CanEditMultipleObjects]
    public class AbstractBindInspector : Editor
    {
        private BindInspectorLocale mLocaleText = new BindInspectorLocale();

        private AbstractBind mBindScript => target as AbstractBind;


        private string[] mComponentNames;
        private int mComponentNameIndex;

        private void OnEnable()
        {
            mComponentNames = BindSearchHelper.GetSelectableBindTypeFullNameOnGameObject(mBindScript.gameObject);

            mComponentNameIndex = Array.FindIndex(mComponentNames,
                (componentName) => componentName.Contains(mBindScript.TypeName));

            if (mComponentNameIndex == -1 || mComponentNameIndex >= mComponentNames.Length)
            {
                mComponentNameIndex = 0;
            }

            mComponentNameProperty = serializedObject.FindProperty("mComponentName");
            mCustomComponentNameProperty = serializedObject.FindProperty("CustomComponentName");
        }

        private Lazy<GUIStyle> mLabel12 = new Lazy<GUIStyle>(() => new GUIStyle(GUI.skin.label)
        {
            fontSize = 12
        });

        private SerializedProperty mComponentNameProperty;
        private SerializedProperty mCustomComponentNameProperty;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            GUILayout.BeginVertical("box");
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label(mLocaleText.Bind, mLabel12.Value, GUILayout.Width(60));


            EditorGUI.BeginChangeCheck();

            mBindScript.MarkType = (BindType)EditorGUILayout.EnumPopup(mBindScript.MarkType);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            if (mCustomComponentNameProperty.stringValue == null ||
                string.IsNullOrEmpty(mCustomComponentNameProperty.stringValue.Trim()))
            {
                mCustomComponentNameProperty.stringValue = mBindScript.name;
            }

            if (mBindScript.MarkType == BindType.DefaultUnityElement)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label(mLocaleText.Type, mLabel12.Value, GUILayout.Width(60));

                EditorGUI.BeginChangeCheck();
                mComponentNameIndex = EditorGUILayout.Popup(mComponentNameIndex, mComponentNames);
                if (EditorGUI.EndChangeCheck())
                {
                    mComponentNameProperty.stringValue = mComponentNames[mComponentNameIndex];
                    EditorUtility.SetDirty(target);
                }

                GUILayout.EndHorizontal();
            }


            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Label(mLocaleText.BelongsTo, mLabel12.Value, GUILayout.Width(60));

            GUILayout.Label(CodeGenHelper.GetBindBelongs2(mBindScript), mLabel12.Value, GUILayout.Width(200));

            if (GUILayout.Button(mLocaleText.Select, GUILayout.Width(60)))
            {
                Selection.objects = new UnityEngine.Object[]
                {
                    CodeGenHelper.GetBindBelongs2GameObject(target as AbstractBind)
                };
            }

            GUILayout.EndHorizontal();

            if (mBindScript.MarkType != BindType.DefaultUnityElement)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(mLocaleText.ClassName, mLabel12.Value, GUILayout.Width(60));
                mCustomComponentNameProperty.stringValue =
                    EditorGUILayout.TextField(mCustomComponentNameProperty.stringValue);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            GUILayout.Label(mLocaleText.Comment, mLabel12.Value);

            GUILayout.Space(10);

            mBindScript.CustomComment = EditorGUILayout.TextArea(mBindScript.Comment, GUILayout.Height(100));

            var rootGameObj = CodeGenHelper.GetBindBelongs2GameObject(mBindScript);

            if (rootGameObj)
            {
                if (GUILayout.Button(mLocaleText.Generate + " (" + rootGameObj.name + ")",
                        GUILayout.Height(30)))
                {
                    //CodeGenKit.Generate(rootGameObj.GetComponent<IBindGroup>());
                }
            }

            GUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI();
        }

        private void OnDisable()
        {
            mCustomComponentNameProperty = null;
            mComponentNameProperty = null;
        }
    }
    public interface ICodeGenTemplate
    {
        CodeGenTask CreateTask(IBindGroup bindGroup);
    }

    /*public class CodeGenKit
    {
        private static readonly Dictionary<string, ICodeGenTemplate> mTemplates = new Dictionary<string, ICodeGenTemplate>();
        public static ICodeGenTemplate GetTemplate(string templateName)
        {
            return mTemplates.TryGetValue(templateName, out var template) ? template : null;
        }

        public static void Generate(IBindGroup bindGroup)
        {
            var task = GetTemplate(bindGroup.TemplateName).CreateTask(bindGroup);
            Generate(task);
        }

        public static void Generate(CodeGenTask task)
        {
            CodeGenKitPipeline.Default.Generate(task);
        }
    }*/

    public class BindInspectorLocale
    {
        public bool CN => true;

        public string Type => CN ? " 类型:" : " Type:";
        public string Comment => CN ? " 注释" : " Comment";
        public string BelongsTo => CN ? " 属于:" : " Belongs 2:";
        public string Select => CN ? "选择" : "Select";
        public string Generate => CN ? " 生成代码" : " Generate Code";

        public string Bind => CN ? " 绑定设置" : " Bind Setting";
        public string ClassName => CN ? "类名" : " Class Name";
    }

    public class BindSearchHelper
    {
        public static void Search(CodeGenTask task)
        {
            var bindGroupTransforms = task.GameObject.GetComponentsInChildren<IBindGroup>(true)
                .Select(g => g.As<Component>().transform)
                .Where(t => t != task.GameObject.transform);

            var binds = task.GameObject.GetComponentsInChildren<IBind>(true)
                .Where(b => b.Transform != task.GameObject.transform);


            foreach (var bind in binds)
            {
                if (bindGroupTransforms.Any(g => bind.Transform.IsChildOf(g) && bind.Transform != g))
                {
                }
                else
                {
                    task.BindInfos.Add(new BindInfo()
                    {
                        TypeName = bind.TypeName,
                        MemberName = bind.Transform.gameObject.name,
                        BindScript = bind,
                        PathToRoot = PathToParent(bind.Transform, task.GameObject.name),
                    });
                }
            }
        }

        public static string PathToParent(Transform trans, string parentName)
        {
            var retValue = new StringBuilder(trans.name);

            while (trans.parent != null)
            {
                if (trans.parent.name.Equals(parentName))
                {
                    break;
                }

                retValue.AddPrefix("/").AddPrefix(trans.parent.name);

                trans = trans.parent;
            }

            return retValue.ToString();
        }

        public static List<UnityEngine.Object> GetSelectableBindTypeOnGameObject(GameObject gameObject)
        {
            var objects = new List<UnityEngine.Object>();
            objects.AddRange(gameObject.GetComponents<Component>().Where(component => !(component is Bind)));
            objects.Add(gameObject);
            return objects;
        }

        public static string[] GetSelectableBindTypeFullNameOnGameObject(GameObject gameObject)
        {
            return GetSelectableBindTypeOnGameObject(gameObject)
                .Select(o => o.GetType().FullName).ToArray();
        }
    }

    public enum CodeGenTaskStatus
    {
        Search,
        Gen,
        Compile,
        Complete
    }

    public enum GameObjectFrom
    {
        Scene,
        Prefab
    }

    [System.Serializable]
    public class CodeGenTask
    {
        public bool ShowLog = false;

        // state
        public CodeGenTaskStatus Status;

        // input
        public GameObject GameObject;
        public GameObjectFrom From = GameObjectFrom.Scene;


        // search
        public List<StringPair> NameToFullName = new List<StringPair>();
        public List<BindInfo> BindInfos = new List<BindInfo>();

        // info
        public string ScriptsFolder;
        public string ClassName;
        public string Namespace;

        // result
        public string MainCode;
        public string DesignerCode;
    }

    [System.Serializable]
    public class StringPair
    {
        public StringPair(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public string Key;
        public string Value;
    }
}