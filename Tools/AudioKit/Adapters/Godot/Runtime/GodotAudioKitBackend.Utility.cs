#if GODOT
using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame.Godot
{
    public sealed partial class GodotAudioKitBackend
    {
        /// <summary>从 Core PoolKit 借出二维 Player 租约，根节点失效时由借出回调重建宿主节点。</summary>
        private PooledAudioPlayer2D RentPlayer2D()
        {
            EnsureRoot();
            return mPlayer2DPool.Allocate();
        }

        /// <summary>从 Core PoolKit 借出三维 Player 租约，根节点失效时由借出回调重建宿主节点。</summary>
        private PooledAudioPlayer3D RentPlayer3D()
        {
            EnsureRoot();
            return mPlayer3DPool.Allocate();
        }

        /// <summary>创建并挂载一个二维 Player 租约；只由 PoolKit 空缓存工厂调用。</summary>
        private PooledAudioPlayer2D CreatePlayer2DLease()
        {
            return new PooledAudioPlayer2D(CreatePlayer2D());
        }

        /// <summary>创建并挂载一个三维 Player 租约；只由 PoolKit 空缓存工厂调用。</summary>
        private PooledAudioPlayer3D CreatePlayer3DLease()
        {
            return new PooledAudioPlayer3D(CreatePlayer3D());
        }

        /// <summary>创建挂到当前有效根节点的二维 Godot Player。</summary>
        private AudioStreamPlayer CreatePlayer2D()
        {
            AudioStreamPlayer player = new() { Name = "AudioKitVoice" };
            mRoot.AddChild(player);
            return player;
        }

        /// <summary>创建挂到当前有效根节点的三维 Godot Player。</summary>
        private AudioStreamPlayer3D CreatePlayer3D()
        {
            AudioStreamPlayer3D player = new() { Name = "AudioKitVoice3D" };
            mRoot.AddChild(player);
            return player;
        }

        /// <summary>借出二维租约时恢复失效节点并启用处理模式。</summary>
        private void ActivatePlayer2DLease(PooledAudioPlayer2D lease)
        {
            if (!IsValid(lease.Player)) lease.Player = CreatePlayer2D();
            lease.Player.ProcessMode = Node.ProcessModeEnum.Inherit;
        }

        /// <summary>借出三维租约时恢复失效节点并启用处理模式。</summary>
        private void ActivatePlayer3DLease(PooledAudioPlayer3D lease)
        {
            if (!IsValid(lease.Player)) lease.Player = CreatePlayer3D();
            lease.Player.ProcessMode = Node.ProcessModeEnum.Inherit;
        }

        /// <summary>归还二维租约前重置可观察状态，容量溢出由 PoolKit 调用租约释放。</summary>
        private static void ResetPlayer2DLease(PooledAudioPlayer2D lease)
        {
            if (!IsValid(lease.Player)) return;
            AudioStreamPlayer player = lease.Player;
            player.Stop();
            player.Stream = null;
            player.Bus = AudioBus.Master;
            player.VolumeDb = 0f;
            player.PitchScale = 1f;
            player.StreamPaused = false;
            player.ProcessMode = Node.ProcessModeEnum.Disabled;
        }

        /// <summary>归还三维租约前重置可观察状态，容量溢出由 PoolKit 调用租约释放。</summary>
        private static void ResetPlayer3DLease(PooledAudioPlayer3D lease)
        {
            if (!IsValid(lease.Player)) return;
            AudioStreamPlayer3D player = lease.Player;
            player.Stop();
            player.Stream = null;
            player.Bus = AudioBus.Master;
            player.VolumeDb = 0f;
            player.PitchScale = 1f;
            player.StreamPaused = false;
            player.GlobalPosition = Vector3.Zero;
            player.ProcessMode = Node.ProcessModeEnum.Disabled;
        }

        /// <summary>归还二维 Player 租约；容量淘汰与资源释放统一由 Core PoolKit 负责。</summary>
        private void ReturnPlayer2D(PooledAudioPlayer2D lease)
        {
            if (lease == null) return;
            mPlayer2DPool.Recycle(lease);
        }

        /// <summary>归还三维 Player 租约；容量淘汰与资源释放统一由 Core PoolKit 负责。</summary>
        private void ReturnPlayer3D(PooledAudioPlayer3D lease)
        {
            if (lease == null) return;
            mPlayer3DPool.Recycle(lease);
        }

        /// <summary>移除指定索引 voice 并回收其 Player。</summary>
        private void ReleaseVoiceAt(int index)
        {
            VoiceState voice = mVoices[index];
            mVoices.RemoveAt(index);
            ReturnPlayer2D(voice.Player2DLease);
            ReturnPlayer3D(voice.Player3DLease);
#if TOOLS
            AudioKit.NotifyBackendDiagnosticStateChanged();
#endif
        }

        /// <summary>释放 Core PoolKit 缓存中的全部 Player；active 租约已在 StopAll 中归还。</summary>
        private void DestroyPooledPlayers()
        {
            mPlayer2DPool.Dispose();
            mPlayer3DPool.Dispose();
        }

        /// <summary>更新存活跟随目标并映射到 Godot 世界坐标。</summary>
        private static void UpdateFollowTarget(VoiceState voice)
        {
            if (voice.FollowTarget != null && voice.FollowTarget.IsAlive) voice.Position = voice.FollowTarget.Position;
            if (!IsValid(voice.Player3D)) return;
            System.Numerics.Vector3 position = voice.Position;
            voice.Player3D.GlobalPosition = new Vector3(position.X, position.Y, position.Z);
        }

        /// <summary>把可表达的衰减模式映射到 Godot 4 AttenuationModel。</summary>
        private static void ApplyGodotRolloff(AudioStreamPlayer3D player, AudioRolloffMode mode)
        {
            if (mode == AudioRolloffMode.Logarithmic)
            {
                player.AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Logarithmic;
            }
            else
            {
                player.AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance;
            }
        }

        /// <summary>使用实际 AudioServer Bus；不存在的逻辑总线回退 Master。</summary>
        private static string ResolveGodotBus(string bus) => AudioServer.GetBusIndex(bus) >= 0 ? bus : AudioBus.Master;

        /// <summary>确保后端根节点已经挂入当前 SceneTree。</summary>
        private void EnsureRoot()
        {
            if (IsValid(mRoot)) return;
            SceneTree tree = Engine.GetMainLoop() as SceneTree;
            if (tree == null || tree.Root == null) throw new InvalidOperationException("Godot SceneTree root is unavailable.");
            mRoot = new Node { Name = "YokiFrameAudioKit" };
            tree.Root.AddChild(mRoot);
        }

        /// <summary>判断任意 Godot 对象引用是否仍有效。</summary>
        private static bool IsValid(GodotObject target) => target != null && GodotObject.IsInstanceValid(target);

        /// <summary>延迟释放仍有效的 Godot 节点。</summary>
        private static void DestroyNode(Node node)
        {
            if (IsValid(node)) node.QueueFree();
        }

        /// <summary>读取 voice 当前是否实际播放。</summary>
        private static bool IsPlaying(VoiceState voice)
        {
            if (IsValid(voice.Player2D)) return voice.Player2D.Playing;
            return IsValid(voice.Player3D) && voice.Player3D.Playing;
        }

        /// <summary>读取 voice 当前线性音量。</summary>
        private static float GetCurrentLinearVolume(VoiceState voice)
        {
            if (IsValid(voice.Player2D)) return DbToLinear(voice.Player2D.VolumeDb);
            return IsValid(voice.Player3D) ? DbToLinear(voice.Player3D.VolumeDb) : 0f;
        }

        /// <summary>读取 voice 实际播放位置。</summary>
        private static float GetPlaybackPosition(VoiceState voice)
        {
            if (IsValid(voice.Player2D)) return Math.Max(0f, (float)voice.Player2D.GetPlaybackPosition());
            if (IsValid(voice.Player3D)) return Math.Max(0f, (float)voice.Player3D.GetPlaybackPosition());
            return 0f;
        }

        /// <summary>把线性音量转换为 Godot 分贝。</summary>
        private static float LinearToDb(float value) => value <= 0.0001f ? -80f : 20f * (float)Math.Log10(value);

        /// <summary>把 Godot 分贝转换为线性音量。</summary>
        private static float DbToLinear(float value) => value <= -80f ? 0f : (float)Math.Pow(10f, value / 20f);

        /// <summary>把有限值限制到零到一。</summary>
        private static float Clamp01(float value) => value < 0f ? 0f : (value > 1f ? 1f : value);

        /// <summary>验证当前调用运行在创建后端的 Godot 主线程。</summary>
        private void EnsureGodotThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mGodotThreadId)
            {
                throw new InvalidOperationException("AudioKit Godot backend must control nodes on the Godot thread.");
            }
        }

        /// <summary>在已捕获 Godot SynchronizationContext 上执行宿主操作。</summary>
        private Task<T> InvokeOnGodotThreadAsync<T>(Func<T> operation, CancellationToken token)
        {
            if (Thread.CurrentThread.ManagedThreadId == mGodotThreadId)
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(operation());
            }

            if (mGodotContext == null) return Task.FromException<T>(new InvalidOperationException("Godot SynchronizationContext is unavailable."));
            TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            mGodotContext.Post(_ => CompleteGodotOperation(operation, token, completion), null);
            return completion.Task;
        }

        /// <summary>执行一次已回到 Godot 主线程的操作并提交终态。</summary>
        private static void CompleteGodotOperation<T>(Func<T> operation, CancellationToken token, TaskCompletionSource<T> completion)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                completion.TrySetResult(operation());
            }
            catch (OperationCanceledException) { completion.TrySetCanceled(token); }
            catch (Exception exception) { completion.TrySetException(exception); }
        }
    }
}
#endif
