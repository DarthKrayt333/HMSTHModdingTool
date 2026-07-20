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

## Mods Showcase

### v1.4.6-Beta: Shrek BOY Head Swap

<img width="1020" height="548" alt="BOY with Shrek head — created using v1.4.6 head swap feature" src="https://github.com/user-attachments/assets/05ecbb49-693f-4de0-8c12-86447e4bab9a" />

*Custom Shrek head replacing player character — created
with the new `xbatches`/`cbatches` per-batch workflow.*

---

### v1.4.6-Beta: Custom Player Model v1

<img width="1094" height="605" alt="Custom player model with swapped head and modded textures" src="https://github.com/user-attachments/assets/7f8d7107-b644-4e41-ba1d-c9e4c6034245" />

*Custom player model — swapped head and modded textures.*

---

## Changelog

### Version v1.4.8-Beta

- **Fixed** SRDB modding — embedded RDTB texture
  assignments in the `_obj` folder output now
  correctly align to their real material IDs from
  the RDTB material table. Previously, only the
  first batch got the correct texture and all
  other batches fell back to `mat_00`. Fix works
  for both standalone `.rdtb` files AND `.srdb`
  extracted embeds. Applies automatically after
  `x3d` — no new commands needed.
- **Fixed** Scaling & moving unchanged-vertex
  batches — when you scale or move a batch in
  Blender without changing its vertex count, the
  edit now correctly applies in-game. Previously
  the tool would skip these batches entirely
  because it only detected vertex-count changes.
  New standalone in-place XYZ overwrite runs
  automatically after `cbatches`, preserves all
  VIF structure (headers, GIF tags, EOF
  terminators, bone weights, normals, UVs), and
  works across BIG, SMALL, MIRRORED RDTBs, and
  SRDB embedded RDTBs. Silent no-op when no
  scale/move edits detected — zero risk to
  unedited batches.
- **Added** `fixps2logo` — replicates Disc Patcher
  v3.0 functionality directly inside the tool
  (fixes PS2 logo + Master Disc markers so PS2
  BIOS accepts modded ISOs)
- **Added** `fixiso` — all-in-one auto-fix command
  that runs 3 operations in order: repairs ISO
  structure, patches PS2 logo, fixes LBA table
  in SLUS_202.51
- **Added** `fixisoonly` — renamed from old
  `fixiso` (structure repair only, without logo
  patch or LBA fix)
- **Added** `fixps2logo` supports both `.iso`
  (2048 bytes/sector) and `.bin` (2352 bytes
  /sector) formats automatically
- **Added** ISO format preservation — 2048 ISOs
  stay as 2048 ISO after patching, 2352 BINs
  stay as 2352 BIN (no forced conversion)
- **Added** `fakeyear` command — changes year on
  all files inside the ISO/BIN with year > 2001
  to your specified year. Files with year ≤ 2001
  are left unchanged
- **Added** `fakeyear` also patches the ISO's own
  PVD dates (Creation, Modification, Expiration,
  Effective) using the same year > 2001 rule
- **Added** `fakeyear` also updates Windows
  Explorer file timestamps (Created, Modified,
  Accessed) so the ISO appears with the fake
  year in Windows as well
- **Added** `fakeyear` defaults to year 2001 if
  no year is specified (e.g., `fakeyear
  HMSTH.iso` uses 2001 automatically)
- **Added** Master Disc marker application
  (CDVDGEN 1.20, PlayStation Master Disc 2
  identifiers) via `fixps2logo` — replicates
  Sony's official master disc format
- **Added** `cmusic` auto-detects the VAG's
  sample rate and patches the HD file accordingly
  (2 bytes at offset 0x68). Previously the HD
  was hardcoded to 22050 Hz regardless of input
- **Added** `cmusic` PS2 hardware limit
  protection — caps sample rate at 48000 Hz
  (PS2 SPU2 maximum) with a warning message if
  the input VAG exceeds this
- **Added** Fully self-contained tool — embedded
  logo blobs and ECC data directly inside the
  .exe as Base64 (no external `embedded_blobs.
  bin` or `ecc_data.bin` files needed)
- **Added** Universal batch swapping — you can
  now swap ANY batch in an RDTB with a custom
  3D model, including BOY's tools, NPCs,
  animated hair, and body parts (previously
  only batch_0005 head swap was safe)
- **Fixed** All non-head batches now render
  correctly in game with custom meshes
  (animated hair, body parts, tools when held/
  used, NPC parts, items)
- **Fixed** BOY's tools now render correctly
  in-game with swapped 3D models when he holds
  or uses them (previously would render doubled
  or incorrectly)
- **Known Issue** Tool inventory MENU ICONS
  still show the ORIGINAL 3D model even after
  swap. The in-game tool (when BOY holds/uses
  it) renders correctly with the new mesh, but
  the inventory/menu preview icon uses a
  separate render path that isn't updated yet.
  Fix in development.
- **Working On** Menu icon batch identification
  for proper inventory preview swapping

---

### Version v1.4.6-Beta

- **Added** Per-batch 3D model swapping —
  you can now swap individual 3D model batches
  inside an RDTB instead of replacing the
  entire model archive
- **Added** Batch folder workflow (`xbatches` /
  `cbatches`) — extracts every batch as its own
  OBJ file organized by texture into model_NN
  folders for clean Blender editing
- **Added** Player head swap fully working —
  you can now replace BOY's head (batch_0005)
  with any custom 3D model (Shrek, Mario, custom
  characters, anything) and it works correctly
  in game with animations, lighting, and bone
  attachment preserved
- **Added** Auto-hide siblings option (`--all`) —
  when replacing a batch, automatically hides
  other batches in the same model group (useful
  for head swaps to remove the original hair
  and ponytail)
- **Added** Normal copying modes (`--normals
  match` default, `--normals zero`, `--normals
  up`, `--normals-xyz X,Y,Z`) — preserves
  original lighting by copying normals from the
  closest original vertex
- **Added** RDTB format converters:
  - `big2small` — convert big RDTB (14 chunks,
    3 LOD meshes) to small RDTB (10 chunks,
    single mesh)
  - `small2big` — convert small RDTB to big
    RDTB with duplicated LOD data
  - `big2mirror` — convert to mirrored format
    (smaller file, works in big-RDTB slots)
  - `mirror2big` — convert mirrored to full big
  - `small2mirror` / `mirror2small` — convert
    between small and mirrored
  - `fmtrdtb` — detect and show RDTB format
    (BIG / SMALL / MIRRORED / UNKNOWN)
- **Added** Format flags for `c3d` and
  `cbatches` commands — output as `--small`,
  `--mirrored` (default), or `--big`
- **Added** `scanbatch` command — find which
  model group a batch belongs to and lists all
  sibling batches
- **Added** `xbatch` / `extractbatch` command —
  extract a single batch as standalone OBJ
- **Added** `xmodel` / `extractmodel` command —
  extract all batches in a model group as
  combined OBJ
- **Added** Fixed small RDTB batch extraction
  (FLAT, DAVID, animals, items, etc.) — now
  correctly identifies mesh chunks vs material
  chunks regardless of chunk count
- **Added** Globally-sorted pointer lookup for
  small RDTBs — handles unsorted pointer tables
  where batch order doesn't match file order
- **Added** Mesh chunk auto-detection by VIF
  block scanning — finds the real mesh chunk
  even when chunk indices vary between RDTB
  types
- **Changed** Default output format for `c3d`
  and `cbatches` is now MIRRORED (smaller
  files, works in any slot, easier to mod)
- **Fixed** Only player head batch
  (batch_0005) is currently safe to swap for
  3D model mods — other batches (animated hair,
  tools, body parts) may render incorrectly
  when swapped due to bone-skinning data still
  being researched
- **Known Issue** Menu icons in inventory still
  show original 3D model when only the in-game
  batch is swapped (items have separate menu
  render path that's not yet identified)
- **Fixed** Universal batch swapping for
  all batches (animated hair, tools, body
  parts, NPC parts)
- **Working On** Menu icon batch identification
  for proper tool item icon swapping

---

### Version v1.4.5-Beta
- **Added** Full SRDB extractor and creator
  (`xsrdb3d` / `csrdb3d`) — extracts ALL embedded
  RDTBs from SRDB archives with textures, supports
  per-embedded OBJ/DAE output and combined output
- **Added** 3D model editing now fully working —
  you can edit vertex positions in Blender and
  reimport with byte-perfect roundtrip when no
  edits made
- **Added** 3D model upscaling — use `--scale N`
  on `c3d` to scale models up or down
- **Added** Automatic 3D model size optimization
  on extract — oversized items (blueberry,
  large map props, etc.) automatically scale
  down to ~100 units for comfortable Blender
  viewing, then auto-restore to original game
  size on rebuild
- **Added** Smart `.BD` audio file detection —
  recognizes PS2 ADPCM audio body files even
  though they have no magic header, using
  archive context (`.HD` presence) + structural
  ADPCM validation
- **Added** Embedded RDTB texture assignment
  fix — small RDTBs inside SRDBs now get
  proper per-batch texture mapping matching
  SRDB extractor output
- **Added** Single combined OBJ output for
  embedded RDTBs (matches SRDB format with
  per-batch `usemtl` references)
- **Added** Auto-detect file type for `x3d`
  command — automatically routes to SRDB or
  RDTB extractor based on file magic
- **Added** Auto-detect manifest type for `c3d`
  command — routes to SRDB or RDTB creator
  based on rebuild_manifest.json contents
- **Fixed** `.BD` audio file recognition when
  extracting `.HDA` archives (no longer
  labeled `.bin`)
- **Fixed** Texture assignment for embedded
  RDTBs (dog house, small props) — batches
  now correctly map to their assigned
  textures instead of all using texture 0
- **Fixed** SRDB byte-perfect roundtrip when
  no edits made (was previously off by ~49KB
  due to auto-scale not being reversed)
- **Changed** Console output formatting —
  slot numbers and filenames now nicely
  aligned in extraction output
- **Known Issue** Cannot yet change the
  number of vertices in 3D models (must
  keep same vertex count when editing)

---

### Version v1.4.4-Beta
- **Fixed** Hex codes going to wrong positions when translating text
- **Fixed** Hex codes appearing inside translated sentences instead of correct positions
- **Fixed** Hex codes after [dialog] tag being placed incorrectly on rebuild
- **Fixed** [end] bug when removing trailing empty rows from .txt files
- **Added** Hex mode as default text export — all tokens visible inline as [varNN] / [hexNN_MM]
- **Added** Dat/Clean mode for text export — hidden tokens stored in companion .dat file
- **Added** Smart anchor system for hidden hex codes (ControlIndex + LineIndex)
- **Added** Backward compatibility with old .dat files
- **Changed** Default `-xtxt` / `-ctxt` now exports/imports in hex mode (no .dat needed)
- **Changed** Use `-xtxt -dat` or `-xtxt -clean` for .dat mode
- **Fixed** Tools/items 3D models now properly centered
  at world origin when exported (no longer crossing
  through each other or off-center in Blender)
- **Fixed** Tools/items spacing now bounds-aware
  (items separated by their actual width + gap,
  not a fixed offset)
- **Added** 3D Model Extractor (`-x3d`)
- **Added** 3D Model Creator (`-c3d`)
- **Added** Per-texture model files
- **Added** Combined model files (body + tools separate)
- **Added** Skeleton extraction to CSV on RDTB extract
- **Added** RDTB manifest JSON for rebuild tracking
- **Added** RDTB diagnostic tools (diag through diag19)
- **Added** SLUS LBA table analyzer (`-slus`)
- **Added** SLUS LBA updater (`-lbaupdate`)
- **Added** Cross-character mesh wrap (`-x3d_dual`) experimental
- **Added** Native layout mode (`-x3dnative`)
- **Added** Single chunk extractor (`-x3dchunk`)
- **Known Issue** Body 3D model positions still corrupted
- **Known Issue** Some NPC models appear as tube/cylinder shape
- **Known Issue** Cross-character mesh transplant not yet working
- **Working On** Correct world-space vertex positions
- **Working On** Skeleton export with mesh for Blender rigging
- **Working On** Full mesh replacement into game
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

## Known Bugs & Workarounds

### File Size / Memory Limit
Some edited files may become too large for the game's memory limit.
A compressor is now available. Usage to make compressed files inside .HDA is by Default
-chda <folder_name> <new_file_name.hda> or chda <folder_name> <new_file_name.hda>.

---

### 3D Model Upscaling & Downscaling ✅ WORKING
You CAN upscale or downscale 3D models freely
and have them appear correctly in game:

1. Extract with `x3d` (or `xbatches`)
2. Open the OBJ in Blender or any 3D program
3. Scale the model up or down (any size you want)
4. Save the OBJ
5. Rebuild with `c3d` (or `cbatches`)
6. Repack with `chda`

The upscaled/downscaled model will appear at
the new size in game. This works for any
character or item (BOY, NPCs, animals, props).

You can also use the `--scale N` flag on `c3d`
to scale during rebuild:

-c3d BOY_obj BOY_NEW --scale 1.5    (50% bigger)

-c3d BOY_obj BOY_NEW --scale 0.5    (half size)

---

### 3D Model Export Positions ⚠️ NOT YET CORRECT
Body 3D model vertices are currently exported with
incorrect world-space positions. The geometry shape
is extracted correctly and you can edit vertex
positions of the same character without issues,
but the body part positions relative to each other
in the extracted OBJ files are not yet matching
their true world-space positions.

This is caused by PS2 VIF bone transform data
not yet being fully decoded.

Tools/items (model_05) are exported correctly and
centered at the world origin with proper separation.

**What this means for modders:**
- ✅ You CAN edit existing vertices (move, scale,
  reshape any part of the model)
- ✅ You CAN upscale/downscale entire models
- ✅ You CAN replace textures freely
- ✅ You CAN swap the player's head batch
  (batch_0005) with a completely new mesh
- ❌ Extracted body part positions don't match
  the in-game positions (parts appear separated
  in Blender but render correctly in game)

---

### 3D Model Skeleton & Animation Export ⚠️ IN PROGRESS
Currently the tool extracts SOLID 3D models
(geometry + UVs + normals + textures), which
is enough for:

- ✅ Texture editing
- ✅ Vertex position editing
- ✅ Model upscaling/downscaling
- ✅ Player head swap

What is NOT yet exported alongside the mesh:

- ❌ Skeleton/bones (visible in 3D program for rigging)
- ❌ Animation data (idle pose, walk, run, etc.)
- ❌ Bone weights (which vertex belongs to which bone)

**Working on:** Full skeleton + animation export
so you can open the model in Blender already
rigged and posed (idle style), see exactly how
it animates in game, and edit animations.

This is the next major feature being developed.
Until then, you can still mod meshes — just
without skeleton visibility in Blender.

---

### 3D Model Per-Batch Swapping (Updated v1.4.7)
As of v1.4.7, batch swapping works for ALL batches
in the RDTB — you can replace animated hair, body
parts, NPC batches, tools (when used in-game), and
other items with custom 3D models successfully.

**What works for ANY batch (v1.4.7):**
- ✅ Swapping with a completely new mesh
- ✅ Changing vertex count
- ✅ Editing existing vertex positions
- ✅ Modifying UVs and textures
- ✅ Scaling models with `--scale N`
- ✅ Upscaling/downscaling for in-game changes
- ✅ Replacing textures only
- ✅ BOY's tools when he holds/uses them in-game

**Still has one known bug:**
- ❌ Tool inventory menu icons still show ORIGINAL
  mesh even after swap (in-game tool renders
  correctly, only the menu preview is affected)

**Fix coming:**
- Menu icon batch identification for proper
  inventory preview swapping

---

### Menu Icons (Still Not Fixed in v1.4.7)
When you swap a tool batch that has an inventory
menu icon, the in-game 3D model (the tool BOY
holds and uses) updates correctly with your new
mesh. However, the menu icon preview inside the
inventory/tool menu still shows the ORIGINAL 3D
model. This is because items have a separate
render path for menu display that hasn't been
identified yet.

**What works:**
- ✅ Tool renders correctly in BOY's hand
- ✅ Tool animations work with the new mesh
- ✅ All texture changes apply correctly
- ✅ Vertex positions and scaling work

**Still broken:**
- ❌ Inventory menu preview icon uses original mesh

Fix in development for a future update.

---

### PS2 Logo Patching
Works on both ISO (2048) and BIN (2352) formats

The logo functionality works even if you don't run fakeyear (they're independent features)

Original ISO format is preserved (won't convert 2048 to 2352 permanently)

---

### Audio Sample Rate Limit
PS2 SPU2 hardware maximum is 48000 Hz

cmusic automatically caps higher rates at 48000 Hz

For proper playback, encode VAGs at 22050-48000 Hz range

22050 Hz or 16000 Hz recommended for smallest file size

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
| `.BD` | (auto-detected) | PS2 Sound Bank Body (VAG audio data) |
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

**RDTB Internal Structure:**

| Chunk | Label | Contents |
|-------|-------|----------|
| 0 | skeleton | Bone pointer array + bone records |
| 1 | mesh_idx | Index buffer + sub-pointers |
| 2 | mesh_main | Main vertex/normal/UV data |
| 3-6 | mesh_grp1-4 | Mesh groups (LOD/body parts) |
| 7-10 | idx_tbl_0-3 | Small index/lookup tables |
| 11 | mesh_lod0 | VIF mesh - highest detail (1066 VIF blocks in BOY) |
| 12 | mesh_lod1 | VIF mesh - medium detail  (774 VIF blocks in BOY)  |
| 13 | mesh_lod2 | VIF mesh - lowest detail  (612 VIF blocks in BOY)  |

**Texture to body part map (BOY):**

| Texture | Body Part |
|---------|-----------|
| texture_00 | Legs, hands, backpack |
| texture_01 | Torso, arms, forearms |
| texture_02 | Head, cap, hair, ears, neck |
| texture_03 | Eyes (animated, 4-bit) |
| texture_04 | Mouth |
| texture_05 | Tools and items (separate file) |
| texture_06 | Shoes and ankles |

**Status:** Basic extraction working.
Vertex positions still being researched.
Skeleton export with mesh in progress.

---

### 3D Model Tools
> 3D Mesh by DarthKrayt333

-xbatches <file.rdtb> <file.gdtb> <base_name>

    Extract 3D models batches with textures.
    
    Creates 1 output folder:
    
      <base_name>_3d_batches_obj/       moddable OBJ files

-cbatches <models_batches_folder> <output_folder>

    Rebuild RDTB + GDTB from edited model files.
    
    Reads model_NN.obj files
    
    Builds new RDTB + GDTB in the output folder.

-xbatches <file.srdb> <file.gdtb> <base_name>

    Extract 3D models batches with textures.
    
    Creates 1 output folder:
    
      <base_name>_3d_batches_obj/       moddable OBJ files

-cbatches <models_batches_folder> <output_folder>

    Rebuild SRDB + GDTB from edited model files.
    
    Reads model_NN.obj files
    
    Builds new SRDB + GDTB in the output folder.

-x3d <file.rdtb> <file.gdtb> <base_name>

    Extract 3D models with textures

    Creates 2 output folders:

      <base_name>_3d_batches_obj/       moddable OBJ files
      <base_name>_all_obj/       not moddable OBJ files - view only (Also, the extraction's still a bit corrupted)


What works:

  ✅ Texture replacement
  
  ✅ Skeleton scaling (boyscale etc.)
  
  ✅ Editing existing vertices of any character/model
  
  ✅ Tools/items exported centered and separated
  
  ✅ Body part positions correctly placed in world space
  
  ✅ SRDB archive extraction with per-embedded textures
  
  ✅ Auto-scaling for oversized items and map props
  
  ✅ Byte-perfect roundtrip when no edits made
  
  ✅ Edit and reimport modified vertices to game
  
  ✅ **NEW v1.4.7**: Universal batch swap — swap ANY batch (head, hair, body parts, NPCs, tools, items) — full mesh swap now works everywhere
  
  ✅ **NEW**: Per-batch extraction and editing workflow
  
  ✅ **NEW**: RDTB format conversion (big/small/mirrored)
  
  ✅ **NEW**: Auto-hide siblings when replacing batches
  
  ✅ **NEW**: Normal copying for preserved lighting
  
  ❌ Menu icons not yet updated when item batches swapped

---

### .SRDB (Stage/Scene Render Data Binary)
**Magic:** `53 52 44 42` ("SRDB")

The `.SRDB` format is an **archive that contains multiple
RDTB files inside it**. It is NOT only used for maps —
SRDB archives bundle together multiple 3D models (props,
items, terrain pieces, environment objects, characters,
etc.) into a single file with shared GDTB textures.

Each embedded RDTB inside an SRDB has its own:
- Vertex/UV/normal data
- Material/texture assignments
- Bone hierarchy
- Mesh chunks

The SRDB extractor extracts every embedded RDTB as a
separate OBJ/DAE file (`embedded_NN.obj`) and also
produces a combined `<base>_all.obj` containing all
embedded models in one file.

**Status:** Fully working — extraction, editing,
and reimport all supported.

#### SRDB Tools

-xsrdb3d <file.srdb> <file.gdtb> <base_name>

    Extract all 3D models from SRDB archive.
    
    Creates 4 output folders:
    
      <base_name>_embedded_rdtbs_obj/   per-embedded OBJ files
      
      <base_name>_embedded_rdtbs_dae/   per-embedded DAE files
      
      <base_name>_all_obj/              combined OBJ
      
      <base_name>_all_dae/              combined DAE
    
    Each embedded RDTB gets:
    
      embedded_00.obj  (with correct textures)
      
      embedded_01.obj
      
      embedded_NN.obj  ...
    
    Auto-scales oversized models for comfortable
    viewing in Blender. Original scale is preserved
    in the manifest for byte-perfect rebuild.

-csrdb3d <models_folder> <output_folder>

    Rebuild SRDB + GDTB from edited model files.
    
    Reads embedded_NN.obj files and writes back
    to the original SRDB byte positions using the
    .voff sidecar files for byte-perfect injection.
    
    Auto-restores extraction scale during rebuild
    so vertices end up at original game-space
    coordinates regardless of viewing scale.

#### Auto-detection for x3d / c3d

The `x3d` and `c3d` commands automatically detect
whether you're working with a standalone RDTB or
an SRDB archive:

-x3d FRM_MAP_00000.srdb FRM_MAP_00001.gdtb FARMMAP
    → automatically routes to xsrdb3d

-x3d BOY_00000.rdtb BOY_00001.gdtb BOY
    → automatically routes to standard RDTB extractor

-c3d FARMMAP_embedded_rdtbs_obj FARMMAP_OUT
    → automatically routes to csrdb3d

-c3d BOY_obj BOY_OUT
    → automatically routes to standard RDTB creator

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

**v1.4.5 update:** `.BD` audio body files are now
automatically detected on `.HDA` extraction.
Previously they were saved with `.bin` extension
because they have no magic header. The tool now
uses smart detection — if an `.HD` file is present
in the same archive, any 16-byte-aligned binary
file passing PS2 ADPCM structural validation is
correctly labeled `.BD`. Standalone `.BD` files
(without nearby `.HD`) are also detected via
strict ADPCM block-pattern analysis.

---

### PS2 EXE (SLUS_202.51)
The PS2 game executable for the USA version is `SLUS_202.51`.
It is believed to be responsible for:
- 3D model collision data
- Game scripts and events
- Scene management
- Running and coordinating all game systems and files

**File size:** 1,472,560 bytes
**Developer:** Victor Interactive Software
**Publisher:** Natsume / TOYBOX

**SLUS LBA Table (USA):** `0x162460 - 0x162D30`

**Important:** If you change the size of any .HDA
file you must update the LBA table in SLUS_202.51
or the game will crash on load.

-fixelf SLUS_202.51 (lba) (size in decimal) Fix PS2 EXE entry after modding


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




### 3D Model Commands (SRDB + RDTB) (Older Versions - Before v1-4-6-Beta, c3d no longer supported)

-x3d <file.rdtb_or_srdb> <file.gdtb> <base_name>

    Extract 3D models (auto-detects RDTB or SRDB)

-c3d <models_folder> <output_folder> [--scale N]

    Rebuild 3D models (auto-detects RDTB or SRDB)
    
    Use --scale N to upscale/downscale models

-xsrdb3d <file.srdb> <file.gdtb> <base_name>

    Extract all embedded RDTBs from SRDB archive

-csrdb3d <models_folder> <output_folder>

    Rebuild SRDB from edited embedded RDTB models


### Per-Batch 3D Model Commands (NEW v1.4.6)

-xbatches <file.rdtb> <file.gdtb> <base_name>

    Extract every batch in the RDTB as its own
    OBJ file. Creates a folder structure:
    
      <base>_3d_batches_obj/
        _source.rdtb         (original for rebuild)
        _source.gdtb         (original textures)
        _info.txt            (info about batches)
        model_00/            (texture 0 group)
          texture_00.bmp     (inline texture)
          batch_0000.obj
          batch_0001.obj
          ...
        model_01/            (texture 1 group)
          texture_01.bmp
          batch_0003.obj
          ...
        model_NN/            (additional texture groups)
    
    Each batch is a single OBJ ready for editing
    in Blender. Edit any batch, delete batches
    you want hidden, then rebuild with cbatches.


-cbatches <folder> <out_folder> [flags]

    Rebuild RDTB from per-batch folder. Reads
    every batch_NNNN.obj from each model_XX
    subfolder and writes back to the RDTB.
    
    Flags:
      --normals MODE      match (default), zero,
                          up, keep
      --normals-xyz X,Y,Z custom normal vector
      -all                hide siblings of edited
                          batches in same model
      --small             output as small RDTB
      --mirrored          output as mirrored
                          (DEFAULT)
      --big               output as big RDTB
    
    All flag formats work: 'small', '-small',
    '--small' are equivalent.


-scanbatch <file.rdtb> <batch_index>

    Show which model group a batch belongs to.
    Lists all sibling batches in the same
    texture group. Useful for knowing what to
    hide with --all when replacing one batch.
    
    Example: scanbatch BOY_00000.rdtb 5
    Output:
      Texture ID: 2
      Model name: model_02.obj
      Total batches in this model: 8
      Batch indices: [5, 30, 31, 32, 33, 34, 35, 36]


-xbatch <file.rdtb> <batch_index> <out.obj>

    Extract a single batch as a standalone OBJ
    file (without the folder structure).
    
    Example: xbatch BOY_00000.rdtb 5 head.obj


-xmodel <file.rdtb> <batch_index> <out.obj>

    Extract all batches in the same model group
    (sharing the same texture) as a combined
    OBJ with batch_NNNN groups.
    
    Example: xmodel BOY_00000.rdtb 5 model_02.obj
    (extracts all 8 batches that use texture 2)


### RDTB Format Converters (NEW v1.4.6)

The tool now supports three RDTB formats:
- **BIG** — 14 active chunks with 3 separate
  LOD meshes (high/medium/low quality)
- **SMALL** — 10 active chunks with single
  mesh, slots 9/10/12/13 = 0xFFFFFFFF
- **MIRRORED** — 14 slots but 9/10 share
  offset with 8, 12/13 share offset with 11
  (smaller file size, works in big-RDTB slots)

-fmtrdtb <file.rdtb>

    Detect and show the RDTB format type
    along with full slot table.

-big2small <in.rdtb> <out.rdtb>

    Convert big RDTB (14 chunks, 3 LODs) to
    small RDTB (10 chunks, single mesh).
    Clears bit 7 of chunk 8 flags to tell
    the game "no external LOD chunks".

-small2big <in.rdtb> <out.rdtb>

    Convert small RDTB to big RDTB by
    duplicating the mesh and lookup data
    into chunks 9, 10, 12, 13.

-big2mirror <in.rdtb> <out.rdtb>

    Convert big RDTB to mirrored format
    (smaller file, slots 9/10 point to 8
    and slots 12/13 point to 11).

-mirror2big <in.rdtb> <out.rdtb>

    Convert mirrored RDTB to full big RDTB
    with unique data in all 14 slots.

-small2mirror <in.rdtb> <out.rdtb>

    Convert small RDTB to mirrored format
    (fills slots 9/10 and 12/13 to point at
    existing chunk 8 and 11 data).

-mirror2small <in.rdtb> <out.rdtb>

    Convert mirrored RDTB back to small
    format (sets slots 9/10/12/13 to
    0xFFFFFFFF and fixes chunk 8 flags).


### Text Commands

-xtxt <text.bin> <ptr.bin> <out.txt> Export text to .txt file

-ctxt <in.txt> <text.bin> <ptr.bin> Import text from .txt file


---


### Text Modes Explained

#### Hex Mode (Default)

[hex0B_06][hex09_10]Hey, you don't look

familiar. You can't

possibly be here on[roll]

vacation...Um...[roll][dialog]

[hex0B_00][hex09_51][hex0B_06][hex0C_04][hex03_78]...Oh, so you're that

old farmer guy's

grandson.[end]


Everything is visible. You can translate freely without worrying
about hidden codes — they stay where they are in the text.

#### Dat/Clean Mode

Hey, you don't look

familiar. You can't

possibly be here on[roll]

vacation...Um...[roll][dialog]

...Oh, so you're that

old farmer guy's

grandson.[end]


Hidden codes are stored in a companion `.dat` file.
The .txt looks cleaner but you must keep the .dat file.
The tool automatically places hidden codes at the correct
positions when rebuilding, even if you change the text length.


---


#### Default Mode (Hex — recommended for translation)
All hidden tokens are visible inline in the .txt as `[varNN]` / `[hexNN_MM]` tags.
No companion .dat file needed. What you see is what you get.

-xtxt <text.bin> <ptr.bin> <out.txt> Export text (hex mode, default)

xtxt <text.bin> <ptr.bin> <out.txt> Same without dash

-ctxt <in.txt> <text.bin> <ptr.bin> Import text (hex mode, default)

ctxt <in.txt> <text.bin> <ptr.bin> Same without dash

---


#### Dat/Clean Mode (experimental — hidden hex tokens in .dat file)
Hidden tokens are stripped from the .txt and stored in a companion .dat file.
The .dat file must stay next to the .txt with the same base name.

-xtxt -dat <text.bin> <ptr.bin> <out.txt> Export text + .dat file

-xtxt -clean <text.bin> <ptr.bin> <out.txt> Same as -dat

xtxt dat <text.bin> <ptr.bin> <out.txt> Same without dashes

xtxt clean <text.bin> <ptr.bin> <out.txt> Same without dashes

-ctxt -dat <in.txt> <text.bin> <ptr.bin> Import text using .dat file

-ctxt -clean <in.txt> <text.bin> <ptr.bin> Same as -dat

ctxt dat <in.txt> <text.bin> <ptr.bin> Same without dashes

ctxt clean <in.txt> <text.bin> <ptr.bin> Same without dashes


#### Important Notes
- **Hex mode** (default) is recommended for translation work —
  you can see all control codes directly in the text
- **Dat/Clean mode** gives cleaner .txt files but requires keeping
  the .dat file — if you lose the .dat, you cannot rebuild
- After switching modes, always **re-export** with `-xtxt` before editing
- Trailing empty rows at the end of .txt files are no longer required


---


### ELF Commands
-fixelf SLUS_202.51 (lba) (size in decimal) Fix PS2 EXE entry after modding


---

### PS2 ISO/BIN Commands (NEW v1.4.7)
-fixiso <file.iso>
    ALL-IN-ONE auto-fix. Runs 3 fixes in order:
    1. Repairs ISO structure
    2. Patches PS2 logo + Master Disc markers
    3. Fixes LBA table in SLUS_202.51

-fixisoonly <file.iso>
    Only repairs ISO structure
    (no logo patch, no LBA fix)

-fixps2logo <file.iso>
    Replicates Disc Patcher v3.0.
    Fixes PS2 logo + Master Disc markers.
    Works on both .iso (2048) and .bin (2352).
    Preserves the original format.

-fakeyear [year] <file.iso>
    Changes year on all files with year > 2001
    to your specified year. Leaves files with
    year <= 2001 unchanged. Only year is changed
    (month/day/time stay the same). Also patches
    the ISO's own PVD dates + Windows file
    timestamps.
    Default year is 2001 if not specified.

    Examples:
      fakeyear 2000 HMSTH.iso
      fakeyear HMSTH.iso   (defaults to 2001)

---


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


NEW v1.4.7: cmusic now auto-detects the input VAG's
sample rate and patches the HD file accordingly.
Previous versions were hardcoded to 22050 Hz. Now
any sample rate up to 48000 Hz (PS2 hardware max)
is supported automatically.


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

##### BoyModV4 — Taller Player Mod - Best Current

boymodv4 00_skeleton.bin

boymodv4 BOY_00000.rdtb

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

### Player Head Swap (NEW v1.4.6)

**Step 1:** Extract BOY archive and convert to batches:

-xhda BOY.HDA BOY

-xbatches BOY\BOY_00000.RDTB BOY\BOY_00001.GDTB BOY

This creates `BOY_3d_batches_obj/` folder with all batches.

**Step 2:** Find which batch is the head:

-scanbatch BOY\BOY_00000.RDTB 5

Output shows: model_02, batches [5, 30, 31, 32, 33, 34, 35, 36]
(batch 5 = head, batches 30-36 = hair/ponytail)

**Step 3:** Open `BOY_3d_batches_obj/model_02/batch_0005.obj`
in Blender, replace with your custom head mesh,
edit `texture_02.bmp` if needed, save the OBJ.

**Step 4:** Rebuild — use `-all` flag to hide the
original hair batches (30-36) which would clash
with your new head:

-cbatches BOY_3d_batches_obj BOY_NEW -all

**Step 5:** Repack the HDA:

-chda BOY_NEW BOY.HDA

Done! Your custom head is now in the game.

**Note (v1.4.7):** Universal batch swapping now
works for ALL batches (head, hair, body parts,
NPCs, tools). You can swap any batch with a
custom mesh and it renders correctly in-game.
The only remaining bug is that tool inventory
MENU ICONS still show the original mesh even
after a tool swap — the tool renders correctly
when BOY holds/uses it, only the menu preview
is affected. Fix in development.

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


### Experimental - Extract BOY 3D Models
-x3d BOY_00000.rdtb BOY_00001.gdtb BOY

Output folders:
  BOY_obj/       per-texture OBJ files
  
  BOY_dae/       per-texture DAE files
  
  BOY_all_obj/   body + tools OBJ (2 files)
  
  BOY_all_dae/   combined DAE

Edit models in Blender then rebuild:
-c3d BOY_obj BOY_NEW

-chda BOY BOY.HDA

### Extract Kurt (HAYATO) NPC Models
-xhda HAYATO.HDA HAYATO

-x3d HAYATO_00000.rdtb HAYATO_00001.gdtb KURT

### Analyze SLUS LBA Table
-slus SLUS_202.51

-slus SLUS_202.51 jp

-lbaupdate SLUS_202.51 42 2097152



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

Export text (default hex mode — all codes visible)

-xtxt EVTMSG12_00001.bin EVTMSG12_00002.bin output.txt

Or export with dat/clean mode (codes hidden in .dat file)

-xtxt -dat EVTMSG12_00001.bin EVTMSG12_00002.bin output.txt

-xtxt -clean EVTMSG12_00001.bin EVTMSG12_00002.bin output.txt

Edit output.txt in any text editor

Import text back (must match the mode you exported with)

-ctxt output.txt EVTMSG12_00001new.bin EVTMSG12_00002new.bin

Or if you used dat/clean mode:

-ctxt -dat output.txt EVTMSG12_00001new.bin EVTMSG12_00002new.bin

Repack
-chda EVTMSG12 EVTMSG12.HDA



### Export and edit NPC text
NPC text is located inside a .HDA file inside another .HDA file.

For example: /CGDATA/CHARA/HAYATO.HDA/HAYATO_02.HDA/

-xtxt HAYATO_02_00001.bin HAYATO_02_00000.bin output.txt

Edit output.txt in any text editor

Import NPC text back

-ctxt output.txt HAYATO_02_00001new.bin HAYATO_02_00000new.bin



#### Why Hex Mode is Better for Modding

The hex codes you see in text like `[hex0B_06]`,
`[hex09_10]`, `[hexNN_MM]`, and `[varNN]` are NOT
just text formatting codes — they are **animation
and behavior control codes** for the player and NPCs
during cutscenes and dialogues.

Each hex code tells the game engine to do something,
such as:
- Play an animation on the speaker or listener
- Change facial expressions
- Trigger sound effects or pauses
- Move characters or change camera angles
- Show emotion bubbles
- Substitute variables (player name, item names, etc.)
- And many more behaviors

**The exact meaning of each hex code is currently
unknown / undocumented.** Modders are still
researching what each `[hexNN_MM]` value does in
different contexts. The effects vary by NPC, scene,
and surrounding codes.

#### Why this matters for modders

Because hex codes control animations and behaviors,
you can:
- **Add new animations** to existing dialogue by
  inserting hex codes you find in other lines
- **Create custom cutscene sequences** by moving
  and combining hex codes
- **Make NPCs more expressive** by experimenting
  with codes from other scenes
- **Synchronize animations with translated text**
  when localizing the game
- **Discover what each code does** by testing
  them in PCSX2 emulator
- **Build a community knowledge base** of which
  hex codes trigger which animations

#### Recommendation: use Hex Mode for modding

Dat/Clean mode hides these codes in a companion
`.dat` file. While this looks cleaner for pure
translation work, it's **much harder to mod
animations and behaviors** because you can't see
where the codes are.

**Use Hex Mode (default) when:**
- You want to experiment with animations
- You want to add custom NPC behaviors
- You want full control over cutscene flow
- You want to discover what unknown hex codes do
- You're modding more than just text

**Use Dat/Clean Mode only when:**
- You're doing pure translation with no animation changes
- You want the cleanest possible text file
- You will not lose the `.dat` companion file

#### Experimenting safely

When experimenting with hex codes:
1. **Always back up** the original `.bin` files first
2. **Test in PCSX2** (PS2 emulator) before burning to disc
3. **Try one new hex code at a time** so you know what causes what
4. **Note which codes work where** — keep a personal reference
5. **Copy hex codes from one line to another** to see what they do
6. **Try unused hex values** to discover hidden animations
7. **Share your findings** with the HMSTH modding community
   so we can build a knowledge base of what each
   code does


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

### Extract and edit a full map (SRDB)
-xhda FRM_MAPZ.HDA FRM_MAPZ

Extract all 3D models from SRDB:

-x3d FRM_MAPZ_00000.srdb FRM_MAPZ_00001.gdtb mapz_test

(auto-detects SRDB and routes to xsrdb3d)

Output folders:

mapz_test_embedded_rdtbs_obj/   per-embedded OBJ files with textures
                                
mapz_test_embedded_rdtbs_dae/   per-embedded DAE files

mapz_test_all_obj/              combined OBJ (all models)

mapz_test_all_dae/              combined DAE

Edit models in Blender, then rebuild:

-c3d mapz_test_embedded_rdtbs_obj mapz_modded

(auto-detects SRDB folder and routes to csrdb3d)

Repack HDA:

-chda FRM_MAPZ FRM_MAPZ.HDA

---

### Verify SRDB byte-perfect roundtrip
Extract and immediately rebuild without editing:

-x3d FRM_MAPZ_00000.srdb FRM_MAPZ_00001.gdtb test

-c3d test_embedded_rdtbs_obj test_out

-vsrdb FRM_MAPZ_00000.srdb test_out\FRM_MAPZ_00000.srdb

Should print: "IDENTICAL"

This confirms the tool can extract, then rebuild
the exact same bytes when no edits are made.

---

### Auto-scaling for huge items and map props
Some items extract at huge sizes that are awkward
to view in Blender. The tool automatically detects
oversized models and scales them down to ~100 units:

-x3d FRM_MAP_00000.srdb FRM_MAP_00001.gdtb FRM_MAP_TEST

Console will show:

    Auto-scale: 0.0625x (model was 1600 units, scaling to 100)

When you rebuild, the scale is automatically inverted
so your edits go back into the file at the correct
original size:

-c3d FRM_MAP_TEST_obj FRM_MAP_TEST_NEW

(no flags needed — auto-restore happens automatically)

Works for both RDTB items and SRDB embedded map props.
Standard-sized models (under 250 units) are not
affected and extract at their original size.

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

HMSTHModdingTool> -x3d BOY_00000.rdtb BOY_00001.gdtb BOY

HMSTHModdingTool> -c3d BOY_obj BOY_NEW

HMSTHModdingTool> fixiso HMSTH_MODDED.iso

HMSTHModdingTool> fixps2logo HMSTH.bin

HMSTHModdingTool> fakeyear 2001 HMSTH.iso

HMSTHModdingTool> cmusic mysong.vag





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
