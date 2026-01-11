using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 输入录制器
    /// 支持录制和回放输入序列，用于调试和测试
    /// </summary>
    public class InputRecorder
    {
        #region 数据结构

        /// <summary>
        /// 录制的输入帧
        /// </summary>
        public struct RecordedFrame
        {
            public float Timestamp;
            public string ActionName;
            public InputRecordType Type;
            public Vector2 Value;
        }

        #endregion

        #region 字段

        private readonly List<RecordedFrame> mFrames = new(256);
        private bool mIsRecording;
        private bool mIsPlaying;
        private float mRecordStartTime;
        private float mPlaybackStartTime;
        private int mPlaybackIndex;
        private Action<RecordedFrame> mOnPlaybackFrame;

        #endregion

        #region 属性

        /// <summary>是否正在录制</summary>
        public bool IsRecording => mIsRecording;

        /// <summary>是否正在回放</summary>
        public bool IsPlaying => mIsPlaying;

        /// <summary>录制的帧数</summary>
        public int FrameCount => mFrames.Count;

        /// <summary>录制时长</summary>
        public float Duration => mFrames.Count > 0 
            ? mFrames[mFrames.Count - 1].Timestamp 
            : 0f;

        #endregion

        #region 录制

        /// <summary>
        /// 开始录制
        /// </summary>
        public void StartRecording()
        {
            if (mIsRecording) return;

            mFrames.Clear();
            mIsRecording = true;
            mRecordStartTime = Time.unscaledTime;
        }

        /// <summary>
        /// 停止录制
        /// </summary>
        public void StopRecording()
        {
            mIsRecording = false;
        }

        /// <summary>
        /// 录制一帧输入
        /// </summary>
        public void RecordFrame(string actionName, InputRecordType type, Vector2 value = default)
        {
            if (!mIsRecording) return;

            mFrames.Add(new RecordedFrame
            {
                Timestamp = Time.unscaledTime - mRecordStartTime,
                ActionName = actionName,
                Type = type,
                Value = value
            });
        }

        /// <summary>
        /// 清空录制数据
        /// </summary>
        public void Clear()
        {
            mFrames.Clear();
            mIsRecording = false;
            mIsPlaying = false;
        }

        #endregion

        #region 回放

        /// <summary>
        /// 开始回放
        /// </summary>
        public void StartPlayback(Action<RecordedFrame> onFrame)
        {
            if (mIsPlaying || mFrames.Count == 0) return;

            mIsPlaying = true;
            mPlaybackStartTime = Time.unscaledTime;
            mPlaybackIndex = 0;
            mOnPlaybackFrame = onFrame;
        }

        /// <summary>
        /// 停止回放
        /// </summary>
        public void StopPlayback()
        {
            mIsPlaying = false;
            mOnPlaybackFrame = default;
        }

        /// <summary>
        /// 更新回放（需要在 Update 中调用）
        /// </summary>
        public void UpdatePlayback()
        {
            if (!mIsPlaying || mOnPlaybackFrame == default) return;

            float elapsed = Time.unscaledTime - mPlaybackStartTime;

            while (mPlaybackIndex < mFrames.Count)
            {
                var frame = mFrames[mPlaybackIndex];
                if (frame.Timestamp > elapsed) break;

                mOnPlaybackFrame.Invoke(frame);
                mPlaybackIndex++;
            }

            if (mPlaybackIndex >= mFrames.Count)
            {
                StopPlayback();
            }
        }

        #endregion

        #region 序列化

        /// <summary>
        /// 导出为 JSON
        /// </summary>
        public string ExportToJson()
        {
            var data = new RecordingData { Frames = mFrames.ToArray() };
            return JsonUtility.ToJson(data, true);
        }

        /// <summary>
        /// 从 JSON 导入
        /// </summary>
        public void ImportFromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonUtility.FromJson<RecordingData>(json);
            if (data.Frames == default) return;

            mFrames.Clear();
            mFrames.AddRange(data.Frames);
        }

        [Serializable]
        private struct RecordingData
        {
            public RecordedFrame[] Frames;
        }

        #endregion
    }

    /// <summary>
    /// 输入录制类型
    /// </summary>
    public enum InputRecordType
    {
        ButtonDown,
        ButtonUp,
        Axis,
        Vector2
    }
}
