# NScummUWP v1.3
The SCUMM UWP client for the Windows 10 (Mobile) platform. :)
    
## my 2 cents
1. RnD nSCUMM by Scemino (https://github.com/scemino/nscumm)
2. RnD nSCUMMUWP by Bashar Astifan (https://github.com/basharast/NScummUWP) 
3. nSCUMM "refubrishing" (SceminoNSCUMM+NSCUMMUWP synthez...)
4. Some "project optimization" applied (reducing srccode's filesize from 166mb to 32mb)
5. ru_RU local. added
6. Game-loop (run cycle) stability increazed
7. Experimental mode added (game engine "zero", or "0") for veeery oooldes dos games support :)

## Screenshots / Images
![W10 Desktop](https://github.com/mediaexplorer74/nscumm/blob/master/Images/shot1.png)
![W10M 15252 on L950](https://github.com/mediaexplorer74/nscumm/blob/master/Images/shot2.png)
![W10M Astoria on L640DS](https://github.com/mediaexplorer74/nscumm/blob/master/Images/shot3.png)

## Build instructions
1. First, check that you have the [necessary tools](#requirements) installed.
2. Clone the repo `git clone --recursive https://github.com/mediaexplorer74/NScummUWP.git`.
3. Update all project dependencies (packages) 
4. Compile the UWP app for needed platform (ARM is best one... but x86 is supported too)
5. Try to load some SCUMMVM-compatible game, i.e. "The Curse of Monkey Island (Windows CD)"
6. Test & enjoy game process, sound tracks, oldschool cool graphics, etc.... =))
 

## Requirements
The following tools and SDKs are mandatory for the project development:
* Visual Studio 2022 Preview, with
    * .NET Native;
    * .NET Framework 4.8 SDK;
    * NuGet package manager;
    * Universal Windows Platform tools;
    * Windows 10 19041 SDK;
    * Min. os. build can/must be set to 15063 (or even 10240) for all Universal projects 


## Current features
- Audio / sounds seem(s) to be ok
- Project Astoria compatibility added (uwp projects switched to win 10 10240 "mode")

## Planned features
- Check & fix audio plugins (i.e. LOOM goes at no-sound... so strange)


## Special thanks
- Scemino (https://github.com/scemino/) Original nSCUMM's developer/author - see his project [nSCUMM](https://github.com/scemino/nscumm)
- Bashar Astifan (https://github.com/basharast) nSCUMMUWP's developer/remaker/enhancer - see his project [NScummUWP](https://github.com/basharast/NScummUWP) 

## License
Copyright © 2015-2023 [nSCUMM Authors](https://github.com/scemino/nscumm/graphs/contributors).

nSCUMM free software: you can redistribute it and / or modify it under the terms of the GNU General Public License 
as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

nSCUMM is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty 
of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with nSCUMM. 
If not, see http://www.gnu.org/licenses/.

## ..
AS IS. No support. RnD only. DIY

## .
[m][e] 2023
