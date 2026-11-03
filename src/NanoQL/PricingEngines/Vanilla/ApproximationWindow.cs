// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The American approximations' market reads and their price of an exercise window opening after the reference date.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's Barone-Adesi-Whaley, Bjerksund-Stensland, Ju, QD+ and QD fixed-point engines read only the exercise's last
/// date, so a window opening later prices as exercisable now. Here the option, which can't be exercised before the
/// window opens at t₁, is worth the discount to t₁ times the expectation, over the lognormal density of S(t₁), of the
/// engine's own American on [t₁, T] priced at t₁ on the market's forward discounts and variance over [t₁, T]: the
/// trapezoid rule over z ∈ [−8, 8] with 241 nodes. The Greeks the engine reports are the same integral's: the delta
/// pathwise in S(t₁), which is proportional to the spot; the gamma from the nodes' deltas through the density of S(t₁),
/// E[Δ(S₁)·S₁·(z/√v₁ − 1)]/S², since an approximation's delta can jump at its exercise boundary (Ju's does), where a
/// pathwise gamma would miss the jump; the rho, dividend rho and vega through S(t₁) and the node's own; the theta by the
/// Black-Scholes equation the option satisfies before t₁; the strike sensitivity by the value's homogeneity in the spot
/// and strike. A node's refusal refuses the window, raised as the node raised it.
/// </para>
/// <para>
/// Each structure is read at a date by date, nothing accrued before its own reference date (the mismatched-dates class),
/// and the window opens after the engines' clock's reference date, the earliest structure's
/// (<see cref="FdmBlackScholesOp.Clock"/>).
/// </para>
/// </remarks>
internal static class ApproximationWindow
{
    private const int Nodes = 241;
    private const double ZMax = 8.0;

    /// <summary>The market at a date: the spot, each structure's discount or variance and own time to the date, and the
    /// clock's time to it.</summary>
    /// <param name="Spot">The spot.</param>
    /// <param name="RiskFreeDiscount">The risk-free discount.</param>
    /// <param name="DividendDiscount">The dividend discount.</param>
    /// <param name="Variance">The Black variance.</param>
    /// <param name="RiskFreeTime">The risk-free curve's time.</param>
    /// <param name="DividendTime">The dividend curve's time.</param>
    /// <param name="VolatilityTime">The volatility's time.</param>
    /// <param name="Time">The clock's time.</param>
    /// <param name="RiskFreeVolatilityTime">The risk-free curve's time on the volatility's day counter, a theta's.</param>
    /// <param name="DividendVolatilityTime">The dividend curve's time on the volatility's day counter.</param>
    internal readonly record struct Market(
        double Spot,
        double RiskFreeDiscount,
        double DividendDiscount,
        double Variance,
        double RiskFreeTime,
        double DividendTime,
        double VolatilityTime,
        double Time,
        double RiskFreeVolatilityTime,
        double DividendVolatilityTime);

    /// <summary>The market at a date, each structure read by date, nothing accrued before its reference date.</summary>
    /// <param name="process">The process.</param>
    /// <param name="clock">The engines' clock.</param>
    /// <param name="date">The date.</param>
    /// <param name="strike">The strike the variance is read at.</param>
    /// <returns>The market.</returns>
    internal static Market At(GeneralizedBlackScholesProcess process, TermStructure clock, Date date, double strike) =>
        new(
            process.StateVariable,
            process.RiskFreeRate.AccruedDiscount(date),
            process.DividendYield.AccruedDiscount(date),
            process.BlackVolatility.AccruedVariance(date, strike),
            process.RiskFreeRate.AccrualTimeTo(date),
            process.DividendYield.AccrualTimeTo(date),
            process.BlackVolatility.AccrualTimeTo(date),
            clock.AccrualTimeTo(date),
            process.RiskFreeRate.AccrualTimeTo(date, process.BlackVolatility.DayCounter),
            process.DividendYield.AccrualTimeTo(date, process.BlackVolatility.DayCounter));

    /// <summary>The window's opening date, if it opens after the clock's reference date.</summary>
    /// <param name="exercise">The exercise.</param>
    /// <param name="clock">The engines' clock.</param>
    /// <returns>The opening, or <see langword="null"/> for an option exercisable from the reference date.</returns>
    internal static Date? Opening(AmericanExercise exercise, TermStructure clock) =>
        exercise.Dates[0] > clock.ReferenceDate ? exercise.Dates[0] : null;

    /// <summary>
    /// The window's price from the engine's price of the American on [t₁, T] at t₁, over the market to the opening and
    /// the market to expiry.
    /// </summary>
    /// <param name="toOpening">The market to the opening, its spot today's.</param>
    /// <param name="toExpiry">The market to expiry.</param>
    /// <param name="strike">The strike, for the strike sensitivity.</param>
    /// <param name="node">The engine's price on the forward market over [t₁, T], at a spot.</param>
    /// <returns>The results the node reports, the window's.</returns>
    internal static OneAssetOptionResults Price(
        Market toOpening, Market toExpiry, double strike, Func<Market, OneAssetOptionResults> node)
    {
        double spot = toOpening.Spot;
        double d1 = toOpening.RiskFreeDiscount;
        double v1 = toOpening.Variance;
        double sd1 = Math.Sqrt(v1);
        double forward1 = spot * toOpening.DividendDiscount / d1;
        var forwardMarket = new Market(
            spot,
            toExpiry.RiskFreeDiscount / toOpening.RiskFreeDiscount,
            toExpiry.DividendDiscount / toOpening.DividendDiscount,
            toExpiry.Variance - toOpening.Variance,
            toExpiry.RiskFreeTime - toOpening.RiskFreeTime,
            toExpiry.DividendTime - toOpening.DividendTime,
            toExpiry.VolatilityTime - toOpening.VolatilityTime,
            toExpiry.Time - toOpening.Time,
            toExpiry.RiskFreeVolatilityTime - toOpening.RiskFreeVolatilityTime,
            toExpiry.DividendVolatilityTime - toOpening.DividendVolatilityTime);

        // the volatility over [0, t₁] per its own time, for the vega's path term and the theta
        double t1v = toOpening.VolatilityTime;
        double sigma1 = t1v > 0.0 ? Math.Sqrt(v1 / t1v) : 0.0;

        double h = 2.0 * ZMax / (Nodes - 1);
        double value = 0.0, delta = 0.0, gamma = 0.0, deltaGamma = 0.0, rho = 0.0, dividendRho = 0.0, vega = 0.0;
        bool hasDelta = true, hasGamma = true, hasRho = true, hasDividendRho = true, hasVega = true;
        bool hasTheta = true, hasStrikeSensitivity = true, hasStrikeGamma = true;
        object? exerciseType = null;
        for (int j = 0; j < Nodes; ++j)
        {
            double z = -ZMax + (j * h);
            double w = (j == 0 || j == Nodes - 1 ? 0.5 * h : h) * Math.Exp(-0.5 * z * z) / Math.Sqrt(2.0 * Math.PI);
            double s1 = forward1 * Math.Exp((-0.5 * v1) + (sd1 * z));
            OneAssetOptionResults r = node(forwardMarket with { Spot = s1 });

            value += w * r.NPV!.Value;
            if (r.Delta is double dn)
            {
                delta += w * dn * s1 / spot;

                // d/dS of the delta's integral through the density: S(t₁) at fixed z moves as S, and z's density with S
                deltaGamma += sd1 > 0.0 ? w * dn * s1 * ((z / sd1) - 1.0) : 0.0;

                // through S(t₁): its log moves by t₁ with the rate, by −t₁ with the dividend yield, and by
                // √t₁·z − σt₁ with the volatility
                rho += w * dn * s1 * toOpening.RiskFreeTime;
                dividendRho -= w * dn * s1 * toOpening.DividendTime;
                vega += w * dn * s1 * ((Math.Sqrt(t1v) * z) - (sigma1 * t1v));
            }
            else
            {
                hasDelta = false;
            }

            if (r.Gamma is double gn)
            {
                gamma += w * gn * s1 * s1 / (spot * spot);
            }
            else
            {
                hasGamma = false;
            }

            if (r.Rho is double rn)
            {
                rho += w * rn;
            }
            else
            {
                hasRho = false;
            }

            if (r.DividendRho is double qn)
            {
                dividendRho += w * qn;
            }
            else
            {
                hasDividendRho = false;
            }

            if (r.Vega is double vn)
            {
                vega += w * vn;
            }
            else
            {
                hasVega = false;
            }

            hasTheta &= r.Theta is not null;
            hasStrikeSensitivity &= r.StrikeSensitivity is not null;
            hasStrikeGamma &= r.AdditionalResults.ContainsKey("strikeGamma");
            if (r.AdditionalResults.TryGetValue("exerciseType", out object? type))
            {
                exerciseType = type;
            }
        }

        double windowValue = d1 * value;
        double? windowDelta = hasDelta ? d1 * delta : null;
        // with no variance to t₁, S(t₁) is the forward and the gamma pathwise
        double? windowGamma = hasGamma && hasDelta ? d1 * (sd1 > 0.0 ? deltaGamma / (spot * spot) : gamma) : null;

        // the discount to t₁ moves with the rate over its time
        double? windowRho = hasDelta && hasRho ? (d1 * rho) - (toOpening.RiskFreeTime * windowValue) : null;
        double? windowDividendRho = hasDelta && hasDividendRho ? d1 * dividendRho : null;
        double? windowVega = hasDelta && hasVega ? d1 * vega : null;

        // the Black-Scholes equation before t₁, at the rates and volatility over [0, t₁]
        double? theta = null;
        if (hasTheta && windowDelta is double wd && windowGamma is double wg)
        {
            double r1 = toOpening.RiskFreeTime > 0.0 ? -Math.Log(d1) / toOpening.RiskFreeTime : 0.0;
            double q1 = toOpening.DividendTime > 0.0 ? -Math.Log(toOpening.DividendDiscount) / toOpening.DividendTime : 0.0;
            theta = (r1 * windowValue) - ((r1 - q1) * spot * wd) - (0.5 * sigma1 * sigma1 * spot * spot * wg);
        }

        return new OneAssetOptionResults
        {
            NPV = windowValue,
            Delta = windowDelta,
            Gamma = windowGamma,
            Rho = windowRho,
            DividendRho = windowDividendRho,
            Vega = windowVega,
            Theta = theta,
            ThetaPerDay = theta / 365.0,
            StrikeSensitivity = hasStrikeSensitivity && windowDelta is double sd ? (windowValue - (spot * sd)) / strike : null,
            AdditionalResults = Additional(hasStrikeGamma ? windowGamma * (spot / strike) * (spot / strike) : null, exerciseType),
        };
    }

    private static ImmutableSortedDictionary<string, object> Additional(double? strikeGamma, object? exerciseType)
    {
        ImmutableSortedDictionary<string, object> additional = ImmutableSortedDictionary<string, object>.Empty;
        if (strikeGamma is double g)
        {
            additional = additional.Add("strikeGamma", g);
        }

        // the window's option is an American one, whatever its nodes' types
        return exerciseType is null ? additional : additional.Add("exerciseType", "American");
    }
}
