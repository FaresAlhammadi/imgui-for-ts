using System;
using System.IO;
using StrayedSupply.Core;
using Xunit;

public class MeshDecoderTests
{
    [Fact]
    public void DecodesInterleavedFloatPositionsAndHalfUvs()
    {
        // one stream: position float3 @0, uv0 half2 @12 -> stride 16, 3 vertices
        var ch = new[] { new VertexChannel(0, 0, 0, 3), new VertexChannel(0, 0, 0, 0), new VertexChannel(0, 0, 0, 0),
                         new VertexChannel(0, 0, 0, 0), new VertexChannel(0, 12, 1, 2) };
        var ms = new MemoryStream(); var w = new BinaryWriter(ms);
        for (int v = 0; v < 3; v++) { w.Write(v * 1f); w.Write(2f); w.Write(-v * 1f); w.Write((ushort)0x3800); w.Write((ushort)0x3C00); }
        var idx = new byte[6]; Buffer.BlockCopy(new ushort[] { 0, 1, 2 }, 0, idx, 0, 6);
        var m = MeshDecoder.Decode("t", 3, ch, ms.ToArray(), idx, false);
        Assert.Equal(new float[] { 0, 2, 0, 1, 2, -1, 2, 2, -2 }, m.Positions);
        Assert.Equal(new float[] { 0.5f, 1, 0.5f, 1, 0.5f, 1 }, m.Uv0);
        Assert.Null(m.Normals);
        Assert.Equal(new[] { 0, 1, 2 }, m.Indices);
    }

    [Fact]
    public void SecondStreamStartsOn16ByteBoundary()
    {
        // stream 0: position float3 (stride 12) x 3 verts = 36 bytes -> stream 1 starts at 48
        var ch = new[] { new VertexChannel(0, 0, 0, 3), new VertexChannel(1, 0, 3, 3) };
        var data = new byte[48 + 9];
        for (int i = 0; i < 9; i++) data[48 + i] = (byte)(sbyte)127;
        var m = MeshDecoder.Decode("t", 3, ch, data, new byte[12], true);
        Assert.All(m.Normals, n => Assert.Equal(1f, n));
        Assert.Equal(3, m.Indices.Length);
    }

    [Fact]
    public void MissingPositionChannelThrows()
    {
        var ch = new[] { new VertexChannel(0, 0, 0, 0) };
        Assert.Throws<InvalidOperationException>(() => MeshDecoder.Decode("t", 1, ch, new byte[0], new byte[0], false));
    }

    [Fact]
    public void MissingStrayedFolderIsReported()
    {
        Assert.Throws<DirectoryNotFoundException>(() => StrayedIndex.FindDataFolder("/no/such/strayed"));
    }

    [Fact]
    public void MissingClassDataIsReportedForFilesWithoutTypeTrees()
    {
        // minimal v22 serialized file: enableTypeTree = false, no types, no objects
        var dir = Directory.CreateTempSubdirectory().FullName;
        var data = Directory.CreateDirectory(Path.Combine(dir, "Game_Data")).FullName;
        File.WriteAllBytes(Path.Combine(data, "globalgamemanagers"), new byte[0]);
        var meta = new MemoryStream(); var mw = new BinaryWriter(meta);
        mw.Write(System.Text.Encoding.ASCII.GetBytes("2022.3.0f1\0")); mw.Write(19); mw.Write((byte)0);
        mw.Write(0); mw.Write(0); mw.Write(0); mw.Write(0); mw.Write(0); mw.Write((byte)0);
        var m = meta.ToArray();
        var f = new MemoryStream(); var w = new BinaryWriter(f);
        void Be32(uint v) { var b = BitConverter.GetBytes(v); Array.Reverse(b); w.Write(b); }
        void Be64(ulong v) { var b = BitConverter.GetBytes(v); Array.Reverse(b); w.Write(b); }
        Be32(0); Be32(0); Be32(22); Be32(0); w.Write(0);
        Be32((uint)m.Length); Be64((ulong)(48 + m.Length)); Be64((ulong)(48 + m.Length)); Be64(0);
        w.Write(m);
        File.WriteAllBytes(Path.Combine(data, "sharedassets0.assets"), f.ToArray());
        try { Assert.Throws<FileNotFoundException>(() => new StrayedIndex(dir, null)); }
        finally { Directory.Delete(dir, true); }
    }
}
