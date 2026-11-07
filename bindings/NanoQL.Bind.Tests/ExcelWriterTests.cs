// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Bind.Emitting;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;
using NanoQL.Bind.Selecting;
using static NanoQL.Bind.Tests.ExcelSurfaces;

namespace NanoQL.Bind.Tests;

/// <summary>
/// The generated worksheet functions: each argument's conversion by its kind, an object left out none where C# lets it
/// be, the codes a member documents, the loop masks, the help text, the repro templates, the tuple forms, the path-like
/// refusal and the budget.
/// </summary>
public class ExcelWriterTests
{
    private const string X = "global::NanoQL.Excel.X";

    /// <summary>An optional handle and an optional snapshot left out are none; a required handle C# annotates nullable too; a plain required one is refused by its conversion.</summary>
    [Fact]
    public void ObjectsLeftOutAreNone()
    {
        string optional = Function(Users, "fxLeftOutOptional");
        Assert.Contains($"({X}.IsMissing(a[1]) ? null : {X}.Obj<global::NanoQL.Bind.Tests.Fixtures.Excel.Square>(a[1], \"square\"))", optional, StringComparison.Ordinal);
        Assert.Contains($"({X}.IsMissing(a[2]) ? null : {X}.Generator<global::NanoQL.Bind.Tests.Fixtures.UserRng>(a[2], \"rng\"))", optional, StringComparison.Ordinal);
        Assert.Contains($"({X}.IsMissing(a[0]) ? null : {X}.Obj<global::NanoQL.Bind.Tests.Fixtures.Excel.Square>(a[0], \"square\"))", Function(Users, "fxLeftOutMaybe"), StringComparison.Ordinal);
        Assert.DoesNotContain("IsMissing", Function(Users, "fxLeftOutMust"), StringComparison.Ordinal);
    }

    /// <summary>The member's optional parameters the C signature leaves out (CFunction.Dropped) are passed their default, null.</summary>
    [Fact]
    public void DroppedParametersArePassedNull()
    {
        ExcelOutput x = Run(true, new() { ["nql"] = null, ["fx"] = null }, fixtures: true);
        Assert.Contains($"global::NanoQL.Bind.Tests.Fixtures.Optionals.Scaled({X}.Dbl(a[0], \"x\"), null, null)", Function(x, "fxOptionalsScaled"), StringComparison.Ordinal);
    }

    /// <summary>The codes a member documents for kinds outside the table, emitted as its shims catch them: a fixture's,
    /// as no nanoql member documents such a kind.</summary>
    [Fact]
    public void DocumentedCodesAreEmitted()
    {
        ExcelOutput x = Run(true, new() { ["nql"] = null, ["fx"] = null }, fixtures: true);
        Assert.Contains(
            "static e_ => e_ switch { global::System.IndexOutOfRangeException => 4, _ => 0 }", Function(x, "fxKindsElement"), StringComparison.Ordinal);
    }

    /// <summary>The loop mask per kind: every scalar kind loops, an array's argument doesn't, a function returning a range doesn't loop.</summary>
    [Fact]
    public void TheLoopMaskPerKind()
    {
        var m = Manifest(WholeExperimental);
        Assert.Equal("11111", m["nqlCalendarAdvance2"].GetProperty("loops").GetString());
        Assert.Equal("01", m["nqlJointCalendar4"].GetProperty("loops").GetString());
        Assert.False(m["nqlSwapLeg"].TryGetProperty("loops", out _));
        Assert.Equal("11", m["nqlMersenneTwisterUniformRngDraws"].GetProperty("loops").GetString());
        Assert.Contains(", \"11111\")", Function(WholeExperimental, "nqlCalendarAdvance2"), StringComparison.Ordinal);
    }

    /// <summary>Each template's mask is the manifest's, a family's member its own arguments' part.</summary>
    [Fact]
    public void TheTemplatesMasksAreTheManifests()
    {
        var m = Manifest(WholeExperimental);
        using JsonDocument t = JsonDocument.Parse(WholeExperimental.Templates);
        foreach (JsonElement f in t.RootElement.GetProperty("functions").EnumerateArray().Where(f => !f.TryGetProperty("family", out _)))
        {
            string name = f.GetProperty("excel").GetString()!;
            string? want = m[name].TryGetProperty("loops", out JsonElement l) ? l.GetString() : null;
            string? got = f.TryGetProperty("loops", out JsonElement g) ? g.GetString() : null;
            Assert.True(want == got, $"{name}: the manifest's {want}, the template's {got}");
        }
    }

    /// <summary>The tuple forms: an array of tuples converted by rows, each item by its kind, its help naming the items; the templates' items.</summary>
    [Fact]
    public void TheTupleForms()
    {
        string issuer = Function(ListedExperimental, "nqlIssuer");
        Assert.Contains($"{X}.Tuples<global::NanoQL.Experimental.DefaultProbKey, global::NanoQL.TermStructures.DefaultProbabilityTermStructure>(a[0], \"probabilities\", static (x_, p_) => {X}.Obj<", issuer, StringComparison.Ordinal);
        Assert.Contains("(a range, a row per element: Key, Curve)", issuer, StringComparison.Ordinal);
        using JsonDocument t = JsonDocument.Parse(ListedExperimental.Templates);
        JsonElement tpl = t.RootElement.GetProperty("functions").EnumerateArray().Single(f => f.GetProperty("excel").GetString() == "nqlIssuer");
        Assert.Equal(2, tpl.GetProperty("params")[0].GetProperty("items").GetArrayLength());
        Assert.Contains($"{X}.Tuple<double?, double?>", Function(WholeExperimental, "nqlConcentrating1dMesher"), StringComparison.Ordinal);
    }

    /// <summary>
    /// The help text: a getter's from its property, an <c>&lt;inheritdoc/&gt;</c>'s from its nearest base, an
    /// argument's <c>&lt;param&gt;</c> with its form, a family's with its constructions' count, the signature without
    /// documentation, every text at most 253 characters.
    /// </summary>
    [Fact]
    public void TheHelpText()
    {
        Assert.Contains("Description = \"Gets the area, inherited by a listed derived type.\"", Function(Users, "fxShapeArea"), StringComparison.Ordinal);
        Assert.Contains("Description = \"The side, overridden by the derived type, so it reaches the derived type once.\"", Function(Users, "fxSquareSide"), StringComparison.Ordinal);
        Assert.Contains("Description = \"Checks the option\\u0027s terms before an engine prices it: QuantLib\\u0027s Option::arguments::validate .\"", Function(WholeExperimental, "nqlCliquetOptionValidate"), StringComparison.Ordinal);
        Assert.Contains("Description = \"The value (a number)\"", Function(WholeExperimental, "nqlTCopulaPolicyCumulativeY"), StringComparison.Ordinal);
        Assert.Contains("Description = \"new Linear()\"", Function(WholeExperimental, "nqlLinear"), StringComparison.Ordinal);
        Assert.Matches("constructions\"\\)\\]", Function(WholeExperimental, "nqlTimeSeriesSize"));
        var descriptions = System.Text.RegularExpressions.Regex.Matches(string.Concat(WholeExperimental.Files.Values), "Description = (\"(?:[^\"\\\\]|\\\\.)*\")").Select(m => JsonSerializer.Deserialize<string>(m.Groups[1].Value)!).ToList();
        // Excel-DNA's own cut for a function's and the last argument's description, which ExcelDocs.Limit follows.
        Assert.All(descriptions, d => Assert.True(d.Length <= 253, d));
        Assert.Contains(descriptions, d => d.EndsWith('…'));
    }

    /// <summary>A kept function with a path-like parameter outside the readers' list is refused, synthesized.</summary>
    [Fact]
    public void APathLikeParameterIsRefused()
    {
        var surface = new Surface();
        var f = new CFunction
        {
            Prefix = "fx",
            TypeName = "Files",
            BaseName = "Load",
            Kind = "static",
            Type = typeof(File),
            Holder = typeof(File),
            Member = typeof(File).GetMethod("ReadAllText", [typeof(string)]),
            CName = "fx_Files_Load",
            Return = new CKind("string", typeof(string)),
            Static = true,
        };
        f.Params.Add(new CParam("path", new CKind("string", typeof(string)), false, null));
        surface.Functions.Add(f);
        var e = Assert.Throws<BindException>(() => ExcelSelection.Select(surface, new ExcelOptions(), ExcelShipped.Read(null)));
        Assert.Contains("fx_Files_Load: a path-like parameter outside the file readers' list", e.Message, StringComparison.Ordinal);
    }

    /// <summary>The budget: a build past the ceiling is refused, naming its count and its largest namespaces; one under it passes.</summary>
    [Fact]
    public void TheBudget()
    {
        Assert.Equal(10235, WholeExperimental.Registered);
        var e = Assert.Throws<BindException>(() => ExcelCommand.CheckBudget(WholeExperimental, ExcelCommand.Budget));
        Assert.Contains("registers 10,235 functions", e.Message, StringComparison.Ordinal);
        Assert.Contains("the largest namespaces: NanoQL.", e.Message, StringComparison.Ordinal);
        ExcelCommand.CheckBudget(ListedExperimental, ExcelCommand.Budget);
        Assert.Throws<BindException>(() => ExcelCommand.CheckBudget(ListedStable, 2000));
    }

    /// <summary>
    /// A build from a local feed restores into its own folder, which is no item of the project's: a package restored
    /// there whole (Excel-DNA's, on a machine whose NuGet folder lacks it) brings a template .dna Excel-DNA would pack.
    /// </summary>
    [Fact]
    public void TheRestoreFolderIsNoItemOfTheProjects()
    {
        string dir = Path.Combine(Path.GetTempPath(), "nanoql-bind-project-" + Environment.ProcessId);
        ExcelProject.Write(dir, new Dictionary<string, string>(), [new AssemblyInput("nql", "NanoQL.dll")], false, "feed");
        string project = File.ReadAllText(Path.Combine(dir, ExcelProject.Name + ".csproj"));
        Assert.Contains("<RestorePackagesPath>$(MSBuildProjectDirectory)\\packages</RestorePackagesPath>", project, StringComparison.Ordinal);
        Assert.Contains("<DefaultItemExcludes>$(DefaultItemExcludes);packages\\**</DefaultItemExcludes>", project, StringComparison.Ordinal);
        Directory.Delete(dir, true);
    }
}
