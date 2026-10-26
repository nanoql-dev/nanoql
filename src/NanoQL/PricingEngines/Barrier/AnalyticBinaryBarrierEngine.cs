// SPDX-FileCopyrightText: Copyright (C) 2014 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/analyticbinarybarrierengine.hpp ql/pricingengines/barrier/analyticbinarybarrierengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for binary barrier options: a cash-or-nothing or asset-or-nothing payoff at expiry, knocked in
/// or out at a barrier, by Haug's formulas ("The complete guide to option pricing formulas", 2nd ed., p. 176 and
/// following).
/// </summary>
/// <remarks>
/// <para>
/// The option's exercise is American with its payoff at expiry, its window open from the start, as in QuantLib. The
/// engine reads its discounts and its variance by date, so curves on other day counters or dates price as on one
/// clock. The formulas are first-passage closed forms in one equivalent-constant carry and volatility, the variance
/// and discounts to expiry: no exact closed form exists for a first passage under general deterministic term
/// structures, and on shaped markets they miss QuantLib's finite-difference barrier engine or a Monte Carlo by -1.2% to
/// +1.7% (the S-bar plan's BR4 (d)); the term-structure-exact routes are <see cref="FdBlackScholesBarrierEngine"/>
/// and the Monte Carlo engines.
/// </para>
/// <para>
/// Two documented divergences. At a variance below machine epsilon the underlying follows its forward, and hits the
/// barrier, if at all, when the forward reaches it, as QuantLib's master prices the analytic barrier engine
/// (<c>e29c64126</c>), where QuantLib's indicators read the spot and its power of the barrier overflows to 0 (BR5 (xv),
/// <c>Divergences/BinaryBarrierZeroVolatilityTests.cs</c>). And the option's rebate is priced, a knock-out's at the
/// hit, now where the barrier is already touched, and a knock-in's at expiry, by the analytic barrier engine's rebate
/// terms, where QuantLib's engine drops it (<c>Divergences/BinaryBarrierRebateTests.cs</c>).
/// </para>
/// </remarks>
public static class AnalyticBinaryBarrierEngine
{
    /// <summary>Prices a binary barrier option.</summary>
    /// <param name="option">The option, with a cash-or-nothing or asset-or-nothing payoff and an American exercise
    /// paying at expiry.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results: the value, and the Greeks QuantLib sets where the barrier is already touched;
    /// the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't American, doesn't pay at expiry or opens after the volatility's reference date; the payoff
    /// isn't striked; the spot or the barrier isn't positive.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        BarrierOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(option, process));
    }

    internal static OneAssetOptionResults Body(BarrierOption option, GeneralizedBlackScholesProcess process)
    {
        if (option.Exercise is not AmericanExercise ex)
        {
            throw new NanoQLException("non-American exercise given");
        }

        if (!ex.PayoffAtExpiry)
        {
            throw new NanoQLException("payoff must be at expiry");
        }

        if (!(ex.Dates[0] <= process.BlackVolatility.ReferenceDate))
        {
            throw new NanoQLException("American option with window exercise not handled yet");
        }

        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        double spot = process.StateVariable;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        // nothing accrues before a structure's reference date, where QuantLib's reads throw
        double variance = process.BlackVolatility.AccruedVariance(ex.LastDate(), payoff.Strike);
        double barrier = option.Barrier;
        if (!(barrier > 0.0))
        {
            throw new NanoQLException("positive barrier value required");
        }

        BarrierType barrierType = option.BarrierType;

        if ((barrierType == BarrierType.DownOut && spot <= barrier)
            || (barrierType == BarrierType.UpOut && spot >= barrier))
        {
            // knocked out: its rebate, paid at the hit, now, where QuantLib's has no value
            return new OneAssetOptionResults
            {
                NPV = option.Rebate,
                Delta = 0.0,
                Gamma = 0.0,
                Vega = 0.0,
                Theta = 0.0,
                Rho = 0.0,
                DividendRho = 0.0,
            };
        }

        if ((barrierType == BarrierType.DownIn && spot <= barrier)
            || (barrierType == BarrierType.UpIn && spot >= barrier))
        {
            // knocked in - is a digital european
            var exercise = new EuropeanExercise(option.Exercise.LastDate());
            OneAssetOptionResults european = AnalyticEuropeanEngine.Body(
                new VanillaOption(payoff, exercise), process, process.RiskFreeRate);
            return new OneAssetOptionResults
            {
                NPV = european.NPV,
                Delta = european.Delta,
                Gamma = european.Gamma,
                Vega = european.Vega,
                Theta = european.Theta,
                Rho = european.Rho,
                DividendRho = european.DividendRho,
            };
        }

        double riskFreeDiscount = process.RiskFreeRate.AccruedDiscount(ex.LastDate());
        double value = PayoffAtExpiry(option, process, payoff, spot, variance, riskFreeDiscount);

        // the rebate QuantLib's engine drops: the analytic barrier engine's rebate terms
        if (option.Rebate != 0.0)
        {
            value += AnalyticBarrierEngine.RebateValue(option, process, payoff.Strike);
        }

        return new OneAssetOptionResults { NPV = value };
    }

    // QuantLib's helper's payoffAtExpiry
    private static double PayoffAtExpiry(
        BarrierOption option,
        GeneralizedBlackScholesProcess process,
        StrikedTypePayoff payoff,
        double spot,
        double variance,
        double discount)
    {
        Date lastDate = option.Exercise.LastDate();
        double dividendDiscount = process.DividendYield.AccruedDiscount(lastDate);

        if (!(spot > 0.0))
        {
            throw new NanoQLException("positive spot value required");
        }

        if (!(discount > 0.0))
        {
            throw new NanoQLException("positive discount required");
        }

        if (!(dividendDiscount > 0.0))
        {
            throw new NanoQLException("positive dividend discount required");
        }

        if (!(variance >= 0.0))
        {
            throw new NanoQLException("negative variance not allowed");
        }

        OptionType type = payoff.OptionType;
        double strike = payoff.Strike;
        double barrier = option.Barrier;
        if (!(barrier > 0.0))
        {
            throw new NanoQLException("positive barrier value required");
        }

        BarrierType barrierType = option.BarrierType;

        if (variance < NumericLimits.QL_EPSILON)
        {
            return ZeroVarianceValue(barrierType, payoff, spot, barrier, discount, dividendDiscount);
        }

        double stdDev = Math.Sqrt(variance);
        double mu = (Math.Log(dividendDiscount / discount) / variance) - 0.5;
        double k = 0;

        // binary cash-or-nothing payoff?
        if (payoff is CashOrNothingPayoff coo)
        {
            k = coo.CashPayoff;
        }

        // binary asset-or-nothing payoff?
        if (payoff is AssetOrNothingPayoff)
        {
            mu += 1.0;
            k = spot * dividendDiscount / discount; // forward
        }

        double logSX = Math.Log(spot / strike);
        double logSH = Math.Log(spot / barrier);
        double logHS = Math.Log(barrier / spot);
        double logH2SX = Math.Log(barrier * barrier / (spot * strike));
        double hS2mu = Math.Pow(barrier / spot, 2 * mu);

        double eta = barrierType is BarrierType.DownIn or BarrierType.DownOut ? 1.0 : -1.0;
        double phi = type == OptionType.Call ? 1.0 : -1.0;

        // we calculate using mu*stddev instead of (mu+1)*stddev
        // because cash-or-nothing don't need it. asset-or-nothing
        // mu is really mu+1
        double x1 = phi * ((logSX / stdDev) + (mu * stdDev));
        double x2 = phi * ((logSH / stdDev) + (mu * stdDev));
        double y1 = eta * ((logH2SX / stdDev) + (mu * stdDev));
        double y2 = eta * ((logHS / stdDev) + (mu * stdDev));

        var f = new CumulativeNormalDistribution();
        double cumX1 = f.Evaluate(x1);
        double cumX2 = f.Evaluate(x2);
        double cumY1 = f.Evaluate(y1);
        double cumY2 = f.Evaluate(y2);

        // H_S_2mu can overflow when mu is large (e.g. low volatility). In that case use the log representation
        // to compute the products with the cumulative normals without overflow.
        if (!double.IsFinite(hS2mu))
        {
            cumY1 = Math.Exp((2.0 * mu * logHS) + LogCumNormal(y1));
            cumY2 = Math.Exp((2.0 * mu * logHS) + LogCumNormal(y2));
            hS2mu = 1.0;
        }

        double alpha = barrierType switch
        {
            BarrierType.DownIn => type == OptionType.Call
                // down-in and call
                ? strike >= barrier ? hS2mu * cumY1 : cumX1 - cumX2 + (hS2mu * cumY2)
                // down-in and put
                : strike >= barrier ? cumX2 + (hS2mu * (-cumY1 + cumY2)) : cumX1,
            BarrierType.UpIn => type == OptionType.Call
                // up-in and call
                ? strike >= barrier ? cumX1 : cumX2 + (hS2mu * (-cumY1 + cumY2))
                // up-in and put
                : strike >= barrier ? cumX1 - cumX2 + (hS2mu * cumY2) : hS2mu * cumY1,
            BarrierType.DownOut => type == OptionType.Call
                // down-out and call
                ? strike >= barrier ? cumX1 - (hS2mu * cumY1) : cumX2 - (hS2mu * cumY2)
                // down-out and put
                : strike >= barrier ? cumX1 - cumX2 + (hS2mu * (cumY1 - cumY2)) : 0,
            BarrierType.UpOut => type == OptionType.Call
                // up-out and call
                ? strike >= barrier ? 0 : cumX1 - cumX2 + (hS2mu * (cumY1 - cumY2))
                // up-out and put
                : strike >= barrier ? cumX2 - (hS2mu * cumY2) : cumX1 - (hS2mu * cumY1),
            _ => throw new NanoQLException("invalid barrier type"),
        };

        return discount * k * alpha;
    }

    // QuantLib's master's zero-volatility reading (e29c64126), as the analytic barrier engine takes it: the underlying
    // follows its forward and hits the barrier, if at all, when the forward reaches it, a spot on the barrier a hit
    // now; the payoff at expiry is the forward's (the S-bar plan's BR5 (xv))
    private static double ZeroVarianceValue(
        BarrierType barrierType,
        StrikedTypePayoff payoff,
        double spot,
        double barrier,
        double discount,
        double dividendDiscount)
    {
        double forward = spot * dividendDiscount / discount;
        bool down = barrierType is BarrierType.DownIn or BarrierType.DownOut;
        bool hit = (spot == barrier) || (down ? forward <= barrier : forward >= barrier);
        double atExpiry = discount * payoff.Evaluate(forward);
        bool knockIn = barrierType is BarrierType.DownIn or BarrierType.UpIn;
        return knockIn == hit ? atExpiry : 0.0;
    }

    // log(N(x)) for x where N(x) underflows; asymptotic expansion for x < -30
    private static double LogCumNormal(double x)
    {
        if (x > -30.0)
        {
            return Math.Log(new CumulativeNormalDistribution().Evaluate(x));
        }

        double z2 = 1.0 / (x * x);
        return (-0.5 * x * x) - Math.Log(-x) - (0.5 * Math.Log(2.0 * Math.PI))
            + StdLog1p(-z2 * (1.0 - (z2 * (3.0 - (15.0 * z2)))));
    }

    // std::log1p, which .NET doesn't provide (double.LogP1 computes Math.Log(1 + x), which loses x's low digits): the
    // logarithm of u = 1 + x, rounded, scaled by x/(u − 1), Goldberg's Theorem 4
    private static double StdLog1p(double x)
    {
        double u = 1.0 + x;
        if (u == 1.0)
        {
            return x;
        }

        if (double.IsPositiveInfinity(u))
        {
            return Math.Log(u);
        }

        return Math.Log(u) * x / (u - 1.0);
    }
}
