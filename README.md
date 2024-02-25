# QDTool

### Simple tool for converting and editing different types of SHARP MZ QuickDisk and tape files between each other - QDF, MZQ, MZT, MZF, ...
You can also reorder, add or delete files. Drag&Drop is supported. For example, in addition to QuickDisk files, you can easily create a multi-file MZT file for UniCMT with files in the correct order for sequential load. QDTool also includes a simple file browser.

<img width="786" height="443" src="https://github.com/mlukasek/QDTool/blob/c911edfe775f9a6410da1737b589abbe53808668/images/QDTool_scr_2024-02-25.png">

### Currently QDTool supports following file types:
**QDF** - Japan QuickDisk file format created for emulators, supported by EmuZ-1500, QDC, VirtuaQD tools.  
**MZQ** - European QuickDisk file format with simpler structure without gaps, suppoted by Unicard for SHARP MZ-700/800/1500 and SHARP MZ emulators from Zdenek Adler, Michal Hucik, Bohumil Novacek, etc.  
**MZF** - Tape file conatining header and data, as on tape, supported by most SHARP MZ emulators, UniCMT and others, the extension for the same file type is sometimes also M12 or MZT.  
**MZT** - Multiple MZF tape files concatenated one after the other as on tape, supported by MZ700Win, UniCMT.  

### There is work in progress on support for the following file types:
**RAW** - Raw data grabbed from QuickDisk by QDC.  
**MFM** - MFM data converted from RAW by QDC.  
**QD** - HxC emulator QuickDisk file format.  
**QD** - Michal Franzen emulator QuickDisk file format.  
**QD** - VirtuaQD tools QuickDisk file format.  

### Requirements
QDTool is Windows WPF application and requires .NET Framework 6.0 to run. It is written in C# in Microsoft Visual Studio 2022.

### Releases
**2024-02-25  0.1.0 alpha** - first alpha release

### Known bugs
**0.1.0 alpha**
- color boxes in viewer have bad position on some display resolutions
- 

##### QDTool<br/>Copyright (C) 2024 Martin Lukasek <martin@8bity.cz>, www.8bity.cz  
###### This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
###### This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.
###### You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
