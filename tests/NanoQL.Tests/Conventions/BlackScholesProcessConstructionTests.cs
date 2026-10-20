// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="GeneralizedBlackScholesProcess"/>'s local volatility is chosen in its constructor, by QuantLib's type
/// tests in QuantLib's order (<c>blackscholesprocess.cpp:180-224</c>), where QuantLib chooses it at the first use and
/// caches it: a <see cref="BlackConstantVol"/> gives a <see cref="LocalConstantVol"/> at its volatility at the spot, a
/// <see cref="BlackVarianceCurve"/> a <see cref="LocalVolCurve"/>, both strike-independent, and any other structure a
/// <see cref="LocalVolSurface"/> at the spot; a process given its local volatility uses it and is never
/// strike-independent. <see cref="BlackScholesProcess.Create"/> dates its zero dividend curve by the context, as
/// QuantLib's <c>FlatForward(0, NullCalendar(), 0.0, Actual365Fixed())</c> is by the evaluation date.
/// </summary>
public class BlackScholesProcessConstructionTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();
    private static readonly YieldTermStructure Q = new FlatForward(Today, 0.02, Dc);
    private static readonly YieldTermStructure R = new FlatForward(Today, 0.04, Dc);

    [Fact]
    public void AConstantBlackVolatilityGivesAConstantLocalVolatility()
    {
        var process = new BlackScholesMertonProcess(100.0, Q, R, new BlackConstantVol(Today, null, 0.25, Dc));

        LocalConstantVol local = Assert.IsType<LocalConstantVol>(process.LocalVolatility);
        Assert.Equal(0.25, local.LocalVol(1.0, 130.0));
        Assert.Equal(Today, local.ReferenceDate);

        // strike-independent: the expectation is the exact one
        Assert.Equal(100.0 * Math.Exp(0.5 * (0.04 - 0.02)), process.Expectation(0.0, 100.0, 0.5), 1e-12);
    }

    [Fact]
    public void AVarianceCurveGivesALocalVolatilityCurve()
    {
        var curve = new BlackVarianceCurve(Today, [Today + 182, Today + 365], [0.25, 0.22], Dc);
        var process = new BlackScholesMertonProcess(100.0, Q, R, curve);

        Assert.IsType<LocalVolCurve>(process.LocalVolatility);
        Assert.Equal(curve.BlackVariance(0.75, 100.0) - curve.BlackVariance(0.25, 100.0), process.Variance(0.25, 100.0, 0.5));
    }

    [Fact]
    public void AnyOtherBlackVolatilityGivesALocalVolatilitySurfaceAtTheSpot()
    {
        var vols = new Matrix(2, 2);
        vols[0, 0] = 0.30;
        vols[0, 1] = 0.28;
        vols[1, 0] = 0.20;
        vols[1, 1] = 0.21;
        var surface = new BlackVarianceSurface(Today, null, [Today + 182, Today + 365], [90.0, 110.0], vols, Dc);
        var process = new BlackScholesMertonProcess(100.0, Q, R, surface);

        LocalVolSurface local = Assert.IsType<LocalVolSurface>(process.LocalVolatility);
        Assert.Equal(new LocalVolSurface(surface, R, Q, 100.0).LocalVol(0.5, 100.0, true), local.LocalVol(0.5, 100.0, true));
        Assert.Throws<NanoQLException>(() => process.Expectation(0.0, 100.0, 0.5));
    }

    [Fact]
    public void AGivenLocalVolatilityIsUsedAndNeverStrikeIndependent()
    {
        var given = new LocalConstantVol(Today, 0.3, Dc);
        var process = new GeneralizedBlackScholesProcess(100.0, Q, R, new BlackConstantVol(Today, null, 0.25, Dc), given);

        Assert.Same(given, process.LocalVolatility);
        Assert.Equal(0.3 * 0.3 * 0.5, process.Variance(0.0, 100.0, 0.5), 1e-15);
        Assert.Throws<NanoQLException>(() => process.Expectation(0.0, 100.0, 0.5));
    }

    [Fact]
    public void BlackScholesProcesssDividendCurveIsDatedByItsContext()
    {
        var later = new Date(30, Month.June, 2027);
        GeneralizedBlackScholesProcess process = BlackScholesProcess.Create(
            100.0, R, new BlackConstantVol(Today, null, 0.25, Dc), new EvaluationContext { EvaluationDate = later });

        Assert.Equal(later, process.DividendYield.ReferenceDate);
        Assert.Equal(1.0, process.DividendYield.Discount(later + 365));
        Assert.Equal("Actual/365 (Fixed)", process.DividendYield.DayCounter.Name);
    }
}
