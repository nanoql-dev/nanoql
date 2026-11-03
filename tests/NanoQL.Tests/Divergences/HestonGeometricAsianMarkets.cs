// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The markets and the tolerance of the Heston geometric Asian facts: flat curves on Actual/365 Fixed (r 5%, q 0 unless
/// given), the fixing and exercise dates as days from the evaluation date, the strike given; and the tolerance at the
/// engine's own scale, 1e-10 × max(the price, the lower bound D·max(±(F_G − K), 0), D·F_G/100). The references:
/// QuantLib's <c>AnalyticHestonEngine</c> at 1e-15 at one fixing, QUADPACK on the route's contour and on a second, and
/// QuantLib's formula in 40 digits. A market whose fixings fall within a day is restated on days by the model's time
/// change: over c times the time, r, q, v₀, κ, θ and the vol of vol each over c and ρ kept, which leaves the average's
/// law and the discount unchanged.
/// </summary>
internal static class HestonGeometricAsianMarkets
{
    // the references' evaluation date, and the short-dated market's
    internal static readonly Date Today = new(16, Month.September, 2015);
    internal static readonly Date P9Today = new(1, Month.January, 2026);
    internal static readonly DayCounter A365 = new Actual365Fixed();

    internal static HestonProcess Market(
        Date today, double spot, double v0, double kappa, double theta, double sigma, double rho, double r = 0.05, double q = 0.0) =>
        new(new FlatForward(today, r, A365), new FlatForward(today, q, A365), spot, v0, kappa, theta, sigma, rho);

    internal static DiscreteAveragingAsianOption Option(Date today, OptionType type, double strike, int[] fixingDays, int exerciseDays) =>
        new(AverageType.Geometric, 1.0, 0, [.. fixingDays.Select(d => today + d)], new PlainVanillaPayoff(type, strike), new EuropeanExercise(today + exerciseDays));

    internal static double Price(
        Date today, HestonProcess process, OptionType type, double strike, int[] fixingDays, int exerciseDays, double xiRightLimit = 100.0) =>
        AnalyticDiscreteGeometricAveragePriceAsianHestonEngine.Calculate(
            Option(today, type, strike, fixingDays, exerciseDays), new EvaluationContext { EvaluationDate = today }, process, xiRightLimit).NPV!.Value;

    // The tolerance at the engine's scale: the discount D and the average's forward F_G of the contract
    internal static void AssertR7(double expected, double actual, OptionType type, double strike, double discount, double forward, double r7s = 1.0)
    {
        double lower = discount * Math.Max((double)type * (forward - strike), 0.0);
        double scale = Math.Max(Math.Max(Math.Abs(expected), Math.Abs(actual)), Math.Max(lower, discount * forward / 100.0));
        Assert.True(Math.Abs(actual - expected) <= r7s * 1e-10 * scale, $"{actual:R} against {expected:R}: {Math.Abs(actual - expected) / (1e-10 * scale):G3} R7");
    }

    internal static int[] Days(int count, int step) => [.. Enumerable.Range(1, count).Select(i => step * i)];
}
