#if GODOT && TOOLS
using System;
using System.Reflection;
using Godot;

namespace YokiFrame
{
    [Tool]
    public partial class YokiFrameGodotLiveBehaviourHost : Node
    {
        public string Id { get; private set; }
        public YokiFrameGodotLiveBehaviour Instance { get; private set; }
        public bool Faulted { get; private set; }
        public bool Active { get; private set; }
        private bool mReady;
        private bool mEntered;
        private bool mStopped;
        private Action mEnter;
        private Action mReadyCallback;
        private Action mExit;
        private Action<double> mProcess;
        private Action<double> mPhysics;

        public void Configure(string id, YokiFrameGodotLiveBehaviour instance,
            Func<string, string, object[], object> invoke)
        {
            if (Instance != null || mStopped) throw new InvalidOperationException("A live host can only be configured once.");
            Id = id; Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            instance.LiveInvoker = invoke;
            mEnter = Bind<Action>("_EnterTree", Type.EmptyTypes);
            mReadyCallback = Bind<Action>("_Ready", Type.EmptyTypes);
            mExit = Bind<Action>("_ExitTree", Type.EmptyTypes);
            mProcess = Bind<Action<double>>("_Process", new[] { typeof(double) });
            mPhysics = Bind<Action<double>>("_PhysicsProcess", new[] { typeof(double) });
            ProcessMode = ProcessModeEnum.Disabled;
        }

        public void Activate()
        {
            if (mStopped || Faulted || Instance == null) throw new InvalidOperationException("Live behaviour is unavailable.");
            if (Active) return;
            Active = true;
            try
            {
                if (!mEntered) { mEntered = true; mEnter?.Invoke(); }
                if (!mReady) { mReady = true; mReadyCallback?.Invoke(); }
                ProcessMode = ProcessModeEnum.Inherit;
            }
            catch (Exception error) { Fault(error); throw; }
        }

        public void Suspend()
        {
            Active = false;
            ProcessMode = ProcessModeEnum.Disabled;
        }

        public override void _Process(double delta)
        {
            if (!Active || Faulted) return;
            try { mProcess?.Invoke(delta); }
            catch (Exception error) { Fault(error); }
        }
        public override void _PhysicsProcess(double delta)
        {
            if (!Active || Faulted) return;
            try { mPhysics?.Invoke(delta); }
            catch (Exception error) { Fault(error); }
        }
        public override void _ExitTree() => Stop();
        public void Stop()
        {
            if (mStopped) return;
            mStopped = true;
            Suspend();
            try { if (mEntered) mExit?.Invoke(); }
            catch (Exception error) { Fault(error); }
            finally
            {
                if (Instance != null) Instance.LiveInvoker = null;
                Instance = null;
            }
        }
        private void Fault(Exception error)
        {
            Faulted = true;
            Suspend();
            GD.PushError("[YokiFrame.LiveCode] " + error);
        }
        private T Bind<T>(string name, Type[] parameters) where T : Delegate
        {
            var method = Instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) return null;
            if (method.ReturnType != typeof(void) || method.ContainsGenericParameters)
                throw new NotSupportedException(name + " must return void.");
            var actual = method.GetParameters();
            if (actual.Length != parameters.Length) throw new NotSupportedException("Invalid callback signature: " + name);
            for (int index = 0; index < parameters.Length; index++)
                if (actual[index].ParameterType != parameters[index]) throw new NotSupportedException("Invalid callback signature: " + name);
            return (T)Delegate.CreateDelegate(typeof(T), Instance, method);
        }
    }
}
#endif
