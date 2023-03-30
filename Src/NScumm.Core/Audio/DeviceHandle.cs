//  DeviceHandle.cs
//
//  Author:
//       scemino <scemino74@gmail.com>
//
//  Copyright (c) 2014 


namespace NScumm.Core.Audio
{
    public struct DeviceHandle
    {
        readonly int handle;
        static readonly DeviceHandle _invalid = new DeviceHandle(0);

        /// <summary>
        /// Initializes a new instance of the <see cref="NScumm.Core.DeviceHandle"/> struct.
        /// </summary>
        /// <param name="handle">Handle.</param>
        /// <description>
        /// The value 0 is reserved for an invalid device for now.
        /// TODO: Maybe we should use -1 (i.e. 0xFFFFFFFF) as invalid device?
        /// </description>
        public DeviceHandle(int handle)
            : this()
        {
            this.handle = handle;
        }

        public bool IsValid { get { return handle != 0; } }

        public static DeviceHandle Invalid
        {
            get{ return _invalid; }
        }
    }
    
}
