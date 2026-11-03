// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Two derived curves drop the extrapolation flag. The spread discount curve reads its base without it
/// (<c>spreaddiscountcurve.hpp:157</c>), and so does the ultimate-forward curve at its cut-off and before it
/// (<c>ultimateforwardtermstructure.hpp:168</c>, <c>:173</c>), where the zero- and forward-spreaded, implied and
/// composite curves pass it. On the wheel, asked for the discount at 11 years with extrapolation over a 2%–3% zero
/// curve to 10 years from 15 January 2025 that doesn't extrapolate, the four siblings give 0.640328216626791 (both
/// spreaded at 1%), 0.714823513455133 and 1.0, while the spread discount and ultimate-forward curves, and the piecewise
/// spread curve bootstrapped over that base, throw "time (11.0055) is past max curve time (10.0055)". nanoql reads the
/// base with extrapolation on, as the siblings do. QuantLib's check: the siblings' own reads, and the base's own
/// extrapolated values.
/// </summary>
public class DerivedCurveExtrapolationTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly Date[] Nodes = [Today, Today + new Period(1, TimeUnit.Years), Today + new Period(10, TimeUnit.Years)];
    private static readonly YieldTermStructure Base = new ZeroCurve(Nodes, [0.02, 0.025, 0.03], A365);
    private static readonly double T11 = A365.YearFraction(Today, Today + new Period(11, TimeUnit.Years));

    [Fact]
    public void TheSpreadDiscountAndUltimateForwardCurvesExtrapolateTheirBase()
    {
        // the four siblings, QuantLib's on the wheel
        Assert.True(Math.Abs((new ZeroSpreadedTermStructure(Base, 0.01).Discount(T11, true) / 0.640328216626791) - 1.0) <= 1e-14);
        Assert.True(Math.Abs((new ForwardSpreadedTermStructure(Base, 0.01).Discount(T11, true) / 0.640328216626791) - 1.0) <= 1e-14);
        Assert.True(Math.Abs((new ImpliedTermStructure(Base, Today).Discount(T11, true) / 0.714823513455133) - 1.0) <= 1e-14);
        Assert.Equal(1.0, new CompositeZeroYieldStructure(Base, Base, (a, b) => a - b).Discount(T11, true), 15);

        // the spread discount curve: the base's extrapolated discount times the spread nodes' own extrapolation
        double[] spreads = [1.0, 0.99, 0.95];
        var spreadCurve = new SpreadDiscountCurve(Base, Nodes, spreads);
        double spread = new DiscountCurve(Nodes, spreads, A365).EnableExtrapolation().Discount(T11);
        Assert.True(
            Math.Abs((spreadCurve.Discount(T11, true) / (Base.Discount(T11, true) * spread)) - 1.0) <= 1e-15,
            $"{spreadCurve.Discount(T11, true)}");

        // the ultimate-forward curve before its cut-off: the base's own extrapolated discount
        var ufr = new UltimateForwardTermStructure(Base, 0.03, 0.04, new Period(20, TimeUnit.Years), 0.1);
        Assert.True(
            Math.Abs((ufr.Discount(T11, true) / Base.Discount(T11, true)) - 1.0) <= 1e-14, $"{ufr.Discount(T11, true)}");

        // and past it, from the base's extrapolated zero rate at the cut-off
        double cutOff = Base.TimeFromReference(Today + new Period(20, TimeUnit.Years));
        double t = Base.TimeFromReference(Today + new Period(30, TimeUnit.Years));
        double baseRate = Base.ZeroRate(cutOff, Compounding.Continuous, Frequency.NoFrequency, true).Rate;
        double beta = (1.0 - Math.Exp(-0.1 * (t - cutOff))) / (0.1 * (t - cutOff));
        double expected = ((cutOff * baseRate) + ((t - cutOff) * (0.04 + ((0.03 - 0.04) * beta)))) / t;
        double zero = ufr.ZeroRate(t, Compounding.Continuous, Frequency.NoFrequency, true).Rate;
        Assert.True(Math.Abs(zero - expected) <= 1e-14, $"{zero} against {expected}");
    }

    [Fact]
    public void ThePiecewiseSpreadCurveExtrapolatesItsBase()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        var index = new Euribor6M();
        var helpers = new List<RateHelper>
        {
            new DepositRateHelper(
                0.03, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
                new Actual360(), context),
        };
        foreach ((int years, double rate) in new[] { (2, 0.031), (5, 0.034) })
        {
            helpers.Add(new SwapRateHelper(
                rate, new Period(years, TimeUnit.Years), new TARGET(), Frequency.Annual, BusinessDayConvention.Unadjusted,
                new Thirty360(Thirty360Convention.BondBasis), index, context));
        }

        PiecewiseSpreadYieldCurve<LogLinear, IterativeBootstrap> curve =
            new PiecewiseSpreadYieldCurve<LogLinear, IterativeBootstrap>(Base, helpers, context).EnableExtrapolation();

        // QuantLib's nodes and discount within its range on the wheel
        double[] wheel = [1.0, 0.996169215217395, 0.9901827984137074, 0.9689498882421604];
        for (int i = 0; i < wheel.Length; ++i)
        {
            Assert.True(Math.Abs((curve.Data()[i] / wheel[i]) - 1.0) <= 1e-12, $"node {i}: {curve.Data()[i]}");
        }

        Assert.True(Math.Abs((curve.Discount(4.0) / 0.8772879133501692) - 1.0) <= 1e-12, $"{curve.Discount(4.0)}");

        // past the base's range: the base's extrapolated discount times the spread nodes' own extrapolation
        double spread = new DiscountCurve(curve.Dates(), curve.Data(), A365).EnableExtrapolation().Discount(T11);
        Assert.True(
            Math.Abs((curve.Discount(T11, true) / (Base.Discount(T11, true) * spread)) - 1.0) <= 1e-15,
            $"{curve.Discount(T11, true)}");
    }
}
