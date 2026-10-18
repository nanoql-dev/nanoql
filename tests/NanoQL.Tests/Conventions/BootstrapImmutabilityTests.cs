// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A piecewise curve bootstraps in its constructor over a working state, which it freezes into its nodes (playbook
/// §2): nothing outside the constructor sees the curve change. Its nodes stay as built while its helpers are bound to
/// it and priced and other curves are built on the same helpers, and the bootstrap sorts its own copy of the helpers,
/// never the caller's list. While it is built, the curve answers from the working state, its inspectors as its
/// discounts do, so a helper of one's own that reads them while it is priced sees the nodes under construction, as
/// QuantLib's does.
/// </summary>
public class BootstrapImmutabilityTests
{
    [Fact]
    public void ABuiltCurvesNodesNeverChange()
    {
        var today = new Date(15, Month.January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };
        var later = new EvaluationContext { EvaluationDate = new Date(16, Month.January, 2024) };

        // given out of pillar order
        List<RateHelper> helpers =
        [
            Swap(context, 5, 0.032),
            new DepositRateHelper(
                0.030, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
                new Actual360(), context),
            Swap(context, 2, 0.031),
        ];
        RateHelper[] given = [.. helpers];

        var curve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            new Date(17, Month.January, 2024), helpers, new Actual365Fixed(), context);
        ImmutableArray<KeyValuePair<Date, double>> nodes = curve.Nodes();
        double discount = curve.Discount(3.0);

        foreach (RateHelper helper in helpers)
        {
            _ = helper.SetTermStructure(curve).QuoteError(context);
        }

        _ = new PiecewiseYieldCurve<ZeroYield, Linear, IterativeBootstrap>(
            new Date(17, Month.January, 2024), helpers, new Actual365Fixed(), later);

        Assert.Equal(nodes, curve.Nodes());
        Assert.Equal(discount, curve.Discount(3.0));
        Assert.Equal(given, helpers);
    }

    [Fact]
    public void AHelperReadingTheCurveWhileItIsBuiltSeesTheWorkingNodes()
    {
        var today = new Date(15, Month.January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };
        List<Seen> seen = [];

        var curve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            today,
            [
                new Observing(0.99, new Date(15, Month.January, 2025), seen),
                new Observing(0.97, new Date(15, Month.January, 2026), seen),
            ],
            new Actual365Fixed(),
            context);

        Assert.NotEmpty(seen);
        foreach (Seen s in seen)
        {
            Assert.Equal(curve.Dates(), s.Dates);
            Assert.Equal(curve.Times(), s.Times);
            Assert.Equal(s.Dates.Zip(s.Data, (d, v) => new KeyValuePair<Date, double>(d, v)), s.Nodes);
        }

        // the solver's last evaluation is the node the curve keeps
        Assert.Equal(curve.Data(), seen[^1].Data);
    }

    private static SwapRateHelper Swap(EvaluationContext context, int years, double rate) =>
        new(
            rate,
            new Period(years, TimeUnit.Years),
            new TARGET(),
            Frequency.Annual,
            BusinessDayConvention.Unadjusted,
            new Thirty360(Thirty360Convention.BondBasis),
            new Euribor6M(),
            context);

    private sealed record Seen(
        ImmutableArray<Date> Dates,
        ImmutableArray<double> Times,
        ImmutableArray<double> Data,
        ImmutableArray<KeyValuePair<Date, double>> Nodes);

    // A helper of one's own: a discount factor to a date, reading the curve's inspectors each time it is priced.
    private sealed class Observing : RateHelper
    {
        private readonly List<Seen> seen;

        public Observing(double discount, Date maturity, List<Seen> seen)
            : base(discount)
        {
            latestDate = maturity;
            this.seen = seen;
        }

        public override double ImpliedQuote(EvaluationContext context)
        {
            RequireTermStructure();
            var curve = (PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>)TermStructure!;
            seen.Add(new Seen(curve.Dates(), curve.Times(), curve.Data(), curve.Nodes()));
            return curve.Discount(latestDate!.Value);
        }
    }
}
