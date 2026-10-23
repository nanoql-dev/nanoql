// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The composite curve's second curve on another clock (the S-sr plan's SSR6 (xv)). QuantLib reads the second curve's
/// zero rate at the composite's time (<c>compositezeroyieldstructure.hpp:136-137</c>), a time on the first curve's
/// clock, which the second curve counts from its own reference date on its own day counter; QuantLib's case dates both
/// curves 10 November 2017 on Actual/365 (Fixed) (<c>termstructures.cpp:542</c>). On the wheel, the difference of a
/// 4%–5.5% zero curve and a 1%–3.5% one, both from 15 January 2025, gives the zero rate 0.025136986301369922 at a year
/// with the second on Actual/360, where QuantLib's own composite over the second restated on Actual/365 (Fixed) gives
/// 0.024722222222222062; with the second dated 30 days earlier, 0.020201342281879166 at five years, where the second
/// implied at 15 January 2025 gives 0.01968474385438166. nanoql reads the second curve at the instant the composite's
/// time denotes, its zero rate the one its discount there implies over that time. QuantLib's check: its own composite
/// with the second curve restated on the first's reference date and day counter, at the restated curve's nodes.
/// </summary>
public class CompositeClockTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly Date[] Nodes = [Today, Today + 365, Today + (5 * 365), Today + (30 * 365)];
    private static readonly YieldTermStructure Curve1 = new ZeroCurve(Nodes, [0.04, 0.045, 0.05, 0.055], A365);

    [Fact]
    public void ASecondCurveOnAnotherDayCounterIsReadAtTheInstant()
    {
        YieldTermStructure curve2 = new ZeroCurve(Nodes, [0.01, 0.02, 0.03, 0.035], new Actual360());

        // the second curve restated on Actual/365 (Fixed): its discounts at its nodes
        YieldTermStructure restated = new DiscountCurve(Nodes, [.. Nodes.Select(d => curve2.Discount(d))], A365);
        Check(
            curve2,
            restated,
            [
                (Today + 365, 0.024722222222222062, 0.025136986301369922),
                (Today + (5 * 365), 0.019583333333333355, 0.020171232876712304),
                (Today + (30 * 365), 0.019513888888888886, 0.020082191780821903),
            ]);
    }

    [Fact]
    public void ASecondCurveDatedEarlierIsReadAtTheInstant()
    {
        YieldTermStructure curve2 = new ZeroCurve(
            [Today - 30, Today + 335, Today + (5 * 365), Today + (30 * 365)], [0.01, 0.02, 0.03, 0.035], A365);

        // the second curve restated on the first's date: QuantLib's own implied curve of it
        YieldTermStructure restated = new ImpliedTermStructure(curve2, Today);
        Check(
            curve2,
            restated,
            [
                (Today + (5 * 365), 0.01968474385438166, 0.020201342281879166),
                (Today + (30 * 365), 0.019933758678926626, 0.02001643835616438),
            ]);
    }

    private static void Check(
        YieldTermStructure curve2, YieldTermStructure restated, (Date Date, double QuantLibRestated, double QuantLib)[] wheel)
    {
        var composite = new CompositeZeroYieldStructure(Curve1, curve2, (a, b) => a - b);
        var onRestated = new CompositeZeroYieldStructure(Curve1, restated, (a, b) => a - b);
        foreach ((Date d, double quantLibRestated, double quantLib) in wheel)
        {
            double zero = composite.ZeroRate(d, A365, Compounding.Continuous).Rate;
            Assert.True(Math.Abs(zero - onRestated.ZeroRate(d, A365, Compounding.Continuous).Rate) <= 1e-14, $"{d}: {zero}");
            Assert.True(Math.Abs(zero - quantLibRestated) <= 1e-14, $"{d}: {zero} against {quantLibRestated}");
            Assert.True(Math.Abs(zero - quantLib) > 8e-5, $"{d}: {zero}");
        }
    }
}
