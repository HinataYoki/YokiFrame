#if GODOT
using Godot;
using YokiFrame;
using YokiFrame;

namespace YokiFrame.Godot
{
    /// <summary>
    /// IEngineObject 的 Godot 实现，封装 Godot.Node / PackedScene。
    /// </summary>
    public sealed class GodotEngineObject : IEngineObject
    {
        private Node mNode;
        private PackedScene mPackedScene;

        public Node Node
        {
            get => mNode;
            set => mNode = value;
        }

        public PackedScene PackedScene
        {
            get => mPackedScene;
            set => mPackedScene = value;
        }

        public string Name
        {
            get
            {
                if (mNode != null)
                    return mNode.Name;
                if (mPackedScene != null)
                    return mPackedScene.ResourcePath;
                return string.Empty;
            }
            set
            {
                if (mNode != null)
                    mNode.Name = value;
            }
        }

        public bool IsActive
        {
            get => mNode == null || mNode.ProcessMode != Node.ProcessModeEnum.Disabled;
            set
            {
                if (mNode != null)
                    mNode.ProcessMode = value ? Node.ProcessModeEnum.Inherit : Node.ProcessModeEnum.Disabled;
            }
        }

        public YokiVector3 Position
        {
            get
            {
                if (mNode is Node3D node3D)
                {
                    var position = node3D.GlobalPosition;
                    return new YokiVector3(position.X, position.Y, position.Z);
                }

                if (mNode is Node2D node2D)
                {
                    var position = node2D.GlobalPosition;
                    return new YokiVector3(position.X, position.Y, 0f);
                }

                return YokiVector3.Zero;
            }
            set
            {
                if (mNode is Node3D node3D)
                    node3D.GlobalPosition = new Vector3(value.X, value.Y, value.Z);
                else if (mNode is Node2D node2D)
                    node2D.GlobalPosition = new Vector2(value.X, value.Y);
            }
        }

        public GodotEngineObject()
        {
        }

        public GodotEngineObject(Node node)
        {
            mNode = node;
        }

        public GodotEngineObject(PackedScene packedScene)
        {
            mPackedScene = packedScene;
        }

        public static GodotEngineObject Wrap(Node node)
        {
            return node == null ? null : new GodotEngineObject(node);
        }

        public static GodotEngineObject Wrap(PackedScene packedScene)
        {
            return packedScene == null ? null : new GodotEngineObject(packedScene);
        }

        public T GetComponent<T>() where T : class
        {
            return mNode as T;
        }

        public void Destroy()
        {
            if (mNode == null)
                return;

            mNode.QueueFree();
            mNode = null;
        }

        public IEngineObject Instantiate(IEngineObject prefab)
        {
            if (!(prefab is GodotEngineObject godotPrefab))
                return null;

            var packedScene = godotPrefab.mPackedScene;
            if (packedScene == null)
                return null;

            var instance = packedScene.Instantiate<Node>();
            return new GodotEngineObject(instance);
        }
    }
}
#endif
