using NAudio.Wave;
namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Audio
{
    /// <summary>
    /// 音声ファイルの読み込みとデインターリーブを担当するサービス。
    /// NAudio依存やファイルI/Oをこのクラスに集約します。
    /// </summary>
    [ADRAnchor("OPT-05", nameof(AudioFileReaderService))]
    internal sealed class AudioFileReaderService
    {
        private AudioFileReaderService() { }
        private static readonly Logger<AudioFileReaderService> s_logger = new();

        public static (float[][] samplesPerChannel, AudioFileInfo fileInfo) LoadAndDeinterleave(string path)

        {
            var (memoryStreamToDispose, stream, sampleProvider, fileSize) = OpenAudioFile(path);

            using (memoryStreamToDispose)
            using (stream)
            {
                int sampleRate = stream.WaveFormat.SampleRate;
                int channels = stream.WaveFormat.Channels;
                int bitsPerSample = stream.WaveFormat.BitsPerSample;

                long totalSamples;
                if (stream is AudioFileReader)
                {
                    totalSamples = stream.Length / sizeof(float);
                }
                else
                {
                    totalSamples = stream.Length / (stream.WaveFormat.BitsPerSample / 8);
                }

                if (totalSamples == 0)
                {
                    throw new InvalidOperationException($"File has zero samples: {path}");
                }

                float[] samplesArray = new float[totalSamples];
                int totalRead = 0;
                int bufferSize = Math.Min(sampleRate * channels, (int)totalSamples);

                while (totalRead < totalSamples)
                {
                    int toRead = (int)Math.Min(bufferSize, totalSamples - totalRead);
                    int read = sampleProvider.Read(samplesArray, totalRead, toRead);

                    if (read == 0)
                    {
                        s_logger.WriteDebug($"[CachedSoundData] WARNING: Read returned 0 at {totalRead}/{totalSamples} for {Path.GetFileName(path)}");
                        break;
                    }

                    totalRead += read;
                }

                if (totalRead == 0)
                {
                    throw new InvalidOperationException($"Failed to read any samples from file: {path}");
                }

                if (totalRead < totalSamples)
                {
                    Array.Resize(ref samplesArray, totalRead);
                }

                int samplesPerChannelLen = samplesArray.Length / channels;
                float[][] samplesPerChannel = DeinterleaveChannels(samplesArray, channels, samplesPerChannelLen);
                var fileInfo = new AudioFileInfo(path, sampleRate, channels, bitsPerSample, samplesPerChannelLen * channels, fileSize);

                return (samplesPerChannel, fileInfo);
            }
        }

        private static (Stream? memoryStreamToDispose, WaveStream stream, ISampleProvider sampleProvider, long fileSize) OpenAudioFile(string path)
        {
            var fileName = Path.GetFileName(path);
            if (VirtualAudioRegistry.TryGetStream(fileName, out var vStream))
            {
                VirtualAudioRegistry.TryGetFileSize(fileName, out var size);
                var waveReader = new WaveFileReader(vStream);
                return (vStream, waveReader, waveReader.ToSampleProvider(), size);
            }
            else
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) throw new FileNotFoundException($"File not found: {path}");
                var audioReader = new AudioFileReader(path);
                return (null, audioReader, audioReader, fi.Length);
            }
        }

        private static float[][] DeinterleaveChannels(float[] interleavedData, int channels, int samplesPerChannel)
        {
            var result = new float[channels][];
            for (int ch = 0; ch < channels; ch++)
            {
                result[ch] = new float[samplesPerChannel];
                int srcIdx = ch;
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    result[ch][i] = interleavedData[srcIdx];
                    srcIdx += channels;
                }
            }
            return result;
        }
    }
}

