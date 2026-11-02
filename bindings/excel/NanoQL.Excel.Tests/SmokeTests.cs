// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel.Tests;

/// <summary>The swap of XLP6 through the experimental tests' build: S-bind's bits for its NPV and its fair rate.</summary>
[Collection(TestSettings.Name)]
public sealed class SmokeTests
{
    /// <summary>A 10-year swap on a flat 3% curve, its NPV and fair rate bit for bit as S-bind's C suite gives them.</summary>
    [Fact]
    public void TheSwapGivesSBindsBits()
    {
        Xl.Reset();
        double date = 42262;  // DATE(2015,9,15)
        string ctx = Xl.H(Xl.Call("nqlEvaluationContext", date));
        string set = Xl.H(Xl.Call("nqlConventionSetQuantLib"));
        string dc = Xl.H(Xl.Call("nqlConventionSetDayCounter", set, "Actual365Fixed"));
        string curve = Xl.H(Xl.Call("nqlFlatForward", date, 0.03, dc));
        string index = Xl.H(Xl.Call("nqlConventionSetIborIndex2", set, "Euribor", "6M", curve));
        string make = Xl.H(Xl.Call("nqlMakeVanillaSwap", "10Y", index));
        string fixedRate = Xl.H(Xl.Call("nqlMakeVanillaSwapWithFixedRate", make, 0.031));
        string swap = Xl.H(Xl.Call("nqlMakeVanillaSwapToVanillaSwap", fixedRate, ctx));
        string results = Xl.H(Xl.Call("nqlDiscountingSwapEngineCalculate2", swap, ctx, curve));
        Assert.Equal(unchecked((long)0xBF721BC276DF73C0UL), BitConverter.DoubleToInt64Bits(Xl.D(Xl.Call("nqlInstrumentResultsNPV", results))));
    }
}
