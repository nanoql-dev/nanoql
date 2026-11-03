// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>
/// Lifetime through RTD topics: an object lives while a topic holds it and goes when its last cell does; a value call's
/// objects (a leg's coupons) go with it; looped objects each with their own topic.
/// </summary>
[Collection(TestSettings.Name)]
public sealed class ReleaseTests
{
    private static readonly double Date = new Date(15, Month.September, 2015).SerialNumber;

    /// <summary>Initializes a new instance of the <see cref="ReleaseTests"/> class, on a clean runtime.</summary>
    public ReleaseTests() => Reset();

    /// <summary>A handle's topic closed releases its object; the call again builds it again, the same handle.</summary>
    [Fact]
    public void AClosedTopicReleasesItsObject()
    {
        string dc = H(Call("nqlConventionSetDayCounter", null, "Actual365Fixed"));
        string curve = H(Call("nqlFlatForward", Date, 0.03, dc));
        Assert.True(X.TryEntry(curve, out XlEntry entry));
        int before = X.Count;
        Host.Close(entry.Key);
        Assert.False(X.IsHandle(curve));
        Assert.Equal(before - 1, X.Count);
        Assert.Equal(curve, H(Call("nqlFlatForward", Date, 0.03, dc)));
    }

    /// <summary>A leg's coupons, spilled by a value call, go with its topic.</summary>
    [Fact]
    public void ALegsCouponsGoWithItsCell()
    {
        string set = H(Call("nqlConventionSetQuantLib"));
        string dc = H(Call("nqlConventionSetDayCounter", set, "Actual365Fixed"));
        string curve = H(Call("nqlFlatForward", Date, 0.03, dc));
        string index = H(Call("nqlConventionSetIborIndex2", set, "Euribor", "6M", curve));
        string ctx = H(Call("nqlEvaluationContext", Date));
        string swap = H(Call("nqlMakeVanillaSwapToVanillaSwap", H(Call("nqlMakeVanillaSwapWithFixedRate", H(Call("nqlMakeVanillaSwap", "10Y", index)), 0.031)), ctx));
        int before = X.Count;
        string[] topics = [.. Host.Topics.Keys];
        var leg = Assert.IsType<object[,]>(Call("nqlSwapLeg", swap, 0));
        Assert.Equal(10, leg.Length);
        Assert.Equal(before + 10, X.Count);
        string legTopic = Host.Topics.Keys.Except(topics).Single();
        Host.Close(legTopic);
        Assert.Equal(before, X.Count);
        Assert.False(X.IsHandle((string)leg[0, 0]));
    }

    /// <summary>Looped objects each hold a topic: closing one releases its object alone.</summary>
    [Fact]
    public void LoopedObjectsGoOneByOne()
    {
        string dc = H(Call("nqlConventionSetDayCounter", null, "Actual365Fixed"));
        var curves = Assert.IsType<object[,]>(Call("nqlFlatForward", Date, Col(0.02, 0.03, 0.04), dc));
        Assert.True(X.TryEntry((string)curves[1, 0], out XlEntry middle));
        Host.Close(middle.Key);
        Assert.False(X.IsHandle((string)curves[1, 0]));
        Assert.True(X.IsHandle((string)curves[0, 0]));
        Assert.True(X.IsHandle((string)curves[2, 0]));
    }
}
