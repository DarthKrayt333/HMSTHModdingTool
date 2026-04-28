using System;
using System.IO;

namespace HMSTHModdingTool.IO.Compression
{
    /// <summary>
    ///     Handles the compression format used on the game
    ///     Harvest Moon: Save the Homeland.
    /// </summary>
    class HarvestCompression
    {
        // ═══════════════════════════════════════════════════════
        // DECOMPRESS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        ///     Decompresses data from Harvest Moon: Save the Homeland.
        /// </summary>
        public static byte[] Decompress(byte[] Data)
        {
            int DataOffset = 0;

            using (MemoryStream Output = new MemoryStream())
            {
                while (DataOffset < Data.Length)
                {
                    int Back;
                    int Length;
                    int DirectCopy;
                    byte Header = Data[DataOffset++];

                    if (Header < 0x10)
                    {
                        // Direct copy 0x00 ~ 0x0f
                        if ((Length = Header + 3) == 3)
                        {
                            while ((Header = Data[DataOffset++]) == 0)
                                Length += 0xff;
                            Length += Header + 0xf;
                        }

                        Output.Write(Data, DataOffset, Length);
                        DataOffset += Length;
                    }
                    else
                    {
                        // Compressed
                        if (Header < 0x20)
                        {
                            // 0x10 ~ 0x1f
                            Back = (Header & 8) << 11;

                            if ((Length = (Header & 7) + 2) == 2)
                            {
                                while ((Header = Data[DataOffset++]) == 0)
                                    Length += 0xff;
                                Length += Header + 7;
                            }

                            DirectCopy = Data[DataOffset] & 3;
                            Back = ((Data[DataOffset++] >> 2) |
                                    (Data[DataOffset++] << 6) | Back)
                                   + 0x4000;

                            if (Back == 0x4000) break; // End
                        }
                        else if (Header < 0x40)
                        {
                            // 0x20 ~ 0x3f
                            if ((Length = (Header & 0x1f) + 2) == 2)
                            {
                                while ((Header = Data[DataOffset++]) == 0)
                                    Length += 0xff;
                                Length += Header + 0x1f;
                            }

                            DirectCopy = Data[DataOffset] & 3;
                            Back = ((Data[DataOffset++] >> 2) |
                                    (Data[DataOffset++] << 6)) + 1;
                        }
                        else
                        {
                            // 0x40 ~ 0xff
                            Length = (Header >> 5) + 1;
                            DirectCopy = Header & 3;
                            Back = (((Header >> 2) & 7) |
                                            (Data[DataOffset++] << 3)) + 1;
                        }

                        // Go back and write compressed data
                        long Position = Output.Position;
                        while (Length-- > 0)
                        {
                            Output.Seek(Position - Back, SeekOrigin.Begin);
                            int Value = Output.ReadByte();
                            Output.Seek(Position, SeekOrigin.Begin);
                            Output.WriteByte((byte)Value);
                            Position++;
                        }

                        // Write remaining direct copy data
                        Output.Write(Data, DataOffset, DirectCopy);
                        DataOffset += DirectCopy;
                    }
                }

                return Output.ToArray();
            }
        }

        // ═══════════════════════════════════════════════════════
        // COMPRESS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        ///     Compresses data for Harvest Moon: Save the Homeland.
        ///
        ///     Format rules (derived from decompressor):
        ///
        ///     Literal run (header 0x01..0x0F):
        ///         length = header + 3   → range 4..18
        ///     Literal run extended (header 0x00):
        ///         length = 18 + zeros*255 + final   (final 1..255)
        ///         NOTE: final byte must NEVER be 0 —
        ///               the decompressor treats 0 as another +255 chunk.
        ///
        ///     Type 3 (header 0x40-0xFF):
        ///         length = (header >> 5) + 1   → 3..8
        ///         back   = (((header>>2)&7) | (next<<3)) + 1  → 1..2048
        ///
        ///     Type 2 (header 0x20-0x3F):
        ///         length direct  = (header & 0x1F) + 2  → 3..33
        ///         length extended: 33 + zeros*255 + final
        ///         back   = ((b0>>2) | (b1<<6)) + 1  → 1..0x4000
        ///
        ///     Type 1 (header 0x10-0x1F):
        ///         length direct  = (header & 7) + 2  → 3..9
        ///         length extended: 9 + zeros*255 + final
        ///         back   = ((b0>>2) | (b1<<6) | bk_hi) + 0x4000
        ///                  → 0x4001..0xBFFF
        ///
        ///     EOS: header=0x11, b0=0x00, b1=0x00
        ///         → back = 0x4000 triggers break in decompressor
        /// </summary>
        public static byte[] Compress(
            byte[] Data,
            Action<int, int> progressCallback = null)
        {
            int n = Data.Length;

            if (n == 0)
                return new byte[] { 0x11, 0x00, 0x00 };

            using (MemoryStream out_ = new MemoryStream())
            {
                // ── Hash table for 3-byte matches ────────────────
                // Key  = (b0 << 16) | (b1 << 8) | b2
                // Value = list of positions
                const int CHAIN = 512;
                var ht = new System.Collections.Generic.Dictionary<int,
                    System.Collections.Generic.List<int>>();

                int pos = 0;
                var litBuf = new System.Collections.Generic.List<byte>(64);
                int lastPct = -1;

                // ── Helpers ──────────────────────────────────────

                // Emit extension bytes where final must be 1..255
                void EmitExt(int remain)
                {
                    // remain = zeros*255 + final,  final in 1..255
                    int zeros = (remain - 1) / 255;
                    int final = remain - zeros * 255;   // 1..255
                    for (int z = 0; z < zeros; z++)
                        out_.WriteByte(0);
                    out_.WriteByte((byte)final);
                }

                void FlushLit()
                {
                    int c = litBuf.Count;
                    if (c == 0) return;

                    // Minimum encodable literal = 4 bytes
                    // Pad at EOF only (decompressor stops at EOS)
                    while (c < 4) { litBuf.Add(litBuf[c - 1]); c++; }

                    if (c <= 18)
                    {
                        out_.WriteByte((byte)(c - 3));  // 1..15
                    }
                    else
                    {
                        out_.WriteByte(0);
                        EmitExt(c - 18);
                    }
                    foreach (byte b in litBuf)
                        out_.WriteByte(b);
                    litBuf.Clear();
                }

                bool EmitMatch(int ml, int mb, int dc, byte[] db)
                {
                    if (ml < 3 || mb < 1) return false;

                    if (mb <= 2048 && ml <= 8)
                    {
                        // Type 3
                        int eb = mb - 1;
                        int hdr = ((ml - 1) << 5) |
                                  ((eb & 7) << 2) | (dc & 3);
                        if (hdr < 0x40) return false;
                        out_.WriteByte((byte)hdr);
                        out_.WriteByte((byte)((eb >> 3) & 0xFF));
                    }
                    else if (mb <= 0x4000)
                    {
                        // Type 2
                        int el = ml - 2;    // 1..variable
                        if (el < 1) return false;
                        if (el <= 0x1F)
                        {
                            out_.WriteByte((byte)(0x20 | el));
                        }
                        else
                        {
                            out_.WriteByte(0x20);
                            EmitExt(ml - 33);
                        }
                        int eb = mb - 1;
                        out_.WriteByte((byte)(((eb & 0x3F) << 2) | (dc & 3)));
                        out_.WriteByte((byte)((eb >> 6) & 0xFF));
                    }
                    else if (mb <= 0xBFFF)
                    {
                        // Type 1
                        int bkHiBit = (mb > 0x7FFF) ? 1 : 0;
                        int partial = (mb <= 0x7FFF)
                            ? mb - 0x4000
                            : mb - 0x8000;
                        int el = ml - 2;
                        if (el < 1) return false;
                        if (el <= 7)
                        {
                            out_.WriteByte(
                                (byte)(0x10 | (bkHiBit << 3) | (el & 7)));
                        }
                        else
                        {
                            out_.WriteByte(
                                (byte)(0x10 | (bkHiBit << 3)));
                            EmitExt(ml - 9);
                        }
                        out_.WriteByte(
                            (byte)(((partial & 0x3F) << 2) | (dc & 3)));
                        out_.WriteByte(
                            (byte)((partial >> 6) & 0xFF));
                    }
                    else
                    {
                        return false;
                    }

                    // Direct-copy bytes after match
                    for (int i = 0; i < dc && i < db.Length; i++)
                        out_.WriteByte(db[i]);

                    return true;
                }

                int Key(int p) =>
                    (Data[p] << 16) | (Data[p + 1] << 8) | Data[p + 2];

                void AddHash(int p)
                {
                    if (p + 2 >= n) return;
                    int k = Key(p);
                    System.Collections.Generic.List<int> chain;
                    if (!ht.TryGetValue(k, out chain))
                    {
                        chain = new System.Collections.Generic.List<int>(
                            CHAIN + 1);
                        ht[k] = chain;
                    }
                    chain.Add(p);
                    if (chain.Count > CHAIN)
                        chain.RemoveAt(0);
                }

                (int ml, int mb) FindMatch(int p)
                {
                    if (p + 2 >= n) return (0, 0);
                    int k = Key(p);
                    System.Collections.Generic.List<int> cands;
                    if (!ht.TryGetValue(k, out cands) ||
                        cands.Count == 0)
                        return (0, 0);

                    int bl = 0, bb = 0;
                    int limit = Math.Min(n - p, 0x4000);
                    int checked_ = 0;

                    for (int ci = cands.Count - 1; ci >= 0; ci--)
                    {
                        int cp = cands[ci];
                        if (cp >= p) continue;
                        int bk = p - cp;
                        if (bk < 1 || bk > 0xBFFF) continue;

                        int ml = 0;
                        while (ml < limit &&
                               Data[cp + ml] == Data[p + ml])
                            ml++;

                        if (ml > bl) { bl = ml; bb = bk; }
                        if (bl >= 32768) break;

                        checked_++;
                        if (checked_ >= CHAIN) break;
                    }

                    return (bl, bb);
                }

                string PickType(int ml, int mb)
                {
                    if (ml < 3 || mb < 1) return "";
                    if (mb <= 2048 && ml <= 8) return "T3";
                    if (mb <= 0x4000) return "T2";
                    if (mb <= 0xBFFF) return "T1";
                    return "";
                }

                // ── Report progress ───────────────────────────────
                void ReportPct(int cur)
                {
                    if (progressCallback == null) return;
                    int pct = (int)((long)cur * 100 / n);
                    if (pct == lastPct) return;
                    lastPct = pct;
                    progressCallback(cur, n);
                }

                // ── Main compression loop ─────────────────────────
                while (pos < n)
                {
                    ReportPct(pos);

                    AddHash(pos);
                    var (ml, mb) = FindMatch(pos);
                    string kind = PickType(ml, mb);

                    if (kind == "")
                    {
                        litBuf.Add(Data[pos++]);
                        continue;
                    }

                    int bufLen = litBuf.Count;

                    if (bufLen >= 1 && bufLen <= 3)
                    {
                        // Cannot flush (need ≥4) — skip match,
                        // accumulate more literals
                        litBuf.Add(Data[pos++]);
                        continue;
                    }

                    if (bufLen >= 4)
                        FlushLit();

                    // Determine dc (trailing direct-copy)
                    int me = pos + ml;
                    int dc = 0;
                    if (me < n)
                    {
                        int left = n - me;
                        if (left >= 1 && left <= 3)
                            dc = left;
                    }
                    dc = Math.Min(dc, n - me);

                    byte[] db = new byte[dc];
                    if (dc > 0)
                        Array.Copy(Data, me, db, 0, dc);

                    if (!EmitMatch(ml, mb, dc, db))
                    {
                        litBuf.Add(Data[pos++]);
                        continue;
                    }

                    for (int i = pos + 1;
                         i < Math.Min(me + dc, n);
                         i++)
                        AddHash(i);

                    pos = me + dc;
                }

                // ── Flush remaining literals ──────────────────────
                FlushLit();

                // ── EOS ──────────────────────────────────────────
                out_.WriteByte(0x11);
                out_.WriteByte(0x00);
                out_.WriteByte(0x00);

                ReportPct(n);
                return out_.ToArray();
            }
        }
    }
}
