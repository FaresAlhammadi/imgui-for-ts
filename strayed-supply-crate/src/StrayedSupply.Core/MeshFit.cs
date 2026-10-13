using System;

namespace StrayedSupply.Core
{
    /// Strayed authors its item meshes at different scales (the Rock is 0.8 units long, the Torch 0.005),
    /// so each one is refit to the items sheet's real length: longest axis up (+Y), centred on the origin.
    public static class MeshFit
    {
        public static DecodedMesh Upright(DecodedMesh m, float lengthM)
        {
            if (!(lengthM > 0)) throw new ArgumentOutOfRangeException(nameof(lengthM));
            int nv = m.Positions.Length / 3;
            var min = new[] { float.MaxValue, float.MaxValue, float.MaxValue };
            var max = new[] { float.MinValue, float.MinValue, float.MinValue };
            for (int v = 0; v < nv; v++)
                for (int a = 0; a < 3; a++) { min[a] = Math.Min(min[a], m.Positions[v * 3 + a]); max[a] = Math.Max(max[a], m.Positions[v * 3 + a]); }
            int up = 0;
            for (int a = 1; a < 3; a++) if (max[a] - min[a] > max[up] - min[up]) up = a;
            // a proper rotation (no mirror, so faces keep their winding and the item its handedness):
            // new axis a = sign[a] * old axis order[a]. X longest: 90 deg about Z; Z longest: -90 deg about X.
            int[] order = up == 0 ? new[] { 1, 0, 2 } : up == 2 ? new[] { 0, 2, 1 } : new[] { 0, 1, 2 };
            float[] sign = up == 0 ? new[] { -1f, 1f, 1f } : up == 2 ? new[] { 1f, 1f, -1f } : new[] { 1f, 1f, 1f };
            float scale = lengthM / Math.Max(max[up] - min[up], 1e-9f);

            var outp = new DecodedMesh
            {
                Name = m.Name,
                Positions = new float[m.Positions.Length],
                Normals = m.Normals == null ? null : new float[m.Normals.Length],
                Uv0 = m.Uv0,
                Indices = m.Indices,
            };
            for (int v = 0; v < nv; v++)
                for (int a = 0; a < 3; a++)
                {
                    int src = order[a];
                    outp.Positions[v * 3 + a] = sign[a] * (m.Positions[v * 3 + src] - (min[src] + max[src]) / 2) * scale;
                    if (outp.Normals != null) outp.Normals[v * 3 + a] = sign[a] * m.Normals[v * 3 + src];
                }
            return outp;
        }
    }
}
