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
/// The Monte Carlo European basket engine on structures that measure time differently. QuantLib's engine evolves every
/// process on the first's times (<c>mceuropeanbasketengine.hpp:169-170</c>; <c>stochasticprocessarray.cpp</c>'s time),
/// each process reading its structures at them: with asset 2's dividend curve and volatility on Act/360, their
/// discounts and variances by date unchanged, a spread call prices 0.58% below the same market restated on Act/365 on
/// the same seed, and on a risk-free curve settling two business days after the other structures 0.53% below. nanoql's
/// paths run on the clock spanning every process's structures, each read at the instants its times denote and accruing
/// nothing before its own date (the S-exo plan's ER5 (i)), so on the same seed they price the restated market:
/// QuantLib's engine on that market is the check, well inside its error estimate. A market on one clock keeps
/// QuantLib's draws and arithmetic.
/// </summary>
public class McBasketMismatchedTimesTests
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

        // QuantLib's engine on the market restated on Act/365, seed 42, 8191 samples, 4 steps: 10.904717823351486,
        // its error estimate 0.17114128882381066; on the market as given, 10.84146090810568
        (double npv, double error) = Price(p1, p2);
        Assert.Equal(10.904717823351486, npv, 1e-9);
        Assert.Equal(0.17114128882381066, error, 1e-9);
    }

    [Fact]
    public void ARiskFreeCurveSettlingLaterIsPricedFromTheEarliestDate()
    {
        YieldTermStructure settling = FlatForward.Create(2, new TARGET(), 0.05, A365, Context);
        GeneralizedBlackScholesProcess p1 = Process(100.0, Flat(0.02), settling, Vol(0.25));
        GeneralizedBlackScholesProcess p2 = Process(95.0, Flat(0.03), settling, Vol(0.3));

        // QuantLib's engine on the market restated from today, the risk-free curve discounting nothing before
        // 19 October: 10.857531432749152, its error estimate 0.17071288672768428; on the market as given,
        // 10.800186647710508
        (double npv, double error) = Price(p1, p2);
        Assert.Equal(10.857531432749152, npv, 1e-8);
        Assert.Equal(0.17071288672768428, error, 1e-8);
    }

    private static (double Npv, double Error) Price(GeneralizedBlackScholesProcess p1, GeneralizedBlackScholesProcess p2)
    {
        var rho = new Matrix(2, 2);
        rho[0, 0] = rho[1, 1] = 1.0;
        rho[0, 1] = rho[1, 0] = 0.5;
        var array = new StochasticProcessArray([p1, p2], rho);
        MultiAssetOptionResults r = MCEuropeanBasketEngine.Calculate<PseudoRandom, GeneralStatistics>(
            new BasketOption(SpreadCall(), European()), Context, array, 4, null, false, false, 8191, null, null, 42);
        return (r.NPV!.Value, r.ErrorEstimate!.Value);
    }
}
