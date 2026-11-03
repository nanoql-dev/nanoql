// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;
using NanoQL.Time;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>
/// The fold: a generic type's constructions as one worksheet function, its type arguments by name in any case, each a
/// piece of the construction's C name; another construction's object, and a combination no construction has, refused;
/// the stable build's family holding its stable constructions, an experimental one refused naming the experimental
/// build.
/// </summary>
[Collection(TestSettings.Name)]
public sealed class FoldTests
{
    /// <summary>Initializes a new instance of the <see cref="FoldTests"/> class, on a clean runtime.</summary>
    public FoldTests() => Reset();

    /// <summary>The fold's six checks: a folded constructor, a member shared by two constructions, the refusals, the
    /// count.</summary>
    [Fact]
    public void AFamilyTakesItsTypeArgumentsByName()
    {
        Date[] d = [new(15, Month.September, 2015), new(16, Month.September, 2015), new(17, Month.September, 2015)];
        double[] v = [1.5, 2.25, -0.5];
        object dates = Col([.. d.Select(x => (object)(double)x.SerialNumber)]), values = Col([.. v.Select(x => (object)x)]);
        string? ts = Xl.Experimental.Methods.Keys.Where(n => n.StartsWith("nqlTimeSeries", StringComparison.Ordinal) && Xl.Experimental.Methods[n].GetParameters().Length == 3)
            .Select(n => Call(n, "double", dates, values)).OfType<string>().FirstOrDefault(s => s.StartsWith("TimeSeries#", StringComparison.Ordinal));
        Assert.NotNull(ts);
        var want = new TimeSeries<double>(d, v);
        Assert.Equal((double)want.Size, D(Call("nqlTimeSeriesSize", "Double", ts)));
        var got = Assert.IsType<object[,]>(Call("nqlTimeSeriesValues", "DOUBLE", ts));
        Assert.Equal(want.Values().ToArray().Select(BitConverter.DoubleToInt64Bits), got.Cast<object>().Select(x => BitConverter.DoubleToInt64Bits((double)x)));
        Assert.Equal(ExcelError.ExcelErrorValue, Call("nqlTimeSeriesSize", "IntervalPrice", ts));
        Assert.Equal(ExcelError.ExcelErrorValue, Call("nqlPiecewiseYieldCurveDates", "Discount", "Linear", "NoSuchBootstrap", "x"));
        Assert.Contains("one of", Message(), StringComparison.Ordinal);
        int piecewise = Xl.Experimental.Methods.Keys.Count(n => n.StartsWith("nqlPiecewiseYieldCurve", StringComparison.Ordinal));
        Assert.InRange(piecewise, 1, 29);
    }

    /// <summary>The nine SABR cube checks in the experimental build: every construction reaches its code.</summary>
    [Fact]
    public void TheCubesFamiliesInTheExperimentalBuild() => Cube(Xl.Experimental, stable: false);

    /// <summary>The nine SABR cube checks in the stable build: the NoArb construction refused, naming the experimental
    /// build.</summary>
    [Fact]
    public void TheCubesFamiliesInTheStableBuild() => Cube(Xl.Stable, stable: true);

    private static void Cube(Build build, bool stable)
    {
        var families = build.Functions.Where(f => f.GetProperty("excel").GetString()!.StartsWith("nqlXabrSwaptionVolatilityCube", StringComparison.Ordinal)).ToList();
        Assert.Equal(9, families.Count);
        foreach (var f in families.Where(f => f.GetProperty("kind").GetString() != "new"))
        {
            string n = f.GetProperty("excel").GetString()!;
            object?[] Args(string t) => [t, "XabrSwaptionVolatilityCube#0000000000000000"];

            // A stable construction reaches its code, where the unknown handle is #REF!; the nested argument by its C
            // name's piece in any case; the CLR's arity-marked name refused.
            Assert.Equal(ExcelError.ExcelErrorRef, build.Call(n, Args("SwaptionVolCubeSabrModel")));
            Assert.Equal(ExcelError.ExcelErrorRef, build.Call(n, Args("swaptionvolcubezabrmodel_ZABRSHORTMATURITYLOGNORMAL")));
            Assert.Equal(ExcelError.ExcelErrorValue, build.Call(n, Args("SwaptionVolCubeZabrModel`1")));
            object noarb = build.Call(n, Args("SwaptionVolCubeNoArbSabrModel"));
            Assert.Equal(stable ? ExcelError.ExcelErrorValue : ExcelError.ExcelErrorRef, noarb);
            if (stable)
            {
                Assert.Contains("experimental build", Message(), StringComparison.Ordinal);
            }
        }
    }
}
