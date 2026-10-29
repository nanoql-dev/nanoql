// SPDX-FileCopyrightText: Copyright (C) 2011 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/continuousarithmeticasianlevyengine.hpp ql/pricingengines/asian/continuousarithmeticasianlevyengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// Levy's engine for continuous arithmetic average-price Asian options: the average matched to a log-normal by its
/// first two moments (Haug, "Option Pricing Formulas").
/// </summary>
/// <remarks>
/// <para>
/// The value alone, as QuantLib's. The averaging's start date is the option's (QuantLib's deprecated constructor taking
/// it isn't ported, the S-bar plan's SD7); a seasoned option takes the current average, a value where QuantLib takes a
/// quote.
/// </para>
/// <para>
/// Documented divergences (BR4 (b), BR5 (xxvi), (xxxi), (xxxix)). The two moments are integrated under deterministic
/// term structures, each at 1e-12 relative: E[A] from the forwards on NanoQL's adaptive Kronrod rule, as spot·t₂ plus
/// the integral of F/spot − 1, exact at zero carry (QuantLib's master's #2926 cases; the S-xeq run's stop 5), and the
/// average's covariance, on a first 15-point estimate (SD24), (2/T²)∫∫F(s)F(t)(e^{V(min(s, t))} − 1) on QuantLib's
/// adaptive Gauss-Lobatto rule, the log-variance ln(E[A²]/E[A]²) its log1p over E[A]², so nothing cancels, where
/// QuantLib reads one volatility and the zero rates at maturity, +38.5% and −17.2% off a Monte Carlo on shaped markets,
/// and its closed form cancels near the carries b = 0, −σ²/2 and −σ², up to 75% off and NaN, and at small volatility,
/// NaN (<c>Divergences/LevyTermStructureTests.cs</c>). The covariance's integrands have kinks where the structures have
/// nodes, where the Kronrod rule accepts panels whose two estimates agree while both are off: 9e-9 off the exact value
/// on shaped markets, in tens of millions of evaluations, where the Gauss-Lobatto rule is within 7e-13 of Levy's
/// formula on the moments in 30 digits, in under 100,000. At zero variance the average is its forward. An effective
/// strike at or below zero is the certain exercise, the call Se − X·e^{−rT₂} and the put 0, the formula's limit, where
/// QuantLib takes log(X) and prices NaN (<c>Divergences/LevyEffectiveStrikeTests.cs</c>).
/// </para>
/// <para>
/// An averaging started from the evaluation date to the clock's origin, where the structures settle after the
/// evaluation date, reads the span before the origin at the spot (the S-bar code review's B3-1,
/// <c>Divergences/AsianLateSettlementTests.cs</c>), where QuantLib takes it as seasoned and asks for a current average.
/// </para>
/// </remarks>
public static class ContinuousArithmeticAsianLevyEngine
{
    // SD24's relative tolerance and the rule's safety cap
    private const double Tolerance = 1e-12;
    private const int Cap = 100000;

    private static readonly CumulativeNormalDistribution N = new();

    /// <summary>Prices a continuous arithmetic average-price Asian option.</summary>
    /// <param name="option">The option, its averaging's start date given.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="currentAverage">The average so far, for a seasoned option.</param>
    /// <returns>The option's results: the value; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The average isn't arithmetic, the exercise isn't European, the start date isn't given or is after the risk-free
    /// curve's date, or a seasoned option has no current average.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        ContinuousAveragingAsianOption option, EvaluationContext context, GeneralizedBlackScholesProcess process, double? currentAverage)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new OneAssetOptionResults { NPV = Value(option, context.EvaluationDate, process, currentAverage) });
    }

    private static double Value(
        ContinuousAveragingAsianOption option, Date evaluationDate, GeneralizedBlackScholesProcess process, double? currentAverage)
    {
        if (!(option.AverageType == AverageType.Arithmetic))
        {
            throw new NanoQLException("not an Arithmetic average option");
        }

        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European Option");
        }

        if (option.StartDate is not Date startDate)
        {
            throw new NanoQLException("start date not provided");
        }

        YieldTermStructure riskFree = process.RiskFreeRate;
        YieldTermStructure dividends = process.DividendYield;
        BlackVolTermStructure volatility = process.BlackVolatility;
        if (!(startDate <= riskFree.ReferenceDate))
        {
            throw new NanoQLException("start date must be earlier than or equal to reference date");
        }

        double spot = process.StateVariable;
        var payoff = (StrikedTypePayoff)option.Payoff!;

        // the original and the remaining time to maturity, on the volatility's clock
        Date maturity = option.Exercise.LastDate();
        double t = volatility.DayCounter.YearFraction(startDate, maturity);
        double t2 = volatility.AccrualTimeTo(maturity);
        double strike = payoff.Strike;
        double discount = riskFree.AccruedDiscount(maturity);

        // the forward and the variance at the volatility's time u, each curve read at the instant u denotes
        double Forward(double u) => spot * dividends.DiscountAt(volatility, u, true) / riskFree.DiscountAt(volatility, u, true);
        double Variance(double u) => u > 0.0 ? volatility.BlackVariance(u, strike, true) : 0.0;

        // E[A] = (1/T)∫F and the covariance (2/T²)∫₀^{T₂} F(t) ∫₀ᵗ F(s) expm1(V(s)) ds dt (BR4 (b))
        // E[A] = (1/T)∫F as spot·t₂ plus the integral of F/spot − 1 on its own scale, exact where the carry is 0
        // (QuantLib's master's #2926 cases at a volatility of 1e-8; the S-xeq run's stop 5), the integral's tolerance
        // the former relative one on spot·t₂
        double meanAverage = Tolerance * t2 > NumericLimits.QL_EPSILON
            ? spot * (t2 + new GaussKronrodAdaptive(Tolerance * t2, Cap).Integrate(u => (Forward(u) / spot) - 1.0, 0.0, t2)) / t
            : GaussKronrodAdaptive.IntegrateOnFirstEstimate(Forward, 0.0, t2, Tolerance, Cap) / t;
        double covariance = 2.0 / (t * t) * LobattoOnFirstEstimate(
            u => Forward(u) * LobattoOnFirstEstimate(s => Forward(s) * Expm1Functions.StdExpm1(Variance(s)), 0.0, u), 0.0, t2);
        double se = discount * meanAverage;

        double x;
        if (t2 < t)
        {
            // an averaging started on or after the evaluation date, before the structures' date: the span before the
            // clock's origin at the spot, today's limit (the S-bar code review's B3-1), where QuantLib asks for a
            // current average
            double? average0 = startDate >= evaluationDate ? spot : currentAverage;
            if (average0 is not double average)
            {
                throw new NanoQLException("current average required for seasoned option");
            }

            x = strike - ((t - t2) / t * average);
        }
        else
        {
            x = strike;
        }

        // the certain exercise (BR5 (xxxi)), the formula's limit as X → 0⁺, where QuantLib takes log(X)
        if (!(x > 0.0))
        {
            return payoff.OptionType == OptionType.Call ? se - (x * discount) : 0.0;
        }

        double v = Log1pFunctions.Log1p(covariance / (meanAverage * meanAverage));

        // no variance: the average is its forward
        if (!(v > 0.0))
        {
            return payoff.OptionType == OptionType.Call
                ? discount * Math.Max(meanAverage - x, 0.0)
                : discount * Math.Max(x - meanAverage, 0.0);
        }

        double d1 = (Math.Log(meanAverage / x) + (v / 2)) / Math.Sqrt(v);
        double d2 = d1 - Math.Sqrt(v);

        double value = (se * N.Evaluate(d1)) - (x * discount * N.Evaluate(d2));
        return payoff.OptionType == OptionType.Call ? value : value - se + (x * discount);
    }

    // SD24's first-estimate scale on QuantLib's adaptive Gauss-Lobatto rule, which kinks at the structures' nodes don't
    // fool: the integrand over its first 15-point estimate at 1e-12, and 0 where the estimate is 0, the integrand being
    // non-negative
    private static double LobattoOnFirstEstimate(Func<double, double> f, double a, double b)
    {
        double estimate = new GaussKronrodAdaptive(double.MaxValue, 15).Integrate(f, a, b);
        if (estimate == 0.0)
        {
            return 0.0;
        }

        return new GaussLobattoIntegral(Cap, Tolerance).Integrate(x => f(x) / estimate, a, b) * estimate;
    }
}
