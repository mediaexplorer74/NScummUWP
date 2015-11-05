﻿using System;
using System.IO;
using NScumm.Core;
using NScumm.Core.Audio;
using NScumm.Core.Audio.Decoders;

namespace NScumm.Sky
{
    internal struct SfxQueue
    {
        public byte Count, FxNo, Chan, Vol;
    }

    internal partial class Sound : IDisposable
    {
        private const int SoundFileBase = 60203;
        private const int MaxQueuedFx = 4;
        private const int MaxFxNumber = 393;
        private const int SfxfStartDelay = 0x80;
        private const int SfxfSave = 0x20;

        private const int SoundChannel0 = 0;
        private const int SoundChannel1 = 1;

        public const int SoundBg = 2;
        public const int SoundVoice = 3;

        private const int SoundSpeech = 4;

        public readonly ushort[] SaveSounds = new ushort[2];

        private static readonly SfxQueue[] SfxQueue = new SfxQueue[MaxQueuedFx];

        private readonly byte _mainSfxVolume;
        private readonly Mixer _mixer;
        private readonly Disk _skyDisk;

        private SoundHandle _ingameSound0;
        private SoundHandle _ingameSound1;
        private bool _isPaused;
        private UShortAccess _sampleRates;
        private ushort _sfxBaseOfs;
        private UShortAccess _sfxInfo;
        private byte[] _soundData;
        private byte _soundsTotal;

        public Sound(Mixer mixer, Disk disk, byte volume)
        {
            _skyDisk = disk;
            _mixer = mixer;
            SaveSounds[0] = SaveSounds[1] = 0xFFFF;
            _mainSfxVolume = volume;
        }


        public Logic Logic { get; internal set; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        ~Sound()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            _mixer.StopAll();
        }

        public SoundHandle PlaySound(int id, byte[] sound, int size)
        {
            var flags = AudioFlags.Unsigned;
            var sizeOfDataFileHeader = ServiceLocator.Platform.SizeOf<DataFileHeader>();
            size -= sizeOfDataFileHeader;
            var buffer = new byte[size];
            Array.Copy(sound, sizeOfDataFileHeader, buffer, 0, size);

            _mixer.StopID(id);

            var stream = new RawStream(flags, 11025, true, new MemoryStream(buffer, 0, size));
            return _mixer.PlayStream(SoundType.SFX, stream, id);
        }

        public void PlaySound(ushort sound, ushort volume, byte channel)
        {
            if (channel == 0)
                _mixer.StopID(SoundChannel0);
            else
                _mixer.StopID(SoundChannel1);

            if (_soundData == null)
            {
                // TODO: warning
                //warning("Sound::playSound(%04X, %04X) called with a section having been loaded", sound, volume);
                return;
            }

            if (sound > _soundsTotal)
            {
                // TODO: debug
                //debug(5, "Sound::playSound %d ignored, only %d sfx in file", sound, _soundsTotal);
                return;
            }

            volume = (ushort) ((volume & 0x7F) << 1);
            sound &= 0xFF;

            // Note: All those tables are big endian. Don't ask me why. *sigh*

            // Use the sample rate from game data, see bug #1507757.
            var sampleRate = ScummHelper.SwapBytes(_sampleRates[sound << 2]);
            if (sampleRate > 11025)
                sampleRate = 11025;
            var dataOfs = ScummHelper.SwapBytes(_sfxInfo[((sound << 3) + 0)/2]) << 4;
            int dataSize = ScummHelper.SwapBytes(_sfxInfo[((sound << 3) + 2)/2]);
            int dataLoop = ScummHelper.SwapBytes(_sfxInfo[((sound << 3) + 6)/2]);
            dataOfs += _sfxBaseOfs;

            var stream = new RawStream(AudioFlags.Unsigned, sampleRate, false,
                new MemoryStream(_soundData, dataOfs, dataSize));

            IAudioStream output = null;
            if (dataLoop != 0)
            {
                var loopSta = dataSize - dataLoop;
                var loopEnd = dataSize;

                output = new SubLoopingAudioStream(stream, 0, new Timestamp(0, loopSta, sampleRate),
                    new Timestamp(0, loopEnd, sampleRate), true);
            }
            else
            {
                output = stream;
            }

            if (channel == 0)
                _ingameSound0 = _mixer.PlayStream(SoundType.SFX, output, SoundChannel0, volume, 0);
            else
                _ingameSound1 = _mixer.PlayStream(SoundType.SFX, output, SoundChannel1, volume, 0);
        }

        public void RestoreSfx()
        {
            // queue sfx, so they will be started when the player exits the control panel
            Array.Clear(SfxQueue, 0, SfxQueue.Length);
            byte queueSlot = 0;
            if (SaveSounds[0] != 0xFFFF)
            {
                SfxQueue[queueSlot].FxNo = (byte) SaveSounds[0];
                SfxQueue[queueSlot].Vol = (byte) (SaveSounds[0] >> 8);
                SfxQueue[queueSlot].Chan = 0;
                SfxQueue[queueSlot].Count = 1;
                queueSlot++;
            }
            if (SaveSounds[1] != 0xFFFF)
            {
                SfxQueue[queueSlot].FxNo = (byte) SaveSounds[1];
                SfxQueue[queueSlot].Vol = (byte) (SaveSounds[1] >> 8);
                SfxQueue[queueSlot].Chan = 1;
                SfxQueue[queueSlot].Count = 1;
            }
        }

        public void LoadSection(byte section)
        {
            FnStopFx();
            _mixer.StopAll();

            _soundData = _skyDisk.LoadFile(section*4 + SoundFileBase);
            ushort asmOfs;
            if (SystemVars.Instance.GameVersion.Version.Minor == 109)
            {
                if (section == 0)
                    asmOfs = 0x78;
                else
                    asmOfs = 0x7C;
            }
            else
                asmOfs = 0x7E;

            if ((_soundData[asmOfs] != 0x3C) || (_soundData[asmOfs + 0x27] != 0x8D) ||
                (_soundData[asmOfs + 0x28] != 0x1E) || (_soundData[asmOfs + 0x2F] != 0x8D) ||
                (_soundData[asmOfs + 0x30] != 0x36))
                throw new NotSupportedException("Unknown sounddriver version");

            _soundsTotal = _soundData[asmOfs + 1];
            var sRateTabOfs = _soundData.ToUInt16(asmOfs + 0x29);
            _sfxBaseOfs = _soundData.ToUInt16(asmOfs + 0x31);
            _sampleRates = new UShortAccess(_soundData, sRateTabOfs);

            _sfxInfo = new UShortAccess(_soundData, _sfxBaseOfs);
            // if we just restored a savegame, the sfxqueue holds the sound we need to restart
            if (!SystemVars.Instance.SystemFlags.HasFlag(SystemFlags.GAME_RESTORED))
                for (var cnt = 0; cnt < MaxQueuedFx; cnt++)
                    SfxQueue[cnt].Count = 0;
        }

        public void FnStopFx()
        {
            _mixer.StopID(SoundChannel0);
            _mixer.StopID(SoundChannel1);
            SaveSounds[0] = SaveSounds[1] = 0xFFFF;
        }

        public void FnPauseFx()
        {
            if (!_isPaused)
            {
                _isPaused = true;
                _mixer.PauseId(SoundChannel0, true);
                _mixer.PauseId(SoundChannel1, true);
            }
        }

        public void FnUnPauseFx()
        {
            if (_isPaused)
            {
                _isPaused = false;
                _mixer.PauseId(SoundChannel0, false);
                _mixer.PauseId(SoundChannel1, false);
            }
        }

        public void CheckFxQueue()
        {
            for (byte cnt = 0; cnt < MaxQueuedFx; cnt++)
            {
                if (SfxQueue[cnt].Count != 0)
                {
                    SfxQueue[cnt].Count--;
                    if (SfxQueue[cnt].Count == 0)
                        PlaySound(SfxQueue[cnt].FxNo, SfxQueue[cnt].Vol, SfxQueue[cnt].Chan);
                }
            }
        }

        public void StopSpeech()
        {
            _mixer.StopID(SoundSpeech);
        }

        public void FnStartFx(uint sound, byte channel)
        {
            SaveSounds[channel] = 0xFFFF;
            if (sound < 256 || sound > MaxFxNumber || SystemVars.Instance.SystemFlags.HasFlag(SystemFlags.FX_OFF))
                return;

            var screen = (byte) (Logic.ScriptVariables[Logic.SCREEN] & 0xff);
            if (sound == 278 && screen == 25) // is this weld in room 25
                sound = 394;

            unchecked
            {
                sound &= (uint) ~(1 << 8);
            }

            var sfx = MusicList[sound];
            var roomList = sfx.RoomList;

            var i = 0;
            if (roomList[i].RoomNo != 0xff) // if room list empty then do all rooms
                while (roomList[i].RoomNo != screen)
                {
                    // check rooms
                    i++;
                    if (roomList[i].RoomNo == 0xff)
                        return;
                }

            // get fx volume

            var volume = _mainSfxVolume; // start with standard vol

            if (SystemVars.Instance.SystemFlags.HasFlag(SystemFlags.SBLASTER))
                volume = roomList[i].AdlibVolume;
            if (SystemVars.Instance.SystemFlags.HasFlag(SystemFlags.ROLAND))
                volume = roomList[i].RolandVolume;
            volume = (byte) ((volume*_mainSfxVolume) >> 8);

            // Check the flags, the sound may come on after a delay.
            if ((sfx.Flags & SfxfStartDelay) != 0)
            {
                for (var cnt = 0; cnt < MaxQueuedFx; cnt++)
                {
                    if (SfxQueue[cnt].Count == 0)
                    {
                        SfxQueue[cnt].Chan = channel;
                        SfxQueue[cnt].FxNo = sfx.SoundNo;
                        SfxQueue[cnt].Vol = volume;
                        SfxQueue[cnt].Count = (byte) (sfx.Flags & 0x7F);
                        return;
                    }
                }
                return; // ignore sound if it can't be queued
            }

            if ((sfx.Flags & SfxfSave) != 0)
                SaveSounds[channel] = (ushort) (sfx.SoundNo | (volume << 8));

            PlaySound(sfx.SoundNo, volume, channel);
        }

        private struct Room
        {
            public readonly byte RoomNo;
            public readonly byte AdlibVolume;
            public readonly byte RolandVolume;

            public Room(byte roomNo, byte adlibVolume, byte rolandVolume)
            {
                RoomNo = roomNo;
                AdlibVolume = adlibVolume;
                RolandVolume = rolandVolume;
            }
        }

        private struct Sfx
        {
            public readonly byte SoundNo;
            public readonly byte Flags;
            public readonly Room[] RoomList;

            public Sfx(byte soundNo, byte flags, Room[] rooms)
            {
                SoundNo = soundNo;
                Flags = flags;
                RoomList = rooms;
            }
        }
    }
}