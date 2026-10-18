// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A global bootstrap's penalties and initial-guess function receive the working state of the curve under
/// construction and the context, where QuantLib's receive the curve's times and data and reach the helpers through
/// the user's own pointers. The guess function is called once per curve built, and sees no data, as QuantLib's sees
/// none on a curve with no previous state; the penalties are called at each of the optimizer's evaluations, with the
/// curve's data, and the state's additional helpers are bound to the curve, the expired ones unbound.
/// </summary>
public class GlobalBootstrapFunctionsTests
{
    [Fact]
    public void TheFunctionsSeeTheWorkingState()
    {
        var today = new Date(15, Month.January, 2024);
        var spot = new Date(17, Month.January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };
        RateHelper[] helpers =
        [
            Deposit(new Period(6, TimeUnit.Months), 2, 0.030, context),
            Swap(2, 0.031, context),
            Swap(5, 0.032, context),
        ];
        RateHelper[] additional =
        [
            Deposit(new Period(1, TimeUnit.Days), 0, 0.029, context),
            Deposit(new Period(3, TimeUnit.Months), 2, 0.0302, context),
            Deposit(new Period(9, TimeUnit.Months), 2, 0.0304, context),
        ];

        int guesses = 0, penalties = 0;
        int guessData = -1;
        double[]? penaltyData = null;
        bool? expiredBound = null;
        var bootstrap = new GlobalBootstrap(
            additionalHelpers: additional,
            additionalPenalties: (state, c) =>
            {
                penalties++;
                penaltyData = state.Data.ToArray();
                try
                {
                    _ = state.AdditionalHelpers[0].ImpliedQuote(c);
                    expiredBound = true;
                }
                catch (NanoQLException)
                {
                    expiredBound = false;
                }

                return new Vector([state.AdditionalHelpers[2].QuoteError(c)]);
            },
            initialGuessFn: (state, _) =>
            {
                guesses++;
                guessData = state.Data.Length;
                return new Vector(state.Times.Length - 1, 0.97);
            });

        var curve = new PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>(
            spot, helpers, new Actual365Fixed(), context, bootstrap: bootstrap);

        Assert.Equal(1, guesses);
        Assert.Equal(0, guessData);
        Assert.True(penalties > 1);
        Assert.Equal(curve.Data(), penaltyData!);
        Assert.False(expiredBound);
    }

    private static DepositRateHelper Deposit(Period tenor, int fixingDays, double rate, EvaluationContext context) =>
        new(
            rate, tenor, fixingDays, new TARGET(), BusinessDayConvention.ModifiedFollowing, false, new Actual360(), context);

    private static SwapRateHelper Swap(int years, double rate, EvaluationContext context) =>
        new(
            rate,
            new Period(years, TimeUnit.Years),
            new TARGET(),
            Frequency.Annual,
            BusinessDayConvention.Unadjusted,
            new Thirty360(Thirty360Convention.BondBasis),
            new Euribor6M(),
            context);
}
