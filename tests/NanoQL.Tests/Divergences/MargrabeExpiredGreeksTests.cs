// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An expired Margrabe option's assets' deltas and gammas. QuantLib's option never initializes them
/// (<c>margrabeoption.hpp:55</c>, members its constructor leaves unset) and its expired path sets only
/// <c>MultiAssetOption</c>'s results (<c>multiassetoption.cpp:74-77</c>, through <c>instrument.hpp:132-133</c>), so
/// <c>delta1()</c> and the others read uninitialized memory (<c>margrabeoption.cpp:32-54</c>), which is undefined.
/// nanoql refuses the read as QuantLib's results refuse an unset value: the four are <see langword="null"/>, the NPV
/// and the multi-asset Greeks 0 as QuantLib's (the playbook's undefined-behaviour row).
/// </summary>
public class MargrabeExpiredGreeksTests
{
    [Fact]
    public void BothEnginesLeaveTheAssetsGreeksUnset()
    {
        var p1 = Process(100.0, 0.02, 0.05, 0.25);
        var p2 = Process(95.0, 0.01, 0.05, 0.3);
        MargrabeOptionResults[] expired =
        [
            AnalyticEuropeanMargrabeEngine.Calculate(new MargrabeOption(1, 1, new EuropeanExercise(Today - 1)), Context, p1, p2, 0.5),
            AnalyticAmericanMargrabeEngine.Calculate(new MargrabeOption(2, 1, new AmericanExercise(Today - 30, Today - 1)), Context, p1, p2, 0.5),
        ];
        foreach (MargrabeOptionResults r in expired)
        {
            Assert.Null(r.Delta1);
            Assert.Null(r.Delta2);
            Assert.Null(r.Gamma1);
            Assert.Null(r.Gamma2);
            Assert.Equal(0.0, r.NPV);
            Assert.Equal(0.0, r.Delta);
            Assert.Equal(0.0, r.Gamma);
            Assert.Equal(0.0, r.Theta);
            Assert.Equal(0.0, r.Vega);
            Assert.Equal(0.0, r.Rho);
            Assert.Equal(0.0, r.DividendRho);
        }
    }
}
