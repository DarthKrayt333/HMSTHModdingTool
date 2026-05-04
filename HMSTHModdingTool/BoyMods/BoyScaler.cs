using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace HMSTHModdingTool.BoyMods
{
    // ═══════════════════════════════════════════════════════════════
    // BOY ADVANCED BONE SCALER
    // Individual XYZ bone scaling for BOY player character
    //
    // BOY 3D Tools by DarthKrayt333
    // HMSTHModdingTool
    // Harvest Moon: Save The Homeland (PS2)
    // ═══════════════════════════════════════════════════════════════
    public static class BoyScaler
    {
        // ─────────────────────────────────────────
        // CONSTANTS
        // ─────────────────────────────────────────
        private const float BONE0_Y_LOCK = 64.0f;
        private const int BONE_REC_SIZE = 16;
        private const int MAX_BONES = 68;

        // ─────────────────────────────────────────
        // BONE NAMES
        // ─────────────────────────────────────────
        private static readonly Dictionary<int, string>
            BoneNames = new Dictionary<int, string>
        {
            { 0,  "ROOT"      }, { 1,  "SEC_ROOT"  },
            { 2,  "SPINE_BASE"}, { 3,  "SPINE_MID" },
            { 4,  "SPINE_TOP" }, { 5,  "NECK"      },
            { 6,  "EYES"      }, { 7,  "FACE_1"    },
            { 8,  "FACE_2"    }, { 9,  "EYE_L"     },
            { 10, "EYE_C"     }, { 11, "EYE_R"     },
            { 12, "CHEST_R"   }, { 13, "CHEST_C"   },
            { 14, "CHEST_L"   }, { 15, "SHLDR_R"   },
            { 16, "UARM_R"    }, { 17, "FARM_R"    },
            { 18, "ELBOW_R"   }, { 19, "LARM_R"    },
            { 20, "HAND_R"    }, { 21, "F1B_R"     },
            { 22, "F1M_R"     }, { 23, "F1T_R"     },
            { 24, "F2B_R"     }, { 25, "F2M_R"     },
            { 26, "F2T_R"     }, { 27, "F3B_R"     },
            { 28, "HD1_R"     }, { 29, "HD2_R"     },
            { 30, "HD3_R"     }, { 31, "HD4_R"     },
            { 32, "SHLDR_L"   }, { 33, "UARM_L"    },
            { 34, "FARM_L"    }, { 35, "ELBOW_L"   },
            { 36, "LARM_L"    }, { 37, "HAND_L"    },
            { 38, "F1B_L"     }, { 39, "F1M_L"     },
            { 40, "F1T_L"     }, { 41, "F2B_L"     },
            { 42, "F2M_L"     }, { 43, "F2T_L"     },
            { 44, "F3B_L"     }, { 45, "HD1_L"     },
            { 46, "HD2_L"     }, { 47, "HD3_L"     },
            { 48, "HD4_L"     }, { 49, "HD5_L"     },
            { 50, "HIP_R"     }, { 51, "THIGH_R"   },
            { 52, "SHIN_R"    }, { 53, "ANKLE_R"   },
            { 54, "FOOT_R"    }, { 55, "TOEB_R"    },
            { 56, "TOE_R"     }, { 57, "TOET1_R"   },
            { 58, "TOET2_R"   }, { 59, "HIP_L"     },
            { 60, "THIGH_L"   }, { 61, "SHIN_L"    },
            { 62, "ANKLE_L"   }, { 63, "FOOT_L"    },
            { 64, "TOEB_L"    }, { 65, "TOE_L"     },
            { 66, "TOET1_L"   }, { 67, "TOET2_L"   },
        };

        // ─────────────────────────────────────────
        // BONE SAFETY
        // ─────────────────────────────────────────
        private static readonly Dictionary<int, string>
            BoneSafety = new Dictionary<int, string>
        {
            { 0,  "locked"  }, { 1,  "locked"  },
            { 2,  "safe"    }, { 3,  "safe"    },
            { 4,  "safe"    }, { 5,  "safe"    },
            { 6,  "danger"  }, { 7,  "danger"  },
            { 8,  "danger"  }, { 9,  "danger"  },
            { 10, "danger"  }, { 11, "danger"  },
            { 12, "danger"  }, { 13, "danger"  },
            { 14, "danger"  }, { 15, "safe"    },
            { 16, "safe"    }, { 17, "safe"    },
            { 18, "safe"    }, { 19, "safe"    },
            { 20, "safe"    }, { 21, "warning" },
            { 22, "warning" }, { 23, "warning" },
            { 24, "warning" }, { 25, "warning" },
            { 26, "warning" }, { 27, "warning" },
            { 28, "warning" }, { 29, "warning" },
            { 30, "warning" }, { 31, "warning" },
            { 32, "safe"    }, { 33, "safe"    },
            { 34, "safe"    }, { 35, "safe"    },
            { 36, "safe"    }, { 37, "safe"    },
            { 38, "warning" }, { 39, "warning" },
            { 40, "warning" }, { 41, "warning" },
            { 42, "warning" }, { 43, "warning" },
            { 44, "warning" }, { 45, "warning" },
            { 46, "warning" }, { 47, "warning" },
            { 48, "warning" }, { 49, "warning" },
            { 50, "safe"    }, { 51, "safe"    },
            { 52, "safe"    }, { 53, "safe"    },
            { 54, "safe"    }, { 55, "safe"    },
            { 56, "safe"    }, { 57, "safe"    },
            { 58, "safe"    }, { 59, "safe"    },
            { 60, "safe"    }, { 61, "safe"    },
            { 62, "safe"    }, { 63, "safe"    },
            { 64, "safe"    }, { 65, "safe"    },
            { 66, "safe"    }, { 67, "safe"    },
        };

        // ─────────────────────────────────────────
        // GROUP DEFINITIONS
        // ─────────────────────────────────────────
        private static readonly
            Dictionary<string, HashSet<int>> Groups
            = new Dictionary<string, HashSet<int>>
        {
            { "spine",
                new HashSet<int>{ 2, 3, 4 }},
            { "neck",
                new HashSet<int>{ 5 }},
            { "head",
                new HashSet<int>{ 6,7,8,9,10,11 }},
            { "chest",
                new HashSet<int>{ 12,13,14 }},
            { "rshldr",
                new HashSet<int>{ 15 }},
            { "lshldr",
                new HashSet<int>{ 32 }},
            { "rarm",
                new HashSet<int>{ 16,17,18 }},
            { "larm",
                new HashSet<int>{ 33,34,35 }},
            { "rhand",
                new HashSet<int>{ 19,20 }},
            { "lhand",
                new HashSet<int>{ 36,37 }},
            { "rfing",
                new HashSet<int>{
                    21,22,23,24,25,
                    26,27,28,29,30,31 }},
            { "lfing",
                new HashSet<int>{
                    38,39,40,41,42,
                    43,44,45,46,47,48,49 }},
            { "rhip",
                new HashSet<int>{ 50 }},
            { "lhip",
                new HashSet<int>{ 59 }},
            { "rthigh",
                new HashSet<int>{ 51 }},
            { "lthigh",
                new HashSet<int>{ 60 }},
            { "rshin",
                new HashSet<int>{ 52 }},
            { "lshin",
                new HashSet<int>{ 61 }},
            { "rankle",
                new HashSet<int>{ 53 }},
            { "lankle",
                new HashSet<int>{ 62 }},
            { "rfoot",
                new HashSet<int>{ 54 }},
            { "lfoot",
                new HashSet<int>{ 63 }},
            { "rtoebase",
                new HashSet<int>{ 55 }},
            { "ltoebase",
                new HashSet<int>{ 64 }},
            { "rtoe",
                new HashSet<int>{ 56 }},
            { "ltoe",
                new HashSet<int>{ 65 }},
            { "rtoetip",
                new HashSet<int>{ 57, 58 }},
            { "ltoetip",
                new HashSet<int>{ 66, 67 }},
        };

        // ─────────────────────────────────────────
        // PAIR ALIASES (both sides at once)
        // ─────────────────────────────────────────
        private static readonly
            Dictionary<string, string[]> PairAliases
            = new Dictionary<string, string[]>
        {
            { "arms",
                new[]{ "larm",     "rarm"     }},
            { "hands",
                new[]{ "lhand",    "rhand"    }},
            { "fingers",
                new[]{ "lfing",    "rfing"    }},
            { "shoulders",
                new[]{ "lshldr",   "rshldr"   }},
            { "hips",
                new[]{ "lhip",     "rhip"     }},
            { "thighs",
                new[]{ "lthigh",   "rthigh"   }},
            { "shins",
                new[]{ "lshin",    "rshin"    }},
            { "ankles",
                new[]{ "lankle",   "rankle"   }},
            { "feet",
                new[]{ "lfoot",    "rfoot"    }},
            { "toebases",
                new[]{ "ltoebase", "rtoebase" }},
            { "toes",
                new[]{ "ltoe",     "rtoe"     }},
            { "toetips",
                new[]{ "ltoetip",  "rtoetip"  }},
        };

        // Mega aliases
        private static readonly string[] LegGroups =
        {
            "lhip",    "rhip",
            "lthigh",  "rthigh",
            "lshin",   "rshin",
            "lankle",  "rankle",
            "lfoot",   "rfoot",
            "ltoebase","rtoebase",
            "ltoe",    "rtoe",
            "ltoetip", "rtoetip",
        };

        private static readonly string[] TorsoGroups =
        {
            "spine",
        };

        // ═════════════════════════════════════════
        // PUBLIC ENTRY POINT
        // ═════════════════════════════════════════
        public static void Run(string[] args)
        {
            if (args.Length < 2)
            {
                PrintHelp();
                return;
            }

            string filepath = null;
            var groupCfg =
                new Dictionary<string, float[]>();
            var boneCfg =
                new Dictionary<int, float[]>();
            bool force = false;

            var boneRe = new Regex(
                @"^--?b(\d+)(x|y|z)?$",
                RegexOptions.IgnoreCase);

            int i = 1;
            while (i < args.Length)
            {
                string a = args[i];

                // ── --force ───────────────────────
                if (a.Equals("--force",
                        StringComparison
                            .OrdinalIgnoreCase) ||
                    a.Equals("-force",
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    force = true;
                    i++;
                    continue;
                }

                // ── --help ────────────────────────
                if (a.Equals("--help",
                        StringComparison
                            .OrdinalIgnoreCase) ||
                    a.Equals("-help",
                        StringComparison
                            .OrdinalIgnoreCase) ||
                    a.Equals("help",
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    PrintHelp();
                    return;
                }

                // ── Option with value ─────────────
                if (a.StartsWith("--") ||
                    a.StartsWith("-"))
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.ForegroundColor =
                            ConsoleColor.Red;
                        Console.WriteLine(
                            $"  ERROR: {a}" +
                            " needs a value");
                        Console.ResetColor();
                        return;
                    }

                    float val;
                    if (!float.TryParse(
                            args[i + 1],
                            System.Globalization
                                .NumberStyles.Float,
                            System.Globalization
                                .CultureInfo
                                .InvariantCulture,
                            out val))
                    {
                        Console.ForegroundColor =
                            ConsoleColor.Red;
                        Console.WriteLine(
                            $"  ERROR: {a} value" +
                            " must be a number," +
                            $" got: {args[i + 1]}");
                        Console.ResetColor();
                        return;
                    }

                    // Try individual bone: --b53y
                    var m = boneRe.Match(a);
                    if (m.Success)
                    {
                        int bidx = int.Parse(
                            m.Groups[1].Value);
                        string axis =
                            m.Groups[2].Success
                            ? m.Groups[2].Value
                                .ToLower()
                            : null;

                        if (bidx >= MAX_BONES)
                        {
                            Console.ForegroundColor =
                                ConsoleColor.Red;
                            Console.WriteLine(
                                $"  ERROR: Bone" +
                                $" {bidx} out of" +
                                $" range" +
                                $" (0-{MAX_BONES - 1})");
                            Console.ResetColor();
                            return;
                        }

                        if (!boneCfg
                                .ContainsKey(bidx))
                            boneCfg[bidx] =
                                new float[]
                                { 1f, 1f, 1f };

                        if (axis == null)
                        {
                            boneCfg[bidx][0] = val;
                            boneCfg[bidx][1] = val;
                            boneCfg[bidx][2] = val;
                        }
                        else if (axis == "x")
                            boneCfg[bidx][0] = val;
                        else if (axis == "y")
                            boneCfg[bidx][1] = val;
                        else if (axis == "z")
                            boneCfg[bidx][2] = val;

                        i += 2;
                        continue;
                    }

                    // Try group/alias
                    string raw = a.TrimStart('-')
                        .ToLower();
                    string axisStr = null;
                    string baseStr = raw;

                    if (raw.EndsWith("x"))
                    {
                        string c = raw.Substring(
                            0, raw.Length - 1);
                        if (Groups.ContainsKey(c) ||
                            PairAliases
                                .ContainsKey(c) ||
                            c == "legs" ||
                            c == "torso")
                        {
                            axisStr = "x";
                            baseStr = c;
                        }
                    }
                    else if (raw.EndsWith("y"))
                    {
                        string c = raw.Substring(
                            0, raw.Length - 1);
                        if (Groups.ContainsKey(c) ||
                            PairAliases
                                .ContainsKey(c) ||
                            c == "legs" ||
                            c == "torso")
                        {
                            axisStr = "y";
                            baseStr = c;
                        }
                    }
                    else if (raw.EndsWith("z"))
                    {
                        string c = raw.Substring(
                            0, raw.Length - 1);
                        if (Groups.ContainsKey(c) ||
                            PairAliases
                                .ContainsKey(c) ||
                            c == "legs" ||
                            c == "torso")
                        {
                            axisStr = "z";
                            baseStr = c;
                        }
                    }

                    bool handled = false;

                    if (Groups.ContainsKey(baseStr))
                    {
                        SetGroupAxis(groupCfg,
                            baseStr,
                            axisStr, val);
                        handled = true;
                    }
                    else if (PairAliases
                        .ContainsKey(baseStr))
                    {
                        foreach (var g in
                            PairAliases[baseStr])
                            SetGroupAxis(groupCfg,
                                g, axisStr, val);
                        handled = true;
                    }
                    else if (baseStr == "legs")
                    {
                        foreach (var g in LegGroups)
                            SetGroupAxis(groupCfg,
                                g, axisStr, val);
                        handled = true;
                    }
                    else if (baseStr == "torso")
                    {
                        foreach (var g in TorsoGroups)
                            SetGroupAxis(groupCfg,
                                g, axisStr, val);
                        handled = true;
                    }

                    if (!handled)
                    {
                        Console.ForegroundColor =
                            ConsoleColor.Red;
                        Console.WriteLine(
                            $"  ERROR: Unknown" +
                            $" option '{a}'");
                        Console.WriteLine(
                            "  Use --help for" +
                            " all options.");
                        Console.ResetColor();
                        return;
                    }

                    i += 2;
                }
                else
                {
                    filepath = a;
                    i++;
                }
            }

            if (filepath == null)
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "  ERROR: No input file" +
                    " specified!");
                Console.ResetColor();
                Console.WriteLine();
                PrintHelp();
                return;
            }

            if (!File.Exists(filepath))
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    $"  ERROR: File not found:" +
                    $" {filepath}");
                Console.ResetColor();
                return;
            }

            DoScale(filepath,
                groupCfg, boneCfg, force);
        }

        // ─────────────────────────────────────────
        // SET GROUP AXIS
        // ─────────────────────────────────────────
        private static void SetGroupAxis(
            Dictionary<string, float[]> cfg,
            string gname,
            string axis,
            float val)
        {
            if (!cfg.ContainsKey(gname))
                cfg[gname] =
                    new float[] { 1f, 1f, 1f };

            if (axis == null)
            {
                cfg[gname][0] = val;
                cfg[gname][1] = val;
                cfg[gname][2] = val;
            }
            else if (axis == "x")
                cfg[gname][0] = val;
            else if (axis == "y")
                cfg[gname][1] = val;
            else if (axis == "z")
                cfg[gname][2] = val;
        }

        // ─────────────────────────────────────────
        // BUILD SCALE MAP
        // ─────────────────────────────────────────
        private static void BuildScaleMap(
            Dictionary<string, float[]> groupCfg,
            Dictionary<int, float[]> boneCfg,
            out float[] sxMap,
            out float[] syMap,
            out float[] szMap,
            out string[] srcMap)
        {
            sxMap = new float[MAX_BONES];
            syMap = new float[MAX_BONES];
            szMap = new float[MAX_BONES];
            srcMap = new string[MAX_BONES];

            for (int i = 0; i < MAX_BONES; i++)
            {
                sxMap[i] = 1.0f;
                syMap[i] = 1.0f;
                szMap[i] = 1.0f;
                srcMap[i] = "KEEP";
            }

            // Apply groups
            foreach (var kv in groupCfg)
            {
                string gname = kv.Key;
                float[] gs = kv.Value;

                if (!Groups.ContainsKey(gname))
                    continue;

                foreach (int bidx in Groups[gname])
                {
                    if (Math.Abs(gs[0] - 1f)
                        > 0.0001f)
                    {
                        sxMap[bidx] = gs[0];
                        srcMap[bidx] =
                            "GRP:" + gname;
                    }
                    if (Math.Abs(gs[1] - 1f)
                        > 0.0001f)
                    {
                        syMap[bidx] = gs[1];
                        srcMap[bidx] =
                            "GRP:" + gname;
                    }
                    if (Math.Abs(gs[2] - 1f)
                        > 0.0001f)
                    {
                        szMap[bidx] = gs[2];
                        srcMap[bidx] =
                            "GRP:" + gname;
                    }
                }
            }

            // Individual bone overrides
            foreach (var kv in boneCfg)
            {
                int bidx = kv.Key;
                float[] bs = kv.Value;

                if (bidx < 0 ||
                    bidx >= MAX_BONES)
                    continue;

                if (Math.Abs(bs[0] - 1f) > 0.0001f)
                {
                    sxMap[bidx] = bs[0];
                    srcMap[bidx] =
                        "BONE:" + bidx;
                }
                if (Math.Abs(bs[1] - 1f) > 0.0001f)
                {
                    syMap[bidx] = bs[1];
                    srcMap[bidx] =
                        "BONE:" + bidx;
                }
                if (Math.Abs(bs[2] - 1f) > 0.0001f)
                {
                    szMap[bidx] = bs[2];
                    srcMap[bidx] =
                        "BONE:" + bidx;
                }
            }
        }

        // ═════════════════════════════════════════
        // MAIN SCALE LOGIC
        // ═════════════════════════════════════════
        private static void DoScale(
            string filepath,
            Dictionary<string, float[]> groupCfg,
            Dictionary<int, float[]> boneCfg,
            bool force)
        {
            byte[] data =
                File.ReadAllBytes(filepath);

            int boneCount = CountBones(data);
            int boneStart =
                GetBoneStart(data, boneCount);

            float[] sxMap;
            float[] syMap;
            float[] szMap;
            string[] srcMap;

            BuildScaleMap(
                groupCfg, boneCfg,
                out sxMap, out syMap,
                out szMap, out srcMap);

            // ── Header ────────────────────────────
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  ╔══════════════════════════════════" +
                "════════════════════════════════════╗");
            Console.WriteLine(
                "  ║  BOY Advanced Bone Scaler" +
                " & Height Tool" +
                "                              ║");
            Console.WriteLine(
                "  ║  HMSTHModdingTool  |  " +
                "BOY 3D Tools by DarthKrayt333" +
                "                          ║");
            Console.WriteLine(
                "  ╚══════════════════════════════════" +
                "════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine(
                $"  File   : " +
                Path.GetFileName(filepath));
            Console.WriteLine(
                $"  Size   : {data.Length:N0} bytes");
            Console.WriteLine(
                $"  Bones  : {boneCount}");
            Console.WriteLine();

            // ── Bone 0 check ──────────────────────
            float b0y = ReadF32(data,
                boneStart +
                0 * BONE_REC_SIZE + 4);

            bool b0ok =
                Math.Abs(b0y - BONE0_Y_LOCK)
                < 0.01f;

            Console.ForegroundColor =
                b0ok
                ? ConsoleColor.Green
                : ConsoleColor.Yellow;
            Console.WriteLine(
                $"  Bone 0 Y = {b0y:F4}" +
                $"  ({FloatToHex(b0y)})  " +
                (b0ok ? "✓ Correct"
                      : "⚠ Unexpected"));
            Console.ResetColor();
            Console.WriteLine(
                $"  Bone 0 Y will stay locked" +
                $" at {BONE0_Y_LOCK:F1}" +
                " (00 00 80 42)");
            Console.WriteLine();

            // ── Unsafe bone check ─────────────────
            if (!force)
            {
                bool hasUnsafe = false;

                for (int idx = 0;
                     idx < Math.Min(
                         boneCount, MAX_BONES);
                     idx++)
                {
                    if (Math.Abs(sxMap[idx] - 1)
                            < 0.001f &&
                        Math.Abs(syMap[idx] - 1)
                            < 0.001f &&
                        Math.Abs(szMap[idx] - 1)
                            < 0.001f)
                        continue;

                    string safety =
                        BoneSafety.ContainsKey(idx)
                        ? BoneSafety[idx]
                        : "unknown";

                    if (safety == "danger" ||
                        safety == "locked")
                    {
                        if (!hasUnsafe)
                        {
                            Console.ForegroundColor
                                = ConsoleColor.Red;
                            Console.WriteLine(
                                "  ╔════════════" +
                                "═══════════════" +
                                "═════════════════╗");
                            Console.WriteLine(
                                "  ║  ⚠  WARNING:" +
                                " UNSAFE BONES" +
                                " DETECTED!       ║");
                            Console.WriteLine(
                                "  ╠════════════" +
                                "═══════════════" +
                                "═════════════════╣");
                            Console.ResetColor();
                            hasUnsafe = true;
                        }

                        string bname =
                            BoneNames
                                .ContainsKey(idx)
                            ? BoneNames[idx]
                            : $"UNK{idx}";

                        Console.ForegroundColor =
                            ConsoleColor.Yellow;
                        Console.WriteLine(
                            $"  ║  Bone {idx,2}" +
                            $" ({bname,-10})" +
                            $" : {safety.ToUpper(),-8}" +
                            " - moves head/hair! ║");
                        Console.ResetColor();
                    }
                }

                if (hasUnsafe)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Red;
                    Console.WriteLine(
                        "  ╠════════════════" +
                        "═══════════════════════╣");
                    Console.WriteLine(
                        "  ║  Add -force to" +
                        " override this check.  ║");
                    Console.WriteLine(
                        "  ╚════════════════" +
                        "═══════════════════════╝");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.ForegroundColor =
                        ConsoleColor.Cyan;
                    Console.WriteLine(
                        "  Safe alternatives:");
                    Console.ResetColor();
                    Console.WriteLine(
                        "    --b4x 1.20 --b4z 1.20" +
                        "   (wider upper spine)");
                    Console.WriteLine(
                        "    --b15x 1.20 --b32x 1.20" +
                        " (wider shoulders)");
                    Console.WriteLine();
                    Console.WriteLine(
                        "  Operation CANCELLED.");
                    Console.WriteLine();
                    return;
                }
            }

            // ── Changes list ──────────────────────
            Console.WriteLine(
                "  ┌─ CHANGES TO APPLY ──────────────" +
                "───────────────────────────────────┐");

            bool anyChange = false;

            for (int idx = 0;
                 idx < Math.Min(
                     boneCount, MAX_BONES);
                 idx++)
            {
                float sx = sxMap[idx];
                float sy = syMap[idx];
                float sz = szMap[idx];

                if (Math.Abs(sx - 1) < 0.001f &&
                    Math.Abs(sy - 1) < 0.001f &&
                    Math.Abs(sz - 1) < 0.001f)
                    continue;

                string safety =
                    BoneSafety.ContainsKey(idx)
                    ? BoneSafety[idx]
                    : "?";

                string safeTag =
                    safety == "safe" ? "✓ SAFE" :
                    safety == "warning" ? "⚠ WARN" :
                    safety == "danger" ? "✗ DNGR" :
                    safety == "locked" ? "🔒LOCK" :
                    "? UNK";

                string bname =
                    BoneNames.ContainsKey(idx)
                    ? BoneNames[idx]
                    : $"UNK{idx}";

                Console.ForegroundColor =
                    safety == "safe"
                    ? ConsoleColor.Green
                    : safety == "warning"
                        ? ConsoleColor.Yellow
                        : ConsoleColor.Red;

                Console.WriteLine(
                    $"  │  Bone {idx,2}" +
                    $" ({bname,-10})" +
                    $"  X×{sx:F2}" +
                    $"  Y×{sy:F2}" +
                    $"  Z×{sz:F2}" +
                    $"  {safeTag}");

                Console.ResetColor();
                anyChange = true;
            }

            if (!anyChange)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  │  (nothing to change" +
                    " - all bones stay original)");
                Console.ResetColor();
            }

            Console.WriteLine(
                "  └─────────────────────────────────" +
                "───────────────────────────────────┘");
            Console.WriteLine();

            // ── Backup ────────────────────────────
            string backup = filepath + ".original";
            if (!File.Exists(backup))
            {
                File.Copy(filepath, backup);
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "  Backup created: " +
                    Path.GetFileName(backup));
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(
                    "  Backup exists : " +
                    Path.GetFileName(backup));
            }
            Console.WriteLine();

            // ── Bone table ────────────────────────
            Console.WriteLine(
                $"  {"#",3}  {"NAME",-10}  " +
                $"{"OX",9} {"OY",9} {"OZ",9}  │  " +
                $"{"NX",9} {"NY",9} {"NZ",9}  " +
                $"{"SX",4} {"SY",4} {"SZ",4}  SRC");
            Console.WriteLine(
                "  " + new string('─', 100));

            int totalChanged = 0;

            for (int idx = 0;
                 idx < boneCount; idx++)
            {
                int off =
                    boneStart +
                    idx * BONE_REC_SIZE;

                float ox = ReadF32(data, off + 0);
                float oy = ReadF32(data, off + 4);
                float oz = ReadF32(data, off + 8);

                float sx = idx < MAX_BONES
                    ? sxMap[idx] : 1f;
                float sy = idx < MAX_BONES
                    ? syMap[idx] : 1f;
                float sz = idx < MAX_BONES
                    ? szMap[idx] : 1f;
                string src = idx < MAX_BONES
                    ? srcMap[idx] : "KEEP";

                float nx, ny, nz;

                if (idx == 0)
                {
                    nx = ox * sx;
                    ny = BONE0_Y_LOCK;
                    nz = oz * sz;
                    src = "ROOT[Y=64.0]";
                }
                else if (idx == 1)
                {
                    nx = ox;
                    ny = oy;
                    nz = oz;
                    src = "KEEP[SEC_ROOT]";
                }
                else
                {
                    nx = ox * sx;
                    ny = oy * sy;
                    nz = oz * sz;
                }

                WriteF32(data, off + 0, nx);
                WriteF32(data, off + 4, ny);
                WriteF32(data, off + 8, nz);

                bool changed =
                    Math.Abs(nx - ox) > 0.0001f ||
                    Math.Abs(ny - oy) > 0.0001f ||
                    Math.Abs(nz - oz) > 0.0001f;

                if (changed) totalChanged++;

                string mark = changed ? "→" : " ";
                string bname =
                    BoneNames.ContainsKey(idx)
                    ? BoneNames[idx]
                    : $"UNK{idx}";

                Console.ForegroundColor =
                    changed
                    ? ConsoleColor.Green
                    : ConsoleColor.DarkGray;

                Console.WriteLine(
                    $"  {idx,3}  {bname,-10}  " +
                    $"{ox,9:F4} {oy,9:F4}" +
                    $" {oz,9:F4}  │  " +
                    $"{nx,9:F4} {ny,9:F4}" +
                    $" {nz,9:F4}  " +
                    $"{sx,4:F2} {sy,4:F2}" +
                    $" {sz,4:F2}  " +
                    $"{mark} {src}");

                Console.ResetColor();
            }

            Console.WriteLine(
                "  " + new string('─', 100));
            Console.WriteLine();

            // ── Force bone 0 Y ────────────────────
            WriteF32(data,
                boneStart +
                0 * BONE_REC_SIZE + 4,
                BONE0_Y_LOCK);

            float finalB0Y = ReadF32(data,
                boneStart +
                0 * BONE_REC_SIZE + 4);

            bool finalOk =
                Math.Abs(finalB0Y - BONE0_Y_LOCK)
                < 0.001f;

            Console.ForegroundColor =
                finalOk
                ? ConsoleColor.Green
                : ConsoleColor.Red;
            Console.WriteLine(
                $"  Final Bone 0 Y = {finalB0Y:F4}" +
                $"  ({FloatToHex(finalB0Y)})  " +
                (finalOk
                    ? "✓ CORRECT"
                    : "✗ ERROR"));
            Console.ResetColor();
            Console.WriteLine(
                $"  Total changed  =" +
                $" {totalChanged} / {boneCount}");
            Console.WriteLine();

            // ── Save ──────────────────────────────
            File.WriteAllBytes(filepath, data);

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "  Saved: " +
                Path.GetFileName(filepath));
            Console.WriteLine(
                $"  Size : {data.Length:N0} bytes");
            Console.ResetColor();
            Console.WriteLine();

            // ── Next steps ────────────────────────
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  ┌─ NEXT STEPS ──────────────────────────────────────────┐");
            Console.WriteLine(
                "  │  1. tool.exe -crdtb <folder> BOY_00000.rdtb           │");
            Console.WriteLine(
                "  │  2. tool.exe -chda BOY BOY.HDA                        │");
            Console.WriteLine(
                "  │  3. Test in game!                                      │");
            Console.WriteLine(
                "  └───────────────────────────────────────────────────────┘");
            Console.ResetColor();
            Console.WriteLine();
        }

        // ═════════════════════════════════════════
        // PRINT HELP
        // ═════════════════════════════════════════
        public static void PrintHelp()
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  ╔══════════════════════════════════" +
                "════════════════════════════════════╗");
            Console.WriteLine(
                "  ║  BOY Advanced Bone Scaler" +
                " & Height Tool" +
                "                              ║");
            Console.WriteLine(
                "  ║  HMSTHModdingTool  |  " +
                "BOY 3D Tools by DarthKrayt333" +
                "                          ║");
            Console.WriteLine(
                "  ╚══════════════════════════════════" +
                "════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine(
                "  Usage:");
            Console.WriteLine(
                "    tool.exe -boyscale" +
                " <00_skeleton.bin> [options]");
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "  Individual bone control:");
            Console.ResetColor();
            Console.WriteLine(
                "    --b<N>   <v>  scale bone N" +
                " all axes (X Y Z)");
            Console.WriteLine(
                "    --b<N>x  <v>  scale bone N" +
                " X axis only");
            Console.WriteLine(
                "    --b<N>y  <v>  scale bone N" +
                " Y axis only");
            Console.WriteLine(
                "    --b<N>z  <v>  scale bone N" +
                " Z axis only");
            Console.WriteLine(
                "    N = bone number (0 to 67)");
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "  Group control:");
            Console.ResetColor();
            Console.WriteLine(
                "    --<group>   <v>  all axes");
            Console.WriteLine(
                "    --<group>x  <v>  X only");
            Console.WriteLine(
                "    --<group>y  <v>  Y only");
            Console.WriteLine(
                "    --<group>z  <v>  Z only");
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "  ✓ SAFE bones (won't move hair):");
            Console.ResetColor();
            Console.WriteLine(
                "    Spine     : --b2 --b3 --b4");
            Console.WriteLine(
                "    Neck      : --b5");
            Console.WriteLine(
                "    Shoulder  : --b15 --b32");
            Console.WriteLine(
                "    Upper arm : --b17 (R)" +
                "  --b34 (L)");
            Console.WriteLine(
                "    Elbow     : --b18 (R)" +
                "  --b35 (L)");
            Console.WriteLine(
                "    Lower arm : --b19 (R)" +
                "  --b36 (L)");
            Console.WriteLine(
                "    Hand      : --b20 (R)" +
                "  --b37 (L)");
            Console.WriteLine(
                "    Legs      : --b50 to --b67");
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Red;
            Console.WriteLine(
                "  ✗ DANGER (will move hair/head):");
            Console.ResetColor();
            Console.WriteLine(
                "    --b12 --b13 --b14" +
                " (chest anchors)");
            Console.WriteLine(
                "    --b6 to --b11" +
                " (face and eyes)");
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "  Examples:");
            Console.ResetColor();
            Console.WriteLine(
                "    tool.exe -boyscale" +
                " 00_skeleton.bin" +
                " --b2y 1.20 --b3y 1.20" +
                " --b4y 1.20");
            Console.WriteLine(
                "    tool.exe -boyscale" +
                " 00_skeleton.bin" +
                " --b53y 1.30 --b62y 1.30");
            Console.WriteLine(
                "    tool.exe -boyscale" +
                " 00_skeleton.bin" +
                " --legsy 1.25 --armsy 2.00");
            Console.WriteLine(
                "    tool.exe -boyscale" +
                " 00_skeleton.bin" +
                " --b5y 0.80 --b5x 1.40" +
                " --b5z 1.40");
            Console.WriteLine();
        }

        // ═════════════════════════════════════════
        // LOW LEVEL HELPERS
        // ═════════════════════════════════════════
        private static int CountBones(byte[] data)
        {
            int count = 0;
            int pos = 0;
            while (pos + 4 <= data.Length)
            {
                uint val =
                    BitConverter.ToUInt32(
                        data, pos);
                if (val == 0) break;
                count++;
                pos += 4;
            }
            return count;
        }

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
            byte[] data, int off, float val)
        {
            byte[] b = BitConverter.GetBytes(val);
            Array.Copy(b, 0, data, off, 4);
        }

        private static string FloatToHex(float val)
        {
            byte[] b = BitConverter.GetBytes(val);
            return string.Join(" ",
                Array.ConvertAll(b,
                    x => x.ToString("X2")));
        }
    }
}
