using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using StrayedSupply.Core;
using Xunit;

public class SpriteBakerTests
{
    // 1 wide x 2 tall quad facing +z, UVs covering the whole texture
    static DecodedMesh Quad() => new DecodedMesh
    {
        Name = "quad",
        Positions = new float[] { 0, 0, 0, 1, 0, 0, 1, 2, 0, 0, 2, 0 },
        Uv0 = new float[] { 0, 0, 1, 0, 1, 1, 0, 1 },
        Indices = new[] { 0, 1, 2, 0, 2, 3 },
    };

    [Fact]
    public void LongestAxisBecomesSpriteHeight()
    {
        var px = SpriteBaker.Bake(Quad(), null, 0, 0, 20, out int w, out int h);
        Assert.Equal(20, h);
        Assert.Equal(10, w);
        Assert.Equal(w * h * 4, px.Length);
        Assert.All(Enumerable.Range(0, w * h), i => Assert.Equal(255, px[i * 4 + 3]));
    }

    [Fact]
    public void SamplesTheTexture()
    {
        var red = new byte[4 * 4 * 4];
        for (int i = 0; i < 16; i++) { red[i * 4 + 2] = 255; red[i * 4 + 3] = 255; }   // BGRA
        var px = SpriteBaker.Bake(Quad(), red, 4, 4, 20, out int w, out int h);
        int mid = (h / 2 * w + w / 2) * 4;
        Assert.True(px[mid] > 200);
        Assert.Equal(0, px[mid + 1]);
        Assert.Equal(0, px[mid + 2]);
    }

    [Fact]
    public void TooShortSpriteIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpriteBaker.Bake(Quad(), null, 0, 0, 1, out _, out _));
    }

    [Fact]
    public void PngRoundTripsPixels()
    {
        int w = 3, h = 2;
        var rgba = Enumerable.Range(0, w * h * 4).Select(i => (byte)(i * 7)).ToArray();
        var png = Png.Encode(rgba, w, h);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.Take(8));
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(w, (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19]);
        Assert.Equal(h, (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);
        int idatLen = (png[33] << 24) | (png[34] << 16) | (png[35] << 8) | png[36];
        Assert.Equal("IDAT", System.Text.Encoding.ASCII.GetString(png, 37, 4));
        using var inflate = new DeflateStream(new MemoryStream(png, 41 + 2, idatLen - 6), CompressionMode.Decompress);
        var raw = new MemoryStream(); inflate.CopyTo(raw);
        var rows = raw.ToArray();
        Assert.Equal((w * 4 + 1) * h, rows.Length);
        for (int y = 0; y < h; y++)
        {
            Assert.Equal(0, rows[y * (w * 4 + 1)]);
            Assert.Equal(rgba.Skip(y * w * 4).Take(w * 4), rows.Skip(y * (w * 4 + 1) + 1).Take(w * 4));
        }
    }
}
