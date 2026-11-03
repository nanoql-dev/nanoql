// SPDX-FileCopyrightText: Copyright (C) 2025 William Day
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/analyticsoftbarrierengine.hpp ql/pricingengines/barrier/analyticsoftbarrierengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for European soft barrier options: Haug's closed form ("The Complete Guide to Option Pricing
/// Formulas", 2nd ed., after Hart and Ross, 1994), and the soft barrier's definition, the average of the
/// single-barrier options with barriers in [L, U], where that form doesn't hold.
/// </summary>
/// <remarks>
/// <para>
/// The value alone, as QuantLib's. A soft barrier option is the average of the single-barrier options with barriers in
/// [L, U], as upstream's own test states it (#2883's <c>testSoftBarrierDirection</c>); its knock-in is computed here as
/// that average of <see cref="AnalyticBarrierEngine"/>'s standard knock-ins, (1/(U − L))∫ DI(H) dH, the barriers on the
/// spot's far side counted as knocked in, by the adaptive Gauss-Kronrod rule split at the strike, the spot and the
/// forward and at one to thirty transition widths either side of the last two, at the tolerance's measure on the
/// average, and its knock-out as the vanilla less it.
/// </para>
/// <para>
/// Haug's form integrates the standard knock-in's branch for a barrier at or below the strike, so it is the average
/// only for a down barrier on a call with U ≤ K, or an up barrier on a put with L ≥ K, the spot outside the band; it is
/// taken there, with QuantLib's bits, where it is finite, its carry b = r − q is 2e-3 or more from 0 and from −σ²,
/// where its terms cancel, and the band's relative width (U − L)/U is 3e-3 or more, below which its difference quotient
/// cancels. Elsewhere the average is taken (documented divergences; <c>Divergences/SoftBarrier*Tests.cs</c>):
/// QuantLib's nudge of the rate at r ≈ q, its rebuild of flat Act/360 curves for a band narrower than 1e-4 in price,
/// the NaN of its overflowing powers at small volatility, and its values outside the form's domain are superseded.
/// Upstream's refusal of the directions the form doesn't cover (#2883) isn't taken: they are priced by the average
/// (<c>Divergences/BarrierUpstreamFixesTests.cs</c>). A band of zero width is the analytic barrier engine's single
/// barrier. QuantLib's limits on the rates, which no formula needs, aren't ported. Each rate is read on the
/// volatility's clock and each time is the volatility's, as the analytic barrier engine reads them.
/// </para>
/// <para>
/// The exercise must be European, as the option's documentation states and the average's engine requires; QuantLib's
/// engine doesn't check it and prices any exercise to its last date (<c>Divergences/SoftBarrierExerciseTests.cs</c>).
/// </para>
/// <para>
/// Haug's form and the average are first-passage closed forms in equivalent-constant rates and a volatility, the
/// variance and discounts to expiry: no exact closed form exists for a first passage under general deterministic term
/// structures, and on shaped markets they miss the average of <see cref="FdBlackScholesBarrierEngine"/>'s single-barrier
/// knock-ins by -0.8% to +1.5%; that average is the term-structure-exact route.
/// </para>
/// </remarks>
public static class AnalyticSoftBarrierEngine
{
    // the distance of the carry from Haug's singular points, and the band's relative width, under which the average is
    // taken
    private const double SingularCarry = 2e-3;
    private const double NarrowBand = 3e-3;

    // The integration's tolerance on the average, the vector tolerance's measure, and the cap
    private const double AverageTolerance = 1e-10;
    private const int MaxEvaluations = 100000;

    // the transition widths, in H·σ√T, the average is split at either side of the spot and the forward
    private static readonly double[] TransitionWidths = [1.0, 3.0, 10.0, 30.0];

    private static readonly CumulativeNormalDistribution F = new();

    /// <summary>Prices a soft barrier option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results: the value; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't plain vanilla, the exercise isn't European, or QuantLib's checks of its inputs refuse them
    /// (a non-positive spot, strike, time or volatility; barriers not positive, or the upper below the lower).
    /// </exception>
    public static OneAssetOptionResults Calculate(
        SoftBarrierOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
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

    internal static OneAssetOptionResults Body(SoftBarrierOption option, GeneralizedBlackScholesProcess process)
    {
        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("only european style option are supported");
        }

        Date lastDate = option.Exercise.LastDate();
        BlackVolTermStructure vol = process.BlackVolatility;
        double s = process.X0;
        double x = payoff.Strike;
        double r = AnalyticBarrierEngine.RateOnVolatilityClock(process.RiskFreeRate, vol, lastDate);
        double q = AnalyticBarrierEngine.RateOnVolatilityClock(process.DividendYield, vol, lastDate);
        double sigma = vol.BlackVol(lastDate, x);

        double u = option.BarrierHi;
        double l = option.BarrierLo;
        BarrierType barrierType = option.BarrierType;

        double t = vol.TimeFromReference(lastDate);
        OptionType optionType = payoff.OptionType;
        int eta = optionType == OptionType.Call ? 1 : -1;
        double b = r - q; // cost of carry

        ValidateInputs(s, x, t, u, l, sigma);

        bool isKnockedIn = (barrierType == BarrierType.DownIn && s <= l) || (barrierType == BarrierType.UpIn && s >= u);
        bool isKnockedOut = (barrierType == BarrierType.DownOut && s <= l) || (barrierType == BarrierType.UpOut && s >= u);
        bool knockIn = barrierType is BarrierType.DownIn or BarrierType.UpIn;
        double riskFreeDiscount = process.RiskFreeRate.AccruedDiscount(lastDate);
        double dividendDiscount = process.DividendYield.AccruedDiscount(lastDate);
        double vanilla = VanillaEquivalent(payoff, s, dividendDiscount, riskFreeDiscount, sigma * Math.Sqrt(t));

        // Already knocked in
        if (isKnockedIn)
        {
            return new OneAssetOptionResults { NPV = vanilla };
        }

        // Already knocked out
        if (isKnockedOut)
        {
            return new OneAssetOptionResults { NPV = 0.0 };
        }

        double w;
        if (u == l)
        {
            // a band of zero width is the single barrier
            var single = new BarrierOption(barrierType, u, 0.0, payoff, option.Exercise);
            return new OneAssetOptionResults
            {
                NPV = Math.Max(AnalyticBarrierEngine.Body(single, process).NPV!.Value, 0.0),
            };
        }

        bool down = barrierType is BarrierType.DownIn or BarrierType.DownOut;
        bool haugDomain = down
            ? optionType == OptionType.Call && u <= x && s >= u
            : optionType == OptionType.Put && l >= x && s <= l;
        bool haug = haugDomain
            && Math.Abs(b) >= SingularCarry
            && Math.Abs(b + (sigma * sigma)) >= SingularCarry
            && (u - l) / u >= NarrowBand;
        w = haug ? KnockInValue(s, x, r, sigma, t, u, l, b, eta) : double.NaN;
        if (!double.IsFinite(w))
        {
            w = BandAverage(option, payoff, process, s, sigma, t, dividendDiscount / riskFreeDiscount, vanilla);
        }

        return new OneAssetOptionResults
        {
            NPV = knockIn
                ? w                 // knock in price
                : vanilla - w,      // knock out price
        };
    }

    // Haug's closed form for the soft knock-in, QuantLib's knockInValue
    private static double KnockInValue(
        double s, double x, double r, double sigma, double t, double u, double l, double b, int eta)
    {
        double mu = (b + (0.5 * sigma * sigma)) / (sigma * sigma);
        double sqrtT = Math.Sqrt(t);
        double lambda1 = Math.Exp(-0.5 * sigma * sigma * t * (mu + 0.5) * (mu - 0.5));
        double lambda2 = Math.Exp(-0.5 * sigma * sigma * t * (mu - 0.5) * (mu - 1.5));
        double sx = s * x;
        double logU2SX = Math.Log(u * u / sx);
        double logL2SX = Math.Log(l * l / sx);

        // Pre-computed terms
        double d1 = (logU2SX / (sigma * sqrtT)) + (mu * sigma * sqrtT);
        double d2 = d1 - ((mu + 0.5) * sigma * sqrtT);
        double d3 = (logU2SX / (sigma * sqrtT)) + ((mu - 1) * sigma * sqrtT);
        double d4 = d3 - ((mu - 0.5) * sigma * sqrtT);

        double e1 = (logL2SX / (sigma * sqrtT)) + (mu * sigma * sqrtT);
        double e2 = e1 - ((mu + 0.5) * sigma * sqrtT);
        double e3 = (logL2SX / (sigma * sqrtT)) + ((mu - 1) * sigma * sqrtT);
        double e4 = e3 - ((mu - 0.5) * sigma * sqrtT);

        double nd1 = F.Evaluate(eta * d1);
        double nd2 = F.Evaluate(eta * d2);
        double nd3 = F.Evaluate(eta * d3);
        double nd4 = F.Evaluate(eta * d4);
        double ne1 = F.Evaluate(eta * e1);
        double ne2 = F.Evaluate(eta * e2);
        double ne3 = F.Evaluate(eta * e3);
        double ne4 = F.Evaluate(eta * e4);

        // Term 1
        double term1 = eta * s * Math.Exp((b - r) * t) * Math.Pow(s, -2.0 * mu)
            * Math.Pow(sx, mu + 0.5) / (2.0 * (mu + 0.5));
        term1 *= (Math.Pow(u * u / sx, mu + 0.5) * nd1) - (lambda1 * nd2)
            - (Math.Pow(l * l / sx, mu + 0.5) * ne1) + (lambda1 * ne2);

        // Term 2
        double term2 = eta * x * Math.Exp(-r * t) * Math.Pow(s, -2.0 * (mu - 1))
            * Math.Pow(sx, mu - 0.5) / (2.0 * (mu - 0.5));
        term2 *= (Math.Pow(u * u / sx, mu - 0.5) * nd3) - (lambda2 * nd4)
            - (Math.Pow(l * l / sx, mu - 0.5) * ne3) + (lambda2 * ne4);

        // Final value
        return (1.0 / (u - l)) * (term1 - term2);
    }

    // The soft knock-in as its definition, the average of the standard knock-ins over the band: ∫₀¹ DI(L + h(U − L))dh
    // over the analytic barrier engine, the barriers beyond the spot knocked in, split at the strike, the spot, the
    // forward and their transition widths, at 1e-10 × max(vanilla, 1), shared evenly by the pieces
    private static double BandAverage(
        SoftBarrierOption option,
        PlainVanillaPayoff payoff,
        GeneralizedBlackScholesProcess process,
        double spot,
        double sigma,
        double t,
        double growth,
        double vanilla)
    {
        double l = option.BarrierLo;
        double u = option.BarrierHi;
        bool down = option.BarrierType is BarrierType.DownIn or BarrierType.DownOut;
        BarrierType knockInType = down ? BarrierType.DownIn : BarrierType.UpIn;
        double forward = spot * growth;
        double sd = sigma * Math.Sqrt(t);

        double KnockIn(double h)
        {
            double barrier = l + (h * (u - l));
            bool touched = down ? barrier >= spot : barrier <= spot;
            if (touched)
            {
                return vanilla;
            }

            var standard = new BarrierOption(knockInType, barrier, 0.0, payoff, option.Exercise);
            return AnalyticBarrierEngine.Body(standard, process).NPV!.Value;
        }

        var points = new SortedSet<double> { 0.0, 1.0 };
        void Add(double barrier)
        {
            double h = (barrier - l) / (u - l);
            if (h > 0.0 && h < 1.0)
            {
                points.Add(h);
            }
        }

        Add(payoff.Strike);
        foreach (double centre in new[] { spot, forward })
        {
            Add(centre);
            foreach (double k in TransitionWidths)
            {
                Add(centre * (1.0 - (k * sd)));
                Add(centre * (1.0 + (k * sd)));
            }
        }

        double[] h = [.. points];
        int pieces = h.Length - 1;
        var rule = new GaussKronrodAdaptive(AverageTolerance * Math.Max(vanilla, 1.0) / pieces, MaxEvaluations);
        double average = 0.0;
        for (int i = 0; i < pieces; ++i)
        {
            average += rule.Integrate(KnockIn, h[i], h[i + 1]);
        }

        return average;
    }

    // QuantLib's validateInputs, but for its limits on the rates, which no formula needs
    private static void ValidateInputs(double s, double x, double t, double u, double l, double sigma)
    {
        if (!(s > 0.0))
        {
            throw new NanoQLException("Spot price must be > 0");
        }

        if (!(x > 0.0))
        {
            throw new NanoQLException("Strike price must be > 0");
        }

        if (!(t > 0.0))
        {
            throw new NanoQLException("Option must have time to maturity > 0");
        }

        if (!(sigma > 0))
        {
            throw new NanoQLException("Volatility must be > 0");
        }

        if (!(u > 0.0 && l > 0.0))
        {
            throw new NanoQLException("Barrier levels must be positive");
        }

        if (!(u >= l))
        {
            throw new NanoQLException("Upper barrier must be greater than or equal to lower barrier");
        }
    }

    // QuantLib's vanillaEquivalent: the European, floored at 0
    private static double VanillaEquivalent(
        PlainVanillaPayoff payoff, double spot, double dividendDiscount, double riskFreeDiscount, double stdDeviation)
    {
        double forwardPrice = spot * dividendDiscount / riskFreeDiscount;
        var black = new BlackCalculator(payoff, forwardPrice, stdDeviation, riskFreeDiscount);
        double vanilla = black.Value;
        return Math.Max(vanilla, 0.0);
    }
}
