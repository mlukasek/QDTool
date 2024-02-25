# QDTool
### Simple tool for converting different types of SHARP MZ QuickDisk and tape files between each other.

#### Currently it supports following file types: 
QDF - Japan QuickDisk file format created for emulators, supported by EmuZ-1500, QDC, VirtuaQD tools. 
MZQ - European QuickDisk file format with simpler structure without gaps, suppoted by Unicard for SHARP MZ-700/800/1500 and SHARP MZ emulators from Zdenek Adler, Michal Hucik, Bohumil Novacek, etc. 
MZF - Tape file conatining header and data, as on tape, supported by most SHARP MZ emulators, UniCMT and others, the extension for the same file type is sometimes also M12 or MZT. 
MZT - Multiple MZF tape files concatenated one after the other as on tape, supported by MZ700Win, UniCMT. 

#### There is work in progress on support for following file types: 
RAW - Raw data grabbed from QuickDisk by QDC. 
MFM - MFM data converted from RAW by QDC. 
QD - HxC emulator QuickDisk file format. 
QD - Michal Franzen emulator QuickDisk file format. 
QD - VirtuaQD tools QuickDisk file format. 
