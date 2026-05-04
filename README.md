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

### Version v1.4.2-Beta
- **Renamed** HDATextTool -> HMSTHModdingTool
- **Fixed** File compressor to handle game memory limits - Now it's available, usage by Default -chda or chda to make Compressed files inside .HDA
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

---

### Version v1.4.3-Beta
- **Added** BOY Advanced Bone Scaler & Height Tool (`-boyscale`)
- **Added** BOY Mod Presets - apply pre-made skeleton mods instantly
- **Added** BoyModV2 - Taller Player Mod - Default Farmer Version (`-boymodv2`)
- **Added** BoyModV3 - Taller Player Mod - Uptight Farmer Version (`-boymodv3`)
- **Added** BoyOriginal / BoyBack / BoyOrig / BoyRestore - restore BOY to original vanilla skeleton
- **Added** Auto file-type detection for BOY mod commands (detects `.rdtb` or `.bin` automatically)
- **Added** Individual bone XYZ scaling - scale any bone without affecting anything else
- **Added** Bone safety system - warns when scaling bones that would move hair or face
- **Added** Full group scaling support (spine, neck, arms, legs, ankles, feet etc.)
- **Added** Pair aliases (both sides at once: ankles, arms, legs, thighs etc.)

---

## Known Bugs & Workarounds

### File Size / Memory Limit
Some edited files may become too large for the game's memory limit.
A compressor is now available. Usage to make compressed files inside .HDA is by Default
-chda <folder_name> <new_file_name.hda> or chda <folder_name> <new_file_name.hda>.

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

The compressor had a few issues with .BD .HD .SQ Audio files.

Now it's even better, the compressed Audio files must be bigger than the RAW format for now.

But maybe if the file is too large, when making .HDA, try to use -chda uncomp <folder_name> <new_file.HDA> instead.

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
To avoid crashes or corrupted audio in-game, follow these steps:

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

To make the Audio file smaller, just in case that it doesn't get
over the game's maximum memory limit.

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

---

### BOY Advanced Bone Scaler & Height Tool
> BOY 3D Tools by DarthKrayt333

The BOY Bone Scaler allows you to scale any individual bone of the
BOY player character by X, Y, and Z axes independently, without
affecting any other bones or any other part of the model.

**Bone 0 Y = 64.0 (hex: 00 00 80 42) is always locked.**
This value controls horse mounting and world placement.
It is never changed by any BOY tool command.

---

#### How it works

Each bone can be scaled on its own.
No chain compensation. No cross-effects between bones.
Only the bones you specify are changed.

---

#### Individual Bone Control

-boyscale <00_skeleton.bin> --b<N> <value> Scale bone N on all axes

-boyscale <00_skeleton.bin> --b<N>x <value> Scale bone N X axis only

-boyscale <00_skeleton.bin> --b<N>y <value> Scale bone N Y axis only

-boyscale <00_skeleton.bin> --b<N>z <value> Scale bone N Z axis only


N = bone number (0 to 67)

Values:
- `1.0` = original (no change)
- 
- `> 1.0` = bigger / longer / fatter
- 
- `< 1.0` = smaller / shorter / thinner

---

#### Group Control

Scale multiple bones at once using group names:

-boyscale <00_skeleton.bin> --<group> <value> All axes

-boyscale <00_skeleton.bin> --<group>x <value> X axis only

-boyscale <00_skeleton.bin> --<group>y <value> Y axis only

-boyscale <00_skeleton.bin> --<group>z <value> Z axis only


Available groups:

| Group | Bones |
|-------|-------|
| `spine` | 2, 3, 4 |
| `neck` | 5 |
| `rarm` | 16, 17, 18 |
| `larm` | 33, 34, 35 |
| `rhand` | 19, 20 |
| `lhand` | 36, 37 |
| `rhip` | 50 |
| `lhip` | 59 |
| `rthigh` | 51 |
| `lthigh` | 60 |
| `rshin` | 52 |
| `lshin` | 61 |
| `rankle` | 53 |
| `lankle` | 62 |
| `rfoot` | 54 |
| `lfoot` | 63 |
| `rtoe` | 56 |
| `ltoe` | 65 |

Pair aliases (both sides at once):

| Alias | Groups |
|-------|--------|
| `arms` | larm + rarm |
| `hands` | lhand + rhand |
| `shoulders` | lshldr + rshldr |
| `hips` | lhip + rhip |
| `thighs` | lthigh + rthigh |
| `shins` | lshin + rshin |
| `ankles` | lankle + rankle |
| `feet` | lfoot + rfoot |
| `toes` | ltoe + rtoe |
| `legs` | all leg bones both sides |
| `torso` | spine |

---

#### Safe Bones (will NOT move hair or face)

Spine: --b2 --b3 --b4

Neck: --b5

Shoulder: --b15 --b32

Upper arm: --b17 (R) --b34 (L)

Elbow: --b18 (R) --b35 (L)

Lower arm: --b19 (R) --b36 (L)

Hand: --b20 (R) --b37 (L)

Legs: --b50 to --b67


#### DANGER - Will move hair and head

Chest anchors: --b12 --b13 --b14

Face / Eyes: --b6 to --b11


Use `--b4x` and `--b4z` for wider chest instead of chest anchors.

Use `--b15x` and `--b32x` for wider shoulders.

---

#### BOY Bone Quick Reference

| Bone | Name | Notes |
|------|------|-------|
| 0 | ROOT | Y locked at 64.0 always |
| 2 | SPINE_BASE | Lower back / waist |
| 3 | SPINE_MID | Stomach |
| 4 | SPINE_TOP | Upper chest |
| 5 | NECK | Neck height and width |
| 15 | SHOULDER_R | Right shoulder socket |
| 17 | UPPER_ARM_R | Right upper arm |
| 18 | ELBOW_R | Right elbow |
| 19 | LOWER_ARM_R | Right lower arm |
| 20 | HAND_R | Right hand |
| 32 | SHOULDER_L | Left shoulder socket |
| 34 | UPPER_ARM_L | Left upper arm |
| 35 | ELBOW_L | Left elbow |
| 36 | LOWER_ARM_L | Left lower arm |
| 37 | HAND_L | Left hand |
| 50 | HIP_R | Right hip |
| 51 | THIGH_R | Right thigh |
| 52 | SHIN_R | Right shin |
| 53 | ANKLE_R | Right ankle |
| 54 | FOOT_R | Right foot |
| 59 | HIP_L | Left hip |
| 60 | THIGH_L | Left thigh |
| 61 | SHIN_L | Left shin |
| 62 | ANKLE_L | Left ankle |
| 63 | FOOT_L | Left foot |

---

#### BOY Scaler Examples

Taller spine (20%):
boyscale 00_skeleton.bin --b2y 1.20 --b3y 1.20 --b4y 1.20

Longer ankles:
boyscale 00_skeleton.bin --b53y 1.30 --b62y 1.30

Taller legs + fat arms:
boyscale 00_skeleton.bin --legsy 1.25 --armsy 2.00

Short thick neck:
boyscale 00_skeleton.bin --b5y 0.80 --b5x 1.40 --b5z 1.40

Wider waist:
boyscale 00_skeleton.bin --b2x 1.20 --b2z 1.20 --b3x 1.20 --b3z 1.20

Bigger hands:
boyscale 00_skeleton.bin --b20 1.40 --b37 1.40

Bigger feet:
boyscale 00_skeleton.bin --b54 1.30 --b63 1.30


Full bodybuilder example:

boyscale 00_skeleton.bin --b2y 1.10 --b3y 1.10 --b4y 1.10 --b5y 0.75 --b5x 1.60 --b5z 1.60 --b17y 2.20 --b17z 2.20 --b34y 2.20 --b34z 2.20 --b51x 1.30 --b51z 1.30 --b60x 1.30 --b60z 1.30 --b54 1.20 --b63 1.20


---

#### BOY Mod Presets

Pre-made skeleton mods that can be applied instantly to

`00_skeleton.bin` or directly to `BOY_00000.rdtb`.

Both commands auto-detect the file type from the extension.

---

##### BoyModV2 — Taller Player Mod - Default Farmer Version

boymodv2 00_skeleton.bin

boymodv2 BOY_00000.rdtb

---

##### BoyModV3 — Taller Player Mod - Uptight Farmer Version

boymodv3 00_skeleton.bin

boymodv3 BOY_00000.rdtb

---

##### BoyOriginal — Restore BOY to Original Vanilla Skeleton

All of the following commands do the same thing:

boyoriginal BOY_00000.rdtb

boyorig BOY_00000.rdtb

boyback BOY_00000.rdtb

boyrestore BOY_00000.rdtb

---

#### After applying any BOY mod

If you applied to `00_skeleton.bin`:

tool.exe -crdtb <extracted_folder> BOY_00000.rdtb

tool.exe -chda BOY BOY.HDA

If you applied directly to `BOY_00000.rdtb`:

tool.exe -chda BOY BOY.HDA

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

### Extract a single VAG from the music bank:

-xvag BAR.BD BAR.HD 0 mysong.vag

### Extract all VAGs from the music bank into a Folder:

-xvag all MUSIC.BD MUSIC.HD FOLDERNAME

### Replace a single VAG in the music bank:

-rvag 0 mysong.vag BAR.BD BAR.HD

---

### Extract and replace Sound Effects (SE.HDA)
Extract SE.HDA:
-xhda SE.HDA SE

Output:
SE.BD  ← all sound effects
SE.HD  ← header

### Extract all sound effects:
-xvag all SE.BD SE.HD ./sfx

Output: 000.VAG, 001.VAG, 002.VAG, ...

### Extract a single sound effect:
-xvag SE.BD SE.HD 5

Output: 005.vag (auto-named)

### Replace a single sound effect:
-rvag 5 new_sfx.vag SE.BD SE.HD

### Replace all sound effects from a folder:
-rvag all ./sfx SE.BD SE.HD

Repack SE.HDA:
-chda SE SE.HDA

---

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

HMSTHModdingTool> boymodv3 BOY_00000.rdtb

HMSTHModdingTool> boyback BOY_00000.rdtb

HMSTHModdingTool> boyscale 00_skeleton.bin --b2y 1.20 --b3y 1.20

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
