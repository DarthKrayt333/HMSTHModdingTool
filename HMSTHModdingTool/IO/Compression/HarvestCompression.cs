using System.IO;

namespace HMSTHModdingTool.IO.Compression
{
    /// <summary>
    ///     Handles the compression format used on the game Harvest Moon: Save the Homeland.
    /// </summary>
    class HarvestCompression
    {
        /// <summary>
        ///     Decompresses data from Harvest Moon: Save the Homeland.
        /// </summary>
        /// <param name="Data">The byte array with the data to be decompressed</param>
        /// <returns>The decompressed data</returns>
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
                        //Direct copy
                        //0x00 ~ 0x0f
                        if ((Length = Header + 3) == 3)
                        {
                            while ((Header = Data[DataOffset++]) == 0) Length += 0xff;
                            Length += Header + 0xf;
                        }

                        Output.Write(Data, DataOffset, Length);
                        DataOffset += Length;
                    }
                    else
                    {
                        //Compressed
                        if (Header < 0x20)
                        {
                            //0x10 ~ 0x1f
                            Back = (Header & 8) << 11;

                            if ((Length = (Header & 7) + 2) == 2)
                            {
                                while ((Header = Data[DataOffset++]) == 0) Length += 0xff;
                                Length += Header + 7;
                            }

                            DirectCopy = Data[DataOffset] & 3;
                            Back = ((Data[DataOffset++] >> 2) | (Data[DataOffset++] << 6) | Back) + 0x4000;
                            if (Back == 0x4000) break; //Compression end
                        }
                        else if (Header < 0x40)
                        {
                            //0x20 ~ 0x3f
                            if ((Length = (Header & 0x1f) + 2) == 2)
                            {
                                while ((Header = Data[DataOffset++]) == 0) Length += 0xff;
                                Length += Header + 0x1f;
                            }

                            DirectCopy = Data[DataOffset] & 3;
                            Back = ((Data[DataOffset++] >> 2) | (Data[DataOffset++] << 6)) + 1;
                        }
                        else
                        {
                            //0x40 ~ 0xff
                            Length = (Header >> 5) + 1;
                            DirectCopy = Header & 3;
                            Back = (((Header >> 2) & 7) | (Data[DataOffset++] << 3)) + 1;
                        }

                        //Go back and writes compressed data
                        long Position = Output.Position;
                        while (Length-- > 0)
                        {
                            Output.Seek(Position - Back, SeekOrigin.Begin);
                            int Value = Output.ReadByte();
                            Output.Seek(Position, SeekOrigin.Begin);
                            Output.WriteByte((byte)Value);
                            Position++;
                        }

                        //Writes remaining direct copy data
                        Output.Write(Data, DataOffset, DirectCopy);
                        DataOffset += DirectCopy;
                    }
                }

                return Output.ToArray();
            }
        }

        //TODO: Compression
    }
}
