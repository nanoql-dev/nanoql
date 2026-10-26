// SPDX-FileCopyrightText: Copyright (C) 2012 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/analytictwoassetbarrierengine.hpp ql/pricingengines/barrier/analytictwoassetbarrierengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for a barrier option on two assets: the first asset's payoff, knocked in or out by the second
/// asset at the barrier (Haug, "The Complete Guide to Option Pricing Formulas", 2nd ed.).
/// </summary>
/// <remarks>
/// <para>
/// The value alone, as QuantLib's. The bivariate normal is QuantLib's default,
/// <see cref="BivariateCumulativeNormalDistributionWe04DP"/>, where QuantLib's engine reads Drezner's 1978 algorithm,
/// to six decimal places (the S-bar plan's BR4 (e), the S-exo plan's ER7); on QuantLib's cases the values move by at
/// most 6.7e-6, within QuantLib's tolerance.
/// </para>
/// <para>
/// Five documented divergences (BR5). Each asset's carry reads its own process's curves and the payoff is discounted on
/// the first process's risk-free curve, where QuantLib takes the second asset's carry from the first process's rate
/// (BR5 (vii), the S-exo plan's ED27; <c>Divergences/TwoAssetBarrierTwoCurvesTests.cs</c>). The knock-in's vanilla
/// carries the first asset's dividends, S₁e^((b₁−r)T)N(d₁), where QuantLib's drops them, so in-out parity holds
/// (BR5 (viii), <c>Divergences/TwoAssetBarrierDividendTests.cs</c>). The second asset's volatility is read at the
/// barrier, its own price level, where QuantLib reads it at the first asset's strike (BR5 (ix),
/// <c>Divergences/TwoAssetBarrierSmileTests.cs</c>). And every rate and volatility is read over the time to expiry on
/// the clock across both processes (<see cref="FdmBlackScholesOp.ClockAcross"/>), each structure at the instants that
/// time denotes, accruing nothing before its date, where QuantLib reads everything at the first process's time; a
/// structure sharing the clock's times is read as QuantLib reads it (BR5 (i), the S-exo plan's ED10;
/// <c>Divergences/BarrierClosedFormMismatchedTimesTests.cs</c>). And a non-European exercise is refused, as the
/// family's European engines refuse it, where QuantLib prices it to its last date
/// (<c>Divergences/TwoAssetBarrierExerciseTests.cs</c>).
/// </para>
/// <para>
/// The formula is a first-passage closed form in equivalent-constant rates and volatilities, the variance and
/// discounts to expiry: no exact closed form exists for a first passage under general deterministic term structures,
/// and its single correlation term, ρσ₁σ₂, can't carry a first volatility that moves over the option's life. On shaped
/// markets (1.5 years; forward rates 1%, 3%, 6%, dividends 0%, 1%, 3% and volatilities 15%, 25%, 35%, rising or
/// falling; ρ 0.5) it misses an independent Monte Carlo by −24% to +21%, and by 3% to 7% with the first volatility
/// flat, where on a flat market it agrees (the S-bar plan's BR4 (d), the user's ruling of 2026-10-26). A Monte Carlo on
/// the two processes is the term-structure-exact route.
/// </para>
/// </remarks>
public static class AnalyticTwoAssetBarrierEngine
{
    private static readonly CumulativeNormalDistribution N = new();

    /// <summary>Prices a barrier option on two assets.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process1">The first asset's process, whose payoff the strike reads.</param>
    /// <param name="process2">The second asset's process, which the barrier monitors.</param>
    /// <param name="rho">The correlation of the two assets.</param>
    /// <returns>The option's results: the value.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla or its strike isn't positive, the second spot isn't
    /// positive, or it lies beyond the barrier ("barrier touched").
    /// </exception>
    public static InstrumentResults Calculate(
        TwoAssetBarrierOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process1,
        GeneralizedBlackScholesProcess process2,
        double rho)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process1);
        ArgumentNullException.ThrowIfNull(process2);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new InstrumentResults { NPV = Value(option, process1, process2, rho) });
    }

    // QuantLib's calculate()
    private static double Value(
        TwoAssetBarrierOption option,
        GeneralizedBlackScholesProcess process1,
        GeneralizedBlackScholesProcess process2,
        double rho)
    {
        // QuantLib never reads the exercise's type and prices any exercise to its last date: the formula prices a
        // European exercise only (the S-bar code review's B1-3)
        if (option.Exercise.Type != ExerciseType.European)
        {
            throw new NanoQLException("only european style option are supported");
        }

        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        if (!(payoff.Strike > 0.0))
        {
            throw new NanoQLException("strike must be positive");
        }

        double spot2 = process2.X0;
        if (!(spot2 > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        if (option.Triggered(spot2))
        {
            throw new NanoQLException("barrier touched");
        }

        var f = new Formulas(option, payoff, process1, process2, rho);
        BarrierType barrierType = option.BarrierType;
        return payoff.OptionType switch
        {
            OptionType.Call => barrierType switch
            {
                BarrierType.DownOut => f.A(1, -1) + B(),
                BarrierType.UpOut => f.A(1, 1) + B(),
                BarrierType.DownIn => f.Call() - (f.A(1, -1) + B()),
                _ => f.Call() - (f.A(1, 1) + B()),
            },
            OptionType.Put => barrierType switch
            {
                BarrierType.DownOut => f.A(-1, -1) + B(),
                BarrierType.UpOut => f.A(-1, 1) + B(),
                BarrierType.DownIn => f.Put() - (f.A(-1, -1) + B()),
                _ => f.Put() - (f.A(-1, 1) + B()),
            },
            _ => throw new NanoQLException("unknown type"),
        };
    }

    // QuantLib's B(eta, phi), which returns 0
    private static double B() => 0.0;

    // QuantLib's members, each read once on the clock across both processes
    private sealed class Formulas
    {
        private readonly double s1;
        private readonly double s2;
        private readonly double x;
        private readonly double h;
        private readonly double bigT;
        private readonly double sigma1;
        private readonly double sigma2;
        private readonly double r;
        private readonly double b1;
        private readonly double b2;
        private readonly double rho;

        internal Formulas(
            TwoAssetBarrierOption option,
            PlainVanillaPayoff payoff,
            GeneralizedBlackScholesProcess process1,
            GeneralizedBlackScholesProcess process2,
            double rho)
        {
            this.rho = rho;
            s1 = process1.X0;
            s2 = process2.X0;
            x = payoff.Strike;
            h = option.Barrier;

            // the time to expiry on the clock across both processes, every structure read at it (BR5 (i))
            TermStructure clock = FdmBlackScholesOp.ClockAcross([process1, process2]);
            bigT = clock.TimeFromReference(option.Exercise.LastDate());

            // the first asset's volatility at the strike, the second's at the barrier, its own level (BR5 (ix))
            sigma1 = Volatility(process1.BlackVolatility, clock, bigT, x);
            sigma2 = Volatility(process2.BlackVolatility, clock, bigT, h);

            // each asset's carry on its own process's curves, the discount on the first's (BR5 (vii))
            r = ZeroRate(process1.RiskFreeRate, clock, bigT);
            b1 = r - ZeroRate(process1.DividendYield, clock, bigT);
            b2 = ZeroRate(process2.RiskFreeRate, clock, bigT) - ZeroRate(process2.DividendYield, clock, bigT);
        }

        // the first asset's vanilla call, its dividends carried (BR5 (viii)), where QuantLib's S₁N(d₁) drops them
        internal double Call() => (s1 * Math.Exp((b1 - r) * bigT) * N.Evaluate(D1()))
            - (x * Math.Exp(-r * bigT) * N.Evaluate(D2()));

        internal double Put() => (x * Math.Exp(-r * bigT) * N.Evaluate(-D2()))
            - (s1 * Math.Exp((b1 - r) * bigT) * N.Evaluate(-D1()));

        internal double A(double eta, double phi)
        {
            double mu1 = b1 - (sigma1 * sigma1 / 2.0);
            double mu2 = b2 - (sigma2 * sigma2 / 2.0);

            double d1 = (Math.Log(s1 / x) + ((mu1 + (sigma1 * sigma1)) * bigT)) / (sigma1 * Math.Sqrt(bigT));
            double d2 = d1 - (sigma1 * Math.Sqrt(bigT));
            double d3 = d1 + (2 * rho * Math.Log(h / s2) / (sigma2 * Math.Sqrt(bigT)));
            double d4 = d2 + (2 * rho * Math.Log(h / s2) / (sigma2 * Math.Sqrt(bigT)));

            double e1 = (Math.Log(h / s2) - ((mu2 + (rho * sigma1 * sigma2)) * bigT)) / (sigma2 * Math.Sqrt(bigT));
            double e2 = e1 + (rho * sigma1 * Math.Sqrt(bigT));
            double e3 = e1 - (2 * Math.Log(h / s2) / (sigma2 * Math.Sqrt(bigT)));
            double e4 = e2 - (2 * Math.Log(h / s2) / (sigma2 * Math.Sqrt(bigT)));

            double w =
                (eta * s1 * Math.Exp((b1 - r) * bigT)
                    * (M(eta * d1, phi * e1, -eta * phi * rho)
                        - (Math.Exp(2 * (mu2 + (rho * sigma1 * sigma2)) * Math.Log(h / s2) / (sigma2 * sigma2))
                            * M(eta * d3, phi * e3, -eta * phi * rho))))
                - (eta * x * Math.Exp(-r * bigT)
                    * (M(eta * d2, phi * e2, -eta * phi * rho)
                        - (Math.Exp(2 * mu2 * Math.Log(h / s2) / (sigma2 * sigma2))
                            * M(eta * d4, phi * e4, -eta * phi * rho))));

            return w;
        }

        // QuantLib's d1(): mu(b1, sigma1) + sigma1²
        private double D1() => (Math.Log(s1 / x) + ((b1 - (sigma1 * sigma1 / 2) + (sigma1 * sigma1)) * bigT))
            / (sigma1 * Math.Sqrt(bigT));

        private double D2() => D1() - (sigma1 * Math.Sqrt(bigT));

        // QuantLib's M, on We04DP where QuantLib's reads Dr78 (BR4 (e))
        private static double M(double a, double b, double correlation) =>
            new BivariateCumulativeNormalDistributionWe04DP(correlation).Evaluate(a, b);

        // QuantLib's blackVol(T, strike) where the volatility shares the clock's times; otherwise the volatility of
        // the variance it has accrued by the clock's time T
        private static double Volatility(BlackVolTermStructure volatility, TermStructure clock, double bigT, double strike) =>
            volatility.SharesTimes(clock)
                ? volatility.BlackVol(bigT, strike)
                : Math.Sqrt(volatility.BlackForwardVarianceAt(clock, 0.0, bigT, strike) / bigT);

        // QuantLib's zeroRate(T, Continuous, NoFrequency) where the curve shares the clock's times; otherwise the
        // continuous rate of the discount it has accrued by the clock's time T
        private static double ZeroRate(YieldTermStructure curve, TermStructure clock, double bigT) =>
            curve.SharesTimes(clock)
                ? curve.ZeroRate(bigT, Compounding.Continuous, Frequency.NoFrequency).Rate
                : curve.ContinuousForwardRateAt(clock, 0.0, bigT);
    }
}
