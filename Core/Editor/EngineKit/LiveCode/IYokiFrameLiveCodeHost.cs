#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    public interface IYokiFrameLiveCodeHost
    {
        string WrapBehaviour(string className, string members, bool persistent);
        IDisposable Prepare(string id, object target, Type behaviourType, string previousState);
        void Activate(IDisposable attachment);
        void Suspend(IDisposable attachment);
        bool IsAlive(IDisposable attachment);
        string CaptureState(IDisposable attachment);
        object ReadField(IDisposable attachment, string name);
        void SetField(IDisposable attachment, string name, object value);
        object Invoke(IDisposable attachment, string method, object[] arguments);
        string Export(string id, string className, string source, string sourceHash,
            IDisposable attachment, string outputPath);
        string Bind(string exportId, object target);
    }
}
#endif
