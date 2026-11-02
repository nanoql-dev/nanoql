// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Bind.Emitting;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;
using NanoQL.Bind.Selecting;
using NanoQL.Extensions.Conventions;
using static NanoQL.Bind.Tests.ExcelSurfaces;

namespace NanoQL.Bind.Tests;

/// <summary>
/// Repro export's part in the generator (XLR14): the path reporters, which a build hands the runtime by their Excel names
/// for the export to refuse (round 6's F6-1), and each function's repro template with Python's names beside C#'s (XLP66).
/// </summary>
public sealed class ExcelReproTests
{
    private const string Reporters = "\"nqlConventionSetProvenance\", \"nqlConventionSnapshotSources\"";

    /// <summary>Each reporter by its Excel name, written into the build's module initializer.</summary>
    [Fact]
    public void ThePathReportersByName()
    {
        Assert.Equal(["nqlConventionSetProvenance", "nqlConventionSnapshotSources"], ListedExperimental.Selection.PathReporters);
        Assert.Contains($"X.UseBuild(typeof(XlBuild).Assembly, new string[] {{ {Reporters} }});", ListedExperimental.Files["XlBuild.g.cs"], StringComparison.Ordinal);
    }

    /// <summary>The stable build hands over the experimental build's reporters, on the list and off it.</summary>
    [Fact]
    public void TheStableBuildsReportersAreTheExperimentals()
    {
        Assert.Equal(ListedExperimental.Selection.PathReporters, ListedStable.Selection.PathReporters);
        Assert.Equal(ListedExperimental.Selection.PathReporters, WholeExperimental.Selection.PathReporters);
        Assert.Equal(ListedExperimental.Selection.PathReporters, WholeStable.Selection.PathReporters);
        Assert.Contains(Reporters, ListedStable.Files["XlBuild.g.cs"], StringComparison.Ordinal);
    }

    /// <summary>A surface binding nanoql's extensions without a listed reporter is refused, naming the member it lacks.</summary>
    [Fact]
    public void AMissingReporterIsRefused()
    {
        var surface = new Surface();
        var f = new CFunction
        {
            Prefix = "nql",
            TypeName = "ConventionSnapshot",
            BaseName = "Sources",
            Kind = "get",
            Type = typeof(ConventionSnapshot),
            Holder = typeof(ConventionSnapshot),
            Member = typeof(ConventionSnapshot).GetProperty(nameof(ConventionSnapshot.Sources))!.GetMethod,
            CName = "nql_ConventionSnapshot_get_Sources",
            Return = new CKind("array:string", typeof(ImmutableArray<string>)),
        };
        f.Params.Add(new CParam("self", new CKind("handle", typeof(ConventionSnapshot)), false, null) { IsSelf = true });
        surface.Functions.Add(f);
        var e = Assert.Throws<BindException>(() => ExcelSelection.Select(surface, new ExcelOptions(), ExcelShipped.Read(null)));
        Assert.Contains("the path reporters' list names NanoQL.Extensions.Conventions.ConventionSet.Provenance, which the surface doesn't have", e.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Each template carries Python's module, class, member, call style and parameters (S-bind's Python back end's
    /// names): a constructor, a method, a property, a lookup on the convention set, and the experimental API's module;
    /// a family's on each construction; none for a date's or a period's constructor, which Python takes as values.
    /// </summary>
    [Fact]
    public void EachTemplateCarriesPythonsNames()
    {
        using JsonDocument t = JsonDocument.Parse(ListedExperimental.Templates);
        var all = t.RootElement.GetProperty("functions").EnumerateArray().ToList();
        var py = all.Where(f => !f.TryGetProperty("family", out _))
            .ToDictionary(f => f.GetProperty("excel").GetString()!, f => f.TryGetProperty("py", out JsonElement p) ? p.GetRawText() : null, StringComparer.Ordinal);
        Assert.Equal("[\"nanoql\",\"FlatForward\",\"__init__\",\"new\",[\"reference_date\",\"forward\",\"day_counter\",\"compounding\",\"frequency\"]]", py["nqlFlatForward"]);
        Assert.Equal("[\"nanoql\",\"YieldTermStructure\",\"discount\",\"method\",[\"self\",\"d\",\"extrapolate\"]]", py["nqlYieldTermStructureDiscount"]);
        Assert.Equal("[\"nanoql\",\"ConventionSnapshot\",\"sources\",\"property\",[\"self\"]]", py["nqlConventionSnapshotSources"]);
        Assert.Equal("[\"nanoql\",\"ConventionSet\",\"day_counter\",\"method\",[\"self\",\"name\"]]", py["nqlConventionSetDayCounter"]);
        Assert.Equal("[\"nanoql.experimental\",\"DefaultType\",\"__init__\",\"new\",[\"default_type\",\"restructuring_type\"]]", py["nqlDefaultType"]);
        Assert.Equal(["nqlDate", "nqlDate2", "nqlPeriod", "nqlPeriod2"], py.Where(kv => kv.Value == null).Select(kv => kv.Key).Order(StringComparer.Ordinal));
        var constructions = all.Where(f => f.TryGetProperty("family", out _)).SelectMany(f => f.GetProperty("family").EnumerateArray()).ToList();
        Assert.NotEmpty(constructions);
        Assert.All(constructions, c => Assert.True(c.TryGetProperty("py", out _), c.GetProperty("c").GetString()));
    }
}
