// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Bind.Emitting;
using NanoQL.Bind.Selecting;
using static NanoQL.Bind.Tests.ExcelSurfaces;

namespace NanoQL.Bind.Tests;

/// <summary>
/// The completeness check and the entries' checks (XLD34): a listed function whose required input, an object or an
/// engine, nothing kept makes is withheld to a fixpoint and listed in functions.json's "withheld"; an entry the surface
/// lacks, an entry keeping no function and a deprecated function the list would keep, each refused; every listed
/// instrument priced.
/// </summary>
public class ExcelCheckTests
{
    /// <summary>The seed's 19 withheld, each with what it lacks (XLP83).</summary>
    [Fact]
    public void TheSeedWithholdsNineteen()
    {
        var withheld = Withheld(ListedExperimental);
        Assert.Equal(19, withheld.Count);
        Assert.Equal(["NanoQL.Numerics.Brent"], withheld["nql_BondFunctions_Yield__Brent"]);
        Assert.Equal(["NanoQL.TimeGrid"], withheld["nql_HullWhite_Tree"]);
        Assert.Contains("an engine no listed engine binds", withheld["nql_QuantoEngine_Calculate__QuantoDoubleBarrierOption"]);
    }

    /// <summary>A type only an unlisted maker makes: its listed consumer withheld; one whose only maker needs itself, withheld too.</summary>
    [Fact]
    public void AConsumerOfAnUnmadeTypeIsWithheld()
    {
        var withheld = Withheld(Users);
        Assert.Equal(["NanoQL.Bind.Tests.Fixtures.Excel.Plate"], withheld["fx_PlateUser_Use"]);
        Assert.Equal(["NanoQL.Bind.Tests.Fixtures.Excel.Selfish"], withheld["fx_Selfish_new"]);
        Assert.Contains("fx_Selfish_get_Value", withheld.Keys);
    }

    /// <summary>A preset's type a kept lookup makes counts as made: the BMA index through the set's lookup (F10-2).</summary>
    [Fact]
    public void ALookupsPresetCountsAsMade()
    {
        Assert.NotNull(NameOf(ListedExperimental, "nql_BMAIndex_FixingSchedule"));
        Assert.DoesNotContain("nql_BMAIndex_FixingSchedule", Withheld(ListedExperimental).Keys);
    }

    /// <summary>An entry the surface lacks, an entry keeping no function and a deprecated function on the list: each refused.</summary>
    [Fact]
    public void TheEntriesChecksRefuse()
    {
        var missing = Assert.Throws<BindException>(() => Run(true, new() { ["nql"] = NanoqlWith(["NanoQL.NoSuchType"], []) }));
        Assert.Contains("NanoQL.NoSuchType isn't on the surface", missing.Message, StringComparison.Ordinal);
        var empty = Assert.Throws<BindException>(() => Run(true, new() { ["nql"] = NanoqlWith(["NanoQL.Instruments.BMASwapResults"], []) }));
        Assert.Contains("NanoQL.Instruments.BMASwapResults keeps no function", empty.Message, StringComparison.Ordinal);
        var deprecated = Assert.Throws<BindException>(() => Run(true, new() { ["nql"] = null, ["fx"] = List("NanoQL.Bind.Tests.Fixtures.Excel.Legacy") }, fixtures: true));
        Assert.Contains("fx_Legacy_Old, which is deprecated", deprecated.Message, StringComparison.Ordinal);
    }

    /// <summary>A required engine no listed engine binds: the quanto forward's calculation withheld on a list adding its option.</summary>
    [Fact]
    public void AnUnfilledEngineIsWithheld()
    {
        ExcelOutput x = Run(true, new() { ["nql"] = NanoqlWith(["NanoQL.Instruments.QuantoForwardVanillaOption"], []) });
        Assert.Contains("an engine no listed engine binds", Withheld(x)["nql_QuantoEngine_Calculate__QuantoForwardVanillaOption"]);
    }

    /// <summary>A user prefix's function on a nanoql type off nanoql's list is withheld as nanoql's are, the tool printing it with the type it lacks and the remedy (F11-3).</summary>
    [Fact]
    public void AUserFunctionOnAnUnlistedNanoqlTypeIsWithheld()
    {
        ExcelOutput x = Run(true, new() { ["nql"] = ExcelList.Nanoql(), ["fx"] = null }, fixtures: true);
        Assert.Equal(["NanoQL.Numerics.Brent"], Withheld(x)["fx_Solver_Root"]);
        Assert.Contains(
            "excel: fxSolverRoot (fx_Solver_Root) withheld: no kept function makes NanoQL.Numerics.Brent; where nanoql's list leaves the type out, widen it: --list nql=<file> with the type added (docs/excel.md)",
            ExcelCommand.UserWithheld(x));
    }

    /// <summary>Every listed concrete instrument is priced by a listed function: an engine's calculation taking it or a base, or its own Calculate (the seed's 29).</summary>
    [Fact]
    public void EveryListedInstrumentIsPriced()
    {
        using JsonDocument surface = JsonDocument.Parse(File.ReadAllText(Path.Combine(Surfaces.Repository, "bindings", "surface.json")));
        var types = surface.RootElement.GetProperty("types").EnumerateArray().ToDictionary(t => t.GetProperty("name").GetString()!, t => t.Clone());
        static string Full(string t) => t.Split('[')[0];
        List<string> Bases(string t)
        {
            var out_ = new List<string>();
            for (string? b = types.TryGetValue(t, out JsonElement e) && e.TryGetProperty("base", out JsonElement be) ? be.GetString() : null; b != null;
                 b = types.TryGetValue(Full(b), out JsonElement e2) && e2.TryGetProperty("base", out JsonElement be2) ? be2.GetString() : null)
            {
                out_.Add(Full(b));
            }

            return out_;
        }

        var listed = ExcelList.Nanoql().Types;
        var instruments = listed.Where(t => Bases(t).Contains("NanoQL.Instrument") && !(types.TryGetValue(t, out JsonElement e) && e.TryGetProperty("shape", out JsonElement s) && s.GetString() == "abstract")).ToList();
        var kept = ListedExperimental.Selection.Units.SelectMany(u => u.Functions).Select(f => f.CName).ToHashSet();
        var fns = surface.RootElement.GetProperty("functions").EnumerateArray().Where(f => kept.Contains(f.GetProperty("name").GetString()!) && f.GetProperty("params").GetArrayLength() > 0).ToList();
        var unpriced = instruments.Where(t => !fns.Any(f =>
        {
            string first = Full(f.GetProperty("params")[0].GetProperty("type").GetString()!);
            string type = Full(f.GetProperty("type").GetString()!);
            bool calculate = f.TryGetProperty("member", out JsonElement m) && m.GetString() == "Calculate";
            bool own = type == first && calculate;
            bool engine = type.Split('.')[^1].EndsWith("Engine", StringComparison.Ordinal) && calculate;
            return (first == t || (Bases(t).Contains(first) && !t.Contains("Quanto", StringComparison.Ordinal))) && (own || engine);
        })).ToList();
        Assert.Equal(29, instruments.Count);
        Assert.Empty(unpriced);
    }
}
