// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

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
}
