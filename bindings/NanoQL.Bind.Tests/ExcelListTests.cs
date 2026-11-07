// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Bind.Emitting;
using static NanoQL.Bind.Tests.ExcelSurfaces;

namespace NanoQL.Bind.Tests;

/// <summary>
/// The list: Excel holds the listed types' functions and no others; a listed type brings the instance members it
/// inherits, each once from its nearest declaration, and the extension methods on it; Excel's filter still applies
/// inside a listed type.
/// </summary>
public class ExcelListTests
{
    /// <summary>
    /// The seed's counts: 2,652 functions with the experimental API, 2,324 stable (the first list's 2,575 and 2,253,
    /// the 71 of the seven types the completeness check's least fixpoint showed missing, and DefaultType's 6); the
    /// tests' builds 10,228 and 8,008.
    /// </summary>
    [Fact]
    public void TheBuildsCounts()
    {
        Assert.Equal(2652, ListedExperimental.Selection.Units.Count);
        Assert.Equal(328, ListedExperimental.Selection.Units.Count(u => u.Experimental));
        Assert.Equal(2324, ListedStable.Selection.Units.Count);
        Assert.Equal(10228, WholeExperimental.Selection.Units.Count);
        Assert.Equal(8008, WholeStable.Selection.Units.Count);
        Assert.Equal(309, ExcelList.Nanoql().Entries.Count);
    }

    /// <summary>
    /// A listed type's inherited instance members are kept, each once: the base's getter and its abstract member reach
    /// Square once though Square overrides it; the interface's member once beside the class's; the base's static getter,
    /// static method and constructor aren't inherited; the extension method on the base comes too.
    /// </summary>
    [Fact]
    public void AListedTypeBringsWhatItInherits()
    {
        var names = Users.Selection.Units.SelectMany(u => u.Functions).Select(f => f.CName).ToHashSet();
        Assert.Contains("fx_Shape_get_Area", names);
        Assert.Contains("fx_Square_Side", names);
        Assert.DoesNotContain("fx_Shape_Side", names);
        Assert.Contains("fx_Square_Scaled", names);
        Assert.DoesNotContain("fx_Shape_Scaled", names);
        Assert.DoesNotContain("fx_IShape_Scaled", names);
        Assert.Contains("fx_Square_get_Label", names);
        Assert.DoesNotContain("fx_IShape_get_Label", names);
        Assert.DoesNotContain("fx_Shape_get_Unit", names);
        Assert.DoesNotContain("fx_Shape_SideOf", names);
        Assert.Contains("fx_ShapeExtensions_Twice", names);
        Assert.DoesNotContain("fx_PlateMaker_Make", names);
    }

    /// <summary>On nanoql's list: a swap's fixed rate and legs, a model's parameters and calibration, the inherited
    /// ones.</summary>
    [Fact]
    public void NanoqlsListBringsWhatItsTypesInherit()
    {
        var names = Manifest(ListedExperimental);
        Assert.Contains("nqlFixedVsFloatingSwapFixedRate", names.Keys);
        Assert.Contains("nqlSwapLeg", names.Keys);
        Assert.Contains("nqlCalibratedModelParams", names.Keys);
        Assert.Contains("nqlCalibratedModelExtensionsCalibrate", names.Keys);
        Assert.DoesNotContain("nqlICalibratedModelParams", names.Keys);
        Assert.Equal(157, ListedExperimental.Selection.Inherited);
        Assert.Equal(10, ListedExperimental.Selection.Extensions);
    }

    /// <summary>A type off the list is withheld: its functions in no listed build, the tests' build holds them.</summary>
    [Fact]
    public void ATypeOffTheListIsWithheld()
    {
        Assert.Null(NameOf(ListedExperimental, "nql_Brent_new"));
        Assert.Equal("nqlBrent", NameOf(WholeExperimental, "nql_Brent_new"));
    }

    /// <summary>Excel's filter inside listed types: equality and increment operators, results records' constructors and
    /// with, setters, the members that change state, write files or read the clock, and the engine hooks.</summary>
    [Fact]
    public void ExcelsFilterAppliesInsideTheList()
    {
        var skips = WholeExperimental.Selection.Skips;
        Assert.Equal(356, skips["equality operator"]);
        Assert.Equal(15, skips["in-out"]);
        Assert.Equal(69, skips["engine hook"]);
        Assert.Equal(41, skips["state-changing"]);
        var names = WholeExperimental.Selection.Units.SelectMany(u => u.Functions).Select(f => f.CName).ToHashSet();
        Assert.DoesNotContain("nql_ConventionsXml_WriteDefaults", names);
        Assert.DoesNotContain("nql_Date_TodaysDate", names);
        Assert.DoesNotContain("nql_ConventionProvider_set_Default", names);
        Assert.DoesNotContain("nql_Swap_FetchResults", names);
        Assert.Contains(names, n => n.StartsWith("nql_CliquetOption_Validate", StringComparison.Ordinal));
    }

    /// <summary>The stable build runs its own fixpoint after its cut: none more on the seed.</summary>
    [Fact]
    public void TheStableBuildsOwnFixpoint()
    {
        Assert.Equal(0, ListedStable.Selection.StableWithheld);
        Assert.Equal(19, ListedStable.Selection.Withheld.Count);
    }
}
