#if UNITY_EDITOR && UNITY_2022_3_OR_NEWER
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.Tests
{
    /// <summary>为 AudioKit PlayMode 播放测试准备可被 Resources 加载的临时 AudioClip。</summary>
    internal static class AudioKitPlaybackResourceFixture
    {
        internal const string RESOURCE_PATH = "YokiFrameAudioKitTests/playback";

        private const string ASSET_PATH =
            "Assets/YokiFrame/Tools/AudioKit/Tests/PlayMode/Resources/YokiFrameAudioKitTests/playback.wav";

        /// <summary>写入 1 秒 WAV 并同步导入，保证默认 ResKit 能加载到可播放片段。</summary>
        internal static void EnsureImported()
        {
            string fullPath = GetFullPath();
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, CreateToneWav());
            AssetDatabase.ImportAsset(
                ASSET_PATH,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ApplyPlayableImport();
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ASSET_PATH);
            if (clip == null || clip.length <= 0f)
            {
                throw new InvalidOperationException("临时 AudioClip 导入失败: " + ASSET_PATH);
            }
        }

        /// <summary>删除临时音频、导入元数据和因此创建的空目录。</summary>
        internal static void Delete()
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ASSET_PATH)))
            {
                AssetDatabase.DeleteAsset(ASSET_PATH);
            }

            string fullPath = GetFullPath();
            DeleteFileIfExists(fullPath);
            DeleteFileIfExists(fullPath + ".meta");
            RemoveEmptyDirectory(Path.GetDirectoryName(fullPath));
            RemoveEmptyDirectory(Path.GetDirectoryName(Path.GetDirectoryName(fullPath)));
        }

        /// <summary>把短测试音频设为预加载 PCM，避免 PlayMode 首帧还没有样本。</summary>
        private static void ApplyPlayableImport()
        {
            AudioImporter importer = AssetImporter.GetAtPath(ASSET_PATH) as AudioImporter;
            if (importer == null)
            {
                return;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }

        /// <summary>生成 440Hz、16-bit、单声道 PCM，时长足够跨过一帧仍处于播放中。</summary>
        private static byte[] CreateToneWav()
        {
            const int sampleRate = 44100;
            const int sampleCount = sampleRate;
            const int dataSize = sampleCount * 2;
            byte[] wav = new byte[44 + dataSize];
            WriteAscii(wav, 0, "RIFF");
            WriteInt32(wav, 4, 36 + dataSize);
            WriteAscii(wav, 8, "WAVE");
            WriteAscii(wav, 12, "fmt ");
            WriteInt32(wav, 16, 16);
            WriteInt16(wav, 20, 1);
            WriteInt16(wav, 22, 1);
            WriteInt32(wav, 24, sampleRate);
            WriteInt32(wav, 28, sampleRate * 2);
            WriteInt16(wav, 32, 2);
            WriteInt16(wav, 34, 16);
            WriteAscii(wav, 36, "data");
            WriteInt32(wav, 40, dataSize);
            for (int index = 0; index < sampleCount; index++)
            {
                double angle = index * 2d * Math.PI * 440d / sampleRate;
                short sample = (short)(Math.Sin(angle) * 8000d);
                int offset = 44 + (index * 2);
                wav[offset] = (byte)(sample & 0xff);
                wav[offset + 1] = (byte)((sample >> 8) & 0xff);
            }

            return wav;
        }

        /// <summary>返回临时 WAV 的项目绝对路径。</summary>
        private static string GetFullPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ASSET_PATH));
        }

        /// <summary>文件存在时删除，清理失败的导入残留。</summary>
        private static void DeleteFileIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>目录为空时删除目录及其 meta，避免留下测试生成文件夹。</summary>
        private static void RemoveEmptyDirectory(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return;
            }

            if (Directory.GetFileSystemEntries(path).Length > 0)
            {
                return;
            }

            Directory.Delete(path);
            DeleteFileIfExists(path + ".meta");
        }

        /// <summary>按小端写入 32 位整数。</summary>
        private static void WriteInt32(byte[] target, int offset, int value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        /// <summary>按小端写入 16 位整数。</summary>
        private static void WriteInt16(byte[] target, int offset, short value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
        }

        /// <summary>写入固定 ASCII 头字段。</summary>
        private static void WriteAscii(byte[] target, int offset, string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                target[offset + index] = (byte)value[index];
            }
        }
    }
}
#endif
