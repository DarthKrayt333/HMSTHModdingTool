using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace HMSTHModdingTool.BoyMods
{
    // ═══════════════════════════════════════
    // UNIVERSAL BONE SCALER v3.5
    //
    // Works identical to BoyScaler.
    // Reads current file directly.
    // Backup saved on first run only.
    //
    // Bone layout (same as BoyScaler):
    //   off+0  = X   (--bNx)
    //   off+4  = Y   (--bNy)
    //   off+8  = Z   (--bNz)
    //
    // HMSTHModdingTool by DarthKrayt333
    // ═══════════════════════════════════════
    public static class UniversalBoneScaler
    {
        private const int BONE_REC_SIZE = 16;

        // ═══════════════════════════════════
        // ENTRY POINT
        // ═══════════════════════════════════
        public static void Run(string[] args)
        {
            if (args.Length < 2)
            { PrintHelp(); return; }

            string filepath = null;
            var boneCfg =
                new Dictionary<int, float[]>();
            var groupScales =
                new Dictionary<string,
                    float[]>();
            bool showInfo = false;
            bool doRestore = false;

            var boneRe = new Regex(
                @"^-{0,2}b(\d+)(x|y|z)?$",
                RegexOptions.IgnoreCase);

            int i = 1;
            while (i < args.Length)
            {
                string a = args[i];

                if (EqCI(a, "--info") ||
                    EqCI(a, "-info") ||
                    EqCI(a, "info"))
                { showInfo = true; i++; continue; }

                if (EqCI(a, "--help") ||
                    EqCI(a, "-help") ||
                    EqCI(a, "help"))
                { PrintHelp(); return; }

                if (EqCI(a, "--restore") ||
                    EqCI(a, "-restore") ||
                    EqCI(a, "restore"))
                { doRestore = true; i++; continue; }

                bool hasVal =
                    i + 1 < args.Length;
                float val = 0f;
                bool isNum = false;
                if (hasVal)
                    isNum = float.TryParse(
                        args[i + 1],
                        System.Globalization
                            .NumberStyles.Float,
                        System.Globalization
                            .CultureInfo
                            .InvariantCulture,
                        out val);

                // bone: b5 b5y --b5y
                var bm = boneRe.Match(a);
                if (bm.Success && isNum)
                {
                    int bi = int.Parse(
                        bm.Groups[1].Value);
                    string ax =
                        bm.Groups[2].Success
                        ? bm.Groups[2].Value
                            .ToLower()
                        : null;
                    if (!boneCfg.ContainsKey(bi))
                        boneCfg[bi] =
                            new float[]
                            {1f,1f,1f};
                    ApplyAxis(
                        boneCfg[bi], ax, val);
                    i += 2; continue;
                }

                // group: --legsy legsy
                if (isNum)
                {
                    string raw =
                        a.TrimStart('-')
                         .ToLower();
                    string axG = null;
                    string baseG = raw;

                    if (raw.Length > 1)
                    {
                        char last =
                            raw[raw.Length - 1];
                        if (last == 'x' ||
                            last == 'y' ||
                            last == 'z')
                        {
                            string c =
                                raw.Substring(
                                    0,
                                    raw.Length - 1);
                            if (IsGrp(c))
                            {
                                axG =
                                    last.ToString();
                                baseG = c;
                            }
                        }
                    }

                    if (IsGrp(baseG))
                    {
                        if (!groupScales
                                .ContainsKey(
                                    baseG))
                            groupScales[baseG] =
                                new float[]
                                {1f,1f,1f};
                        ApplyAxis(
                            groupScales[baseG],
                            axG, val);
                        i += 2; continue;
                    }
                }

                // filepath
                if (File.Exists(a) ||
                    a.Contains(".") ||
                    (!a.StartsWith("-") &&
                     !boneRe.IsMatch(a) &&
                     !IsGrpArg(a)))
                {
                    filepath = a;
                    i++; continue;
                }

                if (a.StartsWith("-") &&
                    !boneRe.IsMatch(a))
                {
                    Err("Unknown option: " + a);
                    return;
                }

                filepath = a;
                i++;
            }

            if (filepath == null)
            {
                Err("No file specified");
                PrintHelp();
                return;
            }
            if (!File.Exists(filepath))
            {
                Err("Not found: " + filepath);
                return;
            }

            if (doRestore)
            {
                DoRestore(filepath);
                return;
            }

            DoScale(filepath, boneCfg,
                groupScales, showInfo);
        }

        // ═══════════════════════════════════
        // RESTORE FROM BACKUP
        // ═══════════════════════════════════
        private static void DoRestore(
            string filepath)
        {
            string bk = filepath + ".original";
            if (!File.Exists(bk))
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  No backup found: " +
                    Path.GetFileName(bk));
                Console.ResetColor();
                return;
            }
            File.Copy(bk, filepath, true);
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "  Restored: " +
                Path.GetFileName(filepath) +
                " from .original");
            Console.ResetColor();
        }

        private static bool EqCI(
            string a, string b) =>
            a.Equals(b,
                StringComparison
                    .OrdinalIgnoreCase);

        private static void ApplyAxis(
            float[] arr, string ax, float v)
        {
            if (ax == null)
            { arr[0] = v; arr[1] = v; arr[2] = v; }
            else if (ax == "x") arr[0] = v;
            else if (ax == "y") arr[1] = v;
            else if (ax == "z") arr[2] = v;
        }

        private static bool IsGrpArg(string a)
        {
            string raw =
                a.TrimStart('-').ToLower();
            if (IsGrp(raw)) return true;
            if (raw.Length > 1)
            {
                char last = raw[raw.Length - 1];
                if (last == 'x' || last == 'y'
                    || last == 'z')
                    return IsGrp(
                        raw.Substring(
                            0, raw.Length - 1));
            }
            return false;
        }

        private static readonly
            HashSet<string> _grps =
            new HashSet<string>
        {
            "all",      "body",
            "spine",    "neck",
            "head",     "chest",
            "arms",     "larm",    "rarm",
            "shoulders","lshldr",  "rshldr",
            "hands",    "lhand",   "rhand",
            "fingers",  "lfing",   "rfing",
            "legs",     "lleg",    "rleg",
            "hips",     "lhip",    "rhip",
            "thighs",   "lthigh",  "rthigh",
            "shins",    "lshin",   "rshin",
            "ankles",   "lankle",  "rankle",
            "feet",     "lfoot",   "rfoot",
            "upper",    "lower",
        };

        private static bool IsGrp(string s)
            => _grps.Contains(s);

        // ═══════════════════════════════════
        // COUNT BONES (same as BoyScaler)
        // ═══════════════════════════════════
        private static int CountBones(
            byte[] data)
        {
            int count = 0, pos = 0;
            while (pos + 4 <= data.Length)
            {
                if (BitConverter.ToUInt32(
                        data, pos) == 0)
                    break;
                count++;
                pos += 4;
            }
            return count;
        }

        // ═══════════════════════════════════
        // GET BONE START (same as BoyScaler)
        // ═══════════════════════════════════
        private static int GetBoneStart(
            byte[] data, int boneCount)
        {
            int ptrEnd = boneCount * 4;
            int boneStart = ptrEnd + 4;
            if (boneStart +
                boneCount * BONE_REC_SIZE
                > data.Length)
                boneStart = ptrEnd;
            return boneStart;
        }

        private static float ReadF32(
            byte[] data, int off)
            => BitConverter.ToSingle(data, off);

        private static void WriteF32(
            byte[] data, int off, float v)
        {
            byte[] b = BitConverter.GetBytes(v);
            Array.Copy(b, 0, data, off, 4);
        }

        // ═══════════════════════════════════
        // RESOLVE GROUPS
        // ═══════════════════════════════════
        private static void ResolveGroups(
            Dictionary<string, float[]>
                groupScales,
            float[] sx, float[] sy, float[] sz,
            byte[] data,
            int boneStart,
            int boneCount)
        {
            if (groupScales.Count == 0) return;

            int n = boneCount;
            float[] lx = new float[n];
            float[] ly = new float[n];
            int[] par = new int[n];

            for (int b = 0; b < n; b++)
            {
                int off =
                    boneStart + b * BONE_REC_SIZE;
                if (off + BONE_REC_SIZE >
                    data.Length) break;
                lx[b] = ReadF32(data, off + 0);
                ly[b] = ReadF32(data, off + 4);
                byte pb = data[off + 15];
                int raw = pb & 0x7F;
                if (pb == 0xFF || raw == b || raw >= n)
                    par[b] = -1;
                else
                    par[b] = raw;
            }

            float[] wx = new float[n];
            float[] wy = new float[n];
            for (int b = 0; b < n; b++)
            {
                float ax2 = lx[b];
                float ay2 = ly[b];
                var vis =
                    new HashSet<int> { b };
                int p = par[b];
                while (p >= 0 && p < n)
                {
                    if (vis.Contains(p)) break;
                    vis.Add(p);
                    ax2 += lx[p];
                    ay2 += ly[p];
                    p = par[p];
                }
                wx[b] = ax2;
                wy[b] = ay2;
            }

            float minY = wy[0];
            float maxY = wy[0];
            for (int b = 1; b < n; b++)
            {
                if (wy[b] < minY) minY = wy[b];
                if (wy[b] > maxY) maxY = wy[b];
            }
            float h = Math.Max(maxY - minY, 1f);

            float maxAbsX = 0f;
            for (int b = 0; b < n; b++)
                if (Math.Abs(wx[b]) > maxAbsX)
                    maxAbsX = Math.Abs(wx[b]);
            float armT =
                Math.Max(8f, maxAbsX * 0.4f);

            float[] rel = new float[n];
            for (int b = 0; b < n; b++)
                rel[b] = (wy[b] - minY) / h;

            string[] side = new string[n];
            for (int b = 0; b < n; b++)
                side[b] =
                    wx[b] > 2f ? "R" :
                    wx[b] < -2f ? "L" : "";

            void ApplyGrp(
                float lo, float hi,
                string sf = null,
                bool armOnly = false,
                bool bodyOnly = false,
                float[] scales = null)
            {
                if (scales == null) return;
                bool doX =
                    Math.Abs(scales[0] - 1f)
                    > 0.001f;
                bool doY =
                    Math.Abs(scales[1] - 1f)
                    > 0.001f;
                bool doZ =
                    Math.Abs(scales[2] - 1f)
                    > 0.001f;
                if (!doX && !doY && !doZ) return;
                for (int b = 0; b < n; b++)
                {
                    float abx =
                        Math.Abs(wx[b]);
                    bool arm = abx >= armT;
                    if (armOnly && !arm)
                        continue;
                    if (bodyOnly && arm)
                        continue;
                    if (rel[b] < lo || rel[b] > hi)
                        continue;
                    if (sf != null &&
                        side[b] != sf &&
                        side[b] != "")
                        continue;
                    if (doX) sx[b] = scales[0];
                    if (doY) sy[b] = scales[1];
                    if (doZ) sz[b] = scales[2];
                }
            }

            float[] G(string k)
            {
                float[] v;
                return groupScales
                    .TryGetValue(k, out v)
                    ? v : null;
            }

            ApplyGrp(0.65f, 1.01f, null,
                false, true, G("spine"));
            ApplyGrp(0.65f, 1.01f, null,
                false, true, G("chest"));
            ApplyGrp(0.88f, 0.97f, null,
                false, true, G("neck"));
            ApplyGrp(0.97f, 1.01f, null,
                false, true, G("head"));
            ApplyGrp(0f, 1f, null,
                true, false, G("arms"));
            ApplyGrp(0f, 1f, "L",
                true, false, G("larm"));
            ApplyGrp(0f, 1f, "R",
                true, false, G("rarm"));
            ApplyGrp(0.60f, 1f, null,
                true, false, G("shoulders"));
            ApplyGrp(0.60f, 1f, "L",
                true, false, G("lshldr"));
            ApplyGrp(0.60f, 1f, "R",
                true, false, G("rshldr"));
            ApplyGrp(0f, 0.55f, null,
                true, false, G("hands"));
            ApplyGrp(0f, 0.55f, "L",
                true, false, G("lhand"));
            ApplyGrp(0f, 0.55f, "R",
                true, false, G("rhand"));
            ApplyGrp(0f, 0.45f, null,
                true, false, G("fingers"));
            ApplyGrp(0f, 0.45f, "L",
                true, false, G("lfing"));
            ApplyGrp(0f, 0.45f, "R",
                true, false, G("rfing"));
            ApplyGrp(0f, 0.55f, null,
                false, true, G("legs"));
            ApplyGrp(0f, 0.55f, "L",
                false, false, G("lleg"));
            ApplyGrp(0f, 0.55f, "R",
                false, false, G("rleg"));
            ApplyGrp(0.55f, 0.68f, null,
                false, true, G("hips"));
            ApplyGrp(0.55f, 0.68f, "L",
                false, false, G("lhip"));
            ApplyGrp(0.55f, 0.68f, "R",
                false, false, G("rhip"));
            ApplyGrp(0.38f, 0.55f, null,
                false, true, G("thighs"));
            ApplyGrp(0.38f, 0.55f, "L",
                false, false, G("lthigh"));
            ApplyGrp(0.38f, 0.55f, "R",
                false, false, G("rthigh"));
            ApplyGrp(0.20f, 0.38f, null,
                false, true, G("shins"));
            ApplyGrp(0.20f, 0.38f, "L",
                false, false, G("lshin"));
            ApplyGrp(0.20f, 0.38f, "R",
                false, false, G("rshin"));
            ApplyGrp(0.08f, 0.20f, null,
                false, true, G("ankles"));
            ApplyGrp(0.08f, 0.20f, "L",
                false, false, G("lankle"));
            ApplyGrp(0.08f, 0.20f, "R",
                false, false, G("rankle"));
            ApplyGrp(0f, 0.08f, null,
                false, true, G("feet"));
            ApplyGrp(0f, 0.08f, "L",
                false, false, G("lfoot"));
            ApplyGrp(0f, 0.08f, "R",
                false, false, G("rfoot"));
            ApplyGrp(0.55f, 1.01f, null,
                false, false, G("upper"));
            ApplyGrp(0f, 0.68f, null,
                false, true, G("lower"));

            float[] all = G("all") ?? G("body");
            if (all != null)
            {
                bool doX =
                    Math.Abs(all[0] - 1f) > 0.001f;
                bool doY =
                    Math.Abs(all[1] - 1f) > 0.001f;
                bool doZ =
                    Math.Abs(all[2] - 1f) > 0.001f;
                for (int b = 0; b < n; b++)
                {
                    if (doX) sx[b] = all[0];
                    if (doY) sy[b] = all[1];
                    if (doZ) sz[b] = all[2];
                }
            }
        }

        // ═══════════════════════════════════
        // MAIN SCALE LOGIC
        //
        // Reads current file directly,
        // same as BoyScaler.
        // Use 'restore' to go back to
        // original.
        // ═══════════════════════════════════
        private static void DoScale(
            string filepath,
            Dictionary<int, float[]> boneCfg,
            Dictionary<string, float[]>
                groupScales,
            bool showInfo)
        {
            // Read current file directly
            // (same as BoyScaler)
            byte[] data =
                File.ReadAllBytes(filepath);

            int bc = CountBones(data);
            int boneStart =
                GetBoneStart(data, bc);

            if (bc == 0)
            {
                Err("No bones found");
                return;
            }

            // ── Header ────────────────────
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  ╔═══════════════════════"
                + "═══════════════╗");
            Console.WriteLine(
                "  ║  Universal Bone"
                + " Scaler v3.5        ║");
            Console.WriteLine(
                "  ║  HMSTHModdingTool"
                + "  |  DarthKrayt333  ║");
            Console.WriteLine(
                "  ╚═══════════════════════"
                + "═══════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine(
                "  File  : " +
                Path.GetFileName(filepath));
            Console.WriteLine(
                $"  Size  : {data.Length:N0} B");
            Console.WriteLine(
                $"  Bones : {bc}");
            Console.WriteLine();

            // ── Info mode ─────────────────
            bool doInfo = showInfo ||
                (groupScales.Count == 0 &&
                 boneCfg.Count == 0);

            if (doInfo)
            {
                Console.ForegroundColor =
                    ConsoleColor.White;
                Console.WriteLine(
                    "  Raw bone data"
                    + " (X=off+0, Y=off+4,"
                    + " Z=off+8):");
                Console.ResetColor();
                Console.WriteLine(
                    $"  {"#",3}"
                    + $" {"OX",10}"
                    + $" {"OY",10}"
                    + $" {"OZ",10}");
                Console.WriteLine(
                    "  " + new string('─', 40));

                for (int b = 0; b < bc; b++)
                {
                    int off =
                        boneStart +
                        b * BONE_REC_SIZE;
                    if (off + 12 > data.Length)
                        break;
                    float ox = ReadF32(data, off + 0);
                    float oy = ReadF32(data, off + 4);
                    float oz = ReadF32(data, off + 8);
                    Console.ForegroundColor =
                        ConsoleColor.DarkGray;
                    Console.WriteLine(
                        $"  {b,3}"
                        + $" {ox,10:F4}"
                        + $" {oy,10:F4}"
                        + $" {oz,10:F4}");
                    Console.ResetColor();
                }

                Console.WriteLine(
                    "  " + new string('─', 40));
                Console.WriteLine();

                if (groupScales.Count == 0 &&
                    boneCfg.Count == 0)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Cyan;
                    Console.WriteLine(
                        "  b<N>y <v>"
                        + "   scale bone N Y");
                    Console.WriteLine(
                        "  legsy <v>"
                        + "   scale all leg Y");
                    Console.WriteLine(
                        "  restore"
                        + "     restore original");
                    Console.WriteLine(
                        "  Use help for"
                        + " all options.");
                    Console.ResetColor();
                    Console.WriteLine();
                    return;
                }
            }

            // ── Build scale arrays ────────
            float[] sxArr = new float[bc];
            float[] syArr = new float[bc];
            float[] szArr = new float[bc];
            for (int b = 0; b < bc; b++)
            {
                sxArr[b] = 1f;
                syArr[b] = 1f;
                szArr[b] = 1f;
            }

            ResolveGroups(
                groupScales,
                sxArr, syArr, szArr,
                data, boneStart, bc);

            foreach (var kv in boneCfg)
            {
                int bi = kv.Key;
                float[] v = kv.Value;
                if (bi >= bc) continue;
                if (Math.Abs(v[0] - 1f) > 0.001f)
                    sxArr[bi] = v[0];
                if (Math.Abs(v[1] - 1f) > 0.001f)
                    syArr[bi] = v[1];
                if (Math.Abs(v[2] - 1f) > 0.001f)
                    szArr[bi] = v[2];
            }

            // ── Backup (first run only) ───
            string bk = filepath + ".original";
            if (!File.Exists(bk))
            {
                File.Copy(filepath, bk);
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "  Backup: " +
                    Path.GetFileName(bk));
                Console.ResetColor();
                Console.WriteLine();
            }

            // ── Apply and report ──────────
            Console.WriteLine(
                $"  {"#",3}"
                + $" {"OX",10}"
                + $" {"OY",10}"
                + $" {"OZ",10}"
                + $"  →"
                + $" {"NX",10}"
                + $" {"NY",10}"
                + $" {"NZ",10}");
            Console.WriteLine(
                "  " + new string('─', 80));

            int changed = 0;
            for (int b = 0; b < bc; b++)
            {
                int off =
                    boneStart + b * BONE_REC_SIZE;
                if (off + 12 > data.Length) break;

                float ox = ReadF32(data, off + 0);
                float oy = ReadF32(data, off + 4);
                float oz = ReadF32(data, off + 8);

                float nx = ox * sxArr[b];
                float ny = oy * syArr[b];
                float nz = oz * szArr[b];

                bool diff =
                    Math.Abs(nx - ox) > 0.0001f ||
                    Math.Abs(ny - oy) > 0.0001f ||
                    Math.Abs(nz - oz) > 0.0001f;

                if (diff)
                {
                    WriteF32(data, off + 0, nx);
                    WriteF32(data, off + 4, ny);
                    WriteF32(data, off + 8, nz);
                    changed++;
                }

                Console.ForegroundColor = diff
                    ? ConsoleColor.Green
                    : ConsoleColor.DarkGray;
                Console.WriteLine(
                    $"  {b,3}"
                    + $" {ox,10:F4}"
                    + $" {oy,10:F4}"
                    + $" {oz,10:F4}"
                    + $"  →"
                    + $" {nx,10:F4}"
                    + $" {ny,10:F4}"
                    + $" {nz,10:F4}"
                    + (diff ? " ←" : ""));
                Console.ResetColor();
            }

            Console.WriteLine(
                "  " + new string('─', 80));
            Console.WriteLine(
                $"  Changed: {changed}/{bc}");
            Console.WriteLine();

            File.WriteAllBytes(filepath, data);
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "  Saved: " +
                Path.GetFileName(filepath));
            Console.ResetColor();
            Console.WriteLine();
        }

        // ═══════════════════════════════════
        // HELP
        // ═══════════════════════════════════
        private static void PrintHelp()
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  Universal Bone Scaler v3.5");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("  Usage:");
            Console.WriteLine(
                "    bonescale"
                + " <skeleton.bin> [options]");
            Console.WriteLine();

            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "  Bone layout"
                + " (same as BoyScaler):");
            Console.ResetColor();
            Console.WriteLine(
                "    off+0 = X  (--bNx)");
            Console.WriteLine(
                "    off+4 = Y  (--bNy)");
            Console.WriteLine(
                "    off+8 = Z  (--bNz)");
            Console.WriteLine();

            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "  Direct bone scaling:");
            Console.ResetColor();
            Console.WriteLine(
                "    b<N> <v>"
                + "       all axes × v");
            Console.WriteLine(
                "    b<N>x <v>"
                + "      X axis × v");
            Console.WriteLine(
                "    b<N>y <v>"
                + "      Y axis × v");
            Console.WriteLine(
                "    b<N>z <v>"
                + "      Z axis × v");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    (-- or - optional)");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "  Group scaling:");
            Console.ResetColor();
            Console.WriteLine(
                "    spine  neck  head  chest");
            Console.WriteLine(
                "    arms   larm  rarm");
            Console.WriteLine(
                "    shoulders  lshldr  rshldr");
            Console.WriteLine(
                "    hands  lhand  rhand");
            Console.WriteLine(
                "    fingers  lfing  rfing");
            Console.WriteLine(
                "    legs   lleg   rleg");
            Console.WriteLine(
                "    hips   lhip   rhip");
            Console.WriteLine(
                "    thighs  lthigh  rthigh");
            Console.WriteLine(
                "    shins   lshin   rshin");
            Console.WriteLine(
                "    ankles  lankle  rankle");
            Console.WriteLine(
                "    feet   lfoot   rfoot");
            Console.WriteLine(
                "    upper  lower   all  body");
            Console.WriteLine(
                "    Add x/y/z:"
                + " legsy 1.25");
            Console.WriteLine();

            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "  Restore original:");
            Console.ResetColor();
            Console.WriteLine(
                "    bonescale"
                + " <file.bin> restore");
            Console.WriteLine();

            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine("  Other:");
            Console.ResetColor();
            Console.WriteLine(
                "    info"
                + "   Show raw bone data");
            Console.WriteLine();

            Console.ForegroundColor =
                ConsoleColor.DarkYellow;
            Console.WriteLine("  Examples:");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    bonescale"
                + " 00_skeleton.bin info");
            Console.WriteLine(
                "    bonescale"
                + " 00_skeleton.bin"
                + " --b51y 1.1 --b60y 1.1");
            Console.WriteLine(
                "    bonescale"
                + " 00_skeleton.bin"
                + " b51y 1.1 b60y 1.1");
            Console.WriteLine(
                "    bonescale"
                + " 00_skeleton.bin"
                + " legsy 1.25");
            Console.WriteLine(
                "    bonescale"
                + " 00_skeleton.bin"
                + " all 1.1");
            Console.WriteLine(
                "    bonescale"
                + " 00_skeleton.bin"
                + " restore");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void Err(string m)
        {
            Console.ForegroundColor =
                ConsoleColor.Red;
            Console.WriteLine(
                "  ERROR: " + m);
            Console.ResetColor();
        }
    }
}
