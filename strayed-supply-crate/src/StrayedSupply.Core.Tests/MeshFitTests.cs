using System;
using StrayedSupply.Core;
using Xunit;

public class MeshFitTests
{
    // a 10 x 2 x 1 box-ish triangle fan lying along +X, off the origin, in "centimetres"
    static DecodedMesh LongAlongX() => new DecodedMesh
    {
        Name = "stick",
        Positions = new float[] { 100, 50, 0,  110, 50, 0,  110, 52, 1,  100, 52, 1 },
        Normals = new float[] { 1, 0, 0,  1, 0, 0,  1, 0, 0,  1, 0, 0 },
        Indices = new[] { 0, 1, 2, 0, 2, 3 },
    };

    [Fact]
    public void LongestAxisBecomesUpAtTheRequestedLength()
    {
        var m = MeshFit.Upright(LongAlongX(), 0.85f);
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int v = 0; v < 4; v++) { minY = Math.Min(minY, m.Positions[v * 3 + 1]); maxY = Math.Max(maxY, m.Positions[v * 3 + 1]); }
        Assert.Equal(0.85f, maxY - minY, 4);
        Assert.Equal(0f, (maxY + minY) / 2, 4);          // centred
        Assert.Equal(1f, m.Normals[1]);                   // old +X normal now points +Y
        Assert.Equal(0f, m.Normals[0]);
    }

    [Fact]
    public void ReorientingIsARotationNotAMirror()
    {
        var src = LongAlongX();
        var m = MeshFit.Upright(src, 1f);
        Assert.Equal(new[] { 0, 1, 2, 0, 2, 3 }, m.Indices);   // winding unchanged
        // the first face's normal (from its winding) must be the source normal rotated X->Y, Y->-X
        var n0 = FaceNormal(src.Positions); var n1 = FaceNormal(m.Positions);
        var expected = Normalize(new[] { -n0[1], n0[0], n0[2] });
        Assert.Equal(expected[0], n1[0], 4);
        Assert.Equal(expected[1], n1[1], 4);
        Assert.Equal(expected[2], n1[2], 4);
    }

    static float[] FaceNormal(float[] p)
    {
        float ux = p[3] - p[0], uy = p[4] - p[1], uz = p[5] - p[2], vx = p[6] - p[0], vy = p[7] - p[1], vz = p[8] - p[2];
        return Normalize(new[] { uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx });
    }

    static float[] Normalize(float[] v)
    {
        float l = (float)Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
        return new[] { v[0] / l, v[1] / l, v[2] / l };
    }

    [Fact]
    public void AlreadyUprightMeshKeepsWinding()
    {
        var up = new DecodedMesh { Name = "u", Positions = new float[] { 0, 0, 0, 1, 0, 0, 0, 5, 0 }, Indices = new[] { 0, 1, 2 } };
        Assert.Equal(new[] { 0, 1, 2 }, MeshFit.Upright(up, 2f).Indices);
    }

    [Fact]
    public void NonPositiveLengthIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MeshFit.Upright(LongAlongX(), 0f));
    }
}
