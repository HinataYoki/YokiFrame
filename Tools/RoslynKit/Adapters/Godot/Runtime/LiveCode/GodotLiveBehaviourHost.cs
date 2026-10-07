#if GODOT && TOOLS
using System;
using System.Reflection;
using Godot;

namespace YokiFrame
{
    [Tool]
    public partial class GodotLiveBehaviourHost : Node
    {
        public string Id { get; private set; }
        public GodotLiveBehaviour Instance { get; private set; }
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
        private Action<Node> mBodyEntered;
        private Action<Node> mBodyExited;
        private Action<Area3D> mAreaEntered;
        private Action<Area3D> mAreaExited;
        private double mDelay;
        private bool mDelayActive;
        private Action mDelayCallback;
        private Area3D mBridge;
        private CollisionObject3D mAttachedFor;
        private Node mOwner;
        private bool mOwnsBridge;

        public void Configure(string id, GodotLiveBehaviour instance,
            Func<string, string, object[], object> invoke, Node owner = null)
        {
            if (Instance != null || mStopped) throw new InvalidOperationException("A live host can only be configured once.");
            Id = id; Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            instance.LiveInvoker = invoke;
            instance.Host = this;
            mOwner = owner;
            instance.Host = this;
            mEnter = Bind<Action>("_EnterTree", Type.EmptyTypes);
            mReadyCallback = Bind<Action>("_Ready", Type.EmptyTypes);
            mExit = Bind<Action>("_ExitTree", Type.EmptyTypes);
            mProcess = Bind<Action<double>>("_Process", new[] { typeof(double) });
            mPhysics = Bind<Action<double>>("_PhysicsProcess", new[] { typeof(double) });
            // RigidBody3D 的信号携带 Node，Area3D 的携带 Node3D；原型统一按 Node 绑定以同时兼容两者。
            mBodyEntered = Bind<Action<Node>>("_OnBodyEntered", new[] { typeof(Node) });
            mBodyExited = Bind<Action<Node>>("_OnBodyExited", new[] { typeof(Node) });
            mAreaEntered = Bind<Action<Area3D>>("_OnAreaEntered", new[] { typeof(Area3D) });
            mAreaExited = Bind<Action<Area3D>>("_OnAreaExited", new[] { typeof(Area3D) });
            ProcessMode = ProcessModeEnum.Disabled;
        }

        /// <summary>
        /// 连接碰撞信号。宿主自身是从 Node 直接派生的 sealed 类型，永远不是碰撞体，
        /// 因此碰撞必须挂在原型所附着的目标节点上：目标或其父级已有碰撞体就复用它，
        /// 否则创建一个临时 Area3D 子节点承载检测。这是 Godot 相对 Unity 声明式碰撞消息的核心差异。
        /// </summary>
        /// <param name="owner">原型附着并随后成为宿主父节点的目标节点。</param>
        private void EnsureCollisionBridge(Node owner)
        {
            if (mAttachedFor != null
                || (mBodyEntered == null && mBodyExited == null && mAreaEntered == null && mAreaExited == null)) return;
            mOwner = owner;

            if (owner as Area3D is Area3D area)
            {
                mAttachedFor = area;
                area.BodyEntered += OnBridgeBodyEntered;
                area.BodyExited += OnBridgeBodyExited;
                area.AreaEntered += OnBridgeAreaEntered;
                area.AreaExited += OnBridgeAreaExited;
                return;
            }

            if (owner as RigidBody3D is RigidBody3D rigid)
            {
                // RigidBody3D 的 body_entered/exited 携带 Node，而不是 Node3D。
                mAttachedFor = rigid;
                rigid.BodyEntered += OnRigidBodyEntered;
                rigid.BodyExited += OnRigidBodyExited;
                return;
            }

            if (owner as CollisionObject3D != null) return; // CharacterBody3D 等无 body_entered 信号

            var parentCollision = FindAncestorCollisionObject(owner);
            if (parentCollision != null)
            {
                mAttachedFor = parentCollision;
                if (parentCollision as Area3D is Area3D parentArea)
                {
                    parentArea.BodyEntered += OnBridgeBodyEntered;
                    parentArea.BodyExited += OnBridgeBodyExited;
                    parentArea.AreaEntered += OnBridgeAreaEntered;
                    parentArea.AreaExited += OnBridgeAreaExited;
                }
                else if (parentCollision as RigidBody3D is RigidBody3D parentRigid)
                {
                    parentRigid.BodyEntered += OnRigidBodyEntered;
                    parentRigid.BodyExited += OnRigidBodyExited;
                }

                return;
            }

            var bridge = new Area3D { Name = "YokiFrameLiveCollision" };
            bridge.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.5f } });
            AddChild(bridge);
            mBridge = bridge;
            mOwnsBridge = true;
            mAttachedFor = bridge;
            bridge.BodyEntered += OnBridgeBodyEntered;
            bridge.BodyExited += OnBridgeBodyExited;
            bridge.AreaEntered += OnBridgeAreaEntered;
            bridge.AreaExited += OnBridgeAreaExited;
        }

        /// <summary>从目标节点向上查找可用的碰撞体，用于复用项目已有的物理配置而不是叠加临时节点。</summary>
        /// <param name="node">起始节点。</param>
        /// <returns>最近的碰撞体；不存在时返回 null。</returns>
        private static CollisionObject3D FindAncestorCollisionObject(Node node)
        {
            if (!GodotObject.IsInstanceValid(node)) return null;
            for (Node current = node.GetParent(); GodotObject.IsInstanceValid(current); current = current.GetParent())
            {
                if (current as CollisionObject3D is CollisionObject3D collision) return collision;
            }

            return null;
        }

        private void OnBridgeBodyEntered(Node3D body) { if (Active && !Faulted) Invoke(mBodyEntered, (Node)body); }
        private void OnBridgeBodyExited(Node3D body) { if (Active && !Faulted) Invoke(mBodyExited, (Node)body); }
        private void OnBridgeAreaEntered(Area3D area) { if (Active && !Faulted) Invoke(mAreaEntered, area); }
        private void OnBridgeAreaExited(Area3D area) { if (Active && !Faulted) Invoke(mAreaExited, area); }
        private void OnRigidBodyEntered(Node body) { if (Active && !Faulted) Invoke(mBodyEntered, body); }
        private void OnRigidBodyExited(Node body) { if (Active && !Faulted) Invoke(mBodyExited, body); }

        /// <summary>延迟调用一次；宿主已停止或故障时不再触发。</summary>
        /// <param name="delaySeconds">延迟秒数；非有限值按 0 处理。</param>
        /// <param name="callback">延迟结束后调用的委托。</param>
        public void Delay(float delaySeconds, Action callback)
        {
            if (mStopped || callback == null) return;
            double wait = delaySeconds > 0f && !float.IsNaN(delaySeconds) && !float.IsInfinity(delaySeconds)
                ? delaySeconds
                : 0d;
            SceneTree tree = GetTree();
            if (!GodotObject.IsInstanceValid(tree)) return;
            SceneTreeTimer timer = tree.CreateTimer(wait);
            if (!GodotObject.IsInstanceValid(timer)) return;
            mDelayCallback = callback;
            mDelay = wait;
            mDelayActive = true;
            timer.Timeout += OnDelayTimeout;
        }

        /// <summary>取消尚未触发的延迟调用。</summary>
        public void CancelDelay()
        {
            mDelayActive = false;
            mDelayCallback = null;
        }

        private void OnDelayTimeout()
        {
            if (!mDelayActive || mStopped || Faulted) return;
            mDelayActive = false;
            Action callback = mDelayCallback;
            mDelayCallback = null;
            if (callback == null) return;
            try { callback(); }
            catch (Exception error) { Fault(error); }
        }

        public void Activate()
        {
            if (mStopped || Faulted || Instance == null) throw new InvalidOperationException("Live behaviour is unavailable.");
            if (Active) return;
            Active = true;
            try
            {
                if (!mEntered) { mEntered = true; mEnter?.Invoke(); }
                EnsureCollisionBridge(mOwner);
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
            CancelDelay();
            DetachCollisionBridge();
            try { if (mEntered) mExit?.Invoke(); }
            catch (Exception error) { Fault(error); }
            finally
            {
                if (Instance != null)
                {
                    Instance.LiveInvoker = null;
                    Instance.Host = null;
                }

                Instance = null;
            }
        }

        /// <summary>断开碰撞信号；临时桥接节点由本宿主创建时一并移除，避免残留子节点。</summary>
        private void DetachCollisionBridge()
        {
            if (mAttachedFor == null) return;
            if (mAttachedFor is Area3D area)
            {
                area.BodyEntered -= OnBridgeBodyEntered;
                area.BodyExited -= OnBridgeBodyExited;
                area.AreaEntered -= OnBridgeAreaEntered;
                area.AreaExited -= OnBridgeAreaExited;
            }
            else if (mAttachedFor is RigidBody3D rigid)
            {
                rigid.BodyEntered -= OnRigidBodyEntered;
                rigid.BodyExited -= OnRigidBodyExited;
            }

            if (mOwnsBridge && mBridge != null && GodotObject.IsInstanceValid(mBridge)) mBridge.QueueFree();
            mAttachedFor = null;
            mBridge = null;
            mOwnsBridge = false;
        }

        /// <summary>调用带单参数的原型回调，并把异常收敛为宿主故障。</summary>
        /// <typeparam name="T">回调参数类型。</typeparam>
        /// <param name="callback">原型回调；为空时直接返回。</param>
        /// <param name="argument">回调参数。</param>
        private void Invoke<T>(Action<T> callback, T argument)
        {
            if (callback == null) return;
            try { callback(argument); }
            catch (Exception error) { Fault(error); }
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
