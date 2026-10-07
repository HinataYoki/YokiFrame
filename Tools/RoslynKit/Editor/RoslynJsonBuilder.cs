#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// RoslynKit 共享的最小 JSON 写出器；Core 与各引擎适配器用它构造结果载荷。
    /// </summary>
    /// <remarks>
    /// 只支持对象、数组与标量，刻意不引入任何序列化库：Unity 2022.3 没有 System.Text.Json，
    /// 工程内的第三方包还可能把它以 internal 形式合并进来（C16）。
    /// </remarks>
    public sealed class RoslynJsonBuilder
    {
        private sealed class Frame
        {
            internal bool IsObject;
            internal int ItemCount;
        }

        private readonly StringBuilder mBuilder = new StringBuilder(256);
        private readonly List<Frame> mFrames = new List<Frame>(4);
        private bool mNameWritten;
        private bool mRootWritten;

        /// <summary>开始一个对象。</summary>
        /// <returns>当前写出器。</returns>
        public RoslynJsonBuilder StartObject()
        {
            WriteValuePrefix();
            mBuilder.Append('{');
            Push(isObject: true);
            return this;
        }

        /// <summary>结束当前对象。</summary>
        /// <returns>当前写出器。</returns>
        public RoslynJsonBuilder EndObject()
        {
            Pop(expectObject: true);
            mBuilder.Append('}');
            CompleteValue();
            return this;
        }

        /// <summary>开始一个数组。</summary>
        /// <returns>当前写出器。</returns>
        public RoslynJsonBuilder StartArray()
        {
            WriteValuePrefix();
            mBuilder.Append('[');
            Push(isObject: false);
            return this;
        }

        /// <summary>结束当前数组。</summary>
        /// <returns>当前写出器。</returns>
        public RoslynJsonBuilder EndArray()
        {
            Pop(expectObject: false);
            mBuilder.Append(']');
            CompleteValue();
            return this;
        }

        /// <summary>写出对象属性名。</summary>
        /// <param name="name">属性名。</param>
        /// <returns>当前写出器。</returns>
        public RoslynJsonBuilder Name(string name)
        {
            Frame frame = Current();
            if (frame == null || !frame.IsObject)
            {
                throw new InvalidOperationException("JSON property name requires an open object.");
            }

            if (mNameWritten)
            {
                throw new InvalidOperationException("JSON property name was already written.");
            }

            if (frame.ItemCount > 0)
            {
                mBuilder.Append(',');
            }

            WriteEscaped(name ?? string.Empty);
            mBuilder.Append(':');
            mNameWritten = true;
            return this;
        }

        /// <summary>写字符串值。</summary>
        /// <param name="value">字符串。</param>
        /// <returns>当前写出器。</returns>
        public RoslynJsonBuilder String(string value)
        {
            WriteValuePrefix();
            WriteEscaped(value ?? string.Empty);
            CompleteValue();
            return this;
        }

        /// <summary>写布尔值。</summary>
        /// <param name="value">布尔值。</param>
        /// <returns>当前写出器。</returns>
        public RoslynJsonBuilder Boolean(bool value)
        {
            WriteValuePrefix();
            mBuilder.Append(value ? "true" : "false");
            CompleteValue();
            return this;
        }

        /// <summary>写整数。</summary>
        /// <param name="value">整数。</param>
        /// <returns>当前写出器。</returns>
        public RoslynJsonBuilder Number(long value)
        {
            WriteValuePrefix();
            mBuilder.Append(value.ToString(CultureInfo.InvariantCulture));
            CompleteValue();
            return this;
        }

        /// <summary>写字符串属性。</summary>
        /// <param name="name">属性名。</param>
        /// <param name="value">字符串。</param>
        /// <returns>当前写出器。</returns>
        public RoslynJsonBuilder Property(string name, string value)
        {
            return Name(name).String(value);
        }

        /// <summary>写布尔属性。</summary>
        /// <param name="name">属性名。</param>
        /// <param name="value">布尔值。</param>
        /// <returns>当前写出器。</returns>
        public RoslynJsonBuilder Property(string name, bool value)
        {
            return Name(name).Boolean(value);
        }

        /// <summary>写整数属性。</summary>
        /// <param name="name">属性名。</param>
        /// <param name="value">整数。</param>
        /// <returns>当前写出器。</returns>
        public RoslynJsonBuilder Property(string name, long value)
        {
            return Name(name).Number(value);
        }

        /// <summary>获取已写出的 JSON 文本。</summary>
        /// <returns>JSON 文本。</returns>
        public override string ToString()
        {
            return mBuilder.ToString();
        }

        private Frame Current()
        {
            return mFrames.Count == 0 ? null : mFrames[mFrames.Count - 1];
        }

        private void Push(bool isObject)
        {
            mFrames.Add(new Frame { IsObject = isObject });
            mNameWritten = false;
        }

        private void Pop(bool expectObject)
        {
            Frame frame = Current();
            if (frame == null || frame.IsObject != expectObject)
            {
                throw new InvalidOperationException("JSON container end does not match the open container.");
            }

            mFrames.RemoveAt(mFrames.Count - 1);
            mNameWritten = false;
        }

        private void WriteValuePrefix()
        {
            Frame frame = Current();
            if (frame == null)
            {
                if (mRootWritten)
                {
                    throw new InvalidOperationException("JSON writer already produced a root value.");
                }

                return;
            }

            if (frame.IsObject)
            {
                if (!mNameWritten)
                {
                    throw new InvalidOperationException("JSON object value requires a property name.");
                }

                return;
            }

            if (frame.ItemCount > 0)
            {
                mBuilder.Append(',');
            }
        }

        private void CompleteValue()
        {
            Frame frame = Current();
            if (frame == null)
            {
                mRootWritten = true;
                return;
            }

            frame.ItemCount++;
            mNameWritten = false;
        }

        private void WriteEscaped(string value)
        {
            mBuilder.Append('"');
            for (var index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (c < ' ' || c == '"' || c == '\\')
                {
                    mBuilder.Append('\\');
                    mBuilder.Append('u');
                    mBuilder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    mBuilder.Append(c);
                }
            }

            mBuilder.Append('"');
        }
    }
}
#endif