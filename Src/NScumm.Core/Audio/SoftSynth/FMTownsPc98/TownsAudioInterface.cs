//  TownsAudioInterface.cs
//
//  Author:
//       scemino <scemino74@gmail.com>
//
//  Copyright (c) 2015 

using System;

namespace NScumm.Core.Audio.SoftSynth
{
    public class TownsAudioInterface: IDisposable
    {
        public TownsAudioInterface(IMixer mixer, 
            ITownsAudioInterfacePluginDriver driver, bool externalMutexHandling = false)
        {
            _intf = TownsAudioInterfaceInternal.AddNewRef(mixer, this, driver, externalMutexHandling);
        }

        public void Dispose()
        {
            TownsAudioInterfaceInternal.ReleaseRef(this);
            _intf = null;
        }

        public void SetMusicVolume(int volume)
        {
            _intf.SetMusicVolume(volume);
        }

        public bool Init()
        {
            return _intf.Init();
        }

        public void SetSoundEffectVolume(int volume)
        {
            _intf.SetSoundEffectVolume(volume);
        }

        public bool Callback(int command, params object[] args)
        {
            int res = _intf.ProcessCommand(command, args);
            return res != 0;
        }

        public void SetSoundEffectChanMask(int mask)
        {
            _intf.SetSoundEffectChanMask(mask);
        }

        TownsAudioInterfaceInternal _intf;
    }
}

