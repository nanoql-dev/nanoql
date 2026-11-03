// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;
using static NanoQL.Tests.Divergences.Fd2dMismatchedTimesTests;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The n-dimensional finite-difference basket engine on two risk-free curves. QuantLib's American branch builds every
/// asset's forward on the first process's curve (<c>fdndimblackscholesvanillaengine.cpp:60-68</c>, <c>:175-187</c>),
/// so with asset 2 on a curve at 8% and asset 1 at 5% an American average put prices 9.3498 whatever asset 2's rate,
/// 5.2% above the same market restated onto the first curve (asset 2's dividend yield q₂ + r₁ − r₂, the same forward),
/// and its European branch refuses two curves through its final discount ("interest rates need to be the same for all
/// underlyings", <c>vectorbsmprocessextractor.cpp:52-57</c>). nanoql reads each forward on its own process's curves and
/// discounts on the first's in both branches, as QuantLib's spread and Monte Carlo basket engines read two curves:
/// QuantLib's engine on the restated market is the check, and the two-dimensional engine, which drifts each asset on
/// its own curve, prices the American within the grids' error. On one risk-free curve the reads are QuantLib's.
/// </summary>
public class FdNdimTwoRiskFreeCurvesTests
{
    [Fact]
    public void TheAmericanBranchReadsEachForwardOnItsOwnCurve()
    {
        // QuantLib's engine on the restated market, 8.890746625015687; on the two curves, 9.349849896995247
        Assert.Equal(8.890746625015687, FdNdimMismatchedTimesTests.Price([P1(), P2()], AveragePut(), American()), 1e-9);
    }

    [Fact]
    public void TheEuropeanBranchPricesWhereQuantLibRefuses()
    {
        // QuantLib's engine on the restated market, 9.743639218361; on the two curves it refuses
        Assert.Equal(9.743639218361, FdNdimMismatchedTimesTests.Price([P1(), P2()], SpreadCall(), European()), 1e-9);
    }

    [Fact]
    public void TheAmericanMeetsTheTwoDimensionalEngine()
    {
        // the two-dimensional engine on the two curves, QuantLib's on the restated market 8.896714402126468 within its
        // splitting error, against the n-dimensional engine's 8.890746625015687: within the grids' error
        double twoDim = Price(P1(), P2(), AveragePut(), American());
        Assert.Equal(8.896714402126468, twoDim, 1e-4);
        Assert.Equal(twoDim, FdNdimMismatchedTimesTests.Price([P1(), P2()], AveragePut(), American()), 0.01);
    }

    private static GeneralizedBlackScholesProcess P1() => Process(100.0, Flat(0.02), Flat(0.05), Vol(0.25));

    private static GeneralizedBlackScholesProcess P2() => Process(95.0, Flat(0.03), Flat(0.08), Vol(0.3));
}
