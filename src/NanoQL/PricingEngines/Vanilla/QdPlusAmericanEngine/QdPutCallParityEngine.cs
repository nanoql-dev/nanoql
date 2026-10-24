// SPDX-FileCopyrightText: Copyright (C) 2022 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/qdplusamericanengine.hpp ql/pricingengines/vanilla/qdplusamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The base of the QD American engines: a call priced as the put of the put-call symmetry, the edge cases (a zero
/// strike or spot, no early exercise, no volatility) in closed form, and the put by the engine.
/// </summary>
/// <remarks>
/// <para>
/// An extension point, as QuantLib's <c>detail::QdPutCallParityEngine</c>: an engine derives from it and implements
/// <see cref="CalculatePut"/>. Immutable. The value is floored at the exercise value, which the holder can always take.
/// </para>
/// <para>
/// QuantLib reads the volatility at the risk-free curve's time to expiry and each rate as its curve's discount over
/// that time (<c>qdplusamericanengine.cpp:179-184</c>); here the time is the engines' clock's
/// (<see cref="FdmBlackScholesOp.Clock"/>), the rates each curve's accrued discount over it, and the volatility the
/// variance at the expiry date over it: QuantLib's reads where the structures share their times (the S-van plan's VR4
/// (ix), <c>Divergences/QdMismatchedTimesTests.cs</c>). An exercise window opening after the reference date is priced
/// by <see cref="ApproximationWindow"/>, where QuantLib reads the last date alone (VR4 (xxvi)).
/// </para>
/// </remarks>
public abstract class QdPutCallParityEngine
{
    /// <summary>Initializes a new instance of the <see cref="QdPutCallParityEngine"/> class.</summary>
    /// <param name="process">The Black-Scholes process.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    protected QdPutCallParityEngine(GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);
        Process = process;
    }

    /// <summary>Gets the Black-Scholes process.</summary>
    protected GeneralizedBlackScholesProcess Process { get; }

    /// <summary>Prices an American vanilla option: QuantLib's <c>calculate</c>.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The option's results: the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't American, the payoff isn't striked, the spot, strike or volatility is negative, or the engine
    /// refuses the put.
    /// </exception>
    public OneAssetOptionResults Calculate(VanillaOption option, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        if (!(option.Exercise.Type == ExerciseType.American))
        {
            throw new NanoQLException("not an American option");
        }

        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        double spot = Process.X0;
        if (!(spot >= 0.0))
        {
            throw new NanoQLException("negative underlying given");
        }

        TermStructure clock = FdmBlackScholesOp.Clock(Process);
        Date maturity = option.Exercise.LastDate();
        ApproximationWindow.Market market = ApproximationWindow.At(Process, clock, maturity, payoff.Strike);

        OneAssetOptionResults results;
        if (option.Exercise is AmericanExercise ex && ApproximationWindow.Opening(ex, clock) is Date opening)
        {
            results = ApproximationWindow.Price(
                ApproximationWindow.At(Process, clock, opening, payoff.Strike),
                market,
                payoff.Strike,
                m => new OneAssetOptionResults { NPV = Value(payoff, m, null) });
        }
        else
        {
            // QuantLib's volatility read at the clock's time where the volatility shares it (VR4 (ix))
            double? vol = Process.BlackVolatility.SharesTimes(clock)
                ? Process.BlackVolatility.BlackVol(market.Time, payoff.Strike)
                : null;
            results = new OneAssetOptionResults { NPV = Value(payoff, market, vol) };
        }

        return option.FetchResults(results);
    }

    /// <summary>The value of an American put.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="k">The strike.</param>
    /// <param name="r">The risk-free rate.</param>
    /// <param name="q">The dividend yield.</param>
    /// <param name="vol">The volatility.</param>
    /// <param name="t">The time to expiry.</param>
    /// <returns>The value.</returns>
    protected abstract double CalculatePut(double s, double k, double r, double q, double vol, double t);

    // QuantLib's calculate on a market: the rates over the clock's time, the volatility given or the variance's over it,
    // the put of the symmetry for a call, floored at the exercise value.
    private double Value(StrikedTypePayoff payoff, ApproximationWindow.Market market, double? volatility)
    {
        double bigT = market.Time;
        double s = market.Spot;
        double k = payoff.Strike;
        double r = -Math.Log(market.RiskFreeDiscount) / bigT;
        double q = -Math.Log(market.DividendDiscount) / bigT;
        double vol = volatility ?? Math.Sqrt(market.Variance / bigT);

        if (!(s >= 0))
        {
            throw new NanoQLException("zero or positive underlying value is required");
        }

        if (!(k >= 0))
        {
            throw new NanoQLException("zero or positive strike is required");
        }

        if (!(vol >= 0))
        {
            throw new NanoQLException("zero or positive volatility is required");
        }

        double value = payoff.OptionType switch
        {
            OptionType.Put => CalculatePutWithEdgeCases(s, k, r, q, vol, bigT),
            OptionType.Call => CalculatePutWithEdgeCases(k, s, q, r, vol, bigT),
            _ => throw new NanoQLException("unknown option type"),
        };

        // the holder can always exercise, so the value is at least the payoff
        return Max(value, payoff.Evaluate(s));
    }

    private double CalculatePutWithEdgeCases(double s, double k, double r, double q, double vol, double t)
    {
        if (ComparisonFunctions.Close(k, 0.0))
        {
            return 0.0;
        }

        if (ComparisonFunctions.Close(s, 0.0))
        {
            return Max(k, k * Math.Exp(-r * t));
        }

        if (r <= 0.0 && r <= q)
        {
            return Max(
                0.0,
                new BlackCalculator(OptionType.Put, k, s * Math.Exp((r - q) * t), vol * Math.Sqrt(t), Math.Exp(-r * t)).Value);
        }

        if (ComparisonFunctions.Close(vol, 0.0))
        {
            double Intrinsic(double u) => Max(0.0, (k * Math.Exp(-r * u)) - (s * Math.Exp(-q * u)));
            double npv0 = Intrinsic(0.0);
            double npvT = Intrinsic(t);
            double extremT = ComparisonFunctions.CloseEnough(r, q)
                ? NumericLimits.QL_MAX_REAL
                : Math.Log(r * k / (q * s)) / (r - q);

            return extremT > 0.0 && extremT < t
                ? Max(Max(npv0, npvT), Intrinsic(extremT))
                : Max(npv0, npvT);
        }

        return CalculatePut(s, k, r, q, vol, t);
    }

    // std::max's semantics, which keep or drop a NaN by position where Math.Max propagates it.
    private static double Max(double a, double b) => a < b ? b : a;
}
