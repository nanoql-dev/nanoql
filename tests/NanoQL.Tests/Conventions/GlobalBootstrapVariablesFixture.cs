// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib's testGlobalBootstrapVariables setup (<c>test-suite/piecewiseyieldcurve.cpp:1491-1548</c>) on 25 September
/// 2019: its fixture's deposits and swaps less the first swap, the additional helper whose quote error, times 1e4, is
/// the penalty; and three IMM futures on Euribor 3M whose convexity adjustments read the volatility the bootstrap
/// optimizes, built by <see cref="GlobalBootstrap.VariableHelpers"/>.
/// </summary>
internal static class GlobalBootstrapVariablesFixture
{
    public static readonly Date Today = new(25, Month.September, 2019);

    public static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    public static readonly Date Settlement = new TARGET().Advance(Today, 2, TimeUnit.Days);

    private static readonly (int N, TimeUnit Units, double Rate)[] DepositData =
    [
        (1, TimeUnit.Weeks, 4.559), (1, TimeUnit.Months, 4.581), (2, TimeUnit.Months, 4.573),
        (3, TimeUnit.Months, 4.557), (6, TimeUnit.Months, 4.496), (9, TimeUnit.Months, 4.490),
    ];

    private static readonly (int Years, double Rate)[] SwapData =
    [
        (1, 4.54), (2, 4.63), (3, 4.75), (4, 4.86), (5, 4.99), (6, 5.11), (7, 5.23), (8, 5.33), (9, 5.41),
        (10, 5.47), (12, 5.60), (15, 5.75), (20, 5.89), (25, 5.95), (30, 5.96),
    ];

    private static readonly double[] ImmFutPrices = [100.0 - 4.581, 100.0 - 4.573, 100.0 - 4.557];

    /// <summary>Gets the futures' index.</summary>
    public static Euribor3M Euribor3M { get; } = new();

    /// <summary>Gets QuantLib's IMM dates: each the next, jumping one forward where its fixing is before today.</summary>
    public static ImmutableArray<Date> ImmDates { get; } = MakeImmDates();

    /// <summary>The fixture's deposits and swaps.</summary>
    /// <returns>The helpers, deposits first.</returns>
    public static List<RateHelper> CurveHelpers()
    {
        var helpers = new List<RateHelper>();
        foreach ((int n, TimeUnit units, double rate) in DepositData)
        {
            helpers.Add(new DepositRateHelper(rate / 100, new Euribor(new Period(n, units)), Context));
        }

        var euribor6m = new Euribor6M();
        foreach ((int years, double rate) in SwapData)
        {
            helpers.Add(new SwapRateHelper(
                rate / 100, new Period(years, TimeUnit.Years), new TARGET(), Frequency.Annual,
                BusinessDayConvention.Unadjusted, new Thirty360(Thirty360Convention.BondBasis), euribor6m, Context));
        }

        return helpers;
    }

    /// <summary>The futures on a volatility, each adjusted by Hull-White's convexity bias at a 3% mean reversion.</summary>
    /// <param name="vol">The variables' values, the volatility first.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The futures helpers.</returns>
    public static ImmutableArray<IBootstrapHelper> Futures(ImmutableArray<double> vol, EvaluationContext context)
    {
        var futures = ImmutableArray.CreateBuilder<IBootstrapHelper>(ImmDates.Length);
        for (int i = 0; i < ImmDates.Length; i++)
        {
            var convAdj = new FuturesConvAdjustmentQuote(Euribor3M, ImmDates[i], ImmFutPrices[i], vol[0], 0.03, context);
            futures.Add(new FuturesRateHelper(ImmFutPrices[i], ImmDates[i], Euribor3M, convAdj.Value, FuturesType.IMM));
        }

        return futures.MoveToImmutable();
    }

    /// <summary>QuantLib's curve with futures, its bootstrap's other settings given.</summary>
    /// <param name="variableHelpers">The variable helpers; <see langword="null"/> for <see cref="Futures"/>.</param>
    /// <param name="weights">The instrument weights; <see langword="null"/> for none.</param>
    /// <returns>The curve.</returns>
    public static PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap> Curve(
        Func<ImmutableArray<double>, EvaluationContext, ImmutableArray<IBootstrapHelper>>? variableHelpers = null,
        IEnumerable<double>? weights = null)
    {
        List<RateHelper> helpers = CurveHelpers();
        RateHelper firstSwap = helpers[DepositData.Length];
        helpers.RemoveAt(DepositData.Length);

        static Vector Penalties(IBootstrapState state, EvaluationContext context) =>
            new(1, 1e4 * state.AdditionalHelpers[0].QuoteError(context));

        return new PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>(
            Settlement,
            helpers,
            new Actual365Fixed(),
            Context,
            i: default(LogLinear),
            bootstrap: new GlobalBootstrap(
                accuracy: 1e-12,
                instrumentWeights: weights,
                additionalHelpers: [firstSwap],
                additionalPenalties: Penalties,
                additionalVariables: new SimpleQuoteVariables(1, [1.0], [0.0]),
                variableHelpers: variableHelpers ?? Futures));
    }

    private static ImmutableArray<Date> MakeImmDates()
    {
        var dates = ImmutableArray.CreateBuilder<Date>(ImmFutPrices.Length);
        Date immDate = Today;
        for (int i = 0; i < ImmFutPrices.Length; i++)
        {
            immDate = IMM.NextDate(Context, immDate);
            if (Euribor3M.FixingDate(immDate) < Today)
            {
                immDate = IMM.NextDate(Context, immDate);
            }

            dates.Add(immDate);
        }

        return dates.MoveToImmutable();
    }
}
