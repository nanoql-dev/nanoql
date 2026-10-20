// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A piecewise default curve dated a number of business days after the evaluation date
/// (<see cref="PiecewiseDefaultCurve{TTraits, TInterpolator, TBootstrap}.Create"/>, QuantLib's constructor taking
/// settlement days and a calendar), its nodes, and its default density, which on a hazard-rate curve is the hazard rate
/// times the survival probability: on <see cref="CreditFixture"/>'s market, three spread helpers (1, 3 and 5 years at
/// 0.5%, 0.75% and 1%) and one settlement day on <see cref="WeekendsOnly"/>, as QuantLib's
/// <c>PiecewiseFlatHazardRate</c> gives them on the wheel.
/// </summary>
public class PiecewiseDefaultCurveTests
{
    [Fact]
    public void ACurveAfterSettlementDaysIsQuantLibs()
    {
        EvaluationContext context = CreditFixture.Context();
        SpreadCdsHelper[] helpers = [Helper(1, 0.005, context), Helper(3, 0.0075, context), Helper(5, 0.01, context)];

        var curve = PiecewiseDefaultCurve<HazardRate, BackwardFlat, IterativeBootstrap>.Create(
            1, new WeekendsOnly(), helpers, new Actual365Fixed(), context);

        // QuantLib's, on the wheel
        Assert.Equal(new Date(16, Month.January, 2024), curve.ReferenceDate);
        (Date Date, double Rate)[] nodes =
        [
            (new Date(16, Month.January, 2024), 0.00841592285708698),
            (new Date(20, Month.March, 2025), 0.00841592285708698),
            (new Date(22, Month.March, 2027), 0.01527132633100285),
            (new Date(20, Month.March, 2029), 0.02442272966885029),
        ];
        Assert.Equal(nodes.Select(n => n.Date), curve.Nodes().Select(n => n.Key));
        foreach (((Date _, double rate), KeyValuePair<Date, double> node) in nodes.Zip(curve.Nodes()))
        {
            Assert.True(Tolerance.Within(rate, node.Value), $"{node.Key}: {node.Value}");
        }

        (Date Date, double Density)[] densities =
        [
            (new Date(15, Month.July, 2024), 0.008380873239772818),
            (new Date(15, Month.January, 2026), 0.014931779115227863),
            (new Date(15, Month.January, 2028), 0.022988400323515967),
            (new Date(15, Month.January, 2031), 0.02136287610963873),
        ];
        foreach ((Date d, double expected) in densities)
        {
            double density = curve.DefaultDensity(d, true);
            Assert.True(Tolerance.Within(expected, density), $"{d}: {density}");
        }
    }

    private static SpreadCdsHelper Helper(int years, double spread, EvaluationContext context) =>
        new(
            spread,
            new Period(years, TimeUnit.Years),
            0,
            new WeekendsOnly(),
            Frequency.Quarterly,
            BusinessDayConvention.Following,
            DateGenerationRule.CDS,
            new Actual360(),
            0.4,
            CreditFixture.Discount(),
            context);
}
