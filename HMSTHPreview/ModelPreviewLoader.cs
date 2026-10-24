using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace HMSTHPreview
{
    internal sealed class ModelPreviewData
    {
        public Model3DGroup Model { get; set; }
        public Point3D Center { get; set; }
        public double Radius { get; set; }
        public int VertexCount { get; set; }
        public int TriangleCount { get; set; }
        public int MaterialCount { get; set; }
        public List<string> Warnings { get; private set; }

        public ModelPreviewData()
        {
            Warnings = new List<string>();
        }
    }

    internal static class ModelPreviewLoader
    {
        private const int MaximumFileBytes = 512 * 1024 * 1024;
        private const int MaximumGlbFileBytes = 128 * 1024 * 1024;
        private const int MaximumSourceVertices = 1000000;
        private const int MaximumPreviewTriangles = 250000;
        private const long MaximumImagePixels = 20000000L;
        private const uint GlbMagic = 0x46546C67;
        private const uint JsonChunkType = 0x4E4F534A;
        private const uint BinChunkType = 0x004E4942;

        private sealed class BoundsAccumulator
        {
            private bool _hasPoint;
            private double _minX, _minY, _minZ, _maxX, _maxY, _maxZ;

            public void Add(Point3D p)
            {
                if (!_hasPoint)
                {
                    _minX = _maxX = p.X;
                    _minY = _maxY = p.Y;
                    _minZ = _maxZ = p.Z;
                    _hasPoint = true;
                    return;
                }
                _minX = Math.Min(_minX, p.X);
                _minY = Math.Min(_minY, p.Y);
                _minZ = Math.Min(_minZ, p.Z);
                _maxX = Math.Max(_maxX, p.X);
                _maxY = Math.Max(_maxY, p.Y);
                _maxZ = Math.Max(_maxZ, p.Z);
            }

            public Point3D Center
            {
                get
                {
                    if (!_hasPoint) throw new InvalidDataException("Model has no positions.");
                    return new Point3D((_minX + _maxX) / 2.0,
                        (_minY + _maxY) / 2.0, (_minZ + _maxZ) / 2.0);
                }
            }

            public double Radius
            {
                get
                {
                    Point3D center = Center;
                    double dx = _maxX - center.X;
                    double dy = _maxY - center.Y;
                    double dz = _maxZ - center.Z;
                    return Math.Max(Math.Sqrt(dx * dx + dy * dy + dz * dz), 0.001);
                }
            }
        }

        private sealed class ObjFaceVertex
        {
            public int PositionIndex;
            public int UvIndex;
            public int NormalIndex;
        }

        private sealed class ObjMaterial
        {
            public string Name;
            public Color Color = Colors.LightGray;
            public double Opacity = 1.0;
            public string TexturePath;

            public ObjMaterial(string name) { Name = name; }
        }

        private sealed class ObjMeshBuilder
        {
            public string MaterialName;
            public MeshGeometry3D Mesh = new MeshGeometry3D();
            public int TriangleCount;

            public ObjMeshBuilder(string materialName) { MaterialName = materialName; }

            public void AddTriangle(ObjFaceVertex a, ObjFaceVertex b, ObjFaceVertex c,
                IList<Point3D> positions, IList<Point> uvs, IList<Vector3D> normals)
            {
                Point3D pa = positions[a.PositionIndex];
                Point3D pb = positions[b.PositionIndex];
                Point3D pc = positions[c.PositionIndex];
                Vector3D faceNormal = Vector3D.CrossProduct(pb - pa, pc - pa);
                if (faceNormal.Length < 0.0000001) faceNormal = new Vector3D(0, 1, 0);
                else faceNormal.Normalize();
                AddCorner(a, pa, faceNormal, uvs, normals);
                AddCorner(b, pb, faceNormal, uvs, normals);
                AddCorner(c, pc, faceNormal, uvs, normals);
                TriangleCount++;
            }

            private void AddCorner(ObjFaceVertex corner, Point3D position,
                Vector3D faceNormal, IList<Point> uvs, IList<Vector3D> normals)
            {
                int index = Mesh.Positions.Count;
                Mesh.Positions.Add(position);
                Mesh.TriangleIndices.Add(index);
                Vector3D normal = faceNormal;
                if (corner.NormalIndex >= 0 && corner.NormalIndex < normals.Count)
                {
                    normal = normals[corner.NormalIndex];
                    if (normal.Length > 0.0000001) normal.Normalize();
                    else normal = faceNormal;
                }
                Mesh.Normals.Add(normal);
                Mesh.TextureCoordinates.Add(
                    corner.UvIndex >= 0 && corner.UvIndex < uvs.Count
                        ? uvs[corner.UvIndex] : new Point(0, 0));
            }
        }

        public static ModelPreviewData Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Model path cannot be empty.", "path");
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("Model file was not found.", fullPath);
            FileInfo info = new FileInfo(fullPath);
            if (info.Length <= 0 || info.Length > MaximumFileBytes)
                throw new InvalidDataException("Model file size must be between 1 byte and 512 MiB.");

            string extension = Path.GetExtension(fullPath).ToLowerInvariant();
            if (extension == ".obj") return LoadObj(fullPath);
            if (extension == ".glb") return LoadGlb(fullPath);
            throw new NotSupportedException("Supported model formats are OBJ and GLB.");
        }

        private static ModelPreviewData LoadObj(string path)
        {
            var data = new ModelPreviewData();
            var positions = new List<Point3D>();
            var uvs = new List<Point>();
            var normals = new List<Vector3D>();
            var materialLibraries = new List<string>();
            var builders = new Dictionary<string, ObjMeshBuilder>(StringComparer.Ordinal);
            string activeMaterial = "material_default";
            string baseDirectory = Path.GetDirectoryName(path);
            int lineNumber = 0;
            int totalObjTriangles = 0;

            using (var reader = new StreamReader(path, Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed[0] == '#') continue;
                    string[] fields = SplitFields(trimmed);
                    if (fields.Length == 0) continue;
                    string keyword = fields[0];
                    try
                    {
                        if (keyword == "v")
                        {
                            if (fields.Length < 4) throw new InvalidDataException("Vertex requires three coordinates.");
                            positions.Add(new Point3D(ParseDouble(fields[1]), ParseDouble(fields[2]), ParseDouble(fields[3])));
                            if (positions.Count > MaximumSourceVertices)
                                throw new InvalidDataException("OBJ exceeds the " + MaximumSourceVertices + " vertex preview limit.");
                        }
                        else if (keyword == "vt")
                        {
                            if (fields.Length < 3) throw new InvalidDataException("Texture coordinate requires two values.");
                            uvs.Add(new Point(ParseDouble(fields[1]), ParseDouble(fields[2])));
                            if (uvs.Count > MaximumSourceVertices)
                                throw new InvalidDataException("OBJ exceeds the supported texture-coordinate preview limit.");
                        }
                        else if (keyword == "vn")
                        {
                            if (fields.Length < 4) throw new InvalidDataException("Normal requires three values.");
                            normals.Add(new Vector3D(ParseDouble(fields[1]), ParseDouble(fields[2]), ParseDouble(fields[3])));
                            if (normals.Count > MaximumSourceVertices)
                                throw new InvalidDataException("OBJ exceeds the supported normal preview limit.");
                        }
                        else if (keyword == "mtllib")
                        {
                            string remainder = trimmed.Substring(keyword.Length).Trim();
                            foreach (string library in SplitFields(remainder))
                            {
                                string libraryPath = Path.GetFullPath(Path.Combine(baseDirectory, library));
                                if (!materialLibraries.Contains(libraryPath, StringComparer.OrdinalIgnoreCase))
                                    materialLibraries.Add(libraryPath);
                            }
                        }
                        else if (keyword == "usemtl")
                        {
                            activeMaterial = trimmed.Substring(keyword.Length).Trim();
                            if (activeMaterial.Length == 0) activeMaterial = "material_default";
                        }
                        else if (keyword == "f")
                        {
                            if (fields.Length < 4) throw new InvalidDataException("Face requires at least three vertices.");
                            var polygon = new List<ObjFaceVertex>();
                            for (int i = 1; i < fields.Length; i++)
                                polygon.Add(ParseObjFaceVertex(fields[i], positions.Count, uvs.Count, normals.Count));
                            ObjMeshBuilder builder;
                            if (!builders.TryGetValue(activeMaterial, out builder))
                            {
                                builder = new ObjMeshBuilder(activeMaterial);
                                builders.Add(activeMaterial, builder);
                            }
                            for (int i = 1; i + 1 < polygon.Count; i++)
                            {
                                if (totalObjTriangles >= MaximumPreviewTriangles)
                                    throw new InvalidDataException("OBJ exceeds the " + MaximumPreviewTriangles + " triangle preview limit.");
                                builder.AddTriangle(polygon[0], polygon[i], polygon[i + 1], positions, uvs, normals);
                                totalObjTriangles++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException("Invalid OBJ at line " + lineNumber + ": " + ex.Message, ex);
                    }
                }
            }

            if (positions.Count == 0) throw new InvalidDataException("OBJ contains no vertex positions.");
            if (builders.Count == 0) throw new InvalidDataException("OBJ contains no supported triangle faces.");

            var materials = new Dictionary<string, ObjMaterial>(StringComparer.Ordinal);
            foreach (string mtlPath in materialLibraries)
            {
                if (!File.Exists(mtlPath))
                {
                    data.Warnings.Add("Material library not found: " + mtlPath);
                    continue;
                }
                LoadMtl(mtlPath, materials, data.Warnings);
            }

            var model = new Model3DGroup();
            BoundsAccumulator bounds = new BoundsAccumulator();
            foreach (Point3D p in positions) bounds.Add(p);
            int totalTriangles = 0;
            var imageCache = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjMeshBuilder builder in builders.Values)
            {
                if (builder.TriangleCount == 0) continue;
                ObjMaterial material;
                if (!materials.TryGetValue(builder.MaterialName, out material))
                    material = new ObjMaterial(builder.MaterialName);
                Material wpfMaterial = CreateObjMaterial(material, baseDirectory, imageCache, data.Warnings);
                var geometry = new GeometryModel3D(builder.Mesh, wpfMaterial);
                geometry.BackMaterial = wpfMaterial;
                model.Children.Add(geometry);
                totalTriangles += builder.TriangleCount;
            }
            if (totalTriangles == 0) throw new InvalidDataException("OBJ contains no usable triangles.");

            AddLights(model);
            data.Model = model;
            data.Center = bounds.Center;
            data.Radius = bounds.Radius;
            data.VertexCount = positions.Count;
            data.TriangleCount = totalTriangles;
            data.MaterialCount = builders.Count;
            return data;
        }

        private static ModelPreviewData LoadGlb(string path)
        {
            FileInfo glbInfo = new FileInfo(path);
            if (glbInfo.Length <= 0 || glbInfo.Length > MaximumGlbFileBytes)
                throw new InvalidDataException("GLB preview input must be between 1 byte and 128 MiB to limit memory duplication while parsing.");
            byte[] fileBytes = File.ReadAllBytes(path);
            if (fileBytes.Length < 20) throw new InvalidDataException("GLB is too short to contain a header and JSON chunk.");
            if (ReadUInt32(fileBytes, 0) != GlbMagic) throw new InvalidDataException("GLB magic is invalid.");
            if (ReadUInt32(fileBytes, 4) != 2) throw new InvalidDataException("Only GLB version 2 is supported by the previewer.");
            uint declaredLength = ReadUInt32(fileBytes, 8);
            if (declaredLength != fileBytes.Length) throw new InvalidDataException("GLB declared length does not match the file size.");

            byte[] jsonBytes = null;
            byte[] binaryBytes = null;
            int offset = 12;
            while (offset < fileBytes.Length)
            {
                if (fileBytes.Length - offset < 8) throw new InvalidDataException("GLB chunk header is truncated.");
                uint chunkLengthValue = ReadUInt32(fileBytes, offset);
                uint chunkType = ReadUInt32(fileBytes, offset + 4);
                offset += 8;
                if (chunkLengthValue > int.MaxValue || chunkLengthValue > fileBytes.Length - offset)
                    throw new InvalidDataException("GLB chunk length exceeds the remaining file.");
                int chunkLength = (int)chunkLengthValue;
                byte[] chunk = new byte[chunkLength];
                Buffer.BlockCopy(fileBytes, offset, chunk, 0, chunkLength);
                if (chunkType == JsonChunkType)
                {
                    if (jsonBytes != null) throw new InvalidDataException("GLB contains more than one JSON chunk.");
                    jsonBytes = chunk;
                }
                else if (chunkType == BinChunkType)
                {
                    if (binaryBytes != null) throw new InvalidDataException("GLB contains more than one BIN chunk.");
                    binaryBytes = chunk;
                }
                offset += chunkLength;
            }
            if (jsonBytes == null) throw new InvalidDataException("GLB has no JSON chunk.");
            if (binaryBytes == null) throw new InvalidDataException("GLB has no BIN chunk; this preview path expects self-contained GLB files.");

            var serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            string jsonText = Encoding.UTF8.GetString(jsonBytes).TrimEnd('\0', ' ', '\t', '\r', '\n');
            IDictionary<string, object> root;
            try
            {
                root = AsDictionary(serializer.DeserializeObject(jsonText), "GLB JSON root");
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("GLB JSON could not be parsed: " + ex.Message, ex);
            }

            IDictionary<string, object> asset = GetDictionary(root, "asset");
            if (asset == null || !string.Equals(GetString(asset, "version"), "2.0", StringComparison.Ordinal))
                throw new InvalidDataException("GLB JSON does not declare glTF asset version 2.0.");
            IList<object> nodes = GetArray(root, "nodes");
            IList<object> meshes = GetArray(root, "meshes");
            if (nodes.Count != 1 || meshes.Count != 1)
                throw new NotSupportedException("The previewer currently supports the single-node/single-mesh GLB layout emitted by HMSTHModdingTool.");
            IDictionary<string, object> modelNode = AsDictionary(nodes[0], "node");
            if (modelNode.ContainsKey("translation") || modelNode.ContainsKey("rotation") ||
                modelNode.ContainsKey("scale") || modelNode.ContainsKey("matrix") || modelNode.ContainsKey("children"))
                throw new NotSupportedException("GLB node transforms and child-node hierarchies are not supported by this previewer.");
            if (GetInt(modelNode, "mesh", -1) != 0)
                throw new NotSupportedException("GLB scene node does not reference mesh 0 as expected by this previewer.");
            var bufferViews = GetArray(root, "bufferViews");
            var accessors = GetArray(root, "accessors");
            var materials = GetArray(root, "materials");
            var textures = GetArray(root, "textures");
            var images = GetArray(root, "images");
            var result = new ModelPreviewData();
            BitmapSource[] imageCache = DecodeEmbeddedImages(images, bufferViews, binaryBytes, result.Warnings);
            var model = new Model3DGroup();
            BoundsAccumulator bounds = new BoundsAccumulator();
            int vertexCount = 0;
            int triangleCount = 0;
            int materialCount = 0;

            foreach (object meshObject in meshes)
            {
                IDictionary<string, object> mesh = AsDictionary(meshObject, "mesh");
                foreach (object primitiveObject in GetArray(mesh, "primitives"))
                {
                    IDictionary<string, object> primitive = AsDictionary(primitiveObject, "primitive");
                    int mode = GetInt(primitive, "mode", 4);
                    if (mode != 4)
                        throw new NotSupportedException("GLB primitive mode " + mode + " is not supported; triangle lists are required.");
                    IDictionary<string, object> attributes = GetDictionary(primitive, "attributes");
                    if (attributes == null || !attributes.ContainsKey("POSITION"))
                        throw new InvalidDataException("GLB primitive has no POSITION accessor.");
                    int positionAccessor = ToInt(attributes["POSITION"], "POSITION accessor");
                    float[] positionValues = ReadFloatAccessor(positionAccessor, "VEC3", accessors, bufferViews, binaryBytes);
                    int positionsInPrimitive = positionValues.Length / 3;
                    if (positionsInPrimitive == 0) throw new InvalidDataException("GLB primitive has no positions.");
                    if (vertexCount > MaximumSourceVertices - positionsInPrimitive)
                        throw new InvalidDataException("GLB exceeds the " + MaximumSourceVertices + " vertex preview limit.");
                    var meshGeometry = new MeshGeometry3D();
                    for (int i = 0; i < positionValues.Length; i += 3)
                    {
                        Point3D point = new Point3D(positionValues[i], positionValues[i + 1], positionValues[i + 2]);
                        meshGeometry.Positions.Add(point);
                        bounds.Add(point);
                    }

                    Vector3D[] normalValues = null;
                    if (attributes.ContainsKey("NORMAL"))
                    {
                        float[] flatNormals = ReadFloatAccessor(ToInt(attributes["NORMAL"], "NORMAL accessor"), "VEC3", accessors, bufferViews, binaryBytes);
                        if (flatNormals.Length != positionValues.Length)
                            throw new InvalidDataException("GLB NORMAL accessor count does not match POSITION.");
                        normalValues = new Vector3D[positionsInPrimitive];
                        for (int i = 0; i < flatNormals.Length; i += 3)
                            normalValues[i / 3] = new Vector3D(flatNormals[i], flatNormals[i + 1], flatNormals[i + 2]);
                    }

                    if (attributes.ContainsKey("TEXCOORD_0"))
                    {
                        float[] flatUvs = ReadFloatAccessor(ToInt(attributes["TEXCOORD_0"], "TEXCOORD_0 accessor"), "VEC2", accessors, bufferViews, binaryBytes);
                        if (flatUvs.Length / 2 != positionsInPrimitive)
                            throw new InvalidDataException("GLB TEXCOORD_0 accessor count does not match POSITION.");
                        for (int i = 0; i < flatUvs.Length; i += 2)
                            meshGeometry.TextureCoordinates.Add(new Point(flatUvs[i], flatUvs[i + 1]));
                    }
                    else
                    {
                        for (int i = 0; i < positionsInPrimitive; i++) meshGeometry.TextureCoordinates.Add(new Point(0, 0));
                    }

                    List<int> indices;
                    if (primitive.ContainsKey("indices"))
                        indices = ReadIndexAccessor(ToInt(primitive["indices"], "index accessor"), accessors, bufferViews, binaryBytes);
                    else
                    {
                        indices = new List<int>();
                        for (int i = 0; i < positionsInPrimitive; i++) indices.Add(i);
                    }
                    if (indices.Count == 0 || indices.Count % 3 != 0)
                        throw new InvalidDataException("GLB primitive index count is not a non-empty multiple of three.");
                    if (triangleCount > MaximumPreviewTriangles - (indices.Count / 3))
                        throw new InvalidDataException("GLB exceeds the " + MaximumPreviewTriangles + " triangle preview limit.");
                    foreach (int index in indices)
                        if (index < 0 || index >= positionsInPrimitive)
                            throw new InvalidDataException("GLB primitive index is outside the POSITION accessor.");
                    for (int i = 0; i < indices.Count; i++) meshGeometry.TriangleIndices.Add(indices[i]);

                    if (normalValues != null)
                    {
                        foreach (Vector3D normal in normalValues)
                        {
                            Vector3D safeNormal = normal;
                            if (safeNormal.Length > 0.0000001) safeNormal.Normalize();
                            else safeNormal = new Vector3D(0, 1, 0);
                            meshGeometry.Normals.Add(safeNormal);
                        }
                    }
                    else
                    {
                        Vector3D[] generatedNormals = GenerateVertexNormals(meshGeometry);
                        foreach (Vector3D normal in generatedNormals) meshGeometry.Normals.Add(normal);
                    }

                    Material material = MaterialFromGlbPrimitive(primitive, materials, textures, imageCache, result.Warnings);
                    var geometry = new GeometryModel3D(meshGeometry, material);
                    geometry.BackMaterial = material;
                    model.Children.Add(geometry);
                    vertexCount += positionsInPrimitive;
                    triangleCount += indices.Count / 3;
                    materialCount++;
                }
            }

            if (triangleCount == 0) throw new InvalidDataException("GLB contains no triangle geometry that this previewer supports.");
            AddLights(model);
            result.Model = model;
            result.Center = bounds.Center;
            result.Radius = bounds.Radius;
            result.VertexCount = vertexCount;
            result.TriangleCount = triangleCount;
            result.MaterialCount = materialCount;
            return result;
        }

        private static ObjFaceVertex ParseObjFaceVertex(string token, int positionCount, int uvCount, int normalCount)
        {
            string[] indices = token.Split('/');
            if (indices.Length == 0 || indices[0].Length == 0)
                throw new InvalidDataException("Face vertex has no position index: " + token);
            return new ObjFaceVertex
            {
                PositionIndex = ResolveObjIndex(indices[0], positionCount, token),
                UvIndex = indices.Length > 1 && indices[1].Length > 0 ? ResolveObjIndex(indices[1], uvCount, token) : -1,
                NormalIndex = indices.Length > 2 && indices[2].Length > 0 ? ResolveObjIndex(indices[2], normalCount, token) : -1
            };
        }

        private static int ResolveObjIndex(string value, int count, string token)
        {
            int parsed;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) || parsed == 0)
                throw new InvalidDataException("Invalid OBJ index in face vertex: " + token);
            int resolved = parsed > 0 ? parsed - 1 : count + parsed;
            if (resolved < 0 || resolved >= count)
                throw new InvalidDataException("OBJ face index is outside the corresponding table: " + token);
            return resolved;
        }

        private static void LoadMtl(string path, IDictionary<string, ObjMaterial> materials, IList<string> warnings)
        {
            ObjMaterial current = null;
            int lineNumber = 0;
            using (var reader = new StreamReader(path, Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed[0] == '#') continue;
                    int separator = trimmed.IndexOfAny(new char[] { ' ', '\t' });
                    string command = separator < 0 ? trimmed : trimmed.Substring(0, separator);
                    string payload = separator < 0 ? string.Empty : trimmed.Substring(separator + 1).Trim();
                    if (command == "newmtl")
                    {
                        if (payload.Length == 0) throw new InvalidDataException("Empty newmtl name in " + path + ", line " + lineNumber + ".");
                        current = new ObjMaterial(payload);
                        materials[payload] = current;
                    }
                    else if (current != null && command == "Kd")
                    {
                        string[] fields = SplitFields(payload);
                        if (fields.Length < 3) throw new InvalidDataException("Invalid Kd color in " + path + ", line " + lineNumber + ".");
                        current.Color = Color.FromRgb(ToByte(ParseDouble(fields[0]), warnings, "MTL red"),
                            ToByte(ParseDouble(fields[1]), warnings, "MTL green"),
                            ToByte(ParseDouble(fields[2]), warnings, "MTL blue"));
                    }
                    else if (current != null && (command == "d" || command == "Tr"))
                    {
                        string[] fields = SplitFields(payload);
                        if (fields.Length > 0)
                        {
                            double opacity = ParseDouble(fields[0]);
                            current.Opacity = Clamp01(command == "Tr" ? 1.0 - opacity : opacity);
                        }
                    }
                    else if (current != null && command == "map_Kd")
                    {
                        string textureName = payload;
                        if (textureName.StartsWith("-", StringComparison.Ordinal))
                        {
                            string[] fields = SplitFields(payload);
                            if (fields.Length > 0) textureName = fields[fields.Length - 1];
                        }
                        string texturePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), textureName));
                        current.TexturePath = texturePath;
                    }
                }
            }
        }

        private static Material CreateObjMaterial(ObjMaterial source, string objDirectory,
            IDictionary<string, BitmapSource> imageCache, IList<string> warnings)
        {
            Brush brush = new SolidColorBrush(Color.FromArgb(ToByte(source.Opacity, warnings, "material alpha"),
                source.Color.R, source.Color.G, source.Color.B));
            if (!string.IsNullOrWhiteSpace(source.TexturePath))
            {
                if (source.Color.R != 255 || source.Color.G != 255 || source.Color.B != 255)
                    warnings.Add("Textured OBJ material '" + source.Name + "' has a diffuse tint; preview displays the texture without RGB multiplication.");
                string texturePath = source.TexturePath;
                if (!File.Exists(texturePath))
                {
                    string alternate = Path.GetFullPath(Path.Combine(objDirectory, Path.GetFileName(texturePath)));
                    if (File.Exists(alternate)) texturePath = alternate;
                }
                if (File.Exists(texturePath))
                {
                    BitmapSource bitmap;
                    if (!imageCache.TryGetValue(texturePath, out bitmap))
                    {
                        try
                        {
                            long cachedPixels = 0;
                            foreach (BitmapSource cachedImage in imageCache.Values)
                                cachedPixels += (long)cachedImage.PixelWidth * cachedImage.PixelHeight;
                            long requestedPixels;
                            using (FileStream metadataStream = File.OpenRead(texturePath))
                                requestedPixels = ValidateImageDimensions(metadataStream, texturePath);
                            if (requestedPixels > MaximumImagePixels - cachedPixels)
                            {
                                warnings.Add("Texture not loaded because the model's aggregate image pixel budget would exceed " +
                                    MaximumImagePixels.ToString(CultureInfo.InvariantCulture) + " pixels: " + texturePath);
                                bitmap = null;
                            }
                            else
                            {
                                bitmap = LoadImageFromFile(texturePath);
                                imageCache.Add(texturePath, bitmap);
                            }
                        }
                        catch (Exception ex)
                        {
                            warnings.Add("Texture could not be previewed ('" + texturePath + "'): " + ex.Message);
                            bitmap = null;
                        }
                    }
                    if (bitmap != null)
                    {
                        brush = new ImageBrush(bitmap) { Stretch = Stretch.Fill, Opacity = source.Opacity };
                    }
                }
                else
                {
                    warnings.Add("Material texture not found: " + source.TexturePath);
                }
            }
            return new DiffuseMaterial(brush);
        }

        private static Material MaterialFromGlbPrimitive(IDictionary<string, object> primitive,
            IList<object> materials, IList<object> textures, BitmapSource[] images, IList<string> warnings)
        {
            Color baseColor = Colors.White;
            double alpha = 1.0;
            BitmapSource textureImage = null;
            int materialIndex = GetInt(primitive, "material", -1);
            if (materialIndex >= 0 && materialIndex < materials.Count)
            {
                IDictionary<string, object> material = AsDictionary(materials[materialIndex], "material");
                IDictionary<string, object> pbr = GetDictionary(material, "pbrMetallicRoughness");
                if (pbr != null)
                {
                    IList<object> factor = GetArray(pbr, "baseColorFactor");
                    if (factor.Count >= 4)
                    {
                        baseColor = Color.FromArgb(ToByte(ToDouble(factor[3], "baseColorFactor alpha"), warnings, "baseColorFactor alpha"),
                            ToByte(ToDouble(factor[0], "baseColorFactor red"), warnings, "baseColorFactor red"),
                            ToByte(ToDouble(factor[1], "baseColorFactor green"), warnings, "baseColorFactor green"),
                            ToByte(ToDouble(factor[2], "baseColorFactor blue"), warnings, "baseColorFactor blue"));
                        alpha = baseColor.A / 255.0;
                    }
                    IDictionary<string, object> baseTexture = GetDictionary(pbr, "baseColorTexture");
                    if (baseTexture != null)
                    {
                        if (baseColor.R != 255 || baseColor.G != 255 || baseColor.B != 255)
                            warnings.Add("A textured GLB material has a non-white baseColorFactor; this previewer does not multiply RGB tint into the texture.");
                        int textureIndex = GetInt(baseTexture, "index", -1);
                        if (textureIndex >= 0 && textureIndex < textures.Count)
                        {
                            IDictionary<string, object> texture = AsDictionary(textures[textureIndex], "texture");
                            int imageIndex = GetInt(texture, "source", -1);
                            if (imageIndex >= 0 && imageIndex < images.Length) textureImage = images[imageIndex];
                            if (textureImage == null)
                                warnings.Add("A material texture could not be loaded; base color is used instead.");
                        }
                    }
                }
            }

            Brush brush;
            if (textureImage != null)
                brush = new ImageBrush(textureImage) { Stretch = Stretch.Fill, Opacity = alpha };
            else
                brush = new SolidColorBrush(baseColor);
            return new DiffuseMaterial(brush);
        }

        private static BitmapSource[] DecodeEmbeddedImages(IList<object> images, IList<object> views,
            byte[] binaryBytes, IList<string> warnings)
        {
            var result = new BitmapSource[images.Count];
            long totalImagePixels = 0;
            for (int i = 0; i < images.Count; i++)
            {
                IDictionary<string, object> image = AsDictionary(images[i], "image");
                if (!image.ContainsKey("bufferView"))
                {
                    warnings.Add("GLB image " + i + " uses an external URI/data URI; only embedded bufferView images are previewed.");
                    continue;
                }

                int viewIndex = GetInt(image, "bufferView", -1);
                byte[] bytes;
                try
                {
                    bytes = ReadBufferView(viewIndex, views, binaryBytes);
                }
                catch (Exception ex)
                {
                    warnings.Add("GLB image " + i + " could not be read: " + ex.Message);
                    continue;
                }

                long imagePixels;
                try
                {
                    using (var metadataStream = new MemoryStream(bytes, false))
                        imagePixels = ValidateImageDimensions(metadataStream, "GLB image " + i);
                }
                catch (Exception ex)
                {
                    warnings.Add("GLB image " + i + " could not be decoded: " + ex.Message);
                    continue;
                }

                if (imagePixels > MaximumImagePixels - totalImagePixels)
                    throw new InvalidDataException("GLB embedded images exceed the aggregate " +
                        MaximumImagePixels.ToString(CultureInfo.InvariantCulture) + "-pixel preview budget.");
                try
                {
                    result[i] = LoadImageFromBytes(bytes);
                    totalImagePixels += imagePixels;
                }
                catch (Exception ex)
                {
                    warnings.Add("GLB image " + i + " could not be decoded: " + ex.Message);
                }
            }
            return result;
        }

        private static float[] ReadFloatAccessor(int accessorIndex, string expectedType,
            IList<object> accessors, IList<object> views, byte[] binaryBytes)
        {
            if (accessorIndex < 0 || accessorIndex >= accessors.Count)
                throw new InvalidDataException("GLB accessor index is outside the accessor table.");
            IDictionary<string, object> accessor = AsDictionary(accessors[accessorIndex], "accessor");
            if (!string.Equals(GetString(accessor, "type"), expectedType, StringComparison.Ordinal))
                throw new InvalidDataException("GLB accessor type " + GetString(accessor, "type") + " is not " + expectedType + ".");
            if (GetInt(accessor, "componentType", 0) != 5126)
                throw new NotSupportedException("This preview path requires FLOAT componentType for " + expectedType + " accessors.");
            int componentCount = expectedType == "VEC3" ? 3 : expectedType == "VEC2" ? 2 : 1;
            int count = GetInt(accessor, "count", -1);
            if (count < 0 || count > MaximumSourceVertices) throw new InvalidDataException("GLB accessor count exceeds the supported preview limit.");
            if (!accessor.ContainsKey("bufferView")) throw new NotSupportedException("Sparse or bufferView-less GLB accessors are not supported.");
            int viewIndex = GetInt(accessor, "bufferView", -1);
            if (viewIndex < 0 || viewIndex >= views.Count) throw new InvalidDataException("GLB accessor refers to an invalid bufferView.");
            IDictionary<string, object> view = AsDictionary(views[viewIndex], "bufferView");
            if (GetInt(view, "buffer", 0) != 0) throw new NotSupportedException("Only the first embedded GLB buffer is supported.");
            int viewOffset = GetInt(view, "byteOffset", 0);
            int viewLength = GetInt(view, "byteLength", -1);
            int accessorOffset = GetInt(accessor, "byteOffset", 0);
            int elementLength = componentCount * 4;
            int stride = GetInt(view, "byteStride", elementLength);
            if (viewOffset < 0 || viewLength < 0 || accessorOffset < 0 || stride < elementLength)
                throw new InvalidDataException("GLB accessor or bufferView has invalid offsets/lengths.");
            long lastByte = count == 0 ? (long)viewOffset + accessorOffset : (long)viewOffset + accessorOffset + (long)(count - 1) * stride + elementLength;
            long viewEnd = (long)viewOffset + viewLength;
            if (lastByte > viewEnd || viewEnd > binaryBytes.Length)
                throw new InvalidDataException("GLB accessor reads outside its bufferView or BIN chunk.");
            float[] values = new float[checked(count * componentCount)];
            int target = 0;
            for (int i = 0; i < count; i++)
            {
                int itemOffset = checked(viewOffset + accessorOffset + i * stride);
                for (int c = 0; c < componentCount; c++)
                {
                    float value = ReadSingle(binaryBytes, itemOffset + c * 4);
                    if (float.IsNaN(value) || float.IsInfinity(value))
                        throw new InvalidDataException("GLB accessor contains a non-finite float value.");
                    values[target++] = value;
                }
            }
            return values;
        }

        private static List<int> ReadIndexAccessor(int accessorIndex, IList<object> accessors,
            IList<object> views, byte[] binaryBytes)
        {
            if (accessorIndex < 0 || accessorIndex >= accessors.Count)
                throw new InvalidDataException("GLB index accessor is outside the accessor table.");
            IDictionary<string, object> accessor = AsDictionary(accessors[accessorIndex], "index accessor");
            if (!string.Equals(GetString(accessor, "type"), "SCALAR", StringComparison.Ordinal))
                throw new InvalidDataException("GLB indices accessor must be SCALAR.");
            int componentType = GetInt(accessor, "componentType", 0);
            int componentBytes = componentType == 5121 ? 1 : componentType == 5123 ? 2 : componentType == 5125 ? 4 : 0;
            if (componentBytes == 0) throw new NotSupportedException("GLB indices must use UNSIGNED_BYTE, UNSIGNED_SHORT or UNSIGNED_INT.");
            int count = GetInt(accessor, "count", -1);
            if (count < 0 || count > MaximumPreviewTriangles * 3) throw new InvalidDataException("GLB index count exceeds the supported preview limit.");
            if (!accessor.ContainsKey("bufferView")) throw new NotSupportedException("Sparse or bufferView-less index accessors are not supported.");
            int viewIndex = GetInt(accessor, "bufferView", -1);
            if (viewIndex < 0 || viewIndex >= views.Count) throw new InvalidDataException("GLB index accessor refers to an invalid bufferView.");
            IDictionary<string, object> view = AsDictionary(views[viewIndex], "bufferView");
            if (GetInt(view, "buffer", 0) != 0) throw new NotSupportedException("Only the first embedded GLB buffer is supported.");
            int viewOffset = GetInt(view, "byteOffset", 0);
            int viewLength = GetInt(view, "byteLength", -1);
            int accessorOffset = GetInt(accessor, "byteOffset", 0);
            int stride = GetInt(view, "byteStride", componentBytes);
            if (viewOffset < 0 || viewLength < 0 || accessorOffset < 0 || stride < componentBytes)
                throw new InvalidDataException("GLB index accessor has invalid offsets or lengths.");
            long lastByte = count == 0 ? (long)viewOffset + accessorOffset : (long)viewOffset + accessorOffset + (long)(count - 1) * stride + componentBytes;
            long viewEnd = (long)viewOffset + viewLength;
            if (lastByte > viewEnd || viewEnd > binaryBytes.Length)
                throw new InvalidDataException("GLB index accessor reads outside its bufferView or BIN chunk.");
            var values = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                int p = checked(viewOffset + accessorOffset + i * stride);
                uint value;
                if (componentType == 5121) value = binaryBytes[p];
                else if (componentType == 5123) value = ReadUInt16(binaryBytes, p);
                else value = ReadUInt32(binaryBytes, p);
                if (value > int.MaxValue) throw new InvalidDataException("GLB index exceeds the supported signed range.");
                values.Add((int)value);
            }
            return values;
        }

        private static byte[] ReadBufferView(int viewIndex, IList<object> views, byte[] binaryBytes)
        {
            if (viewIndex < 0 || viewIndex >= views.Count) throw new InvalidDataException("GLB image refers to an invalid bufferView.");
            IDictionary<string, object> view = AsDictionary(views[viewIndex], "bufferView");
            if (GetInt(view, "buffer", 0) != 0) throw new NotSupportedException("Only the first embedded GLB buffer is supported.");
            int offset = GetInt(view, "byteOffset", 0);
            int length = GetInt(view, "byteLength", -1);
            if (offset < 0 || length < 0 || offset > binaryBytes.Length - length)
                throw new InvalidDataException("GLB image bufferView is outside the BIN chunk.");
            byte[] bytes = new byte[length];
            Buffer.BlockCopy(binaryBytes, offset, bytes, 0, length);
            return bytes;
        }

        private static Vector3D[] GenerateVertexNormals(MeshGeometry3D mesh)
        {
            var normals = new Vector3D[mesh.Positions.Count];
            for (int i = 0; i + 2 < mesh.TriangleIndices.Count; i += 3)
            {
                int a = mesh.TriangleIndices[i];
                int b = mesh.TriangleIndices[i + 1];
                int c = mesh.TriangleIndices[i + 2];
                Vector3D normal = Vector3D.CrossProduct(mesh.Positions[b] - mesh.Positions[a], mesh.Positions[c] - mesh.Positions[a]);
                if (normal.Length > 0.0000001) normal.Normalize();
                else normal = new Vector3D(0, 1, 0);
                normals[a] += normal;
                normals[b] += normal;
                normals[c] += normal;
            }
            for (int i = 0; i < normals.Length; i++)
            {
                if (normals[i].Length > 0.0000001) normals[i].Normalize();
                else normals[i] = new Vector3D(0, 1, 0);
            }
            return normals;
        }

        private static void AddLights(Model3DGroup model)
        {
            model.Children.Add(new AmbientLight(Color.FromRgb(110, 110, 110)));
            model.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -1, -2)));
            model.Children.Add(new DirectionalLight(Color.FromRgb(150, 150, 150), new Vector3D(1, 0.5, 1)));
        }

        public static BitmapSource LoadImageFromFile(string path)
        {
            FileInfo info = new FileInfo(path);
            if (!info.Exists) throw new FileNotFoundException("Image file was not found.", path);
            if (info.Length <= 0 || info.Length > MaximumFileBytes)
                throw new InvalidDataException("Image file size must be between 1 byte and 512 MiB.");
            // Check metadata before BitmapImage.CacheOption.OnLoad materializes the full decoded image.
            using (FileStream metadataStream = File.OpenRead(path))
                ValidateImageDimensions(metadataStream, path);

            using (FileStream stream = File.OpenRead(path))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }

        public static BitmapSource LoadImageFromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumFileBytes)
                throw new InvalidDataException("Embedded image byte length is empty or exceeds 512 MiB.");
            using (var metadataStream = new MemoryStream(bytes, false))
                ValidateImageDimensions(metadataStream, "embedded image");

            using (var stream = new MemoryStream(bytes, false))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }

        private static long ValidateImageDimensions(Stream stream, string description)
        {
            BitmapDecoder decoder = BitmapDecoder.Create(stream,
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            if (decoder.Frames.Count == 0)
                throw new InvalidDataException("Image has no decodable frames: " + description);

            BitmapFrame frame = decoder.Frames[0];
            long pixels = (long)frame.PixelWidth * frame.PixelHeight;
            if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0 || pixels > MaximumImagePixels)
                throw new InvalidDataException("Image dimensions are invalid or exceed the per-image " +
                    MaximumImagePixels.ToString(CultureInfo.InvariantCulture) + "-pixel preview limit: " + description);
            return pixels;
        }

        private static string[] SplitFields(string value)
        {
            return value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }

        private static double ParseDouble(string value)
        {
            double result;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ||
                double.IsNaN(result) || double.IsInfinity(result))
                throw new InvalidDataException("Invalid finite number: " + value);
            return result;
        }

        private static byte ToByte(double value, IList<string> warnings, string field)
        {
            if (value < 0.0 || value > 1.0) warnings.Add(field + " was clamped to the 0..1 range.");
            return (byte)Math.Round(Clamp01(value) * 255.0);
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static object Get(IDictionary<string, object> dictionary, string key)
        {
            object value;
            return dictionary != null && dictionary.TryGetValue(key, out value) ? value : null;
        }

        private static IDictionary<string, object> GetDictionary(IDictionary<string, object> dictionary, string key)
        {
            object value = Get(dictionary, key);
            return value == null ? null : AsDictionary(value, key);
        }

        private static IList<object> GetArray(IDictionary<string, object> dictionary, string key)
        {
            object value = Get(dictionary, key);
            if (value == null) return new List<object>();
            return AsList(value, key);
        }

        private static string GetString(IDictionary<string, object> dictionary, string key)
        {
            object value = Get(dictionary, key);
            return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int GetInt(IDictionary<string, object> dictionary, string key, int defaultValue)
        {
            object value = Get(dictionary, key);
            return value == null ? defaultValue : ToInt(value, key);
        }

        private static int ToInt(object value, string description)
        {
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch (Exception ex) { throw new InvalidDataException("Invalid integer for " + description + ".", ex); }
        }

        private static double ToDouble(object value, string description)
        {
            try
            {
                double result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(result) || double.IsInfinity(result)) throw new InvalidDataException(description + " is non-finite.");
                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Invalid number for " + description + ".", ex);
            }
        }

        private static IDictionary<string, object> AsDictionary(object value, string description)
        {
            IDictionary<string, object> result = value as IDictionary<string, object>;
            if (result == null) throw new InvalidDataException(description + " is not a JSON object.");
            return result;
        }

        private static IList<object> AsList(object value, string description)
        {
            IList<object> list = value as IList<object>;
            if (list != null) return list;
            object[] array = value as object[];
            if (array != null) return array;
            ArrayList nonGeneric = value as ArrayList;
            if (nonGeneric != null)
            {
                var converted = new List<object>(nonGeneric.Count);
                foreach (object item in nonGeneric) converted.Add(item);
                return converted;
            }
            throw new InvalidDataException(description + " is not a JSON array.");
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            if (offset < 0 || offset > bytes.Length - 4) throw new InvalidDataException("Unexpected end of binary data.");
            if (BitConverter.IsLittleEndian) return BitConverter.ToUInt32(bytes, offset);
            byte[] value = new byte[4];
            Buffer.BlockCopy(bytes, offset, value, 0, 4);
            Array.Reverse(value);
            return BitConverter.ToUInt32(value, 0);
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            if (offset < 0 || offset > bytes.Length - 2) throw new InvalidDataException("Unexpected end of binary data.");
            if (BitConverter.IsLittleEndian) return BitConverter.ToUInt16(bytes, offset);
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static float ReadSingle(byte[] bytes, int offset)
        {
            if (offset < 0 || offset > bytes.Length - 4) throw new InvalidDataException("Unexpected end of binary data.");
            if (BitConverter.IsLittleEndian) return BitConverter.ToSingle(bytes, offset);
            byte[] value = new byte[4];
            Buffer.BlockCopy(bytes, offset, value, 0, 4);
            Array.Reverse(value);
            return BitConverter.ToSingle(value, 0);
        }
    }
}
