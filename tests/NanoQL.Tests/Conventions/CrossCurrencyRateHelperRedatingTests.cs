// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The cross-currency rate helpers follow the evaluation date, as QuantLib's relative-date helpers do: a bootstrap
/// under a later evaluation date re-dates a copy of each, which rebuilds its schedules, legs and swap from that date,
/// so the curve and the implied quotes are those of helpers built on the later date (QuantLib's <c>initializeDates</c>,
/// run by its helpers' <c>update()</c> when the evaluation date moves).
/// </summary>
public class CrossCurrencyRateHelperRedatingTests
{
    private static readonly EvaluationContext Early = new() { EvaluationDate = new Date(15, January, 2025) };
    private static readonly EvaluationContext Late = new() { EvaluationDate = new Date(22, January, 2025) };
    // on the bootstrap's reference date, as the helpers' engines require (QuantLib's own check)
    private static readonly YieldTermStructure Collateral =
        new FlatForward(Late.EvaluationDate, 0.04, new Actual365Fixed());
    private static readonly YieldTermStructure EurForecast =
        new FlatForward(Early.EvaluationDate, 0.027, new Actual365Fixed());
    private static readonly YieldTermStructure UsdForecast =
        new FlatForward(Early.EvaluationDate, 0.043, new Actual365Fixed());
    private static readonly (int Years, double Quote)[] Bases =
        [(1, -0.0015), (2, -0.0018), (3, -0.0020), (5, -0.0023)];

    private static readonly (int Years, double Quote)[] FixedRates =
        [(1, 0.0265), (2, 0.0258), (3, 0.0255), (5, 0.0256)];

    [Fact]
    public void TheConstantNotionalBasisHelperIsRebuiltOnTheLaterDate() =>
        AssertRedated(context => [.. Bases.Select(q =>
            (BootstrapHelper<YieldTermStructure>)new ConstNotionalCrossCurrencyBasisSwapRateHelper(
                q.Quote,
                new Period(q.Years, TimeUnit.Years),
                2,
                new TARGET(),
                BusinessDayConvention.Following,
                false,
                new Euribor3M(EurForecast),
                new USDLibor(new Period(3, TimeUnit.Months), UsdForecast),
                Collateral,
                false,
                true,
                context))]);

    [Fact]
    public void TheMtMBasisHelperIsRebuiltOnTheLaterDate() =>
        AssertRedated(context => [.. Bases.Select(q =>
            (BootstrapHelper<YieldTermStructure>)new MtMCrossCurrencyBasisSwapRateHelper(
                q.Quote,
                new Period(q.Years, TimeUnit.Years),
                2,
                new TARGET(),
                BusinessDayConvention.Following,
                false,
                new Euribor3M(EurForecast),
                new USDLibor(new Period(3, TimeUnit.Months), UsdForecast),
                Collateral,
                false,
                true,
                false,
                context,
                fxResetFixingDays: 2,
                fxResetFixingCalendar: new TARGET()))]);

    [Fact]
    public void TheFixedVsFloatingHelperIsRebuiltOnTheLaterDate() =>
        AssertRedated(context => [.. FixedRates.Select(q =>
            (BootstrapHelper<YieldTermStructure>)new ConstNotionalCrossCurrencySwapRateHelper(
                q.Quote,
                new Period(q.Years, TimeUnit.Years),
                2,
                new TARGET(),
                BusinessDayConvention.ModifiedFollowing,
                false,
                Frequency.Annual,
                new Thirty360(Thirty360Convention.BondBasis),
                new USDLibor(new Period(3, TimeUnit.Months), UsdForecast),
                Collateral,
                false,
                context))]);

    // The curve bootstrapped under the later date on helpers built on the earlier one equals the curve on helpers built
    // on the later one, node for node and implied quote for implied quote.
    private static void AssertRedated(Func<EvaluationContext, BootstrapHelper<YieldTermStructure>[]> helpers)
    {
        BootstrapHelper<YieldTermStructure>[] early = helpers(Early);
        BootstrapHelper<YieldTermStructure>[] late = helpers(Late);
        Assert.NotEqual(late[0].MaturityDate, early[0].MaturityDate);

        var movedBound = new List<IBootstrapHelper>();
        var freshBound = new List<IBootstrapHelper>();
        var moved = PiecewiseYieldCurve<Discount, LogLinear, Capturing>.Create(
            0, new TARGET(), early, new Actual365Fixed(), Late, bootstrap: new Capturing(movedBound));
        var fresh = PiecewiseYieldCurve<Discount, LogLinear, Capturing>.Create(
            0, new TARGET(), late, new Actual365Fixed(), Late, bootstrap: new Capturing(freshBound));

        Assert.Equal(fresh.Dates(), moved.Dates());
        Assert.Equal(fresh.Data(), moved.Data());
        Assert.Equal(freshBound.Select(h => h.ImpliedQuote(Late)), movedBound.Select(h => h.ImpliedQuote(Late)));
    }

    // QuantLib's iterative bootstrap, recording the helpers it binds: the copies re-dated to the curve's context.
    private readonly struct Capturing(List<IBootstrapHelper> bound) : IBootstrap
    {
        public void Calculate<TTermStructure, TTraits, TInterpolator>(
            BootstrapState<TTermStructure> state, TInterpolator interpolator, EvaluationContext context)
            where TTermStructure : TermStructure
            where TTraits : struct, IBootstrapTraits<TTermStructure>
            where TInterpolator : struct, IInterpolator
        {
            new IterativeBootstrap().Calculate<TTermStructure, TTraits, TInterpolator>(state, interpolator, context);
            bound.AddRange(state.Instruments);
        }
    }
}
