// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.Fd2dMismatchedTimesTests;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The n-dimensional finite-difference basket engine on structures that measure time differently. QuantLib's engine
/// takes its maturity from the first process's risk-free times and reads every inner value's discounts at them
/// (<c>fdndimblackscholesvanillaengine.cpp:58-63</c>, <c>:134-136</c>): with asset 2's dividend curve and volatility
/// on Act/360, their discounts and variances by date unchanged, a spread call prices 0.16% below the same market
/// restated on Act/365, and an American average put on a risk-free curve settling two business days after the other
/// structures 0.19% below. nanoql runs on the clock spanning every process's structures, each read at the instants its
/// times denote and accruing nothing before its own date (the S-exo plan's ER5 (i)), so it prices the restated market:
/// QuantLib's engine on that market is the check. A market on one clock keeps QuantLib's arithmetic.
/// </summary>
public class FdNdimMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly DayCounter A360 = new Actual360();
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void TheSecondAssetsStructuresOnAnotherDayCounterPriceTheRestatedMarket()
    {
        GeneralizedBlackScholesProcess p1 = Process(100.0, Flat(0.02), Flat(0.05), Vol(0.25));
        GeneralizedBlackScholesProcess p2 = Process(95.0, Flat(0.03 * 360.0 / 365.0, A360), Flat(0.05), Vol(0.3, A360));

        // QuantLib's engine on the market restated on Act/365: 11.02233502733441 and 9.383582334324275; on the market
        // as given, 11.00496879945868 and 9.377033029981401
        Assert.Equal(11.02233502733441, Price([p1, p2], SpreadCall(), European()), 1e-9);
        Assert.Equal(9.383582334324275, Price([p1, p2], AveragePut(), American()), 1e-9);
    }

    [Fact]
    public void ARiskFreeCurveSettlingLaterIsPricedFromTheEarliestDate()
    {
        YieldTermStructure settling = FlatForward.Create(2, new TARGET(), 0.05, A365, Context);
        GeneralizedBlackScholesProcess p1 = Process(100.0, Flat(0.02), settling, Vol(0.25));
        GeneralizedBlackScholesProcess p2 = Process(95.0, Flat(0.03), settling, Vol(0.3));

        // QuantLib's engine on the market restated from today (the risk-free curve discounting nothing before
        // 19 October): 10.975221650428653 and 9.38012652754251; on the market as given, 10.973543096482427 and
        // 9.362294476197752
        Assert.Equal(10.975221650428653, Price([p1, p2], SpreadCall(), European()), 1e-8);
        Assert.Equal(9.38012652754251, Price([p1, p2], AveragePut(), American()), 1e-8);
    }

    // QuantLib's engine at 31 points, 25 steps, Douglas's scheme, correlation 0.5
    internal static double Price(GeneralizedBlackScholesProcess[] processes, BasketPayoff payoff, Exercise exercise)
    {
        var rho = new Matrix(2, 2);
        rho[0, 0] = rho[1, 1] = 1.0;
        rho[0, 1] = rho[1, 0] = 0.5;
        return FdndimBlackScholesVanillaEngine.Calculate(
            new BasketOption(payoff, exercise), Context, [.. processes], rho, 31, 25, 0, FdmSchemeDescOf()).NPV!.Value;
    }

    private static Methods.FdmSchemeDesc FdmSchemeDescOf() => Methods.FdmSchemeDesc.Douglas();
}
