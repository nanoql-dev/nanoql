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
/// The basis swap rate helpers follow the evaluation date, as QuantLib's relative-date helpers do: a bootstrap under a
/// later evaluation date re-dates a copy of each, which rebuilds its swap from that date, so the curve and the implied
/// quotes are those of helpers built on the later date (QuantLib's <c>initializeDates</c>, run by its helpers'
/// <c>update()</c> when the evaluation date moves).
/// </summary>
public class BasisSwapRateHelperRedatingTests
{
    private static readonly EvaluationContext Early = new() { EvaluationDate = new Date(15, January, 2025) };
    private static readonly EvaluationContext Late = new() { EvaluationDate = new Date(22, January, 2025) };
    private static readonly YieldTermStructure Known = new FlatForward(Early.EvaluationDate, 0.01, new Actual365Fixed());
    private static readonly YieldTermStructure Discount = new FlatForward(Early.EvaluationDate, 0.005, new Actual365Fixed());
    private static readonly (int Years, double Basis)[] Quotes = [(1, 0.0010), (2, 0.0012), (3, 0.0015), (5, 0.0018)];

    [Fact]
    public void TheIborIborHelperIsRebuiltOnTheLaterDate() =>
        AssertRedated(
            new TARGET(),
            context => [.. Quotes.Select(q => (BootstrapHelper<YieldTermStructure>)new IborIborBasisSwapRateHelper(
                q.Basis,
                new Period(q.Years, TimeUnit.Years),
                2,
                new TARGET(),
                BusinessDayConvention.ModifiedFollowing,
                false,
                new Euribor3M(),
                new Euribor6M(Known),
                Discount,
                true,
                context))]);

    [Fact]
    public void TheOvernightIborHelperIsRebuiltOnTheLaterDate() =>
        AssertRedated(
            new UnitedStates(UnitedStatesMarket.GovernmentBond),
            context => [.. Quotes.Select(q => (BootstrapHelper<YieldTermStructure>)new OvernightIborBasisSwapRateHelper(
                q.Basis,
                new Period(q.Years, TimeUnit.Years),
                2,
                new UnitedStates(UnitedStatesMarket.GovernmentBond),
                BusinessDayConvention.Following,
                false,
                new Sofr(Known),
                new USDLibor(new Period(6, TimeUnit.Months)),
                context,
                Discount))]);

    [Fact]
    public void TheOvernightOvernightHelperIsRebuiltOnTheLaterDate() =>
        AssertRedated(
            new UnitedStates(UnitedStatesMarket.GovernmentBond),
            context => [.. Quotes.Select(q => (BootstrapHelper<YieldTermStructure>)new OvernightOvernightBasisSwapRateHelper(
                -q.Basis,
                new Period(q.Years, TimeUnit.Years),
                2,
                new UnitedStates(UnitedStatesMarket.GovernmentBond),
                BusinessDayConvention.Following,
                false,
                new FedFunds(Known),
                new Sofr(),
                context,
                Discount,
                paymentFrequency: Frequency.Quarterly))]);

    // The curve bootstrapped under the later date on helpers built on the earlier one equals the curve on helpers built
    // on the later one, node for node and implied quote for implied quote.
    private static void AssertRedated(
        Calendar calendar, Func<EvaluationContext, BootstrapHelper<YieldTermStructure>[]> helpers)
    {
        BootstrapHelper<YieldTermStructure>[] early = helpers(Early);
        BootstrapHelper<YieldTermStructure>[] late = helpers(Late);
        Assert.NotEqual(late[0].MaturityDate, early[0].MaturityDate);

        var movedBound = new List<IBootstrapHelper>();
        var freshBound = new List<IBootstrapHelper>();
        var moved = PiecewiseYieldCurve<ZeroYield, Linear, Capturing>.Create(
            0, calendar, early, new Actual365Fixed(), Late, bootstrap: new Capturing(movedBound));
        var fresh = PiecewiseYieldCurve<ZeroYield, Linear, Capturing>.Create(
            0, calendar, late, new Actual365Fixed(), Late, bootstrap: new Capturing(freshBound));

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
