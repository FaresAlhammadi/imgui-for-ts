using System;
using System.IO;
using System.IO.Compression;

namespace StrayedSupply.Core
{
    /// Renders a decoded Strayed mesh + its texture into an upright, side-view pixel sprite for
    /// People Playground (a 2D game). Runs on the player's PC; the sprite never ships with the mod.
    public static class SpriteBaker
    {
        /// Returns RGBA pixels (top row first). The longest axis of the mesh points up and is scaled to
        /// lengthPx pixels; the thinnest axis faces the viewer. texBgra may be null (flat grey).
        public static byte[] Bake(DecodedMesh m, byte[] texBgra, int texW, int texH, int lengthPx, out int w, out int h)
        {
            if (lengthPx < 2) throw new ArgumentOutOfRangeException(nameof(lengthPx));
            int nv = m.Positions.Length / 3;
            var min = new[] { float.MaxValue, float.MaxValue, float.MaxValue };
            var max = new[] { float.MinValue, float.MinValue, float.MinValue };
            for (int v = 0; v < nv; v++)
                for (int a = 0; a < 3; a++) { min[a] = Math.Min(min[a], m.Positions[v * 3 + a]); max[a] = Math.Max(max[a], m.Positions[v * 3 + a]); }
            var ext = new[] { max[0] - min[0], max[1] - min[1], max[2] - min[2] };
            int up = 0, depth = 0;
            for (int a = 1; a < 3; a++) { if (ext[a] > ext[up]) up = a; if (ext[a] < ext[depth]) depth = a; }
            if (up == depth) depth = (up + 1) % 3;
            int side = 3 - up - depth;

            const int SS = 4;                                   // supersampling per pixel side
            float scale = (lengthPx * SS) / Math.Max(ext[up], 1e-6f);
            w = Math.Max(1, (int)Math.Ceiling(ext[side] * scale / SS)); h = lengthPx;
            int W = w * SS, H = h * SS;
            var col = new float[W * H * 3]; var cov = new bool[W * H]; var zb = new float[W * H];
            for (int i = 0; i < zb.Length; i++) zb[i] = float.MinValue;

            float X(int v) => (m.Positions[v * 3 + side] - min[side]) * scale;
            float Y(int v) => (max[up] - m.Positions[v * 3 + up]) * scale;
            float Z(int v) => m.Positions[v * 3 + depth];

            for (int t = 0; t + 2 < m.Indices.Length; t += 3)
            {
                int a = m.Indices[t], b = m.Indices[t + 1], c = m.Indices[t + 2];
                float ax = X(a), ay = Y(a), bx = X(b), by = Y(b), cx = X(c), cy = Y(c);
                float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
                if (Math.Abs(area) < 1e-9f) continue;
                // flat light from the upper left, based on the face normal in sprite space
                float ux = bx - ax, uy = by - ay, uz = Z(b) - Z(a), vx = cx - ax, vy = cy - ay, vz = Z(c) - Z(a);
                float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                float nl = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz) + 1e-12f;
                float light = 0.55f + 0.45f * Math.Abs((-0.4f * nx - 0.5f * ny + 0.77f * nz) / nl);
                int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx)))), x1 = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
                int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy)))), y1 = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float w0 = ((bx - px) * (cy - py) - (by - py) * (cx - px)) / area;
                        float w1 = ((cx - px) * (ay - py) - (cy - py) * (ax - px)) / area;
                        float w2 = 1 - w0 - w1;
                        if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                        float z = w0 * Z(a) + w1 * Z(b) + w2 * Z(c);
                        int k = y * W + x;
                        if (z <= zb[k]) continue;
                        zb[k] = z; cov[k] = true;
                        float r = 0.6f, g = 0.6f, bl = 0.6f;
                        if (texBgra != null && m.Uv0 != null)
                        {
                            float u = w0 * m.Uv0[a * 2] + w1 * m.Uv0[b * 2] + w2 * m.Uv0[c * 2];
                            float vv = w0 * m.Uv0[a * 2 + 1] + w1 * m.Uv0[b * 2 + 1] + w2 * m.Uv0[c * 2 + 1];
                            int tx = Wrap((int)Math.Floor(u * texW), texW), ty = Wrap((int)Math.Floor(vv * texH), texH);
                            int o = (ty * texW + tx) * 4;
                            bl = texBgra[o] / 255f; g = texBgra[o + 1] / 255f; r = texBgra[o + 2] / 255f;
                        }
                        col[k * 3] = r * light; col[k * 3 + 1] = g * light; col[k * 3 + 2] = bl * light;
                    }
            }

            // box-filter down to the pixel grid; a pixel is solid when at least 40% of it is covered
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float r = 0, g = 0, b = 0; int n = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            int k = (y * SS + sy) * W + x * SS + sx;
                            if (!cov[k]) continue;
                            r += col[k * 3]; g += col[k * 3 + 1]; b += col[k * 3 + 2]; n++;
                        }
                    if (n * 10 < SS * SS * 4) continue;
                    int o = (y * w + x) * 4;
                    rgba[o] = To8(r / n); rgba[o + 1] = To8(g / n); rgba[o + 2] = To8(b / n); rgba[o + 3] = 255;
                }
            return rgba;
        }

        static int Wrap(int i, int n) => ((i % n) + n) % n;
        static byte To8(float f) => (byte)Math.Max(0, Math.Min(255, (int)Math.Round(f * 255)));
    }

    /// Minimal PNG writer (RGBA8, no external libraries).
    public static class Png
    {
        public static byte[] Encode(byte[] rgba, int w, int h)
        {
            if (rgba.Length != w * h * 4) throw new ArgumentException("pixel buffer does not match size");
            var outp = new MemoryStream();
            outp.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            var ihdr = new byte[13];
            BE(ihdr, 0, (uint)w); BE(ihdr, 4, (uint)h); ihdr[8] = 8; ihdr[9] = 6;
            Chunk(outp, "IHDR", ihdr);
            var raw = new byte[(w * 4 + 1) * h];
            for (int y = 0; y < h; y++) Buffer.BlockCopy(rgba, y * w * 4, raw, y * (w * 4 + 1) + 1, w * 4);
            var z = new MemoryStream();
            z.WriteByte(0x78); z.WriteByte(0x9C);
            using (var d = new DeflateStream(z, CompressionLevel.Optimal, true)) d.Write(raw, 0, raw.Length);
            var adler = new byte[4]; BE(adler, 0, Adler32(raw)); z.Write(adler, 0, 4);
            Chunk(outp, "IDAT", z.ToArray());
            Chunk(outp, "IEND", new byte[0]);
            return outp.ToArray();
        }

        static void Chunk(Stream s, string type, byte[] data)
        {
            var hdr = new byte[8]; BE(hdr, 0, (uint)data.Length);
            for (int i = 0; i < 4; i++) hdr[4 + i] = (byte)type[i];
            s.Write(hdr, 0, 8); s.Write(data, 0, data.Length);
            uint c = 0xFFFFFFFF;
            for (int i = 4; i < 8; i++) c = Crc(c, hdr[i]);
            foreach (var x in data) c = Crc(c, x);
            var crc = new byte[4]; BE(crc, 0, ~c); s.Write(crc, 0, 4);
        }

        static uint Crc(uint c, byte x) { c ^= x; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; return c; }
        static uint Adler32(byte[] d) { uint a = 1, b = 0; foreach (var x in d) { a = (a + x) % 65521; b = (b + a) % 65521; } return (b << 16) | a; }
        static void BE(byte[] b, int at, uint v) { b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v; }
    }
}
