// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2023 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/bjerksundstenslandengine.hpp ql/pricingengines/vanilla/bjerksundstenslandengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Bjerksund and Stensland approximation engine for American options (1993), puts by the put-call symmetry.
/// </summary>
/// <remarks>
/// <para>
/// The correctness of the returned value is tested by reproducing results available in literature. The engine reports
/// the value, the delta, gamma, theta (and per day), vega, rho, dividend rho and strike sensitivity, and the additional
/// results "strikeGamma" and "exerciseType" (European, American or Immediate); no forward delta, elasticity or
/// in-the-money probability, which QuantLib's engine leaves uninitialized (its results are built in a local it never
/// resets; <c>Divergences/BjerksundStenslandUnsetGreeksTests.cs</c>). The Greeks call the C runtime's <c>erfc</c>,
/// which nanoql reads through Boost's (the S-van plan's VR6), so their last bits can differ from QuantLib's.
/// </para>
/// <para>
/// Documented divergences, each leaving QuantLib's arithmetic where it doesn't apply:
/// </para>
/// <list type="bullet">
/// <item><description>Below a variance of ε the value is the European one floored at the exercise value, as QuantLib's
/// master does since the pin (#2854; VR5 (ii)).</description></item>
/// <item><description>Where the trigger price falls below its lower bound B₀, or isn't finite
/// (<c>bjerksundstenslandengine.cpp:196-206</c>, the trigger's exponent positive), the trigger is the perpetual boundary
/// B∞, in the same closed form and Greeks, its derivatives B∞'s: a flat boundary at or above B₀ is a feasible exercise
/// rule, whose value bounds the option's from below (Bjerksund and Stensland 2002, §3). QuantLib prices there with an
/// infeasible trigger and returns its European floor, up to 3.4 below its own finite-difference engine. Where the
/// perpetual boundary is infinite the European value stands (VR9, the user's ruling of 2026-10-24;
/// <c>Divergences/BjerksundStenslandTriggerTests.cs</c>). The value jumps at the region's edge, between QuantLib's
/// trigger and B∞, two of the method's lower bounds. QuantLib's shortcut to the European value where the trigger lies
/// 12.5 standard deviations above the forward (<c>:212-216</c>) isn't taken at B∞: in the region the forward falls far
/// below the spot while B∞ stays just above it, so the path reaches B∞ early or not at all.</description></item>
/// <item><description>Where a power (I/S)^κ overflows against an underflowing normal tail, QuantLib's product is NaN or
/// 0 (<c>:37-112</c>); there the product is taken through logarithms, log N's asymptotic tail beyond −30, as one
/// exponent with the Gaussian densities it divides; QuantLib's arithmetic elsewhere (VR4 (xxxiii),
/// <c>Divergences/BjerksundStenslandOverflowTests.cs</c>).</description></item>
/// <item><description>The American Greeks' times run from each structure's own reference date, where QuantLib's run from
/// the risk-free curve's (VR4 (viii), <c>Divergences/BjerksundStenslandMismatchedTimesTests.cs</c>); each structure is read
/// at the exercise date by date, nothing accrued before its own reference date; and an exercise window opening after
/// the reference date is priced by <see cref="ApproximationWindow"/> (VR4 (xxvi)).</description></item>
/// </list>
/// </remarks>
public static class BjerksundStenslandApproximationEngine
{
    private static readonly CumulativeNormalDistribution CumNormalDist = new();

    /// <summary>Prices an American vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't American or pays at expiry, the payoff isn't plain, the underlying isn't positive, or the
    /// double-boundary case r &lt; q &lt; 0 is given for a call.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        if (!(option.Exercise.Type == ExerciseType.American))
        {
            throw new NanoQLException("not an American Option");
        }

        if (option.Exercise is not AmericanExercise ex)
        {
            throw new NanoQLException("non-American exercise given");
        }

        if (ex.PayoffAtExpiry)
        {
            throw new NanoQLException("payoff at expiry not handled");
        }

        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        // each structure read by date, nothing accrued before its reference date (the mismatched-dates class); the
        // theta's day fractions from each structure's own reference date (VR4 (viii))
        TermStructure clock = FdmBlackScholesOp.Clock(process);
        Date lastDate = ex.LastDate();
        ApproximationWindow.Market market = ApproximationWindow.At(process, clock, lastDate, payoff.Strike);
        if (!(market.Spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        var days = new DayFractions(
            DayFraction(process.RiskFreeRate, lastDate),
            DayFraction(process.DividendYield, lastDate),
            DayFraction(process.BlackVolatility, lastDate));

        OneAssetOptionResults results = ApproximationWindow.Opening(ex, clock) is Date opening
            && !(market.DividendDiscount >= 1.0 && market.DividendDiscount >= market.RiskFreeDiscount && payoff.OptionType == OptionType.Call)
            && !(market.RiskFreeDiscount >= 1.0 && market.RiskFreeDiscount >= market.DividendDiscount && payoff.OptionType == OptionType.Put)
            ? ApproximationWindow.Price(
                ApproximationWindow.At(process, clock, opening, payoff.Strike),
                market,
                payoff.Strike,
                m => Core(payoff, m, days))
            : Core(payoff, market, days);
        return option.FetchResults(results);
    }

    // QuantLib's calculate on a market: the put by the put-call symmetry, the European call where early exercise is
    // never optimal, the American approximation otherwise, floored at immediate exercise.
    private static OneAssetOptionResults Core(PlainVanillaPayoff payoff, ApproximationWindow.Market market, DayFractions days)
    {
        double variance = market.Variance;
        double dividendDiscount = market.DividendDiscount;
        double riskFreeDiscount = market.RiskFreeDiscount;
        double spot = market.Spot;
        double strike = payoff.Strike;

        // the call's times: each curve's own, swapped with the curves for a put
        var times = new Times(
            market.RiskFreeTime,
            market.DividendTime,
            market.VolatilityTime,
            market.RiskFreeVolatilityTime,
            market.DividendVolatilityTime,
            days);
        bool put = payoff.OptionType == OptionType.Put;
        if (put)
        {
            // use put-call symmetry
            (spot, strike) = (strike, spot);
            (riskFreeDiscount, dividendDiscount) = (dividendDiscount, riskFreeDiscount);
        }

        if (dividendDiscount > 1.0 && riskFreeDiscount > dividendDiscount)
        {
            throw new NanoQLException("double-boundary case r<q<0 for a call given");
        }

        OneAssetOptionResults results;
        if (variance < NumericLimits.QL_EPSILON || (dividendDiscount >= 1.0 && dividendDiscount >= riskFreeDiscount))
        {
            // with no variance the approximation divides by zero; the European
            // value is floored at intrinsic below, as in Barone-Adesi-Whaley
            // (QuantLib's master, #2854)
            results = EuropeanCallResults(spot, strike, riskFreeDiscount, dividendDiscount, variance, times, put);
        }
        else
        {
            // early exercise can be optimal - use approximation
            results = AmericanCallApproximation(spot, strike, riskFreeDiscount, dividendDiscount, variance, times, put);
        }

        // check if immediate exercise gives higher NPV
        if (results.NPV!.Value < (spot - strike) * (1 + (10 * NumericLimits.QL_EPSILON)))
        {
            results = ImmediateExercise(spot, strike);
        }

        if (put)
        {
            object tmp = results.Gamma!.Value;
            results = results with
            {
                Delta = results.StrikeSensitivity,
                StrikeSensitivity = results.Delta,
                Gamma = (double)results.AdditionalResults["strikeGamma"],
                AdditionalResults = results.AdditionalResults.SetItem("strikeGamma", tmp),
                Rho = results.DividendRho * (market.RiskFreeTime / market.DividendTime),
                DividendRho = results.Rho * (market.DividendTime / market.RiskFreeTime),
            };
        }

        return results;
    }

    private static OneAssetOptionResults EuropeanCallResults(
        double s, double x, double rfD, double dD, double variance, Times times, bool put)
    {
        double forwardPrice = s * dD / rfD;
        var black = new BlackCalculator(OptionType.Call, x, forwardPrice, Math.Sqrt(variance), rfD);

        double value = black.Value;
        double delta = black.Delta(s);
        double gamma = black.Gamma(s);

        // each curve's own time, rescaled with the swap for a put, as QuantLib's
        double rho = black.Rho(times.RiskFree);
        double dividendRho = black.DividendRho(times.Dividend);

        double t = times.Volatility;
        double vega = black.Vega(t);

        // the theta's terms over each structure's own time on the volatility's day counter, the call's risk-free and
        // dividend curves the put's dividend and risk-free curves; QuantLib's one time where they agree (the class of
        // S-eq's analytic European engine)
        double rfTime = put ? times.DividendOnVolatility : times.RiskFreeOnVolatility;
        double dTime = put ? times.RiskFreeOnVolatility : times.DividendOnVolatility;
        double theta = black.Theta(s, rfTime, rfD, rfTime, dD, dTime, t);

        return new OneAssetOptionResults
        {
            NPV = value,
            Delta = delta,
            Gamma = gamma,
            Rho = rho,
            DividendRho = dividendRho,
            Vega = vega,
            Theta = theta,
            ThetaPerDay = theta / 365.0,
            StrikeSensitivity = black.StrikeSensitivity,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty
                .Add("strikeGamma", gamma * Squared(s / x))
                .Add("exerciseType", "European"),
        };
    }

    private static OneAssetOptionResults ImmediateExercise(double s, double x)
    {
        double delta = s >= x ? 1.0 : 0.0;
        return new OneAssetOptionResults
        {
            NPV = Max(0.0, s - x),
            Delta = delta,
            Gamma = 0.0,
            Rho = 0.0,
            DividendRho = 0.0,
            Vega = 0.0,
            Theta = 0.0,
            ThetaPerDay = 0.0,
            StrikeSensitivity = -delta,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty
                .Add("strikeGamma", 0.0)
                .Add("exerciseType", "Immediate"),
        };
    }

    private static OneAssetOptionResults AmericanCallApproximation(
        double s, double x, double rfD, double dD, double variance, Times times, bool put)
    {
        OneAssetOptionResults europeanResults = EuropeanCallResults(s, x, rfD, dD, variance, times, put);

        OneAssetOptionResults results;

        double bT = Math.Log(dD / rfD);
        double rT = Math.Log(1.0 / rfD);

        // with q = 0 the radicand is a perfect square, so rounding can take
        // it just below zero where r = -variance/2
        double beta = (0.5 - (bT / variance))
            + Math.Sqrt(Max(0.0, Squared((bT / variance) - 0.5) + (2.0 * rT / variance)));

        double bInfinity = beta / (beta - 1.0) * x;
        double b0 = bT == rT ? x : Max(x, rT / (rT - bT) * x);
        double ht = -(bT + (2.0 * Math.Sqrt(variance))) * b0 / (bInfinity - b0);

        // as beta tends to 1 the perpetual boundary runs to infinity, and I
        // and its derivatives tend to limits in a = (bT + 2 sqrt(v)) B0
        bool infiniteBoundary = Math.Abs(beta - 1.0) < 1e-8;
        double a = (bT + (2.0 * Math.Sqrt(variance))) * b0;
        double i = infiniteBoundary
            ? b0 + a
            : b0 + ((bInfinity - b0) * (1 - Math.Exp(ht)));

        // the trigger below its lower bound: the perpetual boundary in its place, a feasible flat boundary whose
        // value is a lower bound (Bjerksund and Stensland 2002, §3); with no finite one, never exercising (VR9)
        bool perpetualTrigger = !(i >= b0) || !double.IsFinite(i);
        if (perpetualTrigger)
        {
            if (infiniteBoundary)
            {
                return europeanResults;
            }

            i = bInfinity;
        }

        double fwd = s * dD / rfD;
        double q = Math.Log(i / fwd) / Math.Sqrt(variance);

        if (s >= i)
        {
            results = ImmediateExercise(s, x);
        }
        else if (q > 12.5 && !perpetualTrigger)
        {
            // We have a run away exercise boundary. It is numerically
            // more accurate to use the Greeks of the European engine.
            // (Not at the perpetual trigger: its distance from the forward says nothing of the path reaching it,
            // VR9.)
            results = europeanResults;
        }
        else
        {
            double phiSBetaIIrTbTv = Phi(s, beta, i, i, rT, bT, variance);
            double phiS1IIrTbTv = Phi(s, 1.0, i, i, rT, bT, variance);
            double phiS1XIrTbTV = Phi(s, 1.0, x, i, rT, bT, variance);
            double value = ((i - x) * Math.Pow(s / i, beta) * (1 - phiSBetaIIrTbTv))
                + (s * phiS1IIrTbTv)
                - (s * phiS1XIrTbTV)
                - (x * Phi(s, 0.0, i, i, rT, bT, variance))
                + (x * Phi(s, 0.0, x, i, rT, bT, variance));

            double phiSSBetaIIrTbTv = PhiS(s, beta, i, i, rT, bT, variance);
            double phiSS1IIrTbTv = PhiS(s, 1.0, i, i, rT, bT, variance);
            double phiSS1XIrTbTv = PhiS(s, 1.0, x, i, rT, bT, variance);
            double delta = ((i - x) * Math.Pow(s / i, beta - 1) * beta / i * (1 - phiSBetaIIrTbTv))
                - ((i - x) * Math.Pow(s / i, beta) * phiSSBetaIIrTbTv)
                + phiS1IIrTbTv
                + (s * phiSS1IIrTbTv)
                - phiS1XIrTbTV
                - (s * phiSS1XIrTbTv)
                - (x * PhiS(s, 0.0, i, i, rT, bT, variance))
                + (x * PhiS(s, 0.0, x, i, rT, bT, variance));

            // each structure's time from its own reference date (VR4 (viii)), rescaled with the swap for a put, as
            // QuantLib's
            double tq = times.Dividend;

            double betaDq = tq * ((1 / variance)
                - (1 / (2 * Math.Sqrt(Squared((bT / variance) - 0.5) + (2.0 * rT / variance)))
                   * 2 * ((bT / variance) - 0.5) / variance));
            double bInfinityDq = -x / Squared(beta - 1.0) * betaDq;
            double b0Dq = dD <= rfD ? 0.0 : x * Math.Log(rfD) / Squared(Math.Log(dD)) * tq;

            double htDq = (tq * b0 / (bInfinity - b0))
                - ((bT + (2.0 * Math.Sqrt(variance)))
                   * ((b0Dq * (bInfinity - b0)) - (b0 * (bInfinityDq - b0Dq)))
                   / Squared(bInfinity - b0));
            double iDq = perpetualTrigger
                ? bInfinityDq
                : infiniteBoundary
                ? b0Dq - (tq * b0) + ((bT + (2.0 * Math.Sqrt(variance))) * b0Dq) - (Squared(a) * betaDq / (2 * x))
                : b0Dq + ((bInfinityDq - b0Dq) * (1 - Math.Exp(ht))) - ((bInfinity - b0) * Math.Exp(ht) * htDq);

            double phiHSBetaIIrTbTv = PhiH(s, beta, i, i, rT, bT, variance);
            double phiISBetaIIrTbTv = PhiI(s, beta, i, i, rT, bT, variance);
            double phiGammaSBetaIIrTbTv = PhiGamma(s, beta, i, i, rT, bT, variance);
            double phiBtSBetaIIrTbTv = PhiBt(s, beta, i, i, rT, bT, variance);
            double phiHS1IIrTbTv = PhiH(s, 1.0, i, i, rT, bT, variance);
            double phiIS1IIrTbTv = PhiI(s, 1.0, i, i, rT, bT, variance);
            double phiBtS1IIrTbTv = PhiBt(s, 1.0, i, i, rT, bT, variance);
            double phiIS1XIrTbTv = PhiI(s, 1.0, x, i, rT, bT, variance);
            double phiBtS1XIrTbTv = PhiBt(s, 1.0, x, i, rT, bT, variance);
            double phiHS0IIrTbTv = PhiH(s, 0.0, i, i, rT, bT, variance);
            double phiIS0IIrTbTv = PhiI(s, 0.0, i, i, rT, bT, variance);
            double phiBtS0IIrTbTv = PhiBt(s, 0.0, i, i, rT, bT, variance);
            double phiIS0XIrTbTv = PhiI(s, 0.0, x, i, rT, bT, variance);
            double phiBtS0XIrTbTv = PhiBt(s, 0.0, x, i, rT, bT, variance);

            double dividendRho =
                ((((iDq * Math.Pow(s / i, beta))
                   + ((i - x) * Math.Pow(s / i, beta) * ((betaDq * Math.Log(s / i)) - (beta * 1 / i * iDq))))
                  * (1 - phiSBetaIIrTbTv))
                 - ((i - x) * Math.Pow(s / i, beta)
                    * ((phiHSBetaIIrTbTv * iDq)
                       + (phiISBetaIIrTbTv * iDq)
                       + (phiGammaSBetaIIrTbTv * betaDq)
                       - (phiBtSBetaIIrTbTv * tq)))
                 + (s * ((phiHS1IIrTbTv * iDq)
                         + (phiIS1IIrTbTv * iDq)
                         - (phiBtS1IIrTbTv * tq)))
                 - (s * ((phiIS1XIrTbTv * iDq)
                         - (phiBtS1XIrTbTv * tq)))
                 - (x * ((phiHS0IIrTbTv * iDq)
                         + (phiIS0IIrTbTv * iDq)
                         - (phiBtS0IIrTbTv * tq))))
                + (x * ((phiIS0XIrTbTv * iDq)
                        - (phiBtS0XIrTbTv * tq)));

            double tr = times.RiskFree;

            double betaDr = tr * ((-1 / variance)
                + (1 / (2 * Math.Sqrt(Squared((bT / variance) - 0.5) + (2.0 * rT / variance)))
                   * 2 * (((bT / variance) - 0.5) / variance + (1 / variance))));
            double bInfinityDr = -x / Squared(beta - 1.0) * betaDr;
            double b0Dr = dD <= rfD ? 0 : -x * tr / Math.Log(dD);
            double htDr = (-tr * b0 / (bInfinity - b0))
                - ((bT + (2.0 * Math.Sqrt(variance)))
                   * ((b0Dr * (bInfinity - b0)) - (b0 * (bInfinityDr - b0Dr)))
                   / Squared(bInfinity - b0));
            double iDr = perpetualTrigger
                ? bInfinityDr
                : infiniteBoundary
                ? b0Dr + (tr * b0) + ((bT + (2.0 * Math.Sqrt(variance))) * b0Dr) - (Squared(a) * betaDr / (2 * x))
                : b0Dr + ((bInfinityDr - b0Dr) * (1 - Math.Exp(ht))) - ((bInfinity - b0) * Math.Exp(ht) * htDr);

            double rho =
                ((((iDr * Math.Pow(s / i, beta))
                   + ((i - x) * Math.Pow(s / i, beta) * ((betaDr * Math.Log(s / i)) - (beta / i * iDr))))
                  * (1 - phiSBetaIIrTbTv))
                 - ((i - x) * Math.Pow(s / i, beta)
                    * ((phiHSBetaIIrTbTv * iDr)
                       + (phiISBetaIIrTbTv * iDr)
                       + (phiGammaSBetaIIrTbTv * betaDr)
                       + (PhiRt(s, beta, i, i, rT, bT, variance) * tr)
                       + (phiBtSBetaIIrTbTv * tr)))
                 + (s * ((phiHS1IIrTbTv * iDr)
                         + (phiIS1IIrTbTv * iDr)
                         + (PhiRt(s, 1.0, i, i, rT, bT, variance) * tr)
                         + (phiBtS1IIrTbTv * tr)))
                 - (s * ((phiIS1XIrTbTv * iDr)
                         + (PhiRt(s, 1.0, x, i, rT, bT, variance) * tr)
                         + (phiBtS1XIrTbTv * tr)))
                 - (x * ((phiHS0IIrTbTv * iDr)
                         + (phiIS0IIrTbTv * iDr)
                         + (PhiRt(s, 0.0, i, i, rT, bT, variance) * tr)
                         + (phiBtS0IIrTbTv * tr))))
                + (x * ((phiIS0XIrTbTv * iDr)
                        + (PhiRt(s, 0.0, x, i, rT, bT, variance) * tr)
                        + (phiBtS0XIrTbTv * tr)));

            double tv = times.Volatility;
            double varianceDv = 2 * Math.Sqrt(variance * tv);

            double betaDv = (bT / Squared(variance) * varianceDv)
                + (-(1 / (2 * Math.Sqrt(Squared((bT / variance) - 0.5) + (2.0 * rT / variance))))
                   * ((2 * ((bT / variance) - 0.5) * bT * varianceDv / Squared(variance))
                      + (2 * rT / Squared(variance) * varianceDv)));
            double bInfinityDv = -x / Squared(beta - 1.0) * betaDv;
            double htDv = (-1 / Math.Sqrt(variance) * varianceDv * b0 / (bInfinity - b0))
                + ((bT + (2 * Math.Sqrt(variance))) * b0 / Squared(bInfinity - b0) * bInfinityDv);

            double iDv = perpetualTrigger
                ? bInfinityDv
                : infiniteBoundary
                ? (varianceDv / Math.Sqrt(variance) * b0) - (Squared(a) * betaDv / (2 * x))
                : (bInfinityDv * (1 - Math.Exp(ht))) - ((bInfinity - b0) * Math.Exp(ht) * htDv);

            double vega =
                ((((iDv * Math.Pow(s / i, beta))
                   + ((i - x) * Math.Pow(s / i, beta) * ((betaDv * Math.Log(s / i)) - (beta / i * iDv))))
                  * (1 - phiSBetaIIrTbTv))
                 - ((i - x) * Math.Pow(s / i, beta)
                    * ((phiHSBetaIIrTbTv * iDv)
                       + (phiISBetaIIrTbTv * iDv)
                       + (phiGammaSBetaIIrTbTv * betaDv)
                       + (PhiV(s, beta, i, i, rT, bT, variance) * varianceDv)))
                 + (s * ((phiHS1IIrTbTv * iDv)
                         + (phiIS1IIrTbTv * iDv)
                         + (PhiV(s, 1.0, i, i, rT, bT, variance) * varianceDv)))
                 - (s * ((phiIS1XIrTbTv * iDv)
                         + (PhiV(s, 1.0, x, i, rT, bT, variance) * varianceDv)))
                 - (x * ((phiHS0IIrTbTv * iDv)
                         + (phiIS0IIrTbTv * iDv)
                         + (PhiV(s, 0.0, i, i, rT, bT, variance) * varianceDv))))
                + (x * ((phiIS0XIrTbTv * iDv)
                        + (PhiV(s, 0.0, x, i, rT, bT, variance) * varianceDv)));

            double gamma =
                ((i - x) * Math.Pow(s / i, beta - 2) * beta * (beta - 1) / Squared(i) * (1 - phiSBetaIIrTbTv))
                - (2 * (i - x) * Math.Pow(s / i, beta - 1) * beta / i * phiSSBetaIIrTbTv)
                - ((i - x) * Math.Pow(s / i, beta) * PhiSS(s, beta, i, i, rT, bT, variance))
                + (2 * phiSS1IIrTbTv)
                + (s * PhiSS(s, 1.0, i, i, rT, bT, variance))
                - (2 * phiSS1XIrTbTv)
                - (s * PhiSS(s, 1.0, x, i, rT, bT, variance))
                - (x * PhiSS(s, 0.0, i, i, rT, bT, variance))
                + (x * PhiSS(s, 0.0, x, i, rT, bT, variance));

            double vol = Math.Sqrt(variance / tv);

            // the day fractions of each structure's own day counter from its own reference date (VR4 (viii))
            double dtq = times.Days.Dividend;
            double dtr = times.Days.RiskFree;
            double dtv = times.Days.Volatility;

            double thetaPerDay = -((0.5 * vega * vol / tv * dtv)
                + (rho * rT / (tr * tr) * dtr) + (dividendRho * (rT - bT) / (tq * tq) * dtq));

            results = new OneAssetOptionResults
            {
                NPV = value,
                Delta = delta,
                Gamma = gamma,
                Rho = rho,
                DividendRho = dividendRho,
                Vega = vega,
                Theta = 365 * thetaPerDay,
                ThetaPerDay = thetaPerDay,
                StrikeSensitivity = (value / x) - (s / x * delta),
                AdditionalResults = ImmutableSortedDictionary<string, object>.Empty
                    .Add("strikeGamma", gamma * Squared(s / x))
                    .Add("exerciseType", "American"),
            };
        }

        // check if European engine gives higher NPV
        if (results.NPV!.Value < europeanResults.NPV!.Value)
        {
            results = europeanResults;
        }

        return results;
    }

    private static double Phi(double s, double gamma, double h, double i, double rT, double bT, double variance)
    {
        double lambda = -rT + (gamma * bT) + (0.5 * gamma * (gamma - 1.0) * variance);
        double d = -(Math.Log(s / h) + (bT + ((gamma - 0.5) * variance))) / Math.Sqrt(variance);
        double kappa = (2.0 * bT / variance) + ((2.0 * gamma) - 1.0);
        double tailArgument = d - (2.0 * Math.Log(i / s) / Math.Sqrt(variance));
        double power = Math.Pow(i / s, kappa);
        double tail = CumNormalDist.Evaluate(tailArgument);

        // the power overflowing or the tail underflowing: their product as one exponent (VR4 (xxxiii))
        double product = double.IsFinite(power) && tail != 0.0
            ? power * tail
            : Math.Exp((kappa * Math.Log(i / s)) + AmericanPayoffAtHit.LogCumNormal(tailArgument));
        return Math.Exp(lambda) * (CumNormalDist.Evaluate(d) - product);
    }

    private static double PhiS(double s, double gamma, double h, double i, double rT, double bT, double v)
    {
        double lsh = Math.Log(s / h);
        double lis = Math.Log(i / s);
        double sv = Math.Sqrt(v);
        double k = 2 * (gamma + (bT / v));
        double y = (2 * bT) - v + (2 * gamma * v) + (4 * lis) + (2 * lsh);

        double powerOverDensity = PowerOver(i / s, k, lis, y, v, out double power, out double density)
            ? power / (density * i)
            : Math.Exp((k * lis) - (Squared(y) / (8.0 * v))) / i;
        double powerTail = PowerTimesErfc(power, k, lis, y / (2.0 * MathConstants.M_SQRT2 * sv), out double tail)
            ? power * ((2 * bT) + ((-1 + (2 * gamma)) * v)) * tail
            : Math.Exp((k * lis) + LogErfc(y / (2.0 * MathConstants.M_SQRT2 * sv))) * ((2 * bT) + ((-1 + (2 * gamma)) * v));

        return Math.Exp((bT * gamma) - rT + ((-1 + gamma) * gamma * v / 2.0))
            * (((-powerOverDensity
                 - (1 / (Math.Exp(Squared((2 * bT) - v + (2 * gamma * v) + (2 * lsh)) / (8.0 * v)) * s)))
                / (MathConstants.M_SQRT2 * MathConstants.M_SQRTPI * sv))
               + (powerTail / (2.0 * i * v)));
    }

    private static double PhiSS(double s, double gamma, double h, double i, double rT, double bT, double v)
    {
        double lsh = Math.Log(s / h);
        double lis = Math.Log(i / s);
        double sv = Math.Sqrt(v);
        double k = 2 * (gamma + (bT / v));
        double y = (2 * bT) - v + (2 * gamma * v) + (4 * lis) + (2 * lsh);
        double ey = Math.Exp(Squared((2 * bT) + ((-1 + (2 * gamma)) * v) + (2 * lsh)) / (8.0 * v));

        bool plain = PowerOver(i / s, k, lis, y, v, out double power, out double ex);
        double c = (2 * bT) + ((-1 + (2 * gamma)) * v);
        double first = plain
            ? 2 * MathConstants.M_SQRT2 * power * s * sv * c / ex
            : 2 * MathConstants.M_SQRT2 * Math.Exp((k * lis) - (Squared(y) / (8.0 * v))) * s * sv * c;
        double second = PowerTimesErfc(power, k, lis, y / (2.0 * MathConstants.M_SQRT2 * sv), out double tail)
            ? 2 * Math.Sqrt(Math.PI) * power * s * (bT + (gamma * v)) * c * tail
            : 2 * Math.Sqrt(Math.PI) * Math.Exp((k * lis) + LogErfc(y / (2.0 * MathConstants.M_SQRT2 * sv))) * s
                * (bT + (gamma * v)) * c;
        double third = plain
            ? power * s * sv * ((2 * bT) - (3 * v) + (2 * gamma * v) + (4 * lis) + (2 * lsh)) / (MathConstants.M_SQRT2 * ex)
            : Math.Exp((k * lis) - (Squared(y) / (8.0 * v))) * s * sv
                * ((2 * bT) - (3 * v) + (2 * gamma * v) + (4 * lis) + (2 * lsh)) / MathConstants.M_SQRT2;

        return Math.Exp((bT * gamma) - rT + ((-1 + gamma) * gamma * v / 2.0))
            * ((MathConstants.M_SQRT2 * i * v * sv / ey)
               + first
               - second
               + (MathConstants.M_SQRT2 * i * sv * (bT + ((-0.5 + gamma) * v) + lsh) / ey)
               - third)
            / (2.0 * i * MathConstants.M_SQRTPI * Squared(s * v));
    }

    private static double PhiGamma(double s, double gamma, double h, double i, double rT, double bT, double v)
    {
        double lsh = Math.Log(s / h);
        double lis = Math.Log(i / s);
        double sv = Math.Sqrt(v);
        double k = -1 + (2 * gamma) + (2 * bT / v);
        double y = (2 * bT) - v + (2 * gamma * v) + (4 * lis) + (2 * lsh);

        double powerOverDensity = PowerOver(i / s, k, lis, y, v, out double power, out double density)
            ? power / density
            : Math.Exp((k * lis) - (Squared(y) / (8 * v)));
        double powerTail = PowerTimesErfc(power, k, lis, y / (2.0 * MathConstants.M_SQRT2 * sv), out double tail)
            ? power * tail * ((2 * bT) + ((-1 + (2 * gamma)) * v) + (4 * lis))
            : Math.Exp((k * lis) + LogErfc(y / (2.0 * MathConstants.M_SQRT2 * sv)))
                * ((2 * bT) + ((-1 + (2 * gamma)) * v) + (4 * lis));

        return Math.Exp((bT * gamma) - rT + ((-1 + gamma) * gamma * v / 2))
            * (((-Math.Exp(-Squared((2 * bT) - v + (2 * gamma * v) + (2 * lsh)) / (8 * v)) + powerOverDensity) * sv
                / (MathConstants.M_SQRT2 * MathConstants.M_SQRTPI))
               + (((2 * bT) + ((-1 + (2 * gamma)) * v))
                  * ErfFunctions.Erfc(((2 * bT) + ((-1 + (2 * gamma)) * v) + (2 * lsh)) / (2.0 * MathConstants.M_SQRT2 * sv))
                  / 4.0)
               - (powerTail / 4.0));
    }

    private static double PhiH(double s, double gamma, double h, double i, double rT, double bT, double v)
    {
        double lsh = Math.Log(s / h);
        double lis = Math.Log(i / s);
        double k = 2 * (gamma + (bT / v));
        double y = (2 * bT) - v + (2 * gamma * v) + (4 * lis) + (2 * lsh);

        double powerOverDensity = PowerOver(i / s, k, lis, y, v, out double power, out double density)
            ? power * s / density
            : Math.Exp((k * lis) - (Squared(y) / (8.0 * v))) * s;

        return Math.Exp((bT * gamma) - rT + ((-1 + gamma) * gamma * v / 2.0))
            * ((i / Math.Exp(Squared((2 * bT) - v + (2 * gamma * v) + (2 * lsh)) / (8.0 * v))) - powerOverDensity)
            / (h * i * Math.Sqrt(2 * Math.PI) * Math.Sqrt(v));
    }

    private static double PhiI(double s, double gamma, double h, double i, double rT, double bT, double v)
    {
        double lsh = Math.Log(s / h);
        double lis = Math.Log(i / s);
        double sv = Math.Sqrt(v);
        double k = 2 * (gamma + (bT / v));
        double y = (2 * bT) - v + (2 * gamma * v) + (4 * lis) + (2 * lsh);
        double c = 1 - (2 * gamma) - (2 * bT / v);

        double power = Math.Pow(i / s, k);
        double density = Math.Exp(Squared(y) / (8.0 * v));
        double tail = ErfFunctions.Erfc(y / (2.0 * MathConstants.M_SQRT2 * sv));
        double e = Math.Exp((bT * gamma) - rT + ((-1 + gamma) * gamma * v / 2.0));
        if (double.IsFinite(power) && double.IsFinite(density) && tail != 0.0)
        {
            return e * power * s * ((2 * Math.Sqrt(2 / Math.PI) / (density * sv)) + (c * tail)) / (2.0 * i * i);
        }

        // the power overflowing or the density's and tail's underflowing (VR4 (xxxiii))
        return e * s
            * ((2 * Math.Sqrt(2 / Math.PI) / sv * Math.Exp((k * lis) - (Squared(y) / (8.0 * v))))
               + (c * Math.Exp((k * lis) + LogErfc(y / (2.0 * MathConstants.M_SQRT2 * sv)))))
            / (2.0 * i * i);
    }

    private static double PhiRt(double s, double gamma, double h, double i, double rT, double bT, double v)
    {
        double lsh = Math.Log(s / h);
        double lis = Math.Log(i / s);
        double k = 2 * (gamma + (bT / v));
        double argument = ((2 * bT) - v + (2 * gamma * v) + (4 * lis) + (2 * lsh)) / (2.0 * Math.Sqrt(2 * v));

        double power = Math.Pow(i / s, k);
        double powerTail = PowerTimesErfc(power, k, lis, argument, out double tail)
            ? power * s * tail
            : Math.Exp((k * lis) + LogErfc(argument)) * s;

        return Math.Exp((bT * gamma) - rT + ((-1 + gamma) * gamma * v / 2.0))
            * ((-(i * ErfFunctions.Erfc(((2 * bT) - v + (2 * gamma * v) + (2 * lsh)) / (2.0 * Math.Sqrt(2 * v)))))
               + powerTail)
            / (2.0 * i);
    }

    private static double PhiBt(double s, double gamma, double h, double i, double rT, double bT, double v)
    {
        double lsh = Math.Log(s / h);
        double lis = Math.Log(i / s);
        double sv = Math.Sqrt(v);
        double k = 2 * (gamma + (bT / v));
        double y = (2 * bT) - v + (2 * gamma * v) + (4 * lis) + (2 * lsh);

        double powerOverDensity = PowerOver(i / s, k, lis, y, v, out double power, out double density)
            ? power * s / density
            : Math.Exp((k * lis) - (Squared(y) / (8.0 * v))) * s;
        double powerTail = PowerTimesErfc(power, k, lis, y / (2.0 * MathConstants.M_SQRT2 * sv), out double tail)
            ? MathConstants.M_SQRTPI * power * s * tail * ((gamma * v) + (2 * lis))
            : MathConstants.M_SQRTPI * Math.Exp((k * lis) + LogErfc(y / (2.0 * MathConstants.M_SQRT2 * sv))) * s
                * ((gamma * v) + (2 * lis));

        return Math.Exp((bT * gamma) - rT + ((-1 + gamma) * gamma * v / 2.0))
            * ((MathConstants.M_SQRT2
                * (-(i / Math.Exp(Squared((2 * bT) - v + (2 * gamma * v) + (2 * lsh)) / (8.0 * v))) + powerOverDensity)
                * sv)
               + (gamma * i * Math.Sqrt(Math.PI) * v
                  * ErfFunctions.Erfc(((2 * bT) - v + (2 * gamma * v) + (2 * lsh)) / (2.0 * MathConstants.M_SQRT2 * sv)))
               - powerTail)
            / (2.0 * i * Math.Sqrt(Math.PI) * v);
    }

    private static double PhiV(double s, double gamma, double h, double i, double rT, double bT, double v)
    {
        double lsh = Math.Log(s / h);
        double lis = Math.Log(i / s);
        double sv = Math.Sqrt(v);
        double k = 2 * (gamma + (bT / v));
        double k1 = -1 + (2 * gamma) + (2 * bT / v);
        double y = (2 * bT) - v + (2 * gamma * v) + (4 * lis) + (2 * lsh);
        double argument = y / (2.0 * MathConstants.M_SQRT2 * sv);
        double er = ErfFunctions.Erfc(argument);

        double power = Math.Pow(i / s, k);
        double first = double.IsFinite(power) && er != 0.0
            ? power * s * er
            : Math.Exp((k * lis) + LogErfc(argument)) * s;
        double power1 = Math.Pow(i / s, k1);
        double second = double.IsFinite(power1) && er != 0.0
            ? 2 * bT * power1 * er * lis / (v * v)
            : 2 * bT * Math.Exp((k1 * lis) + LogErfc(argument)) * lis / (v * v);
        double density = Math.Exp(Squared(y) / (8.0 * v));
        double third = double.IsFinite(power1) && double.IsFinite(density)
            ? power1 * ((2 * bT) + v - (2 * gamma * v) + (4 * lis) + (2 * lsh))
                / (2.0 * density * MathConstants.M_SQRT2 * MathConstants.M_SQRTPI * v * sv)
            : Math.Exp((k1 * lis) - (Squared(y) / (8.0 * v))) * ((2 * bT) + v - (2 * gamma * v) + (4 * lis) + (2 * lsh))
                / (2.0 * MathConstants.M_SQRT2 * MathConstants.M_SQRTPI * v * sv);

        return Math.Exp((bT * gamma) - rT + ((-1 + gamma) * gamma * v / 2.0))
            * (((-1 + gamma) * gamma
                * ((i * ErfFunctions.Erfc(((2 * bT) - v + (2 * gamma * v) + (2 * lsh)) / (2.0 * MathConstants.M_SQRT2 * sv)))
                   - first)
                / (2.0 * i))
               + second
               + (((2 * bT) + v - (2 * gamma * v) + (2 * lsh))
                  / (2.0 * Math.Exp(Math.Pow((2 * bT) + ((-1 + (2 * gamma)) * v) + (2 * lsh), 2) / (8.0 * v))
                     * MathConstants.M_SQRT2 * MathConstants.M_SQRTPI * v * sv))
               - third)
            / 2.0;
    }

    // (I/S)^k and the Gaussian exp(y²/(8v)) it divides, and whether both are finite, QuantLib's quotient then
    private static bool PowerOver(double ratio, double k, double logRatio, double y, double v, out double power, out double density)
    {
        _ = logRatio;
        power = Math.Pow(ratio, k);
        density = Math.Exp(Squared(y) / (8.0 * v));
        return double.IsFinite(power) && double.IsFinite(density);
    }

    // whether (I/S)^k times erfc at the argument is QuantLib's product: the power finite and the tail positive
    private static bool PowerTimesErfc(double power, double k, double logRatio, double argument, out double tail)
    {
        _ = k;
        _ = logRatio;
        tail = ErfFunctions.Erfc(argument);
        return double.IsFinite(power) && tail != 0.0;
    }

    // ln erfc(x) = ln 2 + ln N(−√2 x), through log N's asymptotic tail where N underflows
    private static double LogErfc(double x) => Math.Log(2.0) + AmericanPayoffAtHit.LogCumNormal(-MathConstants.M_SQRT2 * x);

    // a structure's day fraction: its year fraction to a date from its own reference date less from the day after
    private static double DayFraction(TermStructure structure, Date date) =>
        structure.DayCounter.YearFraction(structure.ReferenceDate, date)
        - structure.DayCounter.YearFraction(structure.ReferenceDate + 1, date);

    private static double Squared(double x) => x * x;

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);

    // each structure's day fraction from its own reference date, the theta's
    private readonly record struct DayFractions(double RiskFree, double Dividend, double Volatility);

    // each structure's own time to expiry, the curves' on the volatility's day counter, and the day fractions
    private readonly record struct Times(
        double RiskFree,
        double Dividend,
        double Volatility,
        double RiskFreeOnVolatility,
        double DividendOnVolatility,
        DayFractions Days);

    // std::max's semantics, which keep or drop a NaN by position where Math.Max propagates it.
    private static double Max(double a, double b) => a < b ? b : a;
}
