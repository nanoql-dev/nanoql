// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using NanoQL.Bind.Selecting;

namespace NanoQL.Bind.Tests;

/// <summary>
/// <c>nanoql-bind freeze</c> (docs/bindings.md, "Names"): on a green tree it writes both shipped files, the C surface
/// <c>surface.json</c> byte for byte and every Excel name by its family's key or its C name, after which the tree's
/// <c>check</c> and <c>excel check</c> stay green; on a tree whose checked-in header or Excel surface is stale it
/// refuses and writes nothing; and a name a release shipped that the surface drops is retired. Each runs on a scratch
/// copy of the repository's checked-in surface.
/// </summary>
public class FreezeTests
{
    [Fact]
    public void AGreenTreeFreezesAndStaysGreen()
    {
        string repo = ScratchRepository();
        try
        {
            Assert.Equal(0, Program.Run(["freeze"], repo, NoProcess));
            Assert.True(File.Exists(Path.Combine(repo, "bindings", "surface.shipped.json")));
            Assert.True(File.Exists(Path.Combine(repo, "bindings", "excel", "functions.shipped.json")));
            Assert.Equal(File.ReadAllText(Path.Combine(repo, "bindings", "surface.json")), File.ReadAllText(Path.Combine(repo, "bindings", "surface.shipped.json")));

            // Every Excel name of functions.json, in its order, a family's by its key and any other by its C name.
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "bindings", "excel", "functions.json")));
            using JsonDocument names = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "bindings", "excel", "functions.shipped.json")));
            var want = manifest.RootElement.GetProperty("functions").EnumerateArray().Select(f => f.TryGetProperty("key", out JsonElement k)
                ? (f.GetProperty("excel").GetString(), "key", k.GetString()) : (f.GetProperty("excel").GetString(), "c", f.GetProperty("c").GetString())).ToList();
            var got = names.RootElement.GetProperty("functions").EnumerateArray().Select(f => f.TryGetProperty("key", out JsonElement k)
                ? (f.GetProperty("excel").GetString(), "key", k.GetString()) : (f.GetProperty("excel").GetString(), "c", f.GetProperty("c").GetString())).ToList();
            Assert.Equal(want, got);
            Assert.Contains(got, n => n.Item2 == "key");
            Assert.Equal(0, names.RootElement.GetProperty("retired").GetArrayLength());
            Assert.Equal(0, Program.Run(["check"], repo, NoProcess));
            Assert.Equal(0, Program.Run(["excel", "check"], repo, NoProcess));
        }
        finally
        {
            Directory.Delete(repo, true);
        }
    }

    [Fact]
    public void AStaleExcelSurfaceRefusesAndWritesNothing()
    {
        string repo = ScratchRepository();
        try
        {
            File.AppendAllText(Path.Combine(repo, "bindings", "excel", "functions.json"), "\n");
            var e = Assert.Throws<BindException>(() => Program.Run(["freeze"], repo, NoProcess));
            Assert.Contains("'check' reads 3/3 and 'excel check' 0/1", e.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(repo, "bindings", "surface.shipped.json")));
            Assert.False(File.Exists(Path.Combine(repo, "bindings", "excel", "functions.shipped.json")));
        }
        finally
        {
            Directory.Delete(repo, true);
        }
    }

    /// <summary>
    /// A name the last release shipped and the surface no longer holds is retired, never freed: a C function, a type
    /// and an Excel name planted in the shipped files, as a removed function's would be, retired by the next freeze,
    /// kept retired by the one after, and the tree still green.
    /// </summary>
    [Fact]
    public void DroppedNamesAreRetired()
    {
        string repo = ScratchRepository();
        try
        {
            string bindings = Path.Combine(repo, "bindings");
            Assert.Equal(0, Program.Run(["freeze"], repo, NoProcess));
            string shipped = Path.Combine(bindings, "surface.shipped.json"), excelShipped = Path.Combine(bindings, "excel", "functions.shipped.json");
            JsonNode c = JsonNode.Parse(File.ReadAllText(shipped))!;
            c["functions"]!.AsArray().Add(new JsonObject { ["name"] = "nql_Zzz_Removed", ["type"] = "NanoQL.Zzz", ["key"] = "NanoQL.Zzz::Removed()" });
            c["types"]!.AsArray().Add(new JsonObject { ["name"] = "NanoQL.Zzz", ["cname"] = "Zzz" });
            File.WriteAllText(shipped, c.ToJsonString());
            JsonNode x = JsonNode.Parse(File.ReadAllText(excelShipped))!;
            x["functions"]!.AsArray().Add(new JsonObject { ["excel"] = "nqlZzzRemoved", ["c"] = "nql_Zzz_Removed" });
            File.WriteAllText(excelShipped, x.ToJsonString());
            foreach (int pass in new[] { 1, 2 })
            {
                Assert.Equal(0, Program.Run(["freeze"], repo, NoProcess));
                using JsonDocument cs = JsonDocument.Parse(File.ReadAllText(shipped));
                using JsonDocument xs = JsonDocument.Parse(File.ReadAllText(excelShipped));
                JsonElement retired = cs.RootElement.GetProperty("retired");
                Assert.Equal(["nql_Zzz_Removed"], retired.GetProperty("functions").EnumerateArray().Select(n => n.GetString()));
                Assert.Equal(["Zzz"], retired.GetProperty("types").EnumerateArray().Select(n => n.GetString()));
                Assert.Equal(["nqlZzzRemoved"], xs.RootElement.GetProperty("retired").EnumerateArray().Select(n => n.GetString()));
                Assert.DoesNotContain(cs.RootElement.GetProperty("functions").EnumerateArray(), f => f.GetProperty("name").GetString() == "nql_Zzz_Removed");
            }

            Assert.Equal(0, Program.Run(["check"], repo, NoProcess));
            Assert.Equal(0, Program.Run(["excel", "check"], repo, NoProcess));
        }
        finally
        {
            Directory.Delete(repo, true);
        }
    }

    [Fact]
    public void AStaleTreeRefusesAndWritesNothing()
    {
        string repo = ScratchRepository();
        try
        {
            File.AppendAllText(Path.Combine(repo, "bindings", "c", "include", "nanoql.h"), "\n");
            var e = Assert.Throws<BindException>(() => Program.Run(["freeze"], repo, NoProcess));
            Assert.Contains("'check' reads 2/3", e.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(repo, "bindings", "surface.shipped.json")));
            Assert.False(File.Exists(Path.Combine(repo, "bindings", "excel", "functions.shipped.json")));
        }
        finally
        {
            Directory.Delete(repo, true);
        }
    }

    private static int NoProcess(string file, IReadOnlyList<string> argv) => throw new InvalidOperationException($"no process is run here: {file}");

    // The repository's checked-in surface and roles in a scratch folder, which the tool finds as a repository.
    private static string ScratchRepository()
    {
        string repo = Path.Combine(Path.GetTempPath(), "nanoql-bind-freeze-" + Guid.NewGuid().ToString("N"));
        foreach (string file in new[] { "roles.json", "surface.json", "coverage.md", "c/include/nanoql.h", "excel/functions.json" })
        {
            string to = Path.Combine(repo, "bindings", file);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(Path.Combine(Surfaces.Repository, "bindings", file), to);
        }

        return repo;
    }
}
