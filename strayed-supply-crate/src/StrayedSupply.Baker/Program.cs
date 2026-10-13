// systems.sprite_bake: turns the player's own Strayed items into People Playground sprites.
// Reads the items sheet, decodes each mesh + texture from the Strayed install and writes <id>.png
// into the mod's strayed/ folder. Runs on the player's PC; nothing from Strayed ships with the mod.
//   StrayedSupplyBaker "<Strayed folder>" <classdata.tpk> <items.json> "<mod folder>/strayed"
using System;
using System.IO;
using System.Text.Json;
using StrayedSupply.Core;

const int PixelsPerMetre = 50;   // People Playground's own Baseball Bat is 42 px for a ~0.84 m bat

if (args.Length != 4) { Console.Error.WriteLine("usage: StrayedSupplyBaker <strayed folder> <classdata.tpk> <items.json> <out folder>"); return 2; }
if (!File.Exists(args[1])) { Console.Error.WriteLine($"classdata.tpk not found: {args[1]}"); return 2; }
var sheet = JsonDocument.Parse(File.ReadAllText(args[2]));
Directory.CreateDirectory(args[3]);
using var idx = new StrayedIndex(args[0], args[1]);
int failed = 0;
foreach (var row in sheet.RootElement.GetProperty("rows").EnumerateArray())
{
    string id = row.GetProperty("id").GetString();
    try
    {
        var mesh = idx.LoadMesh(row.GetProperty("strayedMesh").GetString());
        byte[] tex = idx.LoadTexture(row.GetProperty("strayedTexture").GetString(), out int tw, out int th);
        int lengthPx = (int)Math.Round(row.GetProperty("lengthM").GetDouble() * PixelsPerMetre);
        var rgba = SpriteBaker.Bake(mesh, tex, tw, th, lengthPx, out int w, out int h);
        File.WriteAllBytes(Path.Combine(args[3], id + ".png"), Png.Encode(rgba, w, h));
        Console.WriteLine($"{id,-14} {w}x{h} px  <- {mesh.Name}");
    }
    catch (Exception e) { failed++; Console.Error.WriteLine($"{id}: {e.Message}"); }
}
return failed == 0 ? 0 : 1;
