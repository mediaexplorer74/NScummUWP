using NScumm.Core.Audio;
using System;
using System.IO;
using NScumm.Core.Audio.SampleProviders;
using System.Threading.Tasks;

namespace NScumm.Core.Audio
{
    public enum AudioOutputState
    {
        Stopped,
        Playing,
        Paused
    }

    public class WaveStreamWriter: IAudioOutput, IDisposable
    {
        readonly BinaryWriter _writer;
        long _dataSizePos;
        int _dataChunkSize;
        IAudioSampleProvider _audioSampleProvider;
        AudioOutputState _state;
        Task _writeThread;

        public WaveStreamWriter(Stream stream)
        {
            _writer = new BinaryWriter(stream, System.Text.Encoding.UTF8);
        }

        ~WaveStreamWriter()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public virtual void Dispose(bool isDisposing)
        {
            Stop();
        }

        public void SetSampleProvider(IAudioSampleProvider audioSampleProvider)
        {
            _audioSampleProvider = audioSampleProvider;

            _writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
            _writer.Write(0); // placeholder
            _writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));

            _writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));

            var extraSize = 0;
            var encoding = 1;
            var channels = audioSampleProvider.AudioFormat.Channels;
            var sampleRate = audioSampleProvider.AudioFormat.SampleRate;
            var bitsPerSample = audioSampleProvider.AudioFormat.BitsPerSample;
            var blockAlign = audioSampleProvider.AudioFormat.BlockAlign;
            var averageBytesPerSecond = audioSampleProvider.AudioFormat.AverageBytesPerSecond;
            _writer.Write((18 + extraSize)); // wave format length
            _writer.Write((short)encoding);
            _writer.Write((short)channels);
            _writer.Write(sampleRate);
            _writer.Write(averageBytesPerSecond);
            _writer.Write((short)blockAlign);
            _writer.Write((short)bitsPerSample);
            _writer.Write((short)extraSize);

            _writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
            _dataSizePos = _writer.BaseStream.Position;
            _writer.Write(0); // placeholder
        }

        public void Play()
        {
            if (_audioSampleProvider != null && _state != AudioOutputState.Playing)
            {
                _writeThread = Task.Factory.StartNew(WriteJob, TaskCreationOptions.LongRunning);
            }
        }

        public void Pause()
        {
        }

        public void Stop()
        {
            _state = AudioOutputState.Stopped;
        }

        void WriteJob()
        {
            _state = AudioOutputState.Playing;
            var data = new byte[_audioSampleProvider.AudioFormat.AverageBytesPerSecond];
            while (_state == AudioOutputState.Playing)
            {
                Array.Clear(data, 0, data.Length);
                var bytesRead = _audioSampleProvider.Read(data, data.Length);
                //if (bytesRead != 0)
                {
                    _writer.Write(data, 0, bytesRead);
                    _dataChunkSize += bytesRead;
                }
            }

            UpdateHeader();
        }

        void UpdateHeader()
        {
            UpdateRiffChunk();
            UpdateDataChunk();
        }

        void UpdateDataChunk()
        {
            _writer.Seek((int)_dataSizePos, SeekOrigin.Begin);
            _writer.Write((uint)_dataChunkSize);
        }

        void UpdateRiffChunk()
        {
            _writer.Seek(4, SeekOrigin.Begin);
            _writer.Write((uint)(_writer.BaseStream.Length - 8));
        }
    }
}