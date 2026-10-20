using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Drawing.Imaging;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    /// Converts the OBJ/MTL files produced by the existing RDTB/SRDB
    /// viewers into self-contained glTF 2.0 binary files (GLB).
    /// Geometry decoding remains in the project's existing model parsers.
    /// </summary>
    internal static class ObjGltfExporter
    {
        private const uint GlbMagic = 0x46546C67;
        private const uint JsonChunkType = 0x4E4F534A;
        private const uint BinChunkType = 0x004E4942;

        private sealed class V2
        {
            public float X;
            public float Y;
            public V2(float x, float y) { X = x; Y = y; }
        }

        private sealed class V3
        {
            public float X;
            public float Y;
            public float Z;
            public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
        }

        private sealed class MaterialInfo
        {
            public string Name;
            public string DiffuseMap;
            public float R = 1.0f;
            public float G = 1.0f;
            public float B = 1.0f;
            public float A = 1.0f;
            public int TextureIndex = -1;
            public bool HasTextureAlpha;

            public MaterialInfo(string name) { Name = name; }
        }

        private sealed class ObjVertex
        {
            public V3 Position;
            public V3 Normal;
            public V2 Uv;
            public int SourcePosition;

            public ObjVertex(V3 p, V2 uv, V3 n, int sourcePosition)
            {
                Position = p;
                Uv = uv;
                Normal = n;
                SourcePosition = sourcePosition;
            }
        }

        private sealed class PrimitiveInfo
        {
            public string MaterialName;
            public List<ObjVertex> Vertices = new List<ObjVertex>();
            public List<uint> Indices = new List<uint>();
            public Dictionary<string, int> VertexMap =
                new Dictionary<string, int>(StringComparer.Ordinal);
            public int PositionAccessor;
            public int NormalAccessor;
            public int UvAccessor;
            public int IndexAccessor;
            public int MaterialIndex;

            public PrimitiveInfo(string materialName) { MaterialName = materialName; }
        }

        private sealed class BufferViewInfo
        {
            public int Offset;
            public int Length;
            public int Target;
            public bool HasTarget;
        }

        private sealed class AccessorInfo
        {
            public int BufferView;
            public int ComponentType;
            public int Count;
            public string Type;
            public float[] Min;
            public float[] Max;
        }

        private sealed class EmbeddedImageInfo
        {
            public byte[] Bytes;
            public string MimeType;
            public bool HasAlpha;
            public int BufferView;
        }

        /// <summary>
        /// Exports viewable model OBJ files in the standard output folders.
        /// Per-batch OBJs are skipped because they are working parts, not complete models.
        /// </summary>
        public static int ExportModelFolders(string outputRoot, string baseName)
        {
            if (string.IsNullOrWhiteSpace(outputRoot) ||
                !Directory.Exists(outputRoot))
                throw new DirectoryNotFoundException(
                    "Model output folder not found: " + outputRoot);

            string[] suffixes = new string[]
            {
                "_all_obj", "_obj", "_embedded_rdtbs_obj", "_3d_batches_obj"
            };
            var candidates = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string suffix in suffixes)
            {
                string folder = Path.Combine(outputRoot, baseName + suffix);
                if (!Directory.Exists(folder)) continue;
                foreach (string path in Directory.GetFiles(
                    folder, "*.obj", SearchOption.AllDirectories))
                {
                    string stem = Path.GetFileNameWithoutExtension(path);
                    if (Regex.IsMatch(stem, @"^batch[_-]?\d+$",
                        RegexOptions.IgnoreCase))
                        continue;
                    if (stem.StartsWith("_source", StringComparison.OrdinalIgnoreCase))
                        continue;
                    candidates.Add(Path.GetFullPath(path));
                }
            }

            if (candidates.Count == 0)
            {
                Console.WriteLine("[!] No complete OBJ model was found in the standard output folders.");
                return 0;
            }

            int exported = 0;
            foreach (string objPath in candidates)
            {
                string glbPath = Path.ChangeExtension(objPath, ".glb");
                try
                {
                    ConvertObjToGlb(objPath, glbPath);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[GLB] " + glbPath);
                    Console.ResetColor();
                    exported++;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[GLB] Failed: " + objPath);
                    Console.WriteLine("      " + ex.Message);
                    Console.ResetColor();
                }
            }

            Console.WriteLine("[OK] GLB export: " + exported + " file(s) created.");
            return exported;
        }

        public static void ConvertObjToGlb(string objPath, string glbPath)
        {
            if (string.IsNullOrWhiteSpace(objPath) || !File.Exists(objPath))
                throw new FileNotFoundException("OBJ file not found.", objPath);
            if (string.IsNullOrWhiteSpace(glbPath))
                throw new ArgumentException("GLB output path cannot be empty.", "glbPath");

            string fullObjPath = Path.GetFullPath(objPath);
            string fullGlbPath = Path.GetFullPath(glbPath);
            if (string.Equals(fullObjPath, fullGlbPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("GLB output path must not overwrite the input OBJ file.");

            string outputDirectory = Path.GetDirectoryName(fullGlbPath);
            if (string.IsNullOrEmpty(outputDirectory)) outputDirectory = ".";
            Directory.CreateDirectory(outputDirectory);

            List<V3> positions = new List<V3>();
            List<V2> uvs = new List<V2>();
            List<V3> normals = new List<V3>();
            List<PrimitiveInfo> primitives = new List<PrimitiveInfo>();
            var byMaterial = new Dictionary<string, PrimitiveInfo>(StringComparer.Ordinal);
            var materials = new Dictionary<string, MaterialInfo>(StringComparer.Ordinal);
            var mtllibs = new List<string>();
            string activeMaterial = "material_default";
            bool anyCornerHasNormal = false;
            bool anyCornerMissingNormal = false;

            using (var reader = new StreamReader(objPath, Encoding.UTF8, true))
            {
                string line;
                int lineNumber = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed[0] == '#') continue;
                    int space = trimmed.IndexOfAny(new char[] { ' ', '\t' });
                    string keyword = space < 0 ? trimmed : trimmed.Substring(0, space);
                    string payload = space < 0 ? string.Empty : trimmed.Substring(space + 1).Trim();
                    if (keyword == "v")
                    {
                        string[] p = SplitFields(payload);
                        if (p.Length < 3) continue;
                        positions.Add(new V3(ParseFloat(p[0]), ParseFloat(p[1]), ParseFloat(p[2])));
                    }
                    else if (keyword == "vt")
                    {
                        string[] p = SplitFields(payload);
                        if (p.Length < 2) continue;
                        uvs.Add(new V2(ParseFloat(p[0]), ParseFloat(p[1])));
                    }
                    else if (keyword == "vn")
                    {
                        string[] p = SplitFields(payload);
                        if (p.Length < 3) continue;
                        normals.Add(new V3(ParseFloat(p[0]), ParseFloat(p[1]), ParseFloat(p[2])));
                    }
                    else if (keyword == "mtllib")
                    {
                        foreach (string file in SplitFields(payload))
                        {
                            string path = Path.GetFullPath(Path.Combine(
                                Path.GetDirectoryName(Path.GetFullPath(objPath)), file));
                            if (!mtllibs.Contains(path, StringComparer.OrdinalIgnoreCase))
                                mtllibs.Add(path);
                        }
                    }
                    else if (keyword == "usemtl")
                    {
                        activeMaterial = payload.Length > 0 ? payload : "material_default";
                    }
                    else if (keyword == "f")
                    {
                        string[] tokens = SplitFields(payload);
                        if (tokens.Length < 3) continue;
                        PrimitiveInfo primitive = GetPrimitive(
                            activeMaterial, byMaterial, primitives);
                        var polygon = new List<int>();
                        foreach (string token in tokens)
                        {
                            int vi, ti, ni;
                            ParseFaceIndex(token, positions.Count, uvs.Count,
                                normals.Count, out vi, out ti, out ni);
                            if (ni >= 0) anyCornerHasNormal = true;
                            else anyCornerMissingNormal = true;
                            string key = vi.ToString(CultureInfo.InvariantCulture) + "/" +
                                ti.ToString(CultureInfo.InvariantCulture) + "/" +
                                ni.ToString(CultureInfo.InvariantCulture);
                            int localIndex;
                            if (!primitive.VertexMap.TryGetValue(key, out localIndex))
                            {
                                V3 p = positions[vi];
                                V2 uv = ti >= 0 ? uvs[ti] : new V2(0, 0);
                                V3 n = ni >= 0 ? normals[ni] : new V3(0, 0, 0);
                                localIndex = primitive.Vertices.Count;
                                primitive.Vertices.Add(new ObjVertex(p, uv, n, vi));
                                primitive.VertexMap.Add(key, localIndex);
                            }
                            polygon.Add(localIndex);
                        }
                        // OBJ permits polygons. Fan triangulation is stable for the convex
                        // triangle strips/faces written by HMSTHModdingTool.
                        for (int i = 1; i + 1 < polygon.Count; i++)
                        {
                            int a = polygon[0], b = polygon[i], c = polygon[i + 1];
                            if (a == b || b == c || a == c) continue;
                            primitive.Indices.Add((uint)a);
                            primitive.Indices.Add((uint)b);
                            primitive.Indices.Add((uint)c);
                        }
                    }
                }
                if (positions.Count == 0)
                    throw new InvalidDataException("OBJ contains no vertex positions.");
            }

            if (primitives.Count == 0 || primitives.All(p => p.Indices.Count == 0))
                throw new InvalidDataException("OBJ contains no usable triangle faces.");

            foreach (string mtlPath in mtllibs)
                if (File.Exists(mtlPath)) LoadMtl(mtlPath, materials);

            // RDTB small/embedded exports can intentionally use a constant fallback normal.
            // Recalculate normals in that case while preserving authored normals otherwise.
            bool normalsAreConstant = normals.Count > 0 && AllNormalsNearlyEqual(normals);
            bool recalculateNormals = !anyCornerHasNormal ||
                anyCornerMissingNormal || normalsAreConstant;
            if (recalculateNormals)
            {
                RecalculateNormals(positions.Count, primitives);
                Console.WriteLine("[GLB] Recalculated surface normals for reliable lighting.");
            }

            // Load unique images and convert BMPs to PNG (glTF only mandates PNG/JPEG).
            var images = new List<EmbeddedImageInfo>();
            var imageByPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (MaterialInfo material in materials.Values)
            {
                if (string.IsNullOrWhiteSpace(material.DiffuseMap)) continue;
                string imagePath = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(objPath)), material.DiffuseMap));
                if (!File.Exists(imagePath))
                {
                    // Paths in some MTLs are relative to the MTL file rather than OBJ.
                    foreach (string mtlPath in mtllibs)
                    {
                        string candidate = Path.GetFullPath(Path.Combine(
                            Path.GetDirectoryName(mtlPath), material.DiffuseMap));
                        if (File.Exists(candidate)) { imagePath = candidate; break; }
                    }
                }
                if (!File.Exists(imagePath))
                {
                    Console.WriteLine("[!] Texture missing for material '" + material.Name + "': " + material.DiffuseMap);
                    continue;
                }

                int imageIndex;
                if (!imageByPath.TryGetValue(imagePath, out imageIndex))
                {
                    EmbeddedImageInfo image = LoadImage(imagePath);
                    imageIndex = images.Count;
                    images.Add(image);
                    imageByPath.Add(imagePath, imageIndex);
                }
                material.TextureIndex = imageIndex;
                material.HasTextureAlpha = images[imageIndex].HasAlpha;
            }

            // If a material has no MTL entry, create a neutral fallback material.
            foreach (PrimitiveInfo primitive in primitives)
            {
                if (!materials.ContainsKey(primitive.MaterialName))
                    materials.Add(primitive.MaterialName,
                        new MaterialInfo(primitive.MaterialName));
            }
            var materialList = materials.Values.ToList();
            var materialIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < materialList.Count; i++)
                materialIndices[materialList[i].Name] = i;
            foreach (PrimitiveInfo primitive in primitives)
                primitive.MaterialIndex = materialIndices[primitive.MaterialName];

            byte[] glbBytes = BuildGlb(
                Path.GetFileNameWithoutExtension(objPath),
                primitives, materialList, images);
            WriteAllBytesAtomically(fullGlbPath, glbBytes);
        }

        // Build into a temporary sibling first, then swap it into place. If parsing,
        // image conversion, or disk writing fails, an existing valid GLB remains intact.
        private static void WriteAllBytesAtomically(string destinationPath, byte[] data)
        {
            string directory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrEmpty(directory)) directory = ".";
            string temporaryPath = Path.Combine(directory,
                Path.GetFileName(destinationPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                {
                    stream.Write(data, 0, data.Length);
                    stream.Flush(true);
                }

                if (File.Exists(destinationPath))
                    File.Replace(temporaryPath, destinationPath, null);
                else
                    File.Move(temporaryPath, destinationPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try { File.Delete(temporaryPath); }
                    catch { /* Preserve the primary export error. */ }
                }
            }
        }

        private static string[] SplitFields(string value)
        {
            return (value ?? string.Empty).Split(
                (char[])null, StringSplitOptions.RemoveEmptyEntries);
        }

        private static float ParseFloat(string value)
        {
            float number;
            if (!float.TryParse(value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out number) ||
                float.IsNaN(number) || float.IsInfinity(number))
                throw new InvalidDataException("Invalid floating point value in OBJ: " + value);
            return number;
        }

        private static int ParseObjIndex(string value, int count, string kind)
        {
            if (string.IsNullOrEmpty(value)) return -1;
            int parsed;
            if (!int.TryParse(value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out parsed) || parsed == 0)
                throw new InvalidDataException("Invalid OBJ " + kind + " index: " + value);
            int index = parsed > 0 ? parsed - 1 : count + parsed;
            if (index < 0 || index >= count)
                throw new InvalidDataException("OBJ " + kind + " index is out of range: " + value);
            return index;
        }

        private static void ParseFaceIndex(string token,
            int positionCount, int uvCount, int normalCount,
            out int vi, out int ti, out int ni)
        {
            string[] parts = token.Split('/');
            vi = ParseObjIndex(parts[0], positionCount, "vertex");
            ti = parts.Length > 1 ? ParseObjIndex(parts[1], uvCount, "UV") : -1;
            ni = parts.Length > 2 ? ParseObjIndex(parts[2], normalCount, "normal") : -1;
        }

        private static PrimitiveInfo GetPrimitive(string materialName,
            Dictionary<string, PrimitiveInfo> byMaterial,
            List<PrimitiveInfo> primitives)
        {
            PrimitiveInfo result;
            if (!byMaterial.TryGetValue(materialName, out result))
            {
                result = new PrimitiveInfo(materialName);
                byMaterial.Add(materialName, result);
                primitives.Add(result);
            }
            return result;
        }

        private static void LoadMtl(string path, Dictionary<string, MaterialInfo> materials)
        {
            MaterialInfo current = null;
            using (var reader = new StreamReader(path, Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed[0] == '#') continue;
                    int space = trimmed.IndexOfAny(new char[] { ' ', '\t' });
                    string keyword = space < 0 ? trimmed : trimmed.Substring(0, space);
                    string payload = space < 0 ? string.Empty : trimmed.Substring(space + 1).Trim();
                    if (keyword == "newmtl")
                    {
                        string name = payload.Length > 0 ? payload : "material_default";
                        if (!materials.TryGetValue(name, out current))
                        {
                            current = new MaterialInfo(name);
                            materials.Add(name, current);
                        }
                    }
                    else if (current != null && keyword == "Kd")
                    {
                        string[] p = SplitFields(payload);
                        if (p.Length >= 3)
                        {
                            current.R = Clamp01(ParseFloat(p[0]));
                            current.G = Clamp01(ParseFloat(p[1]));
                            current.B = Clamp01(ParseFloat(p[2]));
                        }
                    }
                    else if (current != null && (keyword == "d" || keyword == "Tr"))
                    {
                        float alpha = Clamp01(ParseFloat(payload));
                        current.A = keyword == "Tr" ? 1.0f - alpha : alpha;
                    }
                    else if (current != null && keyword == "map_Kd")
                    {
                        current.DiffuseMap = ParseMapPath(payload);
                    }
                }
            }
        }

        private static string ParseMapPath(string payload)
        {
            string value = (payload ?? string.Empty).Trim();
            if (value.Length == 0) return null;
            // The project-generated MTLs use a plain filename. Also tolerate quoted paths.
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                value = value.Substring(1, value.Length - 2);
            return value.Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
        }

        private static float Clamp01(float value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }

        private static bool AllNormalsNearlyEqual(List<V3> normals)
        {
            if (normals.Count < 2) return true;
            V3 first = normals[0];
            for (int i = 1; i < normals.Count; i++)
            {
                V3 n = normals[i];
                if (Math.Abs(n.X - first.X) > 0.0001f ||
                    Math.Abs(n.Y - first.Y) > 0.0001f ||
                    Math.Abs(n.Z - first.Z) > 0.0001f)
                    return false;
            }
            return true;
        }

        private static void RecalculateNormals(int sourcePositionCount,
            List<PrimitiveInfo> primitives)
        {
            var sum = new double[sourcePositionCount, 3];
            foreach (PrimitiveInfo primitive in primitives)
            {
                for (int i = 0; i + 2 < primitive.Indices.Count; i += 3)
                {
                    ObjVertex a = primitive.Vertices[(int)primitive.Indices[i]];
                    ObjVertex b = primitive.Vertices[(int)primitive.Indices[i + 1]];
                    ObjVertex c = primitive.Vertices[(int)primitive.Indices[i + 2]];
                    double ax = b.Position.X - a.Position.X;
                    double ay = b.Position.Y - a.Position.Y;
                    double az = b.Position.Z - a.Position.Z;
                    double bx = c.Position.X - a.Position.X;
                    double by = c.Position.Y - a.Position.Y;
                    double bz = c.Position.Z - a.Position.Z;
                    double nx = ay * bz - az * by;
                    double ny = az * bx - ax * bz;
                    double nz = ax * by - ay * bx;
                    if (a.SourcePosition >= 0 && a.SourcePosition < sourcePositionCount)
                    {
                        sum[a.SourcePosition, 0] += nx;
                        sum[a.SourcePosition, 1] += ny;
                        sum[a.SourcePosition, 2] += nz;
                    }
                    if (b.SourcePosition >= 0 && b.SourcePosition < sourcePositionCount)
                    {
                        sum[b.SourcePosition, 0] += nx;
                        sum[b.SourcePosition, 1] += ny;
                        sum[b.SourcePosition, 2] += nz;
                    }
                    if (c.SourcePosition >= 0 && c.SourcePosition < sourcePositionCount)
                    {
                        sum[c.SourcePosition, 0] += nx;
                        sum[c.SourcePosition, 1] += ny;
                        sum[c.SourcePosition, 2] += nz;
                    }
                }
            }
            foreach (PrimitiveInfo primitive in primitives)
            {
                foreach (ObjVertex vertex in primitive.Vertices)
                {
                    int s = vertex.SourcePosition;
                    double x = sum[s, 0], y = sum[s, 1], z = sum[s, 2];
                    double length = Math.Sqrt(x * x + y * y + z * z);
                    if (length > 1e-20)
                        vertex.Normal = new V3((float)(x / length),
                            (float)(y / length), (float)(z / length));
                    else
                        vertex.Normal = new V3(0, 1, 0);
                }
            }
        }

        private static EmbeddedImageInfo LoadImage(string imagePath)
        {
            string extension = Path.GetExtension(imagePath).ToLowerInvariant();
            if (extension == ".bmp")
            {
                using (var bitmap = new Bitmap(imagePath))
                using (var output = new MemoryStream())
                {
                    bool hasAlpha = false;
                    // Scan pixels for true transparency, but stop early when found.
                    for (int y = 0; y < bitmap.Height && !hasAlpha; y++)
                    {
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            if (bitmap.GetPixel(x, y).A < 255)
                            { hasAlpha = true; break; }
                        }
                    }
                    bitmap.Save(output, ImageFormat.Png);
                    return new EmbeddedImageInfo
                    {
                        Bytes = output.ToArray(), MimeType = "image/png", HasAlpha = hasAlpha
                    };
                }
            }
            byte[] bytes = File.ReadAllBytes(imagePath);
            if (extension == ".png")
            {
                return new EmbeddedImageInfo
                {
                    Bytes = bytes, MimeType = "image/png",
                    HasAlpha = PngHasAlpha(bytes)
                };
            }
            if (extension == ".jpg" || extension == ".jpeg")
            {
                return new EmbeddedImageInfo
                {
                    Bytes = bytes, MimeType = "image/jpeg", HasAlpha = false
                };
            }
            // Unsupported image formats are converted through GDI+ if available.
            using (var input = new MemoryStream(bytes))
            using (var image = Image.FromStream(input))
            using (var output = new MemoryStream())
            {
                bool hasAlpha = false;
                using (var bitmap = new Bitmap(image))
                {
                    for (int y = 0; y < bitmap.Height && !hasAlpha; y++)
                        for (int x = 0; x < bitmap.Width; x++)
                            if (bitmap.GetPixel(x, y).A < 255) { hasAlpha = true; break; }
                    bitmap.Save(output, ImageFormat.Png);
                }
                return new EmbeddedImageInfo
                {
                    Bytes = output.ToArray(), MimeType = "image/png", HasAlpha = hasAlpha
                };
            }
        }

        private static bool PngHasAlpha(byte[] bytes)
        {
            // PNG IHDR bit depth and colour type are at bytes 24/25.
            // Colour types 4 (gray+alpha) and 6 (RGBA) carry alpha.
            return bytes != null && bytes.Length > 25 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 &&
                (bytes[25] == 4 || bytes[25] == 6);
        }

        private static byte[] BuildGlb(string modelName,
            List<PrimitiveInfo> primitives,
            List<MaterialInfo> materials,
            List<EmbeddedImageInfo> images)
        {
            var binary = new MemoryStream();
            var bufferViews = new List<BufferViewInfo>();
            var accessors = new List<AccessorInfo>();

            foreach (PrimitiveInfo primitive in primitives)
            {
                if (primitive.Indices.Count == 0) continue;

                Align4(binary);
                int positionOffset = (int)binary.Position;
                float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
                foreach (ObjVertex vertex in primitive.Vertices)
                {
                    WriteFloat(binary, vertex.Position.X);
                    WriteFloat(binary, vertex.Position.Y);
                    WriteFloat(binary, vertex.Position.Z);
                    if (vertex.Position.X < minX) minX = vertex.Position.X;
                    if (vertex.Position.Y < minY) minY = vertex.Position.Y;
                    if (vertex.Position.Z < minZ) minZ = vertex.Position.Z;
                    if (vertex.Position.X > maxX) maxX = vertex.Position.X;
                    if (vertex.Position.Y > maxY) maxY = vertex.Position.Y;
                    if (vertex.Position.Z > maxZ) maxZ = vertex.Position.Z;
                }
                int positionView = AddView(bufferViews, positionOffset,
                    (int)binary.Position - positionOffset, 34962, true);
                primitive.PositionAccessor = AddAccessor(accessors, positionView,
                    5126, primitive.Vertices.Count, "VEC3",
                    new float[] { minX, minY, minZ }, new float[] { maxX, maxY, maxZ });

                Align4(binary);
                int normalOffset = (int)binary.Position;
                foreach (ObjVertex vertex in primitive.Vertices)
                {
                    WriteFloat(binary, vertex.Normal.X);
                    WriteFloat(binary, vertex.Normal.Y);
                    WriteFloat(binary, vertex.Normal.Z);
                }
                int normalView = AddView(bufferViews, normalOffset,
                    (int)binary.Position - normalOffset, 34962, true);
                primitive.NormalAccessor = AddAccessor(accessors, normalView,
                    5126, primitive.Vertices.Count, "VEC3", null, null);

                Align4(binary);
                int uvOffset = (int)binary.Position;
                foreach (ObjVertex vertex in primitive.Vertices)
                {
                    WriteFloat(binary, vertex.Uv.X);
                    WriteFloat(binary, vertex.Uv.Y);
                }
                int uvView = AddView(bufferViews, uvOffset,
                    (int)binary.Position - uvOffset, 34962, true);
                primitive.UvAccessor = AddAccessor(accessors, uvView,
                    5126, primitive.Vertices.Count, "VEC2", null, null);

                Align4(binary);
                int indexOffset = (int)binary.Position;
                foreach (uint index in primitive.Indices) WriteUInt32(binary, index);
                int indexView = AddView(bufferViews, indexOffset,
                    (int)binary.Position - indexOffset, 34963, true);
                primitive.IndexAccessor = AddAccessor(accessors, indexView,
                    5125, primitive.Indices.Count, "SCALAR", null, null);
            }

            for (int i = 0; i < images.Count; i++)
            {
                Align4(binary);
                int imageOffset = (int)binary.Position;
                binary.Write(images[i].Bytes, 0, images[i].Bytes.Length);
                images[i].BufferView = AddView(bufferViews, imageOffset,
                    images[i].Bytes.Length, 0, false);
            }

            byte[] binaryData = binary.ToArray();
            byte[] json = BuildJson(modelName, primitives, materials,
                images, bufferViews, accessors, binaryData.Length);
            byte[] jsonPadded = PadBytes(json, (byte)0x20);
            byte[] binPadded = PadBytes(binaryData, (byte)0x00);
            using (var output = new MemoryStream())
            using (var writer = new BinaryWriter(output))
            {
                long totalLengthLong = 12L + 8L + jsonPadded.Length + 8L + binPadded.Length;
                if (totalLengthLong > int.MaxValue)
                    throw new InvalidDataException("GLB exceeds the supported 2 GB size limit.");
                writer.Write(GlbMagic);
                writer.Write((uint)2);
                writer.Write((uint)totalLengthLong);
                writer.Write((uint)jsonPadded.Length);
                writer.Write(JsonChunkType);
                writer.Write(jsonPadded);
                writer.Write((uint)binPadded.Length);
                writer.Write(BinChunkType);
                writer.Write(binPadded);
                return output.ToArray();
            }
        }

        private static byte[] BuildJson(string modelName,
            List<PrimitiveInfo> primitives,
            List<MaterialInfo> materials,
            List<EmbeddedImageInfo> images,
            List<BufferViewInfo> views,
            List<AccessorInfo> accessors, int binaryLength)
        {
            var sb = new StringBuilder(16384);
            sb.Append("{\"asset\":{\"version\":\"2.0\",\"generator\":\"HMSTHModdingTool OBJ-to-GLB exporter\"},");
            sb.Append("\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"name\":");
            sb.Append(JsonString(modelName));
            sb.Append(",\"mesh\":0}],\"meshes\":[{\"name\":");
            sb.Append(JsonString(modelName));
            sb.Append(",\"primitives\":[");
            bool first = true;
            foreach (PrimitiveInfo primitive in primitives)
            {
                if (primitive.Indices.Count == 0) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"attributes\":{\"POSITION\":");
                sb.Append(primitive.PositionAccessor.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"NORMAL\":");
                sb.Append(primitive.NormalAccessor.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"TEXCOORD_0\":");
                sb.Append(primitive.UvAccessor.ToString(CultureInfo.InvariantCulture));
                sb.Append("},\"indices\":");
                sb.Append(primitive.IndexAccessor.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"material\":");
                sb.Append(primitive.MaterialIndex.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"mode\":4}");
            }
            sb.Append("]}],\"materials\":[");
            for (int i = 0; i < materials.Count; i++)
            {
                if (i > 0) sb.Append(',');
                MaterialInfo m = materials[i];
                sb.Append("{\"name\":"); sb.Append(JsonString(m.Name));
                sb.Append(",\"doubleSided\":true,\"pbrMetallicRoughness\":{\"baseColorFactor\":[");
                sb.Append(F(m.R)); sb.Append(','); sb.Append(F(m.G)); sb.Append(',');
                sb.Append(F(m.B)); sb.Append(','); sb.Append(F(m.A));
                sb.Append("],\"metallicFactor\":0,\"roughnessFactor\":1");
                if (m.TextureIndex >= 0)
                {
                    sb.Append(",\"baseColorTexture\":{\"index\":");
                    sb.Append(m.TextureIndex.ToString(CultureInfo.InvariantCulture));
                    sb.Append("}");
                }
                sb.Append("}");
                if (m.A < 0.9999f || m.HasTextureAlpha) sb.Append(",\"alphaMode\":\"BLEND\"");
                sb.Append('}');
            }
            sb.Append(']');
            if (images.Count > 0)
            {
                sb.Append(",\"samplers\":[{\"magFilter\":9729,\"minFilter\":9987,\"wrapS\":10497,\"wrapT\":10497}],\"textures\":[");
                for (int i = 0; i < images.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"sampler\":0,\"source\":");
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    sb.Append('}');
                }
                sb.Append("],\"images\":[");
                for (int i = 0; i < images.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"bufferView\":");
                    sb.Append(images[i].BufferView.ToString(CultureInfo.InvariantCulture));
                    sb.Append(",\"mimeType\":"); sb.Append(JsonString(images[i].MimeType)); sb.Append('}');
                }
                sb.Append(']');
            }
            sb.Append(",\"buffers\":[{\"byteLength\":");
            sb.Append(binaryLength.ToString(CultureInfo.InvariantCulture));
            sb.Append("}],\"bufferViews\":[");
            for (int i = 0; i < views.Count; i++)
            {
                if (i > 0) sb.Append(',');
                BufferViewInfo view = views[i];
                sb.Append("{\"buffer\":0,\"byteOffset\":");
                sb.Append(view.Offset.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"byteLength\":");
                sb.Append(view.Length.ToString(CultureInfo.InvariantCulture));
                if (view.HasTarget)
                {
                    sb.Append(",\"target\":");
                    sb.Append(view.Target.ToString(CultureInfo.InvariantCulture));
                }
                sb.Append('}');
            }
            sb.Append("],\"accessors\":[");
            for (int i = 0; i < accessors.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AccessorInfo a = accessors[i];
                sb.Append("{\"bufferView\":"); sb.Append(a.BufferView.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"componentType\":"); sb.Append(a.ComponentType.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"count\":"); sb.Append(a.Count.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"type\":"); sb.Append(JsonString(a.Type));
                if (a.Min != null) { sb.Append(",\"min\":"); AppendFloatArray(sb, a.Min); }
                if (a.Max != null) { sb.Append(",\"max\":"); AppendFloatArray(sb, a.Max); }
                sb.Append('}');
            }
            sb.Append("]}");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static void AppendFloatArray(StringBuilder sb, float[] values)
        {
            sb.Append('[');
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(F(values[i]));
            }
            sb.Append(']');
        }

        private static string F(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return "0";
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string JsonString(string value)
        {
            if (value == null) return "\"\"";
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static int AddView(List<BufferViewInfo> views,
            int offset, int length, int target, bool hasTarget)
        {
            int index = views.Count;
            views.Add(new BufferViewInfo
            {
                Offset = offset, Length = length,
                Target = target, HasTarget = hasTarget
            });
            return index;
        }

        private static int AddAccessor(List<AccessorInfo> accessors,
            int bufferView, int componentType, int count,
            string type, float[] min, float[] max)
        {
            int index = accessors.Count;
            accessors.Add(new AccessorInfo
            {
                BufferView = bufferView, ComponentType = componentType,
                Count = count, Type = type, Min = min, Max = max
            });
            return index;
        }

        private static void Align4(Stream stream)
        {
            while ((stream.Position & 3) != 0) stream.WriteByte(0);
        }

        private static byte[] PadBytes(byte[] source, byte pad)
        {
            int length = (source.Length + 3) & ~3;
            if (length == source.Length) return source;
            byte[] result = new byte[length];
            Buffer.BlockCopy(source, 0, result, 0, source.Length);
            for (int i = source.Length; i < result.Length; i++) result[i] = pad;
            return result;
        }

        private static void WriteFloat(Stream stream, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteUInt32(Stream stream, uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
