// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace NanoQL.Bind.Tests;

/// <summary>
/// The C names (docs/bindings.md, "Names"): <c>prefix_Type_member</c>, an overload's shortest distinguishing run of
/// parameter type names, one crossing per shared C signature, and released names that never move.
/// </summary>
public class NameTests
{
    [Fact]
    public void AMemberWithoutOverloadsTakesItsShortName()
    {
        Assert.NotNull(Surfaces.Declaration(Surfaces.Nanoql, "nql_Calendar_IsBusinessDay"));
        Assert.NotNull(Surfaces.Declaration(Surfaces.Nanoql, "nql_FlatForward_new"));
    }

    [Fact]
    public void OverloadsTakeTheShortestRunOfParameterTypeNamesThatTellsThemApart()
    {
        Generation g = Surfaces.WithFixtures;
        Assert.NotNull(Surfaces.Declaration(g, "fx_Overloads_Move__Date_int"));
        Assert.NotNull(Surfaces.Declaration(g, "fx_Overloads_Move__Date_Period"));
        Assert.NotNull(Surfaces.Declaration(g, "fx_Overloads_Move__Date_Period_int"));
        Assert.NotNull(Surfaces.Declaration(Surfaces.Nanoql, "nql_DiscountingSwapEngine_Calculate__FixedVsFloatingSwap"));
    }

    [Fact]
    public void OverloadsWithOneCSignatureCrossOnceTheSpanPreferred()
    {
        Generation g = Surfaces.WithFixtures;
        Assert.Equal("NQL_API nql_status fx_Overloads_Sum(const double* x, int32_t x_count, double* result);", Surfaces.Declaration(g, "fx_Overloads_Sum"));
        Model.CFunction sum = g.Surface.Functions.Single(f => f.CName == "fx_Overloads_Sum");
        Assert.Contains("ReadOnlySpan", sum.Key, StringComparison.Ordinal);
        Assert.Equal(2, g.Surface.Skips.Count(s => s.Display.StartsWith("Overloads.Sum", StringComparison.Ordinal)));
    }

    [Fact]
    public void TypesSharingASimpleNameTakeTheirNamespacesLastSegment()
    {
        Generation g = Surfaces.WithFixtures;
        Assert.NotNull(Surfaces.Declaration(g, "fx_A_Twin_get_Value"));
        Assert.NotNull(Surfaces.Declaration(g, "fx_B_Twin_get_Value"));
    }

    [Fact]
    public void WithoutAReleaseEveryOverloadTakesItsSuffix()
    {
        Generation g = Surfaces.WithFixtures;
        Assert.NotNull(Surfaces.Declaration(g, "fx_Lone_Value__void"));
        Assert.NotNull(Surfaces.Declaration(g, "fx_Lone_Value__int"));
    }

    [Fact]
    public void ReleasedNamesNeverMove()
    {
        // The release's surface names Lone.Value() and A.Twin as they were before Value(int) and B.Twin were added.
        Generation g = Surfaces.Run(Path.Combine(AppContext.BaseDirectory, "Fixtures", "shipped.json"), fixtures: true);
        Assert.NotNull(Surfaces.Declaration(g, "fx_Lone_Value"));
        Assert.NotNull(Surfaces.Declaration(g, "fx_Lone_Value__int"));
        Assert.Null(Surfaces.Declaration(g, "fx_Lone_Value__void"));
        Assert.NotNull(Surfaces.Declaration(g, "fx_Twin_get_Value"));
        Assert.NotNull(Surfaces.Declaration(g, "fx_B_Twin_get_Value"));
        Assert.Null(Surfaces.Declaration(g, "fx_A_Twin_get_Value"));
    }

    [Fact]
    public void NoTwoFunctionsShareACName()
    {
        Assert.Equal(Surfaces.Nanoql.Surface.Functions.Count, Surfaces.Nanoql.Surface.Functions.Select(f => f.CName).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// A member inherited under several types crosses under each, and each crossing keeps its own released name, read
    /// by the type and the member: the checked-in surface as the release's, its ImpliedQuote under CdsHelper renamed,
    /// under AssetSwapHelper kept, both one member of BootstrapHelper.
    /// </summary>
    [Fact]
    public void EachCrossingOfAnInheritedMemberKeepsItsReleasedName()
    {
        JsonNode surface = JsonNode.Parse(File.ReadAllText(Path.Combine(Surfaces.Repository, "bindings", "surface.json")))!;
        JsonArray functions = surface["functions"]!.AsArray();
        JsonNode cds = functions.Single(f => (string?)f!["name"] == "nql_CdsHelper_ImpliedQuote")!;
        JsonNode asset = functions.Single(f => (string?)f!["name"] == "nql_AssetSwapHelper_ImpliedQuote")!;
        Assert.Equal((string?)cds["key"], (string?)asset["key"]);
        cds["name"] = "nql_CdsHelper_ImpliedQuoteReleased";
        string shipped = Path.Combine(Path.GetTempPath(), "nanoql-bind-crossings-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(shipped, surface.ToJsonString());
        try
        {
            Generation g = Surfaces.Run(shipped);
            Assert.NotNull(Surfaces.Declaration(g, "nql_CdsHelper_ImpliedQuoteReleased"));
            Assert.Null(Surfaces.Declaration(g, "nql_CdsHelper_ImpliedQuote"));
            Assert.NotNull(Surfaces.Declaration(g, "nql_AssetSwapHelper_ImpliedQuote"));
        }
        finally
        {
            File.Delete(shipped);
        }
    }

    /// <summary>
    /// A released surface is read by type names without their assemblies' version, culture and public key token, which
    /// a closed generic's arguments carry: the checked-in surface as the release's, with nanoql's assembly version, the
    /// runtime's, the culture and nanoql's public key token each read as another, keeps every function's and type's
    /// name, the three checked-in files byte for byte.
    /// </summary>
    [Fact]
    public void AnotherAssemblyVersionKeepsEveryReleasedName()
    {
        string bindings = Path.Combine(Surfaces.Repository, "bindings");
        string text = File.ReadAllText(Path.Combine(bindings, "surface.json"));
        string[][] moved =
        [
            ["NanoQL, Version=2.0.0.0, ", "NanoQL, Version=9.9.9.9, "],
            ["System.Private.CoreLib, Version=10.0.0.0, ", "System.Private.CoreLib, Version=11.0.0.0, "],
            ["Culture=neutral", "Culture=en-GB"],
            ["PublicKeyToken=null", "PublicKeyToken=0123456789abcdef"],
        ];
        string read = text;
        foreach (string[] m in moved)
        {
            Assert.Contains(m[0], read, StringComparison.Ordinal);
            read = read.Replace(m[0], m[1], StringComparison.Ordinal);
        }

        string shipped = Path.Combine(Path.GetTempPath(), "nanoql-bind-versions-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(shipped, read);
        try
        {
            Generation g = Surfaces.Run(shipped);
            using JsonDocument doc = JsonDocument.Parse(text);
            Assert.Equal(doc.RootElement.GetProperty("functions").EnumerateArray().Select(f => f.GetProperty("name").GetString()!).Order(StringComparer.Ordinal),
                g.Surface.Functions.Select(f => f.CName).Order(StringComparer.Ordinal));
            Assert.Equal(doc.RootElement.GetProperty("types").EnumerateArray().Select(t => t.GetProperty("cname").GetString()!).Order(StringComparer.Ordinal),
                g.Surface.Types.Select(t => t.CName).Order(StringComparer.Ordinal));
            foreach (string file in new[] { "c/include/nanoql.h", "surface.json", "coverage.md" })
            {
                Assert.True(File.ReadAllText(Path.Combine(bindings, file)) == g.Files[file], file + " differs");
            }
        }
        finally
        {
            File.Delete(shipped);
        }
    }

    /// <summary>
    /// A retired name is never taken again: the checked-in surface as the release's, Calendar.IsBusinessDay's entry
    /// gone and its name retired, as when the function was removed and came back; it then takes a fresh name.
    /// </summary>
    [Fact]
    public void ARetiredNameIsNeverTakenAgain()
    {
        JsonNode surface = JsonNode.Parse(File.ReadAllText(Path.Combine(Surfaces.Repository, "bindings", "surface.json")))!;
        JsonArray functions = surface["functions"]!.AsArray();
        functions.Remove(functions.Single(f => (string?)f!["name"] == "nql_Calendar_IsBusinessDay"));
        surface["retired"] = new JsonObject { ["functions"] = new JsonArray("nql_Calendar_IsBusinessDay"), ["types"] = new JsonArray() };
        string shipped = Path.Combine(Path.GetTempPath(), "nanoql-bind-retired-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(shipped, surface.ToJsonString());
        try
        {
            Generation g = Surfaces.Run(shipped);
            Assert.Null(Surfaces.Declaration(g, "nql_Calendar_IsBusinessDay"));
            Model.CFunction f = g.Surface.Functions.Single(f => f.Type.FullName == "NanoQL.Time.Calendar" && f.Member?.Name == "IsBusinessDay");
            Assert.StartsWith("nql_Calendar_IsBusinessDay_", f.CName, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(shipped);
        }
    }

    /// <summary>
    /// A retired type name is never taken again: the checked-in surface as the release's, Calendar's type entry gone
    /// and its name retired; the type then takes its namespace's qualified name.
    /// </summary>
    [Fact]
    public void ARetiredTypeNameIsNeverTakenAgain()
    {
        JsonNode surface = JsonNode.Parse(File.ReadAllText(Path.Combine(Surfaces.Repository, "bindings", "surface.json")))!;
        JsonArray types = surface["types"]!.AsArray();
        types.Remove(types.Single(t => (string?)t!["name"] == "NanoQL.Time.Calendar"));
        surface["retired"] = new JsonObject { ["functions"] = new JsonArray(), ["types"] = new JsonArray("Calendar") };
        string shipped = Path.Combine(Path.GetTempPath(), "nanoql-bind-retired-type-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(shipped, surface.ToJsonString());
        try
        {
            Generation g = Surfaces.Run(shipped);
            Assert.Equal("Time_Calendar", g.Surface.Types.Single(t => t.Type.FullName == "NanoQL.Time.Calendar").CName);
            Assert.DoesNotContain(g.Surface.Types, t => t.CName == "Calendar");
        }
        finally
        {
            File.Delete(shipped);
        }
    }
}
