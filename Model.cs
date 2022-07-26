using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Core.IO;
using OpenTK;
using IONET.Core.Model;
using IONET.Core;

namespace TTLibrary
{
    internal class Model
    {
        public string Name { get; set; }

        public Model(string filePath) {
            Read(new FileReader(filePath));
        }

        private void Read(FileReader reader)
        {
            //Read as big endian. Only the raw vertex/index data is little endian
            reader.SetByteOrder(true);
            //Read to the end and make sure all file chunks are loaded
            while (reader.BaseStream.Length > reader.Position) {
                long pos = reader.Position;
                var chunk = reader.ReadStruct<ResourceChunk>();
                ParseChunk(reader, chunk);
                reader.SeekBegin(pos + chunk.Size + 4);
            }
        }

        private void ParseChunk(FileReader reader, ResourceChunk chunk)
        {
            string type = new string(chunk.Type);
            switch (type)
            {
                case ".CC4HSERHSER": //Resource Hierarchy
                    reader.ReadZeroTerminatedString();
                    Name = ReadString(reader);
                    break;
                case ".CC4HSER2CSG": //Scene Data
                    ParseCSG(reader);
                    break;
            }
        }

        private void ParseCSG(FileReader reader)
        {
            IOModel model = new IOModel();
            model.Name = "Model";

            uint numMaterials = reader.ReadUInt32();
            ushort unk = reader.ReadUInt16();
            reader.ReadInt16(); //padding

            //Read materials first
            Material[] materials = new Material[numMaterials];
            for (int i = 0; i < numMaterials; i++)
            {
                materials[i] = new Material();
                materials[i].FilePath = ReadString(reader);
                materials[i].Name = ReadString(reader);
                reader.Seek(3);
            }
            //Next is the sub meshes.
            reader.ReadUInt32(); //padding
            uint numMeshes = reader.ReadUInt32();
            reader.ReadUInt32(); //1

            SubMesh[] meshes = new SubMesh[numMeshes];
            for (int i = 0; i < numMeshes; i++)
            {
                meshes[i] = reader.ReadStruct<SubMesh>();

                Buffer[] buffers = ParseDXTV(reader, meshes[i]);

                //Indices come after all the buffer DXTVs
                uint numIndices = reader.ReadUInt32();
                uint indexFormat = reader.ReadUInt32(); //2 for ushort

                if (indexFormat != 2 && indexFormat != 4)
                    throw new Exception($"Unknown index format! {indexFormat}");

                reader.SetByteOrder(false);

                uint[] indices = new uint[numIndices];
                for (int j = 0; j < numIndices; j++)
                {
                    if (indexFormat == 2) indices[j] = reader.ReadUInt16();
                    if (indexFormat == 4) indices[j] = reader.ReadUInt32();
                }

                reader.SetByteOrder(true);
                //70 extra bytes we need to skip till the next sub mesh section
                reader.Seek(70 + 4 * buffers.Length);
                //Convert the buffers and indices to a usable .iomesh to make a .dae file
                model.Meshes.Add(ConvertToDae(meshes[i],i, buffers, indices));
            }
            //Convert to ioscene and export as .dae
            var scene = new IOScene();
            scene.Models.Add(model);

            //Todo no material support atm
            foreach (var mat in materials)
            {
               /* scene.Materials.Add(new IOMaterial() {
                    Name = mat.Name,
                });*/
            }
            IONET.IOManager.ExportScene(scene, $"{System.IO.Path.GetFileNameWithoutExtension(Name)}.dae");

            Console.WriteLine();
        }

        private Buffer[] ParseDXTV(FileReader reader, SubMesh mesh)
        {
            Buffer[] buffers = new Buffer[mesh.NumBuffers];
            for (int i = 0; i < mesh.NumBuffers; i++)
            {
                Buffer buffer = new Buffer();
                buffers[i] = buffer;

                reader.ReadSignature(4, "DXTV");
                uint version = reader.ReadUInt32();
                uint numAttributes = reader.ReadUInt32();

                buffer.Attributes = new Attribute[numAttributes];
                for (int j = 0; j < numAttributes; j++)
                    buffer.Attributes[j] = new Attribute()
                    {
                        Type = (AttributeType)reader.ReadByte(),
                        Format = (AttributeFormat)reader.ReadByte(),
                        Offset = reader.ReadByte(),
                    };

                reader.Seek(6);
                var stride = buffer.Attributes.Sum(x => x.GetStride());

                for (int j = 0; j < numAttributes; j++)
                    buffer.Attributes[j].Data = new Vector4[mesh.NumVerts];

                reader.SetByteOrder(false);

                long pos = reader.Position;
                for (int v = 0; v < mesh.NumVerts; v++) {
                    for (int j = 0; j < numAttributes; j++) {
                        reader.SeekBegin(pos + stride * v + buffer.Attributes[j].Offset);
                        buffer.Attributes[j].Data[v] = ParseVertex(reader, buffer.Attributes[j].Format);
                    }
                }
                reader.SetByteOrder(true);

                reader.SeekBegin(pos + stride * mesh.NumVerts + 16);
            }
            return buffers;
        }

        public Vector4 ParseVertex(FileReader reader, AttributeFormat format)
        {
            switch (format)
            {
                case AttributeFormat.Vec2Float:
                    return new Vector4(
                        reader.ReadSingle(), reader.ReadSingle(), 0, 0);
                case AttributeFormat.Vec3Float:
                    return new Vector4(
                        reader.ReadSingle(), reader.ReadSingle(),
                        reader.ReadSingle(), 0);
                case AttributeFormat.Vec4Float:
                    return new Vector4(
                        reader.ReadSingle(), reader.ReadSingle(),
                        reader.ReadSingle(), reader.ReadSingle());
                case AttributeFormat.Vec4Byte:
                    return new Vector4(
                        reader.ReadByte(), reader.ReadByte(),
                        reader.ReadByte(), reader.ReadByte());
                case AttributeFormat.Vec4ByteF:
                    return new Vector4(
                        reader.ReadByte() / 255.0f, reader.ReadByte() / 255.0f,
                        reader.ReadByte() / 255.0f, reader.ReadByte() / 255.0f);
                case AttributeFormat.Vec2Half:
                    return new Vector4(
                        reader.ReadHalfSingle(), reader.ReadHalfSingle(),  0, 0);
                case AttributeFormat.Vec4Half:
                    return new Vector4(
                        reader.ReadHalfSingle(), reader.ReadHalfSingle(),
                        reader.ReadHalfSingle(), reader.ReadHalfSingle());
                case AttributeFormat.Color4Byte:
                    return new Vector4(
                        reader.ReadByte(), reader.ReadByte(),
                        reader.ReadByte(), reader.ReadByte());
            }
            return Vector4.Zero;
        }

        private string ReadString(FileReader reader)
        {
            string str = reader.ReadZeroTerminatedString();
            reader.ReadZeroTerminatedString();
            return str;
        }
        private IOMesh ConvertToDae(SubMesh subMesh, int index, Buffer[] buffers, uint[] indices)
        {
            //Convertable IO mesh to .dae
            IOMesh mesh = new IOMesh();
            mesh.Name = $"Mesh_{index}";

            for (int v = 0; v < subMesh.NumVerts; v++)
            {
                IOVertex vertex = new IOVertex();
                mesh.Vertices.Add(vertex);

                foreach (var buffer in buffers)
                {
                    foreach (var attribute in buffer.Attributes)
                    {
                        switch (attribute.Type)
                        {
                            case AttributeType.Position:
                                vertex.Position = new System.Numerics.Vector3(
                                     attribute.Data[v].X, attribute.Data[v].Y, attribute.Data[v].Z);
                                break;
                            case AttributeType.Normal:
                                vertex.Normal = new System.Numerics.Vector3(
                                     attribute.Data[v].X, attribute.Data[v].Y, attribute.Data[v].Z);
                                break;
                            case AttributeType.UvSet01:
                                vertex.SetUV(attribute.Data[v].X, attribute.Data[v].Y, 0);
                                break;
                            case AttributeType.UvSet2:
                                vertex.SetUV(attribute.Data[v].X, attribute.Data[v].Y, 1);
                                break;
                            case AttributeType.ColorSet0:
                                vertex.SetColor(attribute.Data[v].X/255, attribute.Data[v].Y/255, attribute.Data[v].Z/255, attribute.Data[v].W/255, 0);
                                break;
                            case AttributeType.ColorSet1:
                                vertex.SetColor(attribute.Data[v].X/255, attribute.Data[v].Y/255, attribute.Data[v].Z/255, attribute.Data[v].W/255, 1);
                                break;
                        }
                    }
                }
            }

            var poly = new IOPolygon();
            for (int j = 0; j < indices.Length; j++)
                poly.Indicies.Add((int)indices[j]);

            mesh.Polygons.Add(poly);
            return mesh;
        }

        public class Buffer
        {
            public Attribute[] Attributes { get; set; }
            public Vector4[] Data { get; set; }
        }

        public class Material
        {
            public string FilePath { get; set; }
            public string Name { get; set; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class SubMesh
        {
            public uint Magic;
            public uint Version;
            public uint NumBuffers;
            public uint Unknown1;
            public uint Unknown2;
            public uint NumVerts;
        }

        public class Attribute
        {
            public AttributeType Type;
            public AttributeFormat Format;
            public byte Offset;

            public uint GetStride()
            {
                switch (Format)
                {
                    case AttributeFormat.Vec2Float: return 8;
                    case AttributeFormat.Vec3Float: return 12;
                    case AttributeFormat.Vec4Float: return 16;
                    case AttributeFormat.Vec2Half: return 4;
                    case AttributeFormat.Vec4Half: return 8;
                    case AttributeFormat.Vec4Byte: return 4;
                    case AttributeFormat.Vec4ByteF: return 4;
                    case AttributeFormat.Color4Byte: return 4;
                    default:
                        throw new Exception($"Unknown format! {Format}");
                }
            }
            public Vector4[] Data { get; set; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class ResourceChunk
        {
            public uint Size;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public char[] Type;
            public uint Version;
        }

        public enum AttributeType : byte
        {
            Position = 0,
            Normal = 1,
            ColorSet0 = 2,
            Tangent = 3,
            ColorSet1 = 4,
            UvSet01 = 5,
            unknown6 = 6,
            UvSet2 = 7,
            Unknown8 = 8,
            BlendIndices0 = 9,
            BlendWeight0 = 10,
            Unknown11 = 11, 
            LightDirSet = 12, 
            LightColSet = 13, 
        }

        public enum AttributeFormat : byte
        {
            Vec2Float = 2,
            Vec3Float = 3,
            Vec4Float = 4,
            Vec2Half = 5,
            Vec4Half = 6,
            Vec4Byte = 7,
            Vec4ByteF = 8,
            Color4Byte = 9,
        }
    }
}
