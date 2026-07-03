using System;
using System.Collections.Generic;
using System.IO;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    ///     Handles automated LBA table patching in SLUS_202.51 inside a HMSTH ISO.
    ///     Reads each file's real LBA from the ISO filesystem and writes the
    ///     new LBA table into SLUS_202.51 at offset 0x162460 - 0x162D30 in-place.
    /// </summary>
    public class HarvestIso
    {
        // SLUS_202.51 LBA table location
        private const uint SLUS_LBA_TABLE_START = 0x162460;
        private const uint SLUS_LBA_TABLE_END = 0x162D30;
        private const int SLUS_LBA_TABLE_SIZE = (int)(SLUS_LBA_TABLE_END - SLUS_LBA_TABLE_START); // 2256
        private const int BYTES_PER_SECTOR = 2048;

        // ISO 9660
        private const int LOGICAL_SECTOR_SIZE = 2048;
        private const int ISO_PVD_LBA = 16;
        private const int ISO_ROOT_DIR_OFFSET = 156;

        // Original HMSTH file order for the LBA table
        // Entry 0 = ISO system area (LBA 0 to first_file_LBA - 1)
        // Entries 1..281 = these files in exact order
        public static readonly string[] HmsthFileOrder = new string[]
        {
            @"\IOP\IOPRP22.IMG", @"\IOP\LIBSD.IRX", @"\IOP\MCMAN.IRX",
            @"\IOP\MCSERV.IRX", @"\IOP\MODHSYN.IRX", @"\IOP\MODMIDI.IRX",
            @"\IOP\PADMAN.IRX", @"\IOP\SDRDRV.IRX", @"\IOP\SIO2MAN.IRX",
            @"\IOP\SOUNDDRV.IRX",
            @"\MSG\EVMSG.HDA", @"\MSG\HS_MSG.HDA", @"\MSG\JOB_MSG.HDA",
            @"\EVENT\EVTMSG00.HDA", @"\EVENT\EVTMSG01.HDA", @"\EVENT\EVTMSG02.HDA",
            @"\EVENT\EVTMSG03.HDA", @"\EVENT\EVTMSG04.HDA", @"\EVENT\EVTMSG05.HDA",
            @"\EVENT\EVTMSG06.HDA", @"\EVENT\EVTMSG07.HDA", @"\EVENT\EVTMSG08.HDA",
            @"\EVENT\EVTMSG09.HDA", @"\EVENT\EVTMSG10.HDA", @"\EVENT\EVTMSG11.HDA",
            @"\EVENT\EVTMSG12.HDA", @"\EVENT\EVTMSG13.HDA", @"\EVENT\EVTMSG14.HDA",
            @"\CGDATA\OUTSIDE\NMEN.HDA", @"\CGDATA\OUTSIDE\RACE.HDA",
            @"\CGDATA\OUTSIDE\ROLL.HDA", @"\CGDATA\OUTSIDE\ROOM.HDA",
            @"\CGDATA\OUTSIDE\RTRANS.HDA", @"\CGDATA\OUTSIDE\START.HDA",
            @"\CGDATA\OUTSIDE\STRANS.HDA", @"\CGDATA\OUTSIDE\WHI.HDA",
            @"\CGDATA\OUTSIDE\PROF\PR_BASIL.HDA", @"\CGDATA\OUTSIDE\PROF\PR_CAZIN.HDA",
            @"\CGDATA\OUTSIDE\PROF\PR_DAVID.HDA", @"\CGDATA\OUTSIDE\PROF\PR_DEERE.HDA",
            @"\CGDATA\OUTSIDE\PROF\PR_EBONY.HDA", @"\CGDATA\OUTSIDE\PROF\PR_FLAT.HDA",
            @"\CGDATA\OUTSIDE\PROF\PR_GINA.HDA", @"\CGDATA\OUTSIDE\PROF\PR_HAYAT.HDA",
            @"\CGDATA\OUTSIDE\PROF\PR_KETIE.HDA", @"\CGDATA\OUTSIDE\PROF\PR_LYRA.HDA",
            @"\CGDATA\OUTSIDE\PROF\PR_MARIN.HDA", @"\CGDATA\OUTSIDE\PROF\PR_MARTH.HDA",
            @"\CGDATA\OUTSIDE\PROF\PR_PLY.HDA", @"\CGDATA\OUTSIDE\PROF\PR_RONAL.HDA",
            @"\CGDATA\OUTSIDE\PROF\PR_RUHN.HDA", @"\CGDATA\OUTSIDE\PROF\PR_SARAH.HDA",
            @"\CGDATA\OUTSIDE\PROF\PR_SHIN.HDA", @"\CGDATA\OUTSIDE\PROF\PR_TIM.HDA",
            @"\CGDATA\OUTSIDE\PROF\PR_WALL.HDA", @"\CGDATA\OUTSIDE\PROF\PR_WOOD.HDA",
            @"\CGDATA\MAP\WOODS\W_MONUME\MNT_FDA.HDA", @"\CGDATA\MAP\WOODS\W_MONUME\MNT_FDS.HDA",
            @"\CGDATA\MAP\WOODS\W_MONUME\MNT_FDW.HDA", @"\CGDATA\MAP\WOODS\W_MONUME\MNT_FLD.HDA",
            @"\CGDATA\MAP\WOODS\W_MONUME\MNT_MAP.HDA", @"\CGDATA\MAP\WOODS\W_MONUME\MNT_MPA.HDA",
            @"\CGDATA\MAP\WOODS\W_MONUME\MNT_MPS.HDA", @"\CGDATA\MAP\WOODS\W_MONUME\MNT_MPW.HDA",
            @"\CGDATA\MAP\WOODS\W_MONUME\MNT_PAT.HDA",
            @"\CGDATA\MAP\WOODS\W_LAKE\LAKE_FDA.HDA", @"\CGDATA\MAP\WOODS\W_LAKE\LAKE_FDS.HDA",
            @"\CGDATA\MAP\WOODS\W_LAKE\LAKE_FDW.HDA", @"\CGDATA\MAP\WOODS\W_LAKE\LAKE_FLD.HDA",
            @"\CGDATA\MAP\WOODS\W_LAKE\LAKE_MAP.HDA", @"\CGDATA\MAP\WOODS\W_LAKE\LAKE_MPA.HDA",
            @"\CGDATA\MAP\WOODS\W_LAKE\LAKE_MPS.HDA", @"\CGDATA\MAP\WOODS\W_LAKE\LAKE_MPW.HDA",
            @"\CGDATA\MAP\WOODS\W_LAKE\LAKE_WAT.HDA",
            @"\CGDATA\MAP\WOODS\W_LAKE\TCI.HDA", @"\CGDATA\MAP\WOODS\W_LAKE\TCIJ.HDA",
            @"\CGDATA\MAP\WOODS\W_LAKE\THI.HDA", @"\CGDATA\MAP\WOODS\W_LAKE\THIJ.HDA",
            @"\CGDATA\MAP\WOODS\W_GODDES\GDS_FDA.HDA", @"\CGDATA\MAP\WOODS\W_GODDES\GDS_FDS.HDA",
            @"\CGDATA\MAP\WOODS\W_GODDES\GDS_FDW.HDA", @"\CGDATA\MAP\WOODS\W_GODDES\GDS_FLD.HDA",
            @"\CGDATA\MAP\WOODS\W_GODDES\GDS_MAP.HDA", @"\CGDATA\MAP\WOODS\W_GODDES\GDS_MPA.HDA",
            @"\CGDATA\MAP\WOODS\W_GODDES\GDS_MPS.HDA", @"\CGDATA\MAP\WOODS\W_GODDES\GDS_MPW.HDA",
            @"\CGDATA\MAP\WOODS\W_CRAFT\CFT_FDA.HDA", @"\CGDATA\MAP\WOODS\W_CRAFT\CFT_FDS.HDA",
            @"\CGDATA\MAP\WOODS\W_CRAFT\CFT_FDW.HDA", @"\CGDATA\MAP\WOODS\W_CRAFT\CFT_FLD.HDA",
            @"\CGDATA\MAP\WOODS\W_CRAFT\CFT_MAP.HDA", @"\CGDATA\MAP\WOODS\W_CRAFT\CFT_MPA.HDA",
            @"\CGDATA\MAP\WOODS\W_CRAFT\CFT_MPS.HDA", @"\CGDATA\MAP\WOODS\W_CRAFT\CFT_MPW.HDA",
            @"\CGDATA\MAP\WOODS\W_CRAFT\SHI.HDA", @"\CGDATA\MAP\WOODS\W_CRAFT\SHIJ.HDA",
            @"\CGDATA\MAP\WOODS\W_CRAFT\SKI.HDA", @"\CGDATA\MAP\WOODS\W_CRAFT\SKIJ.HDA",
            @"\CGDATA\MAP\WOODS\W_COTTAG\CTG_FDA.HDA", @"\CGDATA\MAP\WOODS\W_COTTAG\CTG_FDS.HDA",
            @"\CGDATA\MAP\WOODS\W_COTTAG\CTG_FDW.HDA", @"\CGDATA\MAP\WOODS\W_COTTAG\CTG_FLD.HDA",
            @"\CGDATA\MAP\WOODS\W_COTTAG\CTG_MAP.HDA", @"\CGDATA\MAP\WOODS\W_COTTAG\CTG_MPA.HDA",
            @"\CGDATA\MAP\WOODS\W_COTTAG\CTG_MPS.HDA", @"\CGDATA\MAP\WOODS\W_COTTAG\CTG_MPW.HDA",
            @"\CGDATA\MAP\WOODS\W_COTTAG\VHI.HDA", @"\CGDATA\MAP\WOODS\W_COTTAG\VHIJ.HDA",
            @"\CGDATA\MAP\SKY\CLOUD.HDA", @"\CGDATA\MAP\SKY\FINE.HDA",
            @"\CGDATA\MAP\SKY\FINE_S.HDA", @"\CGDATA\MAP\SKY\FINE_W.HDA",
            @"\CGDATA\MAP\FARM\FRM_FDA.HDA", @"\CGDATA\MAP\FARM\FRM_FDS.HDA",
            @"\CGDATA\MAP\FARM\FRM_FDW.HDA", @"\CGDATA\MAP\FARM\FRM_FDZA.HDA",
            @"\CGDATA\MAP\FARM\FRM_FDZS.HDA", @"\CGDATA\MAP\FARM\FRM_FDZW.HDA",
            @"\CGDATA\MAP\FARM\FRM_FLD.HDA", @"\CGDATA\MAP\FARM\FRM_FLDZ.HDA",
            @"\CGDATA\MAP\FARM\FRM_KS.HDA", @"\CGDATA\MAP\FARM\FRM_KSA.HDA",
            @"\CGDATA\MAP\FARM\FRM_KSS.HDA", @"\CGDATA\MAP\FARM\FRM_KSW.HDA",
            @"\CGDATA\MAP\FARM\FRM_MAP.HDA", @"\CGDATA\MAP\FARM\FRM_MAPZ.HDA",
            @"\CGDATA\MAP\FARM\FRM_MPA.HDA", @"\CGDATA\MAP\FARM\FRM_MPS.HDA",
            @"\CGDATA\MAP\FARM\FRM_MPW.HDA", @"\CGDATA\MAP\FARM\FRM_MPZA.HDA",
            @"\CGDATA\MAP\FARM\FRM_MPZS.HDA", @"\CGDATA\MAP\FARM\FRM_MPZW.HDA",
            @"\CGDATA\MAP\FARM\HGI.HDA", @"\CGDATA\MAP\FARM\HGIJ.HDA",
            @"\CGDATA\MAP\FARM\HHI.HDA", @"\CGDATA\MAP\FARM\HHIJ.HDA",
            @"\CGDATA\MAP\FARM\HTI.HDA", @"\CGDATA\MAP\FARM\HTIJ.HDA",
            @"\CGDATA\MAP\FARM\HZI.HDA", @"\CGDATA\MAP\FARM\HZIJ.HDA",
            @"\CGDATA\MAP\COLONY\COLONY2\COL2_FDA.HDA", @"\CGDATA\MAP\COLONY\COLONY2\COL2_FDS.HDA",
            @"\CGDATA\MAP\COLONY\COLONY2\COL2_FDW.HDA", @"\CGDATA\MAP\COLONY\COLONY2\COL2_FLD.HDA",
            @"\CGDATA\MAP\COLONY\COLONY2\COL2_MAP.HDA", @"\CGDATA\MAP\COLONY\COLONY2\COL2_MPA.HDA",
            @"\CGDATA\MAP\COLONY\COLONY2\COL2_MPS.HDA", @"\CGDATA\MAP\COLONY\COLONY2\COL2_MPW.HDA",
            @"\CGDATA\MAP\COLONY\COLONY2\FHI.HDA", @"\CGDATA\MAP\COLONY\COLONY2\FHIJ.HDA",
            @"\CGDATA\MAP\COLONY\COLONY2\TLI.HDA", @"\CGDATA\MAP\COLONY\COLONY2\TLIJ.HDA",
            @"\CGDATA\MAP\COLONY\COLONY2\TSI.HDA", @"\CGDATA\MAP\COLONY\COLONY2\TSIJ.HDA",
            @"\CGDATA\MAP\COLONY\COLONY1\COL1_FDA.HDA", @"\CGDATA\MAP\COLONY\COLONY1\COL1_FDS.HDA",
            @"\CGDATA\MAP\COLONY\COLONY1\COL1_FDW.HDA", @"\CGDATA\MAP\COLONY\COLONY1\COL1_FLD.HDA",
            @"\CGDATA\MAP\COLONY\COLONY1\COL1_MAP.HDA", @"\CGDATA\MAP\COLONY\COLONY1\COL1_MPA.HDA",
            @"\CGDATA\MAP\COLONY\COLONY1\COL1_MPS.HDA", @"\CGDATA\MAP\COLONY\COLONY1\COL1_MPW.HDA",
            @"\CGDATA\MAP\COLONY\COLONY1\FSI.HDA", @"\CGDATA\MAP\COLONY\COLONY1\FSIJ.HDA",
            @"\CGDATA\MAP\COLONY\COLONY1\FTI.HDA", @"\CGDATA\MAP\COLONY\COLONY1\FTIJ.HDA",
            @"\CGDATA\MAP\CAVE\HCI.HDA", @"\CGDATA\MAP\CAVE\HCIJ.HDA",
            @"\CGDATA\MAP\B_FARM\GRASS\BGLS_B.HDA", @"\CGDATA\MAP\B_FARM\GRASS\BGLS_BA.HDA",
            @"\CGDATA\MAP\B_FARM\GRASS\BGLS_BS.HDA", @"\CGDATA\MAP\B_FARM\GRASS\BGLS_BSA.HDA",
            @"\CGDATA\MAP\B_FARM\GRASS\BGLS_BSS.HDA", @"\CGDATA\MAP\B_FARM\GRASS\BGLS_BSU.HDA",
            @"\CGDATA\MAP\B_FARM\GRASS\BGLS_BSW.HDA", @"\CGDATA\MAP\B_FARM\GRASS\BGLS_BW.HDA",
            @"\CGDATA\MAP\B_FARM\GRASS\BGLS_FDA.HDA", @"\CGDATA\MAP\B_FARM\GRASS\BGLS_FDS.HDA",
            @"\CGDATA\MAP\B_FARM\GRASS\BGLS_FDW.HDA", @"\CGDATA\MAP\B_FARM\GRASS\BGLS_FLD.HDA",
            @"\CGDATA\MAP\B_FARM\GRASS\BGLS_MAP.HDA", @"\CGDATA\MAP\B_FARM\GRASS\BGLS_MPA.HDA",
            @"\CGDATA\MAP\B_FARM\GRASS\BGLS_MPS.HDA", @"\CGDATA\MAP\B_FARM\GRASS\BGLS_MPW.HDA",
            @"\CGDATA\MAP\B_FARM\GRASS\GGI.HDA", @"\CGDATA\MAP\B_FARM\GRASS\GGIJ.HDA",
            @"\CGDATA\MAP\B_FARM\GARDEN\BHI.HDA", @"\CGDATA\MAP\B_FARM\GARDEN\BHIJ.HDA",
            @"\CGDATA\MAP\B_FARM\GARDEN\BRG_FDA.HDA", @"\CGDATA\MAP\B_FARM\GARDEN\BRG_FDS.HDA",
            @"\CGDATA\MAP\B_FARM\GARDEN\BRG_FDW.HDA", @"\CGDATA\MAP\B_FARM\GARDEN\BRG_FLD.HDA",
            @"\CGDATA\MAP\B_FARM\GARDEN\BRG_MAP.HDA", @"\CGDATA\MAP\B_FARM\GARDEN\BRG_MPA.HDA",
            @"\CGDATA\MAP\B_FARM\GARDEN\BRG_MPS.HDA", @"\CGDATA\MAP\B_FARM\GARDEN\BRG_MPW.HDA",
            @"\CGDATA\MAP\B_FARM\GARDEN\BSI.HDA", @"\CGDATA\MAP\B_FARM\GARDEN\BSIJ.HDA",
            @"\CGDATA\ITEM\ITEMCAFE.HDA", @"\CGDATA\ITEM\ITEMCROP.HDA",
            @"\CGDATA\ITEM\ITEMOTHE.HDA", @"\CGDATA\ITEM\ITEMTEX.HDA",
            @"\CGDATA\ITEM\MAPCROP.HDA", @"\CGDATA\ICON\ICON.HDA",
            @"\CGDATA\COMMON\CALENDAR.HDA", @"\CGDATA\COMMON\COMMON.HDA",
            @"\CGDATA\COMMON\EFFECT.HDA", @"\CGDATA\COMMON\FONT.HDA",
            @"\CGDATA\COMMON\ITEMWIN.HDA", @"\CGDATA\COMMON\SHADOW.HDA",
            @"\CGDATA\COMMON\SHOP.HDA", @"\CGDATA\COMMON\TOOLNOTE.HDA",
            @"\CGDATA\CHARA\BASIL.HDA", @"\CGDATA\CHARA\BOY.HDA",
            @"\CGDATA\CHARA\DAVID.HDA", @"\CGDATA\CHARA\DEERE.HDA",
            @"\CGDATA\CHARA\DEERE_GD.HDA", @"\CGDATA\CHARA\DEERE_SI.HDA",
            @"\CGDATA\CHARA\EBONY.HDA", @"\CGDATA\CHARA\FLAT.HDA",
            @"\CGDATA\CHARA\GINA.HDA", @"\CGDATA\CHARA\GINA_SIT.HDA",
            @"\CGDATA\CHARA\HAYATO.HDA", @"\CGDATA\CHARA\KAZIN.HDA",
            @"\CGDATA\CHARA\KETIE.HDA", @"\CGDATA\CHARA\KETIE_SI.HDA",
            @"\CGDATA\CHARA\LYRA.HDA", @"\CGDATA\CHARA\LYRA_SIT.HDA",
            @"\CGDATA\CHARA\MARINA.HDA", @"\CGDATA\CHARA\MARTHA.HDA",
            @"\CGDATA\CHARA\RONALD.HDA", @"\CGDATA\CHARA\RUHN.HDA",
            @"\CGDATA\CHARA\SARAH.HDA", @"\CGDATA\CHARA\SHIN.HDA",
            @"\CGDATA\CHARA\TIM.HDA", @"\CGDATA\CHARA\WALL.HDA",
            @"\CGDATA\CHARA\WOOD.HDA",
            @"\CGDATA\CHARA\ANIMALS\ANML00.HDA", @"\CGDATA\CHARA\ANIMALS\ANML01.HDA",
            @"\CGDATA\CHARA\ANIMALS\CHICKEN.HDA", @"\CGDATA\CHARA\ANIMALS\CHICKENB.HDA",
            @"\CGDATA\CHARA\ANIMALS\COW.HDA", @"\CGDATA\CHARA\ANIMALS\DOG_N.HDA",
            @"\CGDATA\CHARA\ANIMALS\DOG_S.HDA",
            @"\CGDATA\CHARA\ANIMALS\HORSE_BI.HDA", @"\CGDATA\CHARA\ANIMALS\HORSE_BL.HDA",
            @"\CGDATA\CHARA\ANIMALS\HORSE_BR.HDA", @"\CGDATA\CHARA\ANIMALS\HORSE_GR.HDA",
            @"\CGDATA\CHARA\ANIMALS\HORSE_WH.HDA",
            @"\SOUND\BGM_BAR.HDA", @"\SOUND\BGM_BD.HDA", @"\SOUND\BGM_BF.HDA",
            @"\SOUND\BGM_BKL.HDA", @"\SOUND\BGM_EA.HDA", @"\SOUND\BGM_EV.HDA",
            @"\SOUND\BGM_FRM.HDA", @"\SOUND\BGM_FRMA.HDA", @"\SOUND\BGM_FRMS.HDA",
            @"\SOUND\BGM_FRMW.HDA", @"\SOUND\BGM_GD.HDA", @"\SOUND\BGM_GDS.HDA",
            @"\SOUND\BGM_HP.HDA", @"\SOUND\BGM_LV.HDA", @"\SOUND\BGM_NT.HDA",
            @"\SOUND\BGM_NTA.HDA", @"\SOUND\BGM_NTS.HDA", @"\SOUND\BGM_NTW.HDA",
            @"\SOUND\BGM_OP.HDA", @"\SOUND\BGM_RC.HDA", @"\SOUND\BGM_RN.HDA",
            @"\SOUND\BGM_SD.HDA", @"\SOUND\BGM_ST.HDA", @"\SOUND\BGM_TTL.HDA",
            @"\SOUND\BGM_WD.HDA", @"\SOUND\BGM_WDA.HDA", @"\SOUND\BGM_WDS.HDA",
            @"\SOUND\BGM_WDW.HDA", @"\SOUND\BGM_WND.HDA", @"\SOUND\SE.HDA",
            @"\SYSTEM.CNF", @"\SLUS_202.51",
        };

        private class IsoEntry
        {
            public uint Lba;
            public uint Size;
        }

        /// <summary>
        ///     Auto-fixes the LBA table inside SLUS_202.51 which is inside the ISO.
        ///     Reads each file's real LBA from the ISO and writes the new table
        ///     into SLUS_202.51 at offset 0x162460 - 0x162D30 (only 2256 bytes).
        ///     Nothing else is modified.
        /// </summary>
        /// <param name="isoPath">Path to the HMSTH ISO file</param>
        /// <returns>Number of LBA entries changed</returns>
        public static int FixLba(string isoPath)
        {
            if (!File.Exists(isoPath))
                throw new FileNotFoundException("ISO file not found", isoPath);

            TextOut.Print($"Opening ISO: {isoPath}");

            // 1. Detect ISO sector format
            int rawSectorSize;
            int userDataOffset;
            DetectIsoFormat(isoPath, out rawSectorSize, out userDataOffset);
            TextOut.Print($"ISO format: raw_sector={rawSectorSize}, user_data_offset={userDataOffset}");

            // 2. Scan ISO filesystem
            var files = ScanIso(isoPath, rawSectorSize, userDataOffset);
            TextOut.Print($"Found {files.Count} files in ISO");

            // 3. Find SLUS_202.51
            string slusKey = @"\SLUS_202.51";
            if (!files.ContainsKey(slusKey.ToUpper()))
                throw new Exception("SLUS_202.51 not found in ISO");

            var slus = files[slusKey.ToUpper()];
            TextOut.Print($"SLUS_202.51 at LBA {slus.Lba}, size {slus.Size} bytes");

            // 4. Get first game file LBA (for entry 0 = system area)
            string firstKey = HmsthFileOrder[0].ToUpper();
            if (!files.ContainsKey(firstKey))
                throw new Exception($"First game file {HmsthFileOrder[0]} not in ISO");
            uint firstLba = files[firstKey].Lba;

            // 5. Build new 2256-byte LBA table
            byte[] newTable = new byte[SLUS_LBA_TABLE_SIZE];
            int pos = 0;

            // Entry 0 = system area
            WriteUInt32Le(newTable, pos, 0);
            WriteUInt32Le(newTable, pos + 4, firstLba - 1);
            pos += 8;

            int missing = 0;
            List<string> missingFiles = new List<string>();

            // Entries 1..N = files in HMSTH order
            foreach (string fname in HmsthFileOrder)
            {
                if (pos + 8 > SLUS_LBA_TABLE_SIZE) break;

                string key = fname.ToUpper();
                if (!files.ContainsKey(key))
                {
                    missing++;
                    missingFiles.Add(fname);
                    WriteUInt32Le(newTable, pos, 0);
                    WriteUInt32Le(newTable, pos + 4, 0);
                    pos += 8;
                    continue;
                }

                var entry = files[key];
                uint sectors = (entry.Size + BYTES_PER_SECTOR - 1) / BYTES_PER_SECTOR;
                if (sectors < 1) sectors = 1;
                uint lbaEnd = entry.Lba + sectors - 1;

                WriteUInt32Le(newTable, pos, entry.Lba);
                WriteUInt32Le(newTable, pos + 4, lbaEnd);
                pos += 8;
            }

            if (missing > 0)
            {
                TextOut.PrintWarning($"{missing} files missing from ISO:");
                foreach (var m in missingFiles)
                    TextOut.Print("  - " + m);
            }

            // 6. Read existing table for diff count
            byte[] oldTable = ReadBytesAtLba(isoPath, rawSectorSize, userDataOffset,
                                             slus.Lba, SLUS_LBA_TABLE_START, SLUS_LBA_TABLE_SIZE);

            int diffCount = 0;
            for (int i = 0; i < SLUS_LBA_TABLE_SIZE; i += 8)
            {
                uint oldS = ReadUInt32Le(oldTable, i);
                uint oldE = ReadUInt32Le(oldTable, i + 4);
                uint newS = ReadUInt32Le(newTable, i);
                uint newE = ReadUInt32Le(newTable, i + 4);
                if (oldS != newS || oldE != newE) diffCount++;
            }

            if (diffCount == 0)
            {
                TextOut.PrintSuccess("LBA table already correct - no changes needed");
                return 0;
            }

            TextOut.Print($"Writing {diffCount} changed LBA entries to SLUS_202.51 at offset 0x{SLUS_LBA_TABLE_START:X}");

            // 7. Write new table into SLUS at offset 0x162460 inside the ISO
            WriteBytesAtLba(isoPath, rawSectorSize, userDataOffset,
                            slus.Lba, SLUS_LBA_TABLE_START, newTable);

            // 8. Verify
            byte[] verify = ReadBytesAtLba(isoPath, rawSectorSize, userDataOffset,
                                            slus.Lba, SLUS_LBA_TABLE_START, SLUS_LBA_TABLE_SIZE);
            for (int i = 0; i < SLUS_LBA_TABLE_SIZE; i++)
            {
                if (verify[i] != newTable[i])
                    throw new Exception("Verification failed - write did not persist");
            }

            TextOut.PrintSuccess($"LBA table patched successfully - {diffCount} entries updated");
            return diffCount;
        }

        // ========================================================
        // ISO 9660 helpers
        // ========================================================

        private static void DetectIsoFormat(string path, out int rawSectorSize, out int userDataOffset)
        {
            long fsize = new FileInfo(path).Length;
            var candidates = new (int raw, int off)[]
            {
                (2048, 0), (2352, 16), (2352, 24), (2336, 8), (2448, 16)
            };

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                foreach (var c in candidates)
                {
                    if (fsize < 17L * c.raw) continue;
                    fs.Seek(16L * c.raw + c.off, SeekOrigin.Begin);
                    byte[] m = new byte[6];
                    fs.Read(m, 0, 6);
                    if (m[0] == 0x01 && m[1] == 'C' && m[2] == 'D' && m[3] == '0' && m[4] == '0' && m[5] == '1')
                    {
                        rawSectorSize = c.raw;
                        userDataOffset = c.off;
                        return;
                    }
                }
            }
            throw new Exception("Not a valid ISO 9660 image");
        }

        private static long LbaToRaw(uint lba, int rawSectorSize, int userDataOffset, uint byteOff = 0)
        {
            return (long)lba * rawSectorSize + userDataOffset + byteOff;
        }

        private static byte[] ReadBytesAtLba(string path, int raw, int uoff, uint lba, uint byteOff, int size)
        {
            byte[] result = new byte[size];
            int remaining = size;
            int resultPos = 0;
            uint curLba = lba + byteOff / LOGICAL_SECTOR_SIZE;
            uint curOff = byteOff % LOGICAL_SECTOR_SIZE;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                while (remaining > 0)
                {
                    int avail = LOGICAL_SECTOR_SIZE - (int)curOff;
                    int toRead = Math.Min(remaining, avail);
                    fs.Seek(LbaToRaw(curLba, raw, uoff, curOff), SeekOrigin.Begin);
                    fs.Read(result, resultPos, toRead);
                    remaining -= toRead;
                    resultPos += toRead;
                    curLba++;
                    curOff = 0;
                }
            }
            return result;
        }

        private static void WriteBytesAtLba(string path, int raw, int uoff, uint lba, uint byteOff, byte[] data)
        {
            int remaining = data.Length;
            int dataPos = 0;
            uint curLba = lba + byteOff / LOGICAL_SECTOR_SIZE;
            uint curOff = byteOff % LOGICAL_SECTOR_SIZE;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read))
            {
                while (remaining > 0)
                {
                    int avail = LOGICAL_SECTOR_SIZE - (int)curOff;
                    int toWrite = Math.Min(remaining, avail);
                    fs.Seek(LbaToRaw(curLba, raw, uoff, curOff), SeekOrigin.Begin);
                    fs.Write(data, dataPos, toWrite);
                    remaining -= toWrite;
                    dataPos += toWrite;
                    curLba++;
                    curOff = 0;
                }
            }
        }

        private static Dictionary<string, IsoEntry> ScanIso(string path, int raw, int uoff)
        {
            var files = new Dictionary<string, IsoEntry>();

            byte[] pvd = ReadBytesAtLba(path, raw, uoff, ISO_PVD_LBA, 0, LOGICAL_SECTOR_SIZE);
            if (pvd[0] != 0x01 || pvd[1] != 'C' || pvd[2] != 'D' || pvd[3] != '0' || pvd[4] != '0' || pvd[5] != '1')
                throw new Exception("PVD not found");

            uint rootLba = ReadUInt32Le(pvd, ISO_ROOT_DIR_OFFSET + 2);
            uint rootSize = ReadUInt32Le(pvd, ISO_ROOT_DIR_OFFSET + 10);

            ParseDirectory(path, raw, uoff, rootLba, rootSize, "", files, 0);
            return files;
        }

        private static void ParseDirectory(string path, int raw, int uoff,
                                            uint dirLba, uint dirSize,
                                            string curPath,
                                            Dictionary<string, IsoEntry> outFiles,
                                            int depth)
        {
            if (depth > 20) return;

            byte[] dirData = ReadBytesAtLba(path, raw, uoff, dirLba, 0, (int)dirSize);
            int pos = 0;

            while (pos < dirData.Length)
            {
                int rlen = dirData[pos];
                if (rlen == 0)
                {
                    int nextSector = ((pos / LOGICAL_SECTOR_SIZE) + 1) * LOGICAL_SECTOR_SIZE;
                    if (nextSector >= dirData.Length) break;
                    pos = nextSector;
                    continue;
                }
                if (pos + rlen > dirData.Length) break;

                uint eLba = ReadUInt32Le(dirData, pos + 2);
                uint eSize = ReadUInt32Le(dirData, pos + 10);
                byte flags = dirData[pos + 25];
                int nlen = dirData[pos + 32];

                bool isDir = (flags & 0x02) != 0;
                bool isDot = (nlen == 1 && (dirData[pos + 33] == 0x00 || dirData[pos + 33] == 0x01));

                if (!isDot && nlen > 0)
                {
                    string nstr = System.Text.Encoding.ASCII.GetString(dirData, pos + 33, nlen);
                    int semi = nstr.IndexOf(';');
                    if (semi >= 0) nstr = nstr.Substring(0, semi);
                    string full = curPath + @"\" + nstr;

                    if (isDir)
                    {
                        ParseDirectory(path, raw, uoff, eLba, eSize, full, outFiles, depth + 1);
                    }
                    else
                    {
                        outFiles[full.ToUpper()] = new IsoEntry { Lba = eLba, Size = eSize };
                    }
                }
                pos += rlen;
            }
        }

        private static uint ReadUInt32Le(byte[] data, int offset)
        {
            return (uint)(data[offset] |
                          (data[offset + 1] << 8) |
                          (data[offset + 2] << 16) |
                          (data[offset + 3] << 24));
        }

        private static void WriteUInt32Le(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
