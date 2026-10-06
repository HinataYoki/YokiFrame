#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    public sealed partial class UnityLiveCodeHost : IYokiFrameLiveCodeHost, IYokiFrameLiveSnapshotHost, IYokiFrameLiveFieldHost, IYokiFrameLiveExportHost
    {
        private const string SourceHeader = "using System;\nusing UnityEngine;\n";
        private sealed class Attachment : IDisposable
        {
            internal YokiFrameUnityLiveBehaviourHost Host;
            internal YokiFrameLiveMethodInvoker Invoker;
            internal readonly System.Collections.Generic.Dictionary<string, YokiFrameLiveFieldBinding> Fields =
                new System.Collections.Generic.Dictionary<string, YokiFrameLiveFieldBinding>(StringComparer.Ordinal);
            public void Dispose()
            {
                if (Host == default) return;
                Host.Stop();
                UnityEngine.Object.DestroyImmediate(Host);
                Host = null;
                Fields.Clear();
            }
        }

        private readonly string mProjectRoot;
        private readonly Func<string, string, object[], object> mInvoke;
        public UnityLiveCodeHost(string projectRoot, Func<string, string, object[], object> invoke = null)
        {
            mProjectRoot = projectRoot;
            mInvoke = invoke;
        }

        public string WrapBehaviour(string className, string members, bool persistent)
        {
            YokiFrameLiveCodeManager.ValidateName(className);
            return SourceHeader + "[Serializable]\npublic sealed class " + className
                + " : " + (persistent ? "UnityEngine.MonoBehaviour" : "YokiFrame.YokiFrameUnityLiveBehaviour")
                + "\n{\n#line 1 \"members.cs\"\n" + members + "\n#line default\n}\n";
        }

        public IDisposable Prepare(string id, object target, Type behaviourType, string previousState)
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Live Unity behaviours require Play Mode.");
            GameObject owner = target as GameObject;
            if (owner == default || !owner.scene.IsValid() || EditorUtility.IsPersistent(owner))
                throw new ArgumentException("A live scene GameObject is required.");
            var instance = Activator.CreateInstance(behaviourType) as YokiFrameUnityLiveBehaviour;
            if (instance == null) throw new ArgumentException("The compiled type is not a Unity live behaviour.");
            instance.gameObject = owner;
            if (!string.IsNullOrEmpty(previousState))
                JsonUtility.FromJson<UnityLiveFieldState>(previousState).Restore(instance, false);
            // Validate the exportable fields before invoking any lifecycle callback.
            UnityLiveFieldState.Capture(instance, false);
            var attachment = new Attachment { Host = owner.AddComponent<YokiFrameUnityLiveBehaviourHost>() };
            try
            {
                attachment.Host.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                attachment.Host.enabled = false;
                attachment.Host.Configure(id, instance, mInvoke);
                attachment.Invoker = new YokiFrameLiveMethodInvoker(instance);
                return attachment;
            }
            catch { attachment.Dispose(); throw; }
        }

        public void Activate(IDisposable attachment) => Require(attachment).Host.Activate();
        public void Suspend(IDisposable attachment) { Require(attachment).Host.enabled = false; }

        public bool IsAlive(IDisposable attachment)
        {
            var value = attachment as Attachment;
            return value != null && value.Host != default && !value.Host.Faulted && value.Host.Instance != null;
        }

        public string CaptureState(IDisposable attachment)
        {
            Attachment value = Require(attachment);
            return JsonUtility.ToJson(UnityLiveFieldState.Capture(value.Host.Instance, false));
        }

        public object ReadField(IDisposable attachment, string name)
        {
            object instance = Require(attachment).Host.Instance;
            return UnityLiveFieldState.RequireField(instance, name).GetValue(instance);
        }

        public void SetField(IDisposable attachment, string name, object value)
        {
            object instance = Require(attachment).Host.Instance;
            var field = UnityLiveFieldState.RequireField(instance, name);
            if (value == null ? field.FieldType.IsValueType : !field.FieldType.IsInstanceOfType(value))
                throw new ArgumentException("Field value must match " + field.FieldType.FullName + ".");
            field.SetValue(instance, value);
        }

        public object Invoke(IDisposable attachment, string method, object[] arguments) =>
            Require(attachment).Invoker.Invoke(method, arguments);

        public string Export(string id, string className, string source, string sourceHash,
            IDisposable attachment, string outputPath)
        {
            if (sourceHash != YokiFrameLiveCodeManager.Hash(source)) throw new ArgumentException("Invalid source hash.");
            var version = new YokiFrameLiveExportVersion
            {
                ExportId = Guid.NewGuid().ToString("N"), BatchId = Guid.NewGuid().ToString("N"),
                ClassName = className, SourcePath = outputPath.Replace('\\', '/')
            };
            version.Source = VersionExportSource(source, version.ExportId);
            version.SourceHash = YokiFrameLiveCodeManager.Hash(version.Source);
            version.Targets.Add(CaptureExportTarget(attachment));
            StageExports(version.BatchId, new[] { version });
            QueueExportCommit(version.BatchId, () => { Require(attachment); });
            return version.ExportId;
        }

        public string Bind(string exportId, object target)
        {
            return BindVersioned(exportId, target);
        }

        private Attachment Require(IDisposable attachment)
        {
            if (!IsAlive(attachment)) throw new InvalidOperationException("Live behaviour is unavailable or faulted.");
            return (Attachment)attachment;
        }

    }
}
#endif
