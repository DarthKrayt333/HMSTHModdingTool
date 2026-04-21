# HMSTHModdingTool
2026 UPDATED - HDA (un)packer and tool for modding in Harvest Moon: Save the Homeland - Original as HDATextTool made by gdkchan


### Originally HDATextTool by gdkchan
### Updated & Expanded by DarthKrayt333 & HMSTH Community

---

## What is this?

HMSTHModdingTool is a modding tool for **Harvest Moon: Save the Homeland** (PS2).
Originally created by **gdkchan** as **HDATextTool**, it has been expanded with many
new features to allow deep modding of the game's assets including textures, audio archives, and text.

---

## Changelog

### Version v1.2.0-Beta
- **Renamed** HDATextTool -> HMSTHModdingTool
- **Fixed** a bug where NPC text was not exporting and importing correctly
- **Fixed** a bug where SHOP Text had empty [end] character inside text, which wasn't importing into BODY File correctly.
- **Added** double-click interactive mode (tool now opens when double clicked in Windows)
- **Added** full .GDTB texture archive support (export, import, replace, info)
- **Added** PS2 BMP converter (PS2 <-> Windows format)
- **Added** BMP raw palette extractor and importer
- **Added** smart .HDA file recognition (GDTB, RDTB, SRDB, BD, HD, SQ auto-detected)
- **Added** commands now work with or without "-" prefix
- **Known Bug** SHOP.HDA text export has a remaining issue, workaround below
- **Known Bug** No compressor yet, some files may exceed game memory limit
- **Working On** .RDTB 3D model archive exporter (BOY.HDA player model, NPC models)
- **Working On** .SRDB map model archive exporter
- **Working On** File compressor to handle game memory limits

---

## Known Bugs & Workarounds

### File Size / Memory Limit
Some edited files may become too large for the game's memory limit.
A compressor is being worked on. For now try to keep edited files
close to their original size.

---

## Game File Structure

### What is .HDA? (Harvest Data Archive)
`.HDA` is the main archive format of Harvest Moon: Save the Homeland.
It contains all game assets packed together. There are also `.HDA` files
nested inside other `.HDA` files.

**Magic Bytes:** `10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`

### Known .HDA Archives in the Game
| Archive | Contents |
|---------|----------|
| `BOY.HDA` | Player character 3D model and textures |
| `COMMON.HDA` | Common game assets shared across the game |
| `SHOP.HDA` | Shop text and assets |
| `BAR.HDA` | Background music (BGM) sound bank |
| `SE.HDA` | Sound effects audio bank |

---

## Supported File Formats (Auto-Detected on HDA Extract)

| Extension | Magic Bytes | Description |
|-----------|-------------|-------------|
| `.GDTB` | `47 44 54 42` | Graphics/Texture Data Archive (BMP textures) |
| `.RDTB` | `52 44 54 42` | 3D Model/Render Data Archive |
| `.SRDB` | `53 52 44 42` | Map/Stage 3D Model Archive |
| `.HDA` | `10 00 00 00...` | Nested HDA Archive |
| `.HD` | `49 45 43 53 73 72 65 56` | PS2 Sound Bank Header |
| `.SQ` | `49 45 43 53 75 71 65 53` | PS2 MIDI Sequence File |
| `.BD` | (by position) | PS2 Sound Bank Body (VAG audio data) |
| `.bin` | unknown | Unrecognized file (preserved in order) |

---

## File Format Details

### .GDTB (Graphics/Texture Data Table Binary)
**Magic:** `47 44 54 42` ("GDTB")

The `.GDTB` format is a texture archive containing PS2 style `.BMP` images.
These textures are used for everything visible in the game:
- Map textures (CGDATA\MAP)
- Player character (CGDATA\CHARA\BOY.HDA)
- NPCs (CGDATA\CHARA\)
- Animals (CGDATA\CHARA\ANIMALS)
- Houses and buildings (CGDATA\MAP)
- Items and inventory
- Water, grass, sky (CGDATA\MAP)
- TV screens (CGDATA\MAP)
- UI elements (CGDATA\MAP)

The tool handles all PS2 BMP quirks automatically:
- **8-bit BMP** - PS2 uses a specific swizzled palette order
- **4-bit BMP** - PS2 uses reversed nibble order

You can edit textures freely in **Photoshop**, **GIMP**, or **Paint**
without worrying about palette conversion or bit order.
The tool handles it all on import and export.

`.GDTB` works together with `.RDTB` which tells it how the textures
are mapped onto the 3D models.

---

### .RDTB (Render/3D Model Data Table Binary)
**Magic:** `52 44 54 42` ("RDTB")

The `.RDTB` format is a 3D model archive. It works together with `.GDTB`:
- `.RDTB` contains the 3D model geometry
- `.GDTB` contains the BMP textures
- `.RDTB` tells `.GDTB` how the textures are applied to the models

**Status:** Exporter is currently being worked on.

---

### .SRDB (Stage/Scene Render Data Binary)
**Magic:** `53 52 44 42` ("SRDB")

The `.SRDB` format contains 3D map models used for the game world stages
and scenes. It is a separate format from `.RDTB` and is used specifically
for the game world environment geometry.

**Status:** Exporter is currently being worked on.

---

### PS2 Audio Files (.BD / .HD / .SQ)
The game uses the standard **PS2 Sound Bank** audio format.

| File | Description |
|------|-------------|
| `.BD` | Body file - contains raw PS2 VAG audio samples |
| `.HD` | Header file - contains the sample map and settings for the body |
| `.SQ` | Sequence file - PS2 MIDI file that plays the audio using BD and HD |

**Magic Bytes:**
- `.HD` offset `0x00`: `49 45 43 53 73 72 65 56` ("IECSsreV")
- `.HD` offset `0x10`: `49 45 43 53 73 72 65 56` ("IECSdaeH")
- `.SQ` offset `0x00`: `49 45 43 53 73 72 65 56` ("IECSsreV")
- `.SQ` offset `0x10`: `49 45 43 53 75 71 65 53` ("IECSquoS")

**BGM Audio (BGM_FRM.HDA)** - Music tracks contain all 3 files:
BGM_FRM.BD ← audio sample data
BGM_FRM.HD ← sample header/map
BGM_FRM.SQ ← MIDI sequence



**Sound Effects (SE.HDA)** - SFX contains only 2 files:
SE.BD ← sound effect sample data
SE.HD ← sample header/map



Sound effects have no `.SQ` because they are triggered directly
by the game engine rather than a MIDI sequence.

---

### PS2 EXE (SLUS_202.51)
The PS2 game executable for the USA version is `SLUS_202.51`.
It is believed to be responsible for:
- 3D model collision data
- Game scripts and events
- Scene management
- Running and coordinating all game systems and files

---

## Installation

1. Download `HMSTHModdingTool.exe`
2. Place it anywhere on your PC
3. **Double click** to open in interactive mode

---

## Usage

Commands work **with or without** the `-` prefix:
-xhda game.hda ./output
xhda game.hda ./output



Both work exactly the same.

---

## Commands

### HDA Archive
-xhda <file.hda> <out_folder> Extract HDA archive

-chda <in_folder> <file.hda> Create HDA archive from folder




### Text Commands
-xtxt <text.bin> <ptr.bin> <out.txt> Export text to .txt file

-ctxt <in.txt> <text.bin> <ptr.bin> Import text from .txt file



### ELF Commands
-fixelf SLUS_202.51 (lba) (size in decimal) Fix PS2 EXE entry after modding




### GDTB Texture Archive
-igdtb <file.gdtb> Show archive info

-xgdtb <file.gdtb> <out_folder> Export all textures as BMP

-cgdtb <in_folder> <file.gdtb> Create GDTB from BMP folder

-rgdtb <image_number> <texture.bmp> <file.gdtb> Replace one texture by index

-rfgdtb <images_folder> <file.gdtb> Replace all textures from folder

-rfgdtb <images_folder> <images_number> <file.gdtb> Replace textures from start index

-cngdtb <images_number> <file.gdtb> Change texture slot count




### PS2 BMP Converter
-tops2bmp <image.bmp> Convert Windows BMP to PS2 BMP format

-towinbmp <image.bmp> Convert PS2 BMP to Windows BMP format



Output is saved automatically:
- `texture.bmp` -> `texture_ps2.bmp`
- `texture_ps2.bmp` -> `texture_win.bmp`

### BMP Palette
-xbmppal <image.bmp> <palette_name> Export raw palette from BMP

-rbmppal <palette_file> <image.bmp> Import raw palette into BMP


---

### Audio / Music & SFX

-cmusic <input.vag>

    Converts a single looped .VAG file into game-ready .BD / .HD / .SQ
    music files. Creates a subfolder named after the .VAG containing
    all three output files.
    
    Example: -cmusic mysong.vag
    
    Output:  MYSONG\MYSONG.BD    
             MYSONG\MYSONG.HD             
             MYSONG\MYSONG.SQ
             

-xvag <bd_file> <hd_file> <index> [output.vag]

    Extracts a single VAG by index from BD/HD.
    
    Output filename is optional — auto-named by index if omitted.
    
    Example: -xvag SE.BD SE.HD 9          → saves as 009.vag
    
    Example: -xvag SE.BD SE.HD 9 myfx.vag → saves as myfx.vag
    

-xvag all <bd_file> <hd_file> <out_folder>

    Extracts all VAGs from BD/HD into a folder.
    
    Files are named 000.VAG, 001.VAG, 002.VAG, ...
    
    Example: -xvag all SE.BD SE.HD ./sfx


-rvag <index> <input.vag> <bd_file> <hd_file>

    Replaces a single VAG by index in BD/HD.
    
    Works with music BD/HD and SE.HDA BD/HD.
    
    Example: -rvag 9 new.vag SE.BD SE.HD


-rvag all <folder_with_vags> <bd_file> <hd_file>

    Replaces all VAGs from a folder in BD/HD.
    
    Stops at max index if folder has more files.
    
    Replaces only up to folder count if fewer files.
    
    Example: -rvag all ./sfx SE.BD SE.HD


---

### Important — VAG File Size Guidelines

The game has a memory limit for files and audio files.
To avoid crashes or corrupted audio in-game, follow these guidelines:

Sample Rate - Use 22050 Hz (matches the game's original audio)

Max file size - Keep .VAG under 800 KB

Ideal file size - Try to keep it similar to the original file size

Audio length - Around 1 minute looped fits comfortably within limits

Format - Convert from .WAV 22050 Hz mono to .VAG before using

### Recommended Workflow

1. Record or prepare your audio track
2. Export as .WAV — 22050 Hz, Mono
3. Convert .WAV to .VAG using a VAG converter tool
4. Check file size — keep it under 800 KB
5. Run: -cmusic mysong.vag
6. Copy output MYSONG\ folder files into the game's HDA

---

### Why 22050 Hz?

The game's original music runs at 22050 Hz.

### Using the same sample rate ensures:

Correct playback speed in-game

No pitch issues

Stays within memory limits

Compatible with the optimized .HD and .SQ templates

### Why keep it under 800 KB?

The PS2 has limited SPU2 audio memory (1 MB total).

The game shares this memory across music and sound effects.

### Keeping your .VAG under 800 KB ensures:

No crashes on music load

Sound effects still work alongside music

No audio corruption in-game

Best practice: Always try to keep your modded audio
similar in size to the original file you are replacing.
The closer to the original size — the safer it is!

---

## Examples

### Extract and repack BOY.HDA (Player textures)
Extract BOY.HDA
-xhda BOY.HDA BOY

This will extract for example:

BOY_00001.GDTB <- textures

BOY_00000.RDTB <- 3D model data

Extract textures from GDTB

-xgdtb BOY_00001.GDTB ./BOY_textures

Edit textures in Photoshop / GIMP / Paint
Then reimport them

-rfgdtb ./BOY_textures BOY_00001.GDTB

Or make a completely new .GDTB File with modded textures
from Folder

-cgdtb ./bmps textures.gdtb

Repack BOY.HDA
-chda BOY BOY.HDA



### Export and edit Pause Menu Texture
Extract COMMON.HDA first
-xhda COMMON.HDA COMMON

-xgdtb COMMON_00000.GDTB ./COMMON_00000

-cgdtb ./COMMON_00000 COMMON_00000_new.GDTB

Pause Menu has 8 bit texture for ex. as COMMON_00000.GDTB
and the other two files 00001 00002 are as well it's palettes.
For now, you can only edit with hex editor this first
palette inside the .bmp file and the other 2 until the next update. 

### Export and edit cutscenes game text
Extract EVTMSG12.HDA first
-xhda EVTMSG12.HDA EVTMSG12

Export text
-xtxt EVTMSG12_00001.bin EVTMSG12_00002.bin output.txt

Edit output.txt in any text editor

Import text back

-ctxt output.txt EVTMSG12_00001new.bin EVTMSG12_00002new.bin

Repack
-chda EVTMSG12 EVTMSG12.HDA

### Export and edit NPC text
NPC text is located inside a .HDA file inside another .HDA file.

For example: /CGDATA/CHARA/HAYATO.HDA/HAYATO_02.HDA/

-xtxt HAYATO_02_00001.bin HAYATO_02_00000.bin output.txt

Edit output.txt in any text editor

Import NPC text back

-ctxt output.txt HAYATO_02_00001new.bin HAYATO_02_00000new.bin

### Replace a single texture
-rgdtb 3 my_new_texture.bmp textures.gdtb




### Replace all textures from a folder
-rfgdtb ./my_textures textures.gdtb




### Replace textures starting from slot 5
-rfgdtb ./my_textures 5 textures.gdtb




### Convert texture for editing
PS2 -> Windows (for editing in Photoshop/GIMP)
-towinbmp texture_ps2.bmp

Windows -> PS2 (after editing, if later importing in hex editor)
-tops2bmp texture_win.bmp




### Extract BGM files from BAR.HDA
-xhda BAR.HDA BAR

Output:
BAR.BD <- vag audio data
BAR.HD <- header
BAR.SQ <- MIDI sequence



---


### Create custom music from VAG
-cmusic mysong.vag

Output:

MYSONG\MYSONG.BD
MYSONG\MYSONG.HD
MYSONG\MYSONG.SQ

Extract a single VAG from the music bank:

-xvag BAR.BD BAR.HD 0 mysong.vag

Extract all VAGs from the music bank into a Folder:

-xvag all MUSIC.BD MUSIC.HD FOLDERNAME

Replace a single VAG in the music bank:

-rvag 0 mysong.vag BAR.BD BAR.HD

---

### Extract and replace Sound Effects (SE.HDA)
Extract SE.HDA:
-xhda SE.HDA SE

Output:
SE.BD  ← all sound effects
SE.HD  ← header

Extract all sound effects:
-xvag all SE.BD SE.HD ./sfx

Output: 000.VAG, 001.VAG, 002.VAG, ...

Extract a single sound effect:
-xvag SE.BD SE.HD 5

Output: 005.vag (auto-named)

Replace a single sound effect:
-rvag 5 new_sfx.vag SE.BD SE.HD

Replace all sound effects from a folder:
-rvag all ./sfx SE.BD SE.HD

Repack SE.HDA:
-chda SE SE.HDA


### HOW TO USE HMSTHModdingTool

DON'T FORGET TO PLACE HMSTHModdingTool.exe
to the same location, where are the
files for working

---


## Interactive Mode

Double click `HMSTHModdingTool.exe` to open interactive mode:

HMSTHModdingTool> -xhda BOY.HDA BOY

HMSTHModdingTool> -xgdtb BOY_00000.GDTB ./textures

HMSTHModdingTool> -xvag all SE.BD SE.HD ./sfx

HMSTHModdingTool> -rvag 5 new.vag SE.BD SE.HD

HMSTHModdingTool> help

HMSTHModdingTool> cls

HMSTHModdingTool> exit




Special interactive commands:
| Command | Action |
|---------|--------|
| `help` or `?` | Show all commands |
| `cls` or `clear` | Clear screen |
| `exit`, `quit`, or `q` | Exit the tool |

---

## Credits

| Who | Role |
|-----|------|
| **gdkchan** | Original HDATextTool creator |
| **DarthKrayt333** | HMSTHModdingTool update, new features, file format research |
| **HMSTH Community** | Testing, research, support |

---

## License
Based on the original HDATextTool by gdkchan.
