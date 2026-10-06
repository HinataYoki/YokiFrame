#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>Editor-only frame host; never serialized into a scene or included in a player.</summary>
    [AddComponentMenu("")]
    public sealed class YokiFrameUnityLiveBehaviourHost : MonoBehaviour
    {
        private Action mUpdate;
        private Action mFixedUpdate;
        private Action mLateUpdate;
        private Action mEnable;
        private Action mDisable;
        private Action mDestroy;
        private Action mStart;
        private Action mAwake;
        private bool mAwakened;
        private bool mActive;
        private bool mStarted;
        private bool mDisposed;
        public YokiFrameUnityLiveBehaviour Instance { get; private set; }
        public string Id { get; private set; }
        public bool Faulted { get; private set; }

        public void Configure(string id, YokiFrameUnityLiveBehaviour instance,
            Func<string, string, object[], object> invoke)
        {
            if (Instance != null || mDisposed) throw new InvalidOperationException("A live host can only be configured once.");
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A live ID is required.", nameof(id));
            Id = id;
            Instance = instance;
            instance.gameObject = gameObject;
            instance.LiveInvoker = invoke;
            mUpdate = Bind("Update");
            mFixedUpdate = Bind("FixedUpdate");
            mLateUpdate = Bind("LateUpdate");
            mEnable = Bind("OnEnable");
            mDisable = Bind("OnDisable");
            mDestroy = Bind("OnDestroy");
            mStart = Bind("Start");
            mAwake = Bind("Awake");
        }

        public void Activate()
        {
            if (mDisposed || Faulted || Instance == null)
                throw new InvalidOperationException("The behaviour is unavailable; reattach its ID to recover a fault.");
            if (!mAwakened)
            {
                mAwakened = true;
                Invoke(mAwake);
            }
            if (!Faulted) enabled = true;
            if (Faulted) throw new InvalidOperationException("The behaviour failed during activation.");
        }

        private Action Bind(string name)
        {
            MethodInfo method = Instance.GetType().GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) return null;
            if (method.ReturnType != typeof(void) || method.GetParameters().Length != 0
                || method.ContainsGenericParameters)
                throw new NotSupportedException(name + " must be a non-generic void callback with no parameters.");
            return (Action)Delegate.CreateDelegate(typeof(Action), Instance, method);
        }

        private void OnEnable()
        {
            if (mDisposed || Faulted) { enabled = false; return; }
            if (Instance == null || mActive) return;
            mActive = true;
            Invoke(mEnable);
        }

        private void OnDisable()
        {
            if (!mActive) return;
            mActive = false;
            Invoke(mDisable);
        }

        private void Update()
        {
            if (!mActive || Faulted) return;
            EnsureStarted();
            if (!Faulted) Invoke(mUpdate);
        }

        private void FixedUpdate() { if (mActive && !Faulted) { EnsureStarted(); if (!Faulted) Invoke(mFixedUpdate); } }
        private void LateUpdate() { if (mActive && !Faulted) { EnsureStarted(); if (!Faulted) Invoke(mLateUpdate); } }
        private void OnDestroy() { Stop(); }

        private void EnsureStarted()
        {
            if (mStarted) return;
            mStarted = true;
            Invoke(mStart);
        }

        public void Stop()
        {
            if (mDisposed) return;
            OnDisable();
            mDisposed = true;
            if (mAwakened) Invoke(mDestroy);
            if (Instance != null) Instance.LiveInvoker = null;
            Instance = null;
            enabled = false;
        }

        private void Invoke(Action callback)
        {
            if (callback == null) return;
            try { callback(); }
            catch (Exception exception)
            {
                Faulted = true;
                Debug.LogException(exception, this);
                enabled = false;
            }
        }
    }
}
#endif
