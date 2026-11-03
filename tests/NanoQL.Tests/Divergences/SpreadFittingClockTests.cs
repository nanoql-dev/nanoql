// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The spread fitting method's discounting curve on another clock. QuantLib's discount function reads the discounting
/// curve at the fitted curve's time (<c>nonlinearfittingmethods.cpp:414-416</c>), which the discounting curve counts
/// from its own reference date on its own day counter, rebased to the fitted curve's reference date. On the wheel, a
/// curve fitted on 15 January 2025 over Nelson-Siegel parameters 0, 0, 0 and 0.5, whose factor is exactly 1, discounts
/// 0.9810709211266746 at a year over a zero curve dated 30 days earlier, where QuantLib's own fit over that curve
/// implied at the fitted curve's date gives 0.9790412094890624; over a flat 3% Actual/360 curve, 0.9704455335485082
/// where the curve restated on Actual/365 (Fixed) gives 0.9700412654712278. nanoql reads the discounting curve at the
/// instant the fitted curve's time denotes. QuantLib's check: its own fitted curve over the discounting curve restated
/// on the fitted curve's reference date and day counter.
/// </summary>
public class SpreadFittingClockTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void ADiscountingCurveDatedEarlierIsReadAtTheInstant()
    {
        YieldTermStructure disc = new ZeroCurve(
            [Today - 30, Today + 335, Today + (3 * 365), Today + (10 * 365)], [0.01, 0.02, 0.03, 0.035], A365);

        // the discounting curve restated on the fitted curve's date: QuantLib's own implied curve of it
        YieldTermStructure restated = new ImpliedTermStructure(disc, Today);
        FittedBondDiscountCurve fitted = Fitted(disc), onRestated = Fitted(restated);

        (Date Date, double QuantLibRestated, double QuantLib)[] wheel =
        [
            (Today + 365, 0.9790412094890624, 0.9810709211266746),
            (new Date(14, Month.January, 2030), 0.8531357886085791, 0.8555935482886413),
            (new Date(13, Month.January, 2035), 0.7032890961466196, 0.70572937030719),
        ];
        foreach ((Date d, double quantLibRestated, double quantLib) in wheel)
        {
            Assert.True(Math.Abs((fitted.Discount(d) / onRestated.Discount(d)) - 1.0) <= 1e-14, $"{d}");
            Assert.True(Math.Abs((fitted.Discount(d) / quantLibRestated) - 1.0) <= 1e-14, $"{d}: {fitted.Discount(d)}");
            Assert.True(Math.Abs(fitted.Discount(d) - quantLib) > 1e-3, $"{d}: {fitted.Discount(d)}");
        }
    }

    [Fact]
    public void ADiscountingCurveOnAnotherDayCounterIsReadAtTheInstant()
    {
        YieldTermStructure disc = new FlatForward(Today, 0.03, new Actual360());
        YieldTermStructure restated = new FlatForward(Today, 0.03 * 365 / 360, A365);
        FittedBondDiscountCurve fitted = Fitted(disc), onRestated = Fitted(restated);

        (Date Date, double QuantLibRestated, double QuantLib)[] wheel =
        [
            (Today + 365, 0.9700412654712278, 0.9704455335485082),
            (new Date(14, Month.January, 2030), 0.8589167013669105, 0.8607079764250578),
            (new Date(13, Month.January, 2035), 0.7377378998870144, 0.7408182206817179),
        ];
        foreach ((Date d, double quantLibRestated, double quantLib) in wheel)
        {
            Assert.True(Math.Abs((fitted.Discount(d) / onRestated.Discount(d)) - 1.0) <= 1e-14, $"{d}");
            Assert.True(Math.Abs((fitted.Discount(d) / quantLibRestated) - 1.0) <= 1e-14, $"{d}: {fitted.Discount(d)}");
            Assert.True(Math.Abs(fitted.Discount(d) - quantLib) > 4e-4, $"{d}: {fitted.Discount(d)}");
        }
    }

    // A fitted curve over the discounting curve, evaluating Nelson-Siegel parameters whose factor is exactly 1.
    private static FittedBondDiscountCurve Fitted(YieldTermStructure disc) =>
        new(Today, new SpreadFittingMethod(new NelsonSiegelFitting(), disc), [0.0, 0.0, 0.0, 0.5], Today + (20 * 365), A365);
}
