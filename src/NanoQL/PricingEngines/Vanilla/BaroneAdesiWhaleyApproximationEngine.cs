// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/baroneadesiwhaleyengine.hpp ql/pricingengines/vanilla/baroneadesiwhaleyengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>The Barone-Adesi and Whaley pricing engine for American options (1987).</summary>
/// <remarks>
/// <para>
/// The correctness of the returned value is tested by reproducing results available in literature. Where early
/// exercise is never optimal the engine reports the European value and every Greek; otherwise the value alone.
/// </para>
/// <para>
/// The critical price's Newton step can leave (0, K) for a put, or (K, ∞) for a call, and the next Black formula then
/// throws (<c>baroneadesiwhaleyengine.cpp:83-130</c>); so does a call's seed where h > 0 takes it below zero
/// (<c>:59-64</c>, then the Black formula of <c>:93</c>). Here the critical price is then the root of QuantLib's own
/// equation over that range, by Brent's method. Every case where Newton stays in range keeps QuantLib's bits
/// (<c>Divergences/BaroneAdesiWhaleyCriticalPriceTests.cs</c>).
/// </para>
/// <para>
/// Each structure is read at the exercise date by date, nothing accrued before its own reference date; where early
/// exercise is never optimal the theta's terms run over each structure's own time, as the analytic European engine's,
/// where QuantLib's run over the volatility's (<c>Divergences/ApproximationThetaMismatchedTimesTests.cs</c>). An
/// exercise window opening after the reference date is priced by <see cref="ApproximationWindow"/>, where QuantLib
/// reads the last date alone (<c>Divergences/ApproximationWindowTests.cs</c>).
/// </para>
/// </remarks>
public static class BaroneAdesiWhaleyApproximationEngine
{
    /// <summary>Prices an American vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't American or pays at expiry, the payoff isn't striked, the underlying isn't positive, or the
    /// critical price refuses.
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

        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        // each structure read by date, nothing accrued before its reference date (the mismatched-dates class)
        TermStructure clock = FdmBlackScholesOp.Clock(process);
        Date lastDate = ex.LastDate();
        ApproximationWindow.Market market = ApproximationWindow.At(process, clock, lastDate, payoff.Strike);
        double spot = market.Spot;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        OneAssetOptionResults results;
        if (NeverOptimal(payoff.OptionType, market.RiskFreeDiscount, market.DividendDiscount))
        {
            results = EuropeanResults(payoff, process, lastDate, market);
        }
        else if (ApproximationWindow.Opening(ex, clock) is Date opening)
        {
            results = ApproximationWindow.Price(
                ApproximationWindow.At(process, clock, opening, payoff.Strike),
                market,
                payoff.Strike,
                m => new OneAssetOptionResults { NPV = Value(payoff, m) });
        }
        else
        {
            results = new OneAssetOptionResults { NPV = Value(payoff, market) };
        }

        return option.FetchResults(results);
    }

    /// <summary>The critical commodity price: the underlying's level at which early exercise becomes optimal.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="riskFreeDiscount">The risk-free discount to expiry, at most 1.</param>
    /// <param name="dividendDiscount">The dividend discount to expiry.</param>
    /// <param name="variance">The Black variance to expiry.</param>
    /// <param name="tolerance">The tolerance on QuantLib's equation, relative to the strike.</param>
    /// <returns>The critical price.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The risk-free discount is above 1, or the option type is unknown.</exception>
    public static double CriticalPrice(
        StrikedTypePayoff payoff, double riskFreeDiscount, double dividendDiscount, double variance, double tolerance = 1e-6)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        if (!(riskFreeDiscount <= 1.0))
        {
            throw new NanoQLException(
                "the Barone-Adesi-Whaley approximation is not applicable with negative interest rates (risk-free "
                + $"discount factor: {Show(riskFreeDiscount)})");
        }

        double strike = payoff.Strike;

        // Calculation of seed value, Si
        double n = 2.0 * Math.Log(dividendDiscount / riskFreeDiscount) / variance;
        double m = -2.0 * Math.Log(riskFreeDiscount) / variance;
        double bT = Math.Log(dividendDiscount / riskFreeDiscount);

        double qu, su, h, si;
        switch (payoff.OptionType)
        {
            case OptionType.Call:
                qu = (-(n - 1.0) + Math.Sqrt(((n - 1.0) * (n - 1.0)) + (4.0 * m))) / 2.0;
                su = strike / (1.0 - (1.0 / qu));
                h = -(bT + (2.0 * Math.Sqrt(variance))) * strike / (su - strike);
                si = strike + ((su - strike) * (1.0 - Math.Exp(h)));
                break;
            case OptionType.Put:
                qu = (-(n - 1.0) - Math.Sqrt(((n - 1.0) * (n - 1.0)) + (4.0 * m))) / 2.0;
                su = strike / (1.0 - (1.0 / qu));
                h = (bT - (2.0 * Math.Sqrt(variance))) * strike / (strike - su);
                si = su + ((strike - su) * Math.Exp(h));

                // for a vanishing variance h diverges and the seed overflows; Su is
                // its finite limit (the critical price tends to the strike)
                if (!(si > 0.0) || !double.IsFinite(si))
                {
                    si = su;
                }

                break;
            default:
                throw new NanoQLException("unknown option type");
        }

        // a call's seed below zero, where h > 0 takes it and QuantLib's first Black formula throws: the equation's root
        // over (K, ∞)
        if (payoff.OptionType == OptionType.Call && !(si > 0.0))
        {
            return Bracketed(payoff, riskFreeDiscount, dividendDiscount, variance);
        }

        // Newton Raphson algorithm for finding critical price Si
        double q, lhs, rhs, bi;
        double forwardSi = si * dividendDiscount / riskFreeDiscount;
        double d1 = (Math.Log(forwardSi / strike) + (0.5 * variance)) / Math.Sqrt(variance);
        var cumNormalDist = new CumulativeNormalDistribution();
        double k = !ComparisonFunctions.Close(riskFreeDiscount, 1.0, 1000)
            ? -2.0 * Math.Log(riskFreeDiscount) / (variance * (1.0 - riskFreeDiscount))
            : 2.0 / variance;
        double temp = BlackFormulaFunctions.BlackFormula(payoff.OptionType, strike, forwardSi, Math.Sqrt(variance))
            * riskFreeDiscount;
        switch (payoff.OptionType)
        {
            case OptionType.Call:
                q = (-(n - 1.0) + Math.Sqrt(((n - 1.0) * (n - 1.0)) + (4 * k))) / 2;
                lhs = si - strike;
                rhs = temp + ((1 - (dividendDiscount * cumNormalDist.Evaluate(d1))) * si / q);
                bi = (dividendDiscount * cumNormalDist.Evaluate(d1) * (1 - (1 / q)))
                    + ((1 - (dividendDiscount * cumNormalDist.Derivative(d1) / Math.Sqrt(variance))) / q);
                while (Math.Abs(lhs - rhs) / strike > tolerance)
                {
                    si = (strike + rhs - (bi * si)) / (1 - bi);

                    // the step left (K, ∞): the equation's root there
                    if (!(si > strike))
                    {
                        return Bracketed(payoff, riskFreeDiscount, dividendDiscount, variance);
                    }

                    forwardSi = si * dividendDiscount / riskFreeDiscount;
                    d1 = (Math.Log(forwardSi / strike) + (0.5 * variance)) / Math.Sqrt(variance);
                    lhs = si - strike;
                    double temp2 = BlackFormulaFunctions.BlackFormula(
                        payoff.OptionType, strike, forwardSi, Math.Sqrt(variance)) * riskFreeDiscount;
                    rhs = temp2 + ((1 - (dividendDiscount * cumNormalDist.Evaluate(d1))) * si / q);
                    bi = (dividendDiscount * cumNormalDist.Evaluate(d1) * (1 - (1 / q)))
                        + ((1 - (dividendDiscount * cumNormalDist.Derivative(d1) / Math.Sqrt(variance))) / q);
                }

                break;
            case OptionType.Put:
                q = (-(n - 1.0) - Math.Sqrt(((n - 1.0) * (n - 1.0)) + (4 * k))) / 2;
                lhs = strike - si;
                rhs = temp - ((1 - (dividendDiscount * cumNormalDist.Evaluate(-d1))) * si / q);
                bi = (-dividendDiscount * cumNormalDist.Evaluate(-d1) * (1 - (1 / q)))
                    - ((1 + (dividendDiscount * cumNormalDist.Derivative(-d1) / Math.Sqrt(variance))) / q);
                while (Math.Abs(lhs - rhs) / strike > tolerance)
                {
                    si = (strike - rhs + (bi * si)) / (1 + bi);

                    // the step left (0, K): the equation's root there
                    if (!(si > 0.0 && si < strike))
                    {
                        return Bracketed(payoff, riskFreeDiscount, dividendDiscount, variance);
                    }

                    forwardSi = si * dividendDiscount / riskFreeDiscount;
                    d1 = (Math.Log(forwardSi / strike) + (0.5 * variance)) / Math.Sqrt(variance);
                    lhs = strike - si;
                    double temp2 = BlackFormulaFunctions.BlackFormula(
                        payoff.OptionType, strike, forwardSi, Math.Sqrt(variance)) * riskFreeDiscount;
                    rhs = temp2 - ((1 - (dividendDiscount * cumNormalDist.Evaluate(-d1))) * si / q);
                    bi = (-dividendDiscount * cumNormalDist.Evaluate(-d1) * (1 - (1 / q)))
                        - ((1 + (dividendDiscount * cumNormalDist.Derivative(-d1) / Math.Sqrt(variance))) / q);
                }

                break;
            default:
                throw new NanoQLException("unknown option type");
        }

        return si;
    }

    // Early exercise never optimal: for a call when D_q ≥ 1 and D_q ≥ D_r, for a put when D_r ≥ 1 and D_r ≥ D_q.
    internal static bool NeverOptimal(OptionType type, double riskFreeDiscount, double dividendDiscount) =>
        (dividendDiscount >= 1.0 && dividendDiscount >= riskFreeDiscount && type == OptionType.Call)
        || (riskFreeDiscount >= 1.0 && riskFreeDiscount >= dividendDiscount && type == OptionType.Put);

    // QuantLib's never-optimal branch, Ju's too: the European value and Greeks, each rate over its own curve's time and
    // the theta's terms over each structure's own (the analytic European engine's form).
    internal static OneAssetOptionResults EuropeanResults(
        StrikedTypePayoff payoff, GeneralizedBlackScholesProcess process, Date lastDate, ApproximationWindow.Market market)
    {
        double spot = market.Spot;
        double forwardPrice = spot * market.DividendDiscount / market.RiskFreeDiscount;
        var black = new BlackCalculator(payoff, forwardPrice, Math.Sqrt(market.Variance), market.RiskFreeDiscount);

        DayCounter voldc = process.BlackVolatility.DayCounter;
        double t = process.BlackVolatility.AccrualTimeTo(lastDate);
        double? theta;
        try
        {
            theta = black.Theta(
                spot,
                process.RiskFreeRate.AccrualTimeTo(lastDate, voldc),
                market.RiskFreeDiscount,
                process.RiskFreeRate.AccrualTimeTo(lastDate, voldc),
                market.DividendDiscount,
                process.DividendYield.AccrualTimeTo(lastDate, voldc),
                t);
        }
        catch (Exception e) when (e is ArgumentException or NanoQLException)
        {
            theta = null;
        }

        return new OneAssetOptionResults
        {
            NPV = black.Value,
            Delta = black.Delta(spot),
            DeltaForward = black.DeltaForward,
            Elasticity = black.Elasticity(spot),
            Gamma = black.Gamma(spot),
            Rho = black.Rho(market.RiskFreeTime),
            DividendRho = black.DividendRho(market.DividendTime),
            Vega = black.Vega(t),
            Theta = theta,
            ThetaPerDay = theta / 365.0,
            StrikeSensitivity = black.StrikeSensitivity,
            ItmCashProbability = black.ItmCashProbability,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty,
        };
    }

    // The value where early exercise can be optimal, or the European value where it never is: QuantLib's calculate on
    // a market, the window's node.
    private static double Value(StrikedTypePayoff payoff, ApproximationWindow.Market market)
    {
        double spot = market.Spot;
        double variance = market.Variance;
        double dividendDiscount = market.DividendDiscount;
        double riskFreeDiscount = market.RiskFreeDiscount;
        double forwardPrice = spot * dividendDiscount / riskFreeDiscount;
        var black = new BlackCalculator(payoff, forwardPrice, Math.Sqrt(variance), riskFreeDiscount);

        if (NeverOptimal(payoff.OptionType, riskFreeDiscount, dividendDiscount))
        {
            return black.Value;
        }
        else if (variance < NumericLimits.QL_EPSILON)
        {
            // no time value: the critical-price calculation degenerates, and the
            // American price is just the European one floored at intrinsic
            return Max(black.Value, payoff.Evaluate(spot));
        }

        // early exercise can be optimal
        var cumNormalDist = new CumulativeNormalDistribution();
        double tolerance = 1e-6;
        double sk = CriticalPrice(payoff, riskFreeDiscount, dividendDiscount, variance, tolerance);
        double forwardSk = sk * dividendDiscount / riskFreeDiscount;
        double d1 = (Math.Log(forwardSk / payoff.Strike) + (0.5 * variance)) / Math.Sqrt(variance);
        double n = 2.0 * Math.Log(dividendDiscount / riskFreeDiscount) / variance;
        double k = !ComparisonFunctions.Close(riskFreeDiscount, 1.0, 1000)
            ? -2.0 * Math.Log(riskFreeDiscount) / (variance * (1.0 - riskFreeDiscount))
            : 2.0 / variance;
        double q, a;
        switch (payoff.OptionType)
        {
            case OptionType.Call:
                q = (-(n - 1.0) + Math.Sqrt(((n - 1.0) * (n - 1.0)) + (4.0 * k))) / 2.0;
                a = sk / q * (1.0 - (dividendDiscount * cumNormalDist.Evaluate(d1)));
                return spot < sk ? black.Value + (a * Math.Pow(spot / sk, q)) : spot - payoff.Strike;
            case OptionType.Put:
                q = (-(n - 1.0) - Math.Sqrt(((n - 1.0) * (n - 1.0)) + (4.0 * k))) / 2.0;
                a = -(sk / q) * (1.0 - (dividendDiscount * cumNormalDist.Evaluate(-d1)));
                return spot > sk ? black.Value + (a * Math.Pow(spot / sk, q)) : payoff.Strike - spot;
            default:
                throw new NanoQLException("unknown option type");
        }
    }

    // The root of QuantLib's critical-price equation, LHS − RHS, over the range Newton left: (0, K) for a put,
    // (K, ∞) for a call, by Brent's method to the strike's 1e-12.
    private static double Bracketed(
        StrikedTypePayoff payoff, double riskFreeDiscount, double dividendDiscount, double variance)
    {
        double strike = payoff.Strike;
        var cumNormalDist = new CumulativeNormalDistribution();

        // QuantLib's Q, as the Newton iteration has it
        double n = 2.0 * Math.Log(dividendDiscount / riskFreeDiscount) / variance;
        double k = !ComparisonFunctions.Close(riskFreeDiscount, 1.0, 1000)
            ? -2.0 * Math.Log(riskFreeDiscount) / (variance * (1.0 - riskFreeDiscount))
            : 2.0 / variance;
        double root = Math.Sqrt(((n - 1.0) * (n - 1.0)) + (4 * k));
        double q = payoff.OptionType == OptionType.Call ? (-(n - 1.0) + root) / 2 : (-(n - 1.0) - root) / 2;

        double Equation(double s)
        {
            double forward = s * dividendDiscount / riskFreeDiscount;
            double d1 = (Math.Log(forward / strike) + (0.5 * variance)) / Math.Sqrt(variance);
            double black = BlackFormulaFunctions.BlackFormula(payoff.OptionType, strike, forward, Math.Sqrt(variance))
                * riskFreeDiscount;
            return payoff.OptionType == OptionType.Call
                ? s - strike - (black + ((1 - (dividendDiscount * cumNormalDist.Evaluate(d1))) * s / q))
                : strike - s - (black - ((1 - (dividendDiscount * cumNormalDist.Evaluate(-d1))) * s / q));
        }

        double accuracy = 1e-12 * strike;
        if (payoff.OptionType == OptionType.Put)
        {
            double low = 1e-12 * strike;
            return new Brent().Solve(Equation, accuracy, 0.5 * strike, low, strike);
        }

        double high = 2.0 * strike;
        while (Equation(high) < 0.0 && high < 1e12 * strike)
        {
            high *= 2.0;
        }

        return new Brent().Solve(Equation, accuracy, 0.5 * (strike + high), strike, high);
    }

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);

    // std::max's semantics, which keep or drop a NaN by position where Math.Max propagates it.
    private static double Max(double a, double b) => a < b ? b : a;
}
