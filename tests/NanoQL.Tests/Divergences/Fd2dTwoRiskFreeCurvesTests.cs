// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;
using static NanoQL.Tests.Divergences.Fd2dMismatchedTimesTests;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The two-dimensional finite-difference basket engine on two risk-free curves. Each asset's operator discounts at its
/// own risk-free rate, and QuantLib's mixed term adds back the first process's
/// (<c>fdm2dblackscholesop.cpp:106-116</c>), so its price discounts at the second's, where every analytic two-asset
/// engine discounts on the first's curve: a spread call with asset 2 on a curve at 8% and asset 1 at 5% prices 2.96%
/// below the same market restated onto the first curve (asset 2's dividend yield q₂ + r₁ − r₂, the same forward).
/// nanoql's mixed term adds back the second's rate, so the price discounts on the first process's curve: QuantLib's
/// engine on the restated market is the check, within the scheme's splitting error, the two curves' rates sitting in
/// the two directions' operators where the restated market's sit in one (6e-6 relative here). On one risk-free curve
/// the two read the same rate.
/// </summary>
public class Fd2dTwoRiskFreeCurvesTests
{
    [Fact]
    public void TheMixedTermAddsBackTheSecondRateSoThePriceDiscountsOnTheFirstCurve()
    {
        GeneralizedBlackScholesProcess p1 = Process(100.0, Flat(0.02), Flat(0.05), Vol(0.25));
        GeneralizedBlackScholesProcess p2 = Process(95.0, Flat(0.03), Flat(0.08), Vol(0.3));

        // QuantLib's engine on the market restated onto the first curve, 9.760842595569178, within the splitting error;
        // on the two curves as given, 9.47231681679573, discounting at 8%
        Assert.Equal(9.760842595569178, Price(p1, p2, SpreadCall(), European()), 1e-4);
    }
}
