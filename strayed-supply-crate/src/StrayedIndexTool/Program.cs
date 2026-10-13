// Lists mesh and texture names in the player's Strayed install that match words, so the items
// sheet's strayedMesh / strayedTexture cells can be filled with real names. Reads only; copies nothing.
//   dotnet run -- "<Strayed folder>" <classdata.tpk> rock torch hatchet bat
using System;
using System.Linq;
using StrayedSupply.Core;

if (args.Length < 2) { Console.Error.WriteLine("usage: StrayedIndexTool <strayed folder> <classdata.tpk> [words...]"); return 2; }
using var idx = new StrayedIndex(args[0], args[1]);
var words = args.Skip(2).ToArray();
bool Match(string n) => words.Length == 0 || words.Any(w => n.Contains(w, StringComparison.OrdinalIgnoreCase));
Console.WriteLine($"# {idx.DataFolder}: {idx.Meshes.Count} meshes, {idx.Textures.Count} textures");
string Show(StrayedIndex.Entry e) => idx.IsAmbiguous(e) ? StrayedIndex.Key(e) + "  [name reused; use this key]" : e.Name;
foreach (var e in idx.Meshes.Values.Distinct().Where(e => Match(e.Name)).OrderBy(e => e.Name)) Console.WriteLine($"mesh     {Show(e)}  ({e.File})");
foreach (var e in idx.Textures.Values.Distinct().Where(e => Match(e.Name)).OrderBy(e => e.Name)) Console.WriteLine($"texture  {Show(e)}  ({e.File})");
return 0;
