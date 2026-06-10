using System;
using System.Collections.Generic;
using System.IO;

namespace HMSTHModdingTool.IO.Compression
{
    class HarvestCompression
    {
        private const int CHAIN = 512;
        private const int THRESH = 9;

        // ==================================================================
        // IS V2 COMPRESSION - Basic structural check only
        // ==================================================================
        public static bool IsV2(byte[] Data)
        {
            if (Data == null || Data.Length < 4)
                return false;
            int fb = Data[0];
            if (fb <= 17 || fb > 240)
                return false;
            int litCount = fb - 17;
            return Data.Length >= 1 + litCount + 3;
        }

        // ==================================================================
        // DECOMPRESS - Simple overload without size hint (keeps existing
        // callers working e.g. -uncompress command)
        // ==================================================================
        public static byte[] Decompress(byte[] Data)
        {
            bool isV2;
            return Decompress(Data, 0, out isV2);
        }

        // ==================================================================
        // DECOMPRESS - With expected size and out param telling caller
        // which format was used. This is the definitive implementation.
        // ==================================================================
        public static byte[] Decompress(
            byte[] Data,
            int expectedDecompSize,
            out bool usedV2)
        {
            usedV2 = false;

            if (IsV2(Data))
            {
                usedV2 = true;
                return DecompressV2(Data);
            }

            return DecompressV1(Data);
        }


        // ==================================================================
        // DECOMPRESS V2 - Exact port of miniLZO lzo1x_decompress_safe
        // ==================================================================
        public static byte[] DecompressV2(byte[] src)
        {
            int ip = 0;
            var o = new List<byte>(src.Length * 4);

            int t = src[ip++];

            if (t > 17)
            {
                int n = t - 17;
                for (int i = 0; i < n; i++)
                    o.Add(src[ip++]);
                t = src[ip++];
                if (t < 16)
                {
                    int mOff = 1 + (t >> 2) + (src[ip++] << 2);
                    int mPos = o.Count - mOff;
                    o.Add(o[mPos]);
                    o.Add(o[mPos + 1]);
                    o.Add(o[mPos + 2]);
                    t = t & 3;
                    if (t > 0)
                    {
                        for (int i = 0; i < t; i++)
                            o.Add(src[ip++]);
                        t = src[ip++];
                    }
                    else
                    {
                        t = src[ip++];
                    }
                }
            }
            else if (t < 16)
            {
                if (t == 0)
                {
                    while (src[ip] == 0) { t += 255; ip++; }
                    t += 15 + src[ip++];
                }
                t += 3;
                for (int i = 0; i < t; i++)
                    o.Add(src[ip++]);
                t = src[ip++];
                if (t < 16)
                {
                    int mOff = 1 + (t >> 2) + (src[ip++] << 2);
                    int mPos = o.Count - mOff;
                    o.Add(o[mPos]);
                    o.Add(o[mPos + 1]);
                    o.Add(o[mPos + 2]);
                    t = t & 3;
                    if (t > 0)
                    {
                        for (int i = 0; i < t; i++)
                            o.Add(src[ip++]);
                        t = src[ip++];
                    }
                    else
                    {
                        t = src[ip++];
                    }
                }
            }

            string mode = "match";

            while (ip < src.Length)
            {
                if (mode == "match")
                {
                    while (true)
                    {
                        if (t >= 64)
                        {
                            int mLen = (t >> 5) + 1;
                            int mOff = 1 + ((t >> 2) & 7)
                                + (src[ip++] << 3);
                            int mPos = o.Count - mOff;
                            for (int i = 0; i < mLen; i++)
                            {
                                o.Add(o[mPos]); mPos++;
                            }
                            t = t & 3;
                        }
                        else if (t >= 32)
                        {
                            int mLen = t & 31;
                            if (mLen == 0)
                            {
                                while (src[ip] == 0)
                                {
                                    mLen += 255; ip++;
                                }
                                mLen += 31 + src[ip++];
                            }
                            mLen += 2;
                            int b1 = src[ip++];
                            int b2 = src[ip++];
                            int mOff = 1 + (b1 >> 2)
                                + (b2 << 6);
                            int mPos = o.Count - mOff;
                            for (int i = 0; i < mLen; i++)
                            {
                                o.Add(o[mPos]); mPos++;
                            }
                            t = b1 & 3;
                        }
                        else if (t >= 16)
                        {
                            int mOffHi = (t & 8) << 11;
                            int mLen = t & 7;
                            if (mLen == 0)
                            {
                                while (src[ip] == 0)
                                {
                                    mLen += 255; ip++;
                                }
                                mLen += 7 + src[ip++];
                            }
                            mLen += 2;
                            int b1 = src[ip++];
                            int b2 = src[ip++];
                            int mOff = (b1 >> 2) + (b2 << 6)
                                + mOffHi;
                            if (mOff == 0)
                                return o.ToArray();
                            mOff += 0x4000;
                            int mPos = o.Count - mOff;
                            for (int i = 0; i < mLen; i++)
                            {
                                o.Add(o[mPos]); mPos++;
                            }
                            t = b1 & 3;
                        }
                        else
                        {
                            int mOff = 1 + (t >> 2)
                                + (src[ip++] << 2);
                            int mPos = o.Count - mOff;
                            o.Add(o[mPos]);
                            o.Add(o[mPos + 1]);
                            t = t & 3;
                        }

                        if (t == 0)
                            break;
                        for (int i = 0; i < t; i++)
                            o.Add(src[ip++]);
                        t = src[ip++];
                    }

                    t = src[ip++];
                    if (t >= 16)
                    {
                        mode = "match";
                        continue;
                    }
                    mode = "outer_lit";
                    continue;
                }
                else if (mode == "outer_lit")
                {
                    if (t == 0)
                    {
                        int x = 0;
                        while (src[ip] == 0)
                        {
                            x += 255; ip++;
                        }
                        t = x + 15 + src[ip++];
                    }
                    t += 3;
                    for (int i = 0; i < t; i++)
                        o.Add(src[ip++]);

                    t = src[ip++];
                    if (t >= 16)
                    {
                        mode = "match";
                        continue;
                    }

                    int mOff2 = 1 + 0x800 + (t >> 2)
                        + (src[ip++] << 2);
                    int mPos2 = o.Count - mOff2;
                    o.Add(o[mPos2]);
                    o.Add(o[mPos2 + 1]);
                    o.Add(o[mPos2 + 2]);
                    t = t & 3;

                    if (t == 0)
                    {
                        t = src[ip++];
                        if (t >= 16)
                            mode = "match";
                        else
                            mode = "outer_lit";
                        continue;
                    }
                    else
                    {
                        for (int i = 0; i < t; i++)
                            o.Add(src[ip++]);
                        t = src[ip++];
                        mode = "match";
                        continue;
                    }
                }
            }

            return o.ToArray();
        }


        // ==================================================================
        // DECOMPRESS V1 - Exact original logic
        // ==================================================================
        public static byte[] DecompressV1(byte[] Data)
        {
            int DataOffset = 0;
            using (MemoryStream Output = new MemoryStream())
            {
                while (DataOffset < Data.Length)
                {
                    int Back, Length, DirectCopy;
                    byte Header = Data[DataOffset++];

                    if (Header < 0x10)
                    {
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
                        if (Header < 0x20)
                        {
                            Back = (Header & 8) << 11;
                            if ((Length = (Header & 7) + 2) == 2)
                            {
                                while ((Header = Data[DataOffset++]) == 0)
                                    Length += 0xff;
                                Length += Header + 7;
                            }
                            DirectCopy = Data[DataOffset] & 3;
                            Back = ((Data[DataOffset++] >> 2) |
                                    (Data[DataOffset++] << 6) | Back) + 0x4000;
                            if (Back == 0x4000) break;
                        }
                        else if (Header < 0x40)
                        {
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
                            Length = (Header >> 5) + 1;
                            DirectCopy = Header & 3;
                            Back = (((Header >> 2) & 7) |
                                    (Data[DataOffset++] << 3)) + 1;
                        }

                        long Position = Output.Position;
                        while (Length-- > 0)
                        {
                            Output.Seek(Position - Back, SeekOrigin.Begin);
                            int Value = Output.ReadByte();
                            Output.Seek(Position, SeekOrigin.Begin);
                            Output.WriteByte((byte)Value);
                            Position++;
                        }

                        Output.Write(Data, DataOffset, DirectCopy);
                        DataOffset += DirectCopy;
                    }
                }

                return Output.ToArray();
            }
        }


        // ==================================================================
        // COMPRESS - Main compressor with fixed ChooseDc parameter name
        // ==================================================================
        public static byte[] Compress(byte[] data, Action<int, int> progressCallback = null)
        {
            int n = data.Length;
            if (n == 0) return new byte[] { 0x11, 0x00, 0x00 };

            List<byte> outBuf = new List<byte>(n);
            Dictionary<int, List<int>> ht = new Dictionary<int, List<int>>();
            int pos = 0;
            List<byte> litBuf = new List<byte>();
            int lastPct = -1;

            void EmitExt(int remain)
            {
                int zeros = (remain - 1) / 255;
                int final = remain - zeros * 255;
                for (int z = 0; z < zeros; z++) outBuf.Add(0);
                outBuf.Add((byte)final);
            }

            void FlushLit()
            {
                int c = litBuf.Count;
                if (c <= 18)
                    outBuf.Add((byte)(c - 3));
                else
                {
                    outBuf.Add(0x00);
                    EmitExt(c - 18);
                }
                outBuf.AddRange(litBuf);
                litBuf.Clear();
            }

            string PickType(int ln, int bk)
            {
                if (ln < 3 || bk < 1) return "";
                if (bk <= 2048)
                {
                    if (ln >= 3 && ln <= 8 && ln < THRESH) return "T3";
                    if (ln >= 3) return "T2";
                    return "";
                }
                if (bk <= 0x4000) return "T2";
                if (bk <= 0xBFFF) return "T1";
                return "";
            }

            bool EmitMatch(int ml, int mb, int dc, byte[] db)
            {
                string kind = PickType(ml, mb);
                if (kind == "") return false;

                if (kind == "T3")
                {
                    int eb = mb - 1;
                    int hdr = ((ml - 1) << 5) | ((eb & 7) << 2) | (dc & 3);
                    outBuf.Add((byte)hdr);
                    outBuf.Add((byte)((eb >> 3) & 0xFF));
                }
                else if (kind == "T2")
                {
                    int el = ml - 2;
                    if (el >= 1 && el <= 0x1F)
                        outBuf.Add((byte)(0x20 | el));
                    else
                    {
                        outBuf.Add(0x20);
                        EmitExt(ml - 33);
                    }
                    int eb = mb - 1;
                    outBuf.Add((byte)(((eb & 0x3F) << 2) | (dc & 3)));
                    outBuf.Add((byte)((eb >> 6) & 0xFF));
                }
                else if (kind == "T1")
                {
                    int bkHiBit = (mb > 0x7FFF) ? 1 : 0;
                    int partial = (mb <= 0x7FFF) ? mb - 0x4000 : mb - 0x8000;
                    int el = ml - 2;
                    if (el >= 1 && el <= 7)
                        outBuf.Add((byte)(0x10 | (bkHiBit << 3) | (el & 7)));
                    else
                    {
                        outBuf.Add((byte)(0x10 | (bkHiBit << 3)));
                        EmitExt(ml - 9);
                    }
                    outBuf.Add((byte)(((partial & 0x3F) << 2) | (dc & 3)));
                    outBuf.Add((byte)((partial >> 6) & 0xFF));
                }
                for (int i = 0; i < dc; i++) outBuf.Add(db[i]);
                return true;
            }

            int MFKey(int p) => (data[p] << 16) | (data[p + 1] << 8) | data[p + 2];

            void MFAdd(int p)
            {
                if (p + 2 >= n) return;
                int k = MFKey(p);
                if (!ht.TryGetValue(k, out List<int> c))
                {
                    c = new List<int>();
                    ht[k] = c;
                }
                c.Add(p);
                if (c.Count > CHAIN)
                    ht[k] = new List<int>(c.GetRange(c.Count - CHAIN, CHAIN));
            }

            void MFFind(int p, out int bl, out int bb)
            {
                bl = 0; bb = 0;
                if (p + 2 >= n) return;
                int k = MFKey(p);
                if (!ht.TryGetValue(k, out List<int> cands) || cands.Count == 0) return;

                int limit = Math.Min(n - p, 0x4000);
                int checkedCount = 0;
                for (int ci = cands.Count - 1; ci >= 0; ci--)
                {
                    int cp = cands[ci];
                    if (cp >= p) continue;
                    int bk = p - cp;
                    if (bk < 1 || bk > 0xBFFF) continue;

                    int ml = 0;
                    while (ml < limit && data[cp + ml] == data[p + ml]) ml++;

                    if (ml > bl)
                    {
                        bl = ml; bb = bk;
                        if (bl >= 32768) break;
                    }
                    checkedCount++;
                    if (checkedCount >= CHAIN) break;
                }
            }

            int ChooseDc(int pos_param, int ml_param)
            {
                int me = pos_param + ml_param;
                if (me >= n) return 0;
                int tail = n - me;
                if (tail >= 1 && tail <= 3) return tail;
                for (int gap = 1; gap <= 3; gap++)
                {
                    int np = me + gap;
                    if (np >= n) return Math.Min(gap, 3);
                    int ml2, mb2;
                    MFFind(np, out ml2, out mb2);
                    if (PickType(ml2, mb2) != "") return gap;
                }
                return 0;
            }

            while (pos < n)
            {
                if (progressCallback != null)
                {
                    int pct = (int)((long)pos * 100 / n);
                    if (pct != lastPct) { lastPct = pct; progressCallback(pos, n); }
                }

                MFAdd(pos);
                int ml, mb;
                MFFind(pos, out ml, out mb);
                string kind = PickType(ml, mb);

                if (kind == "") { litBuf.Add(data[pos++]); continue; }
                if (litBuf.Count > 0 && litBuf.Count < 4) { litBuf.Add(data[pos++]); continue; }
                if (litBuf.Count >= 4) FlushLit();

                int dc = ChooseDc(pos, ml);
                int me = pos + ml;
                dc = Math.Min(dc, Math.Max(0, n - me));

                byte[] db = new byte[dc];
                for (int i = 0; i < dc; i++) db[i] = data[me + i];

                if (!EmitMatch(ml, mb, dc, db)) { litBuf.Add(data[pos++]); continue; }

                int lim = Math.Min(me + dc, n);
                for (int i = pos + 1; i < lim; i++) MFAdd(i);
                pos = me + dc;
            }

            if (litBuf.Count > 0)
            {
                while (litBuf.Count < 4) litBuf.Add(litBuf[litBuf.Count - 1]);
                FlushLit();
            }

            outBuf.AddRange(new byte[] { 0x11, 0x00, 0x00 });
            return outBuf.ToArray();
        }

        // ==================================================================
        // SINGLE LITERAL STREAM - Fixed minimal overhead version
        // ==================================================================
        public static byte[] CompressAsLiterals(byte[] Data)
        {
            int n = Data.Length;
            if (n == 0) return new byte[] { 0x11, 0x00, 0x00 };

            byte[] payload = Data;
            if (n < 4)
            {
                payload = new byte[4];
                Array.Copy(Data, 0, payload, 0, n);
                for (int i = n; i < 4; i++) payload[i] = Data[n - 1];
            }

            using (MemoryStream ms = new MemoryStream())
            {
                int len = payload.Length;
                if (len <= 18) ms.WriteByte((byte)(len - 3));
                else
                {
                    ms.WriteByte(0x00);
                    int remain = len - 18;
                    int zeros = (remain - 1) / 255;
                    int finalByte = remain - zeros * 255;
                    for (int z = 0; z < zeros; z++) ms.WriteByte(0);
                    ms.WriteByte((byte)finalByte);
                }
                ms.Write(payload, 0, payload.Length);
                ms.Write(new byte[] { 0x11, 0x00, 0x00 }, 0, 3);
                return ms.ToArray();
            }
        }

        public static bool VerifyRoundTrip(byte[] raw, byte[] compressed)
        {
            try
            {
                byte[] test = Decompress(compressed);
                if (test.Length != raw.Length) return false;
                for (int i = 0; i < raw.Length; i++) if (test[i] != raw[i]) return false;
                return true;
            }
            catch { return false; }
        }
    }
}
