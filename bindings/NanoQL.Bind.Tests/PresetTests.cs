// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests;

/// <summary>
/// The coded presets: every type the extensions' preset factories build has its constructors withheld, read
/// from IL without running it, and the convention set's lookup by name gives each preset.
/// </summary>
public class PresetTests
{
    [Fact]
    public void ThePresetsConstructorsAreWithheld()
    {
        Generation g = Surfaces.Nanoql;
        Assert.Null(Surfaces.Declaration(g, "nql_TARGET_new"));
        Assert.Null(Surfaces.Declaration(g, "nql_Actual365Fixed_new"));
        Assert.Equal(298, g.Surface.Presets.Select(p => p.Factory.PresetType).Distinct().Count());
        Assert.Equal(311, g.Surface.Skips.Count(s => s.Reason.StartsWith("coded preset constructor", StringComparison.Ordinal)));
    }

    [Fact]
    public void APresetsLookupByNameIsTheSetsFunction()
    {
        Model.PresetRow target = Surfaces.Nanoql.Surface.Presets.Single(p => p.Factory.Dictionary == "Calendars" && p.Factory.Name == "TARGET");
        Assert.Equal("nql_ConventionSet_Calendar", target.Lookup?.CName);
        Model.PresetRow euribor = Surfaces.Nanoql.Surface.Presets.First(p => p.Factory.Dictionary == "IborFamilies" && p.Factory.Name == "Euribor");
        Assert.Equal("nql_ConventionSet_IborIndex__string_Period", euribor.Lookup?.CName);
        Assert.Equal(0, euribor.Construction.Arguments[0].Parameter);
        Assert.Equal(1, euribor.Construction.Arguments[1].Parameter);
    }

    [Fact]
    public void AnOptionalArgumentLeftOutIsReadAsItsDefault()
    {
        // new Thirty360(Thirty360Convention.USA): the left-out Date? is a cleared local, read as null, so the recorder
        // writes the construction as the set's lookup.
        Model.PresetRow usa = Surfaces.Nanoql.Surface.Presets.Single(p => p.Factory.Dictionary == "DayCounters" && p.Factory.Name == "Thirty360.USA");
        Assert.Equal([(true, (object?)0), (true, null)], usa.Construction.Arguments.Select(a => (a.IsConstant, a.Constant)));
        Assert.DoesNotContain(Surfaces.Nanoql.Surface.Presets, p => p.Construction.Arguments.Any(a => !a.IsConstant && a.Parameter < 0));
    }

    [Fact]
    public void EachBranchOfAFactoryHasTheLookupTakingTheParametersItPasses()
    {
        // (tenor, forwarding, exogenous, discounting) => exogenous ? new X(tenor, forwarding, discounting) : new X(tenor,
        // forwarding): exogenous only picks the branch.
        Model.PresetRow[] chf = [.. Surfaces.Nanoql.Surface.Presets.Where(p => p.Factory.Dictionary == "SwapIndexes" && p.Factory.Name == "ChfLiborSwapIsdaFix")
            .OrderBy(p => p.Construction.Arguments.Count)];
        Assert.Equal(["nql_ConventionSet_SwapIndex__string_Period_YieldTermStructure", "nql_ConventionSet_SwapIndex__string_Period_YieldTermStructure_YieldTermStructure"],
            chf.Select(p => p.Lookup?.CName));
        Assert.Equal([0, 1, 3], chf[1].Construction.PassedParameters());
    }
}
