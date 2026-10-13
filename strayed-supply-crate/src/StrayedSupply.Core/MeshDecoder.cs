using System;
using System.Collections.Generic;

namespace StrayedSupply.Core
{
    /// One vertex channel as Unity stores it in Mesh.m_VertexData.m_Channels (Unity 2019+ layout).
    public struct VertexChannel
    {
        public int Stream, Offset, Format, Dimension;
        public VertexChannel(int stream, int offset, int format, int dimension)
        { Stream = stream; Offset = offset; Format = format; Dimension = dimension & 0xF; }
    }

    /// A Strayed mesh decoded into plain arrays, ready to hand to UnityEngine.Mesh in Animal Company.
    public sealed class DecodedMesh
    {
        public string Name;
        public float[] Positions;   // xyz per vertex
        public float[] Normals;     // xyz per vertex, or null
        public float[] Uv0;         // uv per vertex, or null
        public int[] Indices;       // triangle list
    }

    public static class MeshDecoder
    {
        // Unity VertexFormat (2019+): Float32, Float16, UNorm8, SNorm8, UNorm16, SNorm16, UInt8, SInt8, UInt16, SInt16, UInt32, SInt32
        static readonly int[] FormatSize = { 4, 2, 1, 1, 2, 2, 1, 1, 2, 2, 4, 4 };
        const int ChPosition = 0, ChNormal = 1, ChUv0 = 4;

        public static DecodedMesh Decode(string name, int vertexCount, IList<VertexChannel> channels, byte[] vertexData,
                                         byte[] indexBuffer, bool indices32)
        {
            int streams = 0;
            foreach (var c in channels) if (c.Dimension > 0) streams = Math.Max(streams, c.Stream + 1);
            var stride = new int[streams];
            foreach (var c in channels)
                if (c.Dimension > 0) stride[c.Stream] = Math.Max(stride[c.Stream], c.Offset + Size(c.Format) * c.Dimension);
            var start = new int[streams];
            for (int s = 0, pos = 0; s < streams; s++)
            {
                start[s] = pos;
                pos += stride[s] * vertexCount;
                pos = (pos + 15) & ~15;
            }

            float[] Read(int ch, int want)
            {
                if (ch >= channels.Count || channels[ch].Dimension == 0) return null;
                var c = channels[ch];
                var outp = new float[vertexCount * want];
                int size = Size(c.Format);
                for (int v = 0; v < vertexCount; v++)
                    for (int d = 0; d < want && d < c.Dimension; d++)
                        outp[v * want + d] = Component(vertexData, start[c.Stream] + v * stride[c.Stream] + c.Offset + d * size, c.Format);
                return outp;
            }

            var mesh = new DecodedMesh { Name = name, Positions = Read(ChPosition, 3), Normals = Read(ChNormal, 3), Uv0 = Read(ChUv0, 2) };
            if (mesh.Positions == null) throw new InvalidOperationException($"mesh '{name}' has no position channel");
            int n = indexBuffer.Length / (indices32 ? 4 : 2);
            mesh.Indices = new int[n];
            for (int i = 0; i < n; i++)
                mesh.Indices[i] = indices32 ? (int)BitConverter.ToUInt32(indexBuffer, i * 4) : BitConverter.ToUInt16(indexBuffer, i * 2);
            return mesh;
        }

        static int Size(int format) =>
            format >= 0 && format < FormatSize.Length ? FormatSize[format] : throw new NotSupportedException($"vertex format {format}");

        static float Component(byte[] b, int at, int format)
        {
            switch (format)
            {
                case 0: return BitConverter.ToSingle(b, at);
                case 1: return HalfToFloat(BitConverter.ToUInt16(b, at));
                case 2: return b[at] / 255f;
                case 3: return Math.Max((sbyte)b[at] / 127f, -1f);
                case 4: return BitConverter.ToUInt16(b, at) / 65535f;
                case 5: return Math.Max(BitConverter.ToInt16(b, at) / 32767f, -1f);
                case 6: return b[at];
                case 7: return (sbyte)b[at];
                case 8: return BitConverter.ToUInt16(b, at);
                case 9: return BitConverter.ToInt16(b, at);
                case 10: return BitConverter.ToUInt32(b, at);
                case 11: return BitConverter.ToInt32(b, at);
                default: throw new NotSupportedException($"vertex format {format}");
            }
        }

        static float HalfToFloat(ushort h)
        {
            int sign = (h >> 15) & 1, exp = (h >> 10) & 0x1F, man = h & 0x3FF;
            float v = exp == 0 ? man / 1024f * (float)Math.Pow(2, -14)
                    : exp == 31 ? (man == 0 ? float.PositiveInfinity : float.NaN)
                    : (1 + man / 1024f) * (float)Math.Pow(2, exp - 15);
            return sign == 1 ? -v : v;
        }
    }
}
