// SPDX-FileCopyrightText: Copyright (C) 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2013 Fabien Le Floc'h
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/juquadraticengine.hpp ql/pricingengines/vanilla/juquadraticengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The quadratic approximation engine for American options of N. Ju and R. Zhong ("An Approximate Formula for Pricing
/// American Options", Journal of Derivatives, Winter 1999), on Barone-Adesi and Whaley's critical price.
/// </summary>
/// <remarks>
/// <para>
/// The correctness of the returned value is tested by reproducing results available in literature. Where early
/// exercise is never optimal the engine reports the European value and every Greek; otherwise the value, delta and
/// gamma.
/// </para>
/// <para>
/// The value is floored at the European one, which bounds an American option from below, as QuantLib's master does
/// since the pin (#2906; the S-van plan's VR5 (iii)): for short-dated, highly volatile options the correction can take
/// it below. The critical price is <see cref="BaroneAdesiWhaleyApproximationEngine.CriticalPrice"/>, with its bracketed
/// root where Newton leaves its range (VR4 (vii)). Each structure is read at the exercise date by date, nothing accrued
/// before its own reference date; where early exercise is never optimal the theta's terms run over each structure's own
/// time; an exercise window opening after the reference date is priced by <see cref="ApproximationWindow"/> (VR4
/// (xxvi)).
/// </para>
/// </remarks>
public static class JuQuadraticApproximationEngine
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
        if (!(market.Spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        // For a call, exercising yields S-K while holding is worth at least
        // S*dividendDiscount - K*riskFreeDiscount, so early exercise is never
        // optimal when dividendDiscount >= 1 and dividendDiscount >=
        // riskFreeDiscount.  The mirror image holds for a put: exercising
        // yields K-S and holding is worth at least K*riskFreeDiscount -
        // S*dividendDiscount, so early exercise is never optimal when
        // riskFreeDiscount >= 1 and riskFreeDiscount >= dividendDiscount.
        // The put case also has to be caught here because the critical price
        // is 0 when the risk-free rate is null, which would make hA vanish
        // and break down the approximation below.
        OneAssetOptionResults results;
        if (BaroneAdesiWhaleyApproximationEngine.NeverOptimal(
            payoff.OptionType, market.RiskFreeDiscount, market.DividendDiscount))
        {
            results = BaroneAdesiWhaleyApproximationEngine.EuropeanResults(payoff, process, lastDate, market);
        }
        else if (ApproximationWindow.Opening(ex, clock) is Date opening)
        {
            results = ApproximationWindow.Price(
                ApproximationWindow.At(process, clock, opening, payoff.Strike), market, payoff.Strike, m => Core(payoff, m));
        }
        else
        {
            results = Core(payoff, market);
        }

        return option.FetchResults(results);
    }

    // QuantLib's calculate on a market where early exercise may be optimal, the window's node: the value, and where
    // the variance isn't negligible the delta and gamma; the European results where it never is.
    private static OneAssetOptionResults Core(StrikedTypePayoff payoff, ApproximationWindow.Market market)
    {
        double spot = market.Spot;
        double variance = market.Variance;
        double dividendDiscount = market.DividendDiscount;
        double riskFreeDiscount = market.RiskFreeDiscount;
        double forwardPrice = spot * dividendDiscount / riskFreeDiscount;
        var black = new BlackCalculator(payoff, forwardPrice, Math.Sqrt(variance), riskFreeDiscount);

        if (BaroneAdesiWhaleyApproximationEngine.NeverOptimal(payoff.OptionType, riskFreeDiscount, dividendDiscount))
        {
            return new OneAssetOptionResults { NPV = black.Value, Delta = black.Delta(spot), Gamma = black.Gamma(spot) };
        }
        else if (variance < NumericLimits.QL_EPSILON)
        {
            // no time value: the critical-price calculation degenerates, and the
            // American price is just the European one floored at intrinsic
            return new OneAssetOptionResults { NPV = Max(black.Value, payoff.Evaluate(spot)) };
        }

        // early exercise can be optimal
        var cumNormalDist = new CumulativeNormalDistribution();
        var normalDist = new NormalDistribution();

        double tolerance = 1e-6;
        double sk = BaroneAdesiWhaleyApproximationEngine.CriticalPrice(
            payoff, riskFreeDiscount, dividendDiscount, variance, tolerance);

        double forwardSk = sk * dividendDiscount / riskFreeDiscount;

        double alpha = -2.0 * Math.Log(riskFreeDiscount) / variance;
        double beta = 2.0 * Math.Log(dividendDiscount / riskFreeDiscount) / variance;
        double h = 1 - riskFreeDiscount;
        double phi = payoff.OptionType switch
        {
            OptionType.Call => 1,
            OptionType.Put => -1,
            _ => throw new NanoQLException("unknown option type"),
        };

        // When the risk-free rate goes to 0, both alpha and h go to 0
        // as well; their ratio, though, tends to the finite limit
        // 2/variance (the same limit already used inside
        // BaroneAdesiWhaleyApproximationEngine::criticalPrice above).
        // The formulas below are therefore written in terms of
        // alpha/h, alpha*lambda_prime and alpha*V_E_h so that no
        // intermediate quantity diverges in that case.
        double alphaOverH = !ComparisonFunctions.Close(riskFreeDiscount, 1.0, 1000) ? alpha / h : 2.0 / variance;

        double tempRoot = Math.Sqrt(((beta - 1) * (beta - 1)) + (4 * alphaOverH));
        double lambda = (-(beta - 1) + (phi * tempRoot)) / 2;

        // lambda_prime = -phi*alpha/(h*h*temp_root) diverges when the
        // risk-free rate goes to 0, but it only ever enters the formulas
        // below multiplied by alpha, and that product doesn't.
        double alphaLambdaPrime = -phi * alphaOverH * alphaOverH / tempRoot;

        double blackSk = BlackFormulaFunctions.BlackFormula(
            payoff.OptionType, payoff.Strike, forwardSk, Math.Sqrt(variance)) * riskFreeDiscount;
        double hA = (phi * (sk - payoff.Strike)) - blackSk;

        double d1Sk = (Math.Log(forwardSk / payoff.Strike) + (0.5 * variance)) / Math.Sqrt(variance);
        double d2Sk = d1Sk - Math.Sqrt(variance);

        // the three terms below are alpha times the corresponding
        // terms of V_E_h; in part2, alpha/std::log(riskFreeDiscount)
        // simplifies exactly to -2/variance.
        double part1 = forwardSk * normalDist.Evaluate(d1Sk) / Math.Sqrt(variance);
        double part2 = 2.0 * phi * forwardSk * cumNormalDist.Evaluate(phi * d1Sk) * Math.Log(dividendDiscount) / variance;
        double part3 = +phi * alpha * payoff.Strike * cumNormalDist.Evaluate(phi * d2Sk);
        double alphaVEh = part1 + part2 + part3;

        double b = (1 - h) * alphaLambdaPrime / (2 * ((2 * lambda) + beta - 1));
        double c = -((1 - h) / ((2 * lambda) + beta - 1))
            * ((alphaVEh / hA) + alphaOverH + (alphaLambdaPrime / ((2 * lambda) + beta - 1)));
        double tempSpotRatio = Math.Log(spot / sk);
        double chi = tempSpotRatio * ((b * tempSpotRatio) + c);

        double value, delta, gamma;
        if (phi * (sk - spot) > 0)
        {
            value = black.Value + (hA * Math.Pow(spot / sk, lambda) / (1 - chi));
            double tempChiPrime = 2 * b / spot * Math.Log(spot / sk);
            double chiPrime = tempChiPrime + (c / spot);
            double chiDoublePrime = (2 * b / (spot * spot)) - (tempChiPrime / spot) - (c / (spot * spot));
            double d1S = (Math.Log(forwardPrice / payoff.Strike) + (0.5 * variance)) / Math.Sqrt(variance);

            // There is a typo in the original paper from Ju-Zhong
            // the first term is the Black-Scholes delta/gamma.
            delta = (phi * dividendDiscount * cumNormalDist.Evaluate(phi * d1S))
                + (((lambda / (spot * (1 - chi))) + (chiPrime / ((1 - chi) * (1 - chi))))
                   * ((phi * (sk - payoff.Strike)) - blackSk) * Math.Pow(spot / sk, lambda));

            gamma = (dividendDiscount * normalDist.Evaluate(phi * d1S) / (spot * Math.Sqrt(variance)))
                + (((2 * lambda * chiPrime / (spot * (1 - chi) * (1 - chi)))
                    + (2 * chiPrime * chiPrime / ((1 - chi) * (1 - chi) * (1 - chi)))
                    + (chiDoublePrime / ((1 - chi) * (1 - chi)))
                    + (lambda * (lambda - 1) / (spot * spot * (1 - chi))))
                   * ((phi * (sk - payoff.Strike)) - blackSk)
                   * Math.Pow(spot / sk, lambda));

            // deep in the money the correction can land below the
            // exercise value, which the holder can always take instead
            double exerciseValue = payoff.Evaluate(spot);
            if (value < exerciseValue)
            {
                value = exerciseValue;
                delta = phi;
                gamma = 0;
            }
        }
        else
        {
            value = phi * (spot - payoff.Strike);
            delta = phi;
            gamma = 0;
        }

        // for short-dated, highly volatile options the correction can
        // also take the price below the European one, which is a lower
        // bound for the American option; fall back to it in that case
        // (QuantLib's master, #2906)
        if (value < black.Value)
        {
            value = black.Value;
            delta = black.Delta(spot);
            gamma = black.Gamma(spot);
        }

        return new OneAssetOptionResults { NPV = value, Delta = delta, Gamma = gamma };
    }

    // std::max's semantics, which keep or drop a NaN by position where Math.Max propagates it.
    private static double Max(double a, double b) => a < b ? b : a;
}
