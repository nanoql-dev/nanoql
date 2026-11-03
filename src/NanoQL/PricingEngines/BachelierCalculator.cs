// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/bacheliercalculator.hpp ql/pricingengines/bacheliercalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Instruments;
using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Bachelier calculator: the value and sensitivities of a striked payoff on a forward with a normal distribution of
/// a given standard deviation (in price units), discounted. It supports plain vanilla, cash-or-nothing,
/// asset-or-nothing and gap payoffs.
/// </summary>
/// <remarks>
/// <para>
/// Three of QuantLib's results are fixed, documented divergences:
/// </para>
/// <list type="bullet">
/// <item><description>QuantLib tells a call from a put by <c>alpha &gt;= 0</c> (<c>bacheliercalculator.cpp:102-114</c>,
/// <c>:183-188</c>), which a put's N(d) − 1 meets where N(d) rounds to 1, so a put far out of the money prices as the
/// call; here the payoff's type decides (<c>Divergences/BachelierPutBranchTests.cs</c>).</description></item>
/// <item><description>QuantLib's visitor sets alpha, beta and x for cash-or-nothing, asset-or-nothing and gap payoffs,
/// which its value and Greeks never read, so each prices as the plain vanilla; here the value is
/// D·(F·α + x·β + σ·∂α/∂d) and the Greeks its derivatives, Bachelier's closed forms for each payoff
/// (<c>Divergences/BachelierPayoffTests.cs</c>). The plain vanilla's are QuantLib's expressions.</description></item>
/// <item><description>QuantLib's theta reads the spot gamma where the time decay reads the forward gamma, missing
/// (S/F)²; its vanna, the sensitivity of the vega to the spot by its documentation, misses D·F/S; its volga divides by
/// σ√T where the derivative in σ divides by σ, missing √T. Each is here the derivative, the vanna taking the spot as
/// <see cref="BlackCalculator.Vanna"/> does (<c>Divergences/BachelierGreeksTests.cs</c>).</description></item>
/// </list>
/// <para>
/// Immutable. At a standard deviation within QL_EPSILON the density terms are 0, as QuantLib's: the value is the
/// discounted payoff at the forward, the gammas, vega, vanna and volga 0.
/// </para>
/// </remarks>
public sealed class BachelierCalculator
{
    // QuantLib's M_SQRT_2 (1/√2) and M_1_SQRTPI (1/√π).
    private const double M_SQRT_2 = 0.7071067811865475244008443621048490392848359376887;
    private const double M_1_SQRTPI = 0.564189583547756286948;

    private readonly double strike, forward, stdDev, discount, variance;
    private readonly double d;
    private readonly double alpha, beta, dAlphaDd, dBetaDd;
    private readonly double nD, cumD;
    private readonly double x, dXDstrike;
    private readonly OptionType optionType;
    private readonly bool plainVanilla;

    /// <summary>Initializes a new instance of the <see cref="BachelierCalculator"/> class for a striked payoff.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation of the underlying at expiry, in price units.</param>
    /// <param name="discount">The discount factor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The standard deviation is negative, the discount isn't positive, or the payoff isn't a plain vanilla,
    /// cash-or-nothing, asset-or-nothing or gap payoff.
    /// </exception>
    public BachelierCalculator(StrikedTypePayoff payoff, double forward, double stdDev, double discount = 1.0)
        : this(PayoffOf(payoff), payoff.Strike, forward, stdDev, discount)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BachelierCalculator"/> class for a plain vanilla payoff.
    /// </summary>
    /// <param name="optionType">The option's type.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation of the underlying at expiry, in price units.</param>
    /// <param name="discount">The discount factor.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The standard deviation is negative, or the discount isn't positive.</exception>
    public BachelierCalculator(OptionType optionType, double strike, double forward, double stdDev, double discount = 1.0)
        : this(new PlainVanillaPayoff(optionType, strike), strike, forward, stdDev, discount)
    {
    }

    private BachelierCalculator(StrikedTypePayoff p, double strike, double forward, double stdDev, double discount)
    {
        this.strike = strike;
        this.forward = forward;
        this.stdDev = stdDev;
        this.discount = discount;
        variance = stdDev * stdDev;

        // initialize
        if (!(stdDev >= 0.0))
        {
            throw new ArgumentException($"stdDev ({Show(stdDev)}) must be non-negative", nameof(stdDev));
        }

        if (!(discount > 0.0))
        {
            throw new ArgumentException($"discount ({Show(discount)}) must be positive", nameof(discount));
        }

        // For Bachelier model, we use d = (F - K) / σ instead of the Black-Scholes d1, d2
        if (stdDev >= NumericLimits.QL_EPSILON)
        {
            // Bachelier d parameter: d = (F - K) / σ
            d = (forward - strike) / stdDev;

            var f = new CumulativeNormalDistribution();
            cumD = f.Evaluate(d);
            nD = f.Derivative(d);
        }
        else
        {
            // When volatility is zero
            if (ComparisonFunctions.Close(forward, strike))
            {
                d = 0;
                cumD = 0.5;
                nD = M_SQRT_2 * M_1_SQRTPI;
            }
            else if (forward > strike)
            {
                d = NumericLimits.QL_MAX_REAL;
                cumD = 1.0;
                nD = 0.0;
            }
            else
            {
                d = NumericLimits.QL_MIN_REAL;
                cumD = 0.0;
                nD = 0.0;
            }
        }

        x = strike;
        dXDstrike = 1.0;

        optionType = p.OptionType;
        switch (p.OptionType)
        {
            case OptionType.Call:
                alpha = cumD;        // N(d)
                dAlphaDd = nD;       // n(d)
                beta = -cumD;        // -N(d) - base part
                dBetaDd = -nD;       // -n(d)
                break;
            default:
                alpha = cumD - 1.0;  // N(d) - 1 = -N(-d)
                dAlphaDd = nD;       // n(d)
                beta = 1.0 - cumD;   // 1 - N(d) = N(-d)
                dBetaDd = -nD;       // -n(d)
                break;
        }

        // now dispatch on type.
        switch (p)
        {
            case PlainVanillaPayoff:
                plainVanilla = true;
                break;
            case CashOrNothingPayoff payoff:
                alpha = dAlphaDd = 0.0;
                x = payoff.CashPayoff;
                dXDstrike = 0.0;
                if (payoff.OptionType == OptionType.Call)
                {
                    beta = cumD;
                    dBetaDd = nD;
                }
                else
                {
                    beta = 1.0 - cumD;
                    dBetaDd = -nD;
                }

                break;
            case AssetOrNothingPayoff payoff:
                beta = dBetaDd = 0.0;
                if (payoff.OptionType == OptionType.Call)
                {
                    alpha = cumD;
                    dAlphaDd = nD;
                }
                else
                {
                    alpha = 1.0 - cumD;
                    dAlphaDd = -nD;
                }

                break;
            case GapPayoff payoff:
                x = payoff.SecondStrike;
                dXDstrike = 0.0;
                break;
            default:
                throw new ArgumentException($"unsupported payoff type: {p.Name}", "payoff");
        }
    }

    /// <summary>Gets the value: the plain vanilla's Bachelier formula, otherwise D·(F·α + x·β + σ·∂α/∂d).</summary>
    public double Value
    {
        get
        {
            if (!plainVanilla)
            {
                return discount * ((forward * alpha) + (x * beta) + (Density * dAlphaDd));
            }

            // Bachelier option value formula:
            // Call: (F-K)*N(d) + σ*n(d)
            // Put:  (K-F)*N(-d) + σ*n(d)
            // where d = (F-K)/σ
            double intrinsic = forward - strike;
            double timeValue = 0.0;

            if (stdDev > NumericLimits.QL_EPSILON)
            {
                timeValue = stdDev * nD;
            }

            double result = optionType == OptionType.Call
                ? (intrinsic * cumD) + timeValue
                : (-intrinsic * (1.0 - cumD)) + timeValue;
            return discount * Max(result, 0.0);
        }
    }

    /// <summary>Gets the sensitivity to the forward.</summary>
    public double DeltaForward
    {
        get
        {
            if (plainVanilla)
            {
                // Delta_Forward = dV/dF = N(d) for calls, -N(-d) for puts
                return optionType == OptionType.Call ? discount * cumD : discount * (cumD - 1.0);
            }

            return discount * (alpha + (WeightedDensity / DensityStdDev));
        }
    }

    /// <summary>Gets the sensitivity in percent to a percent change in the forward.</summary>
    public double ElasticityForward
    {
        get
        {
            double val = Value;
            double del = DeltaForward;
            return ElasticityOf(val, del, forward);
        }
    }

    /// <summary>Gets the second-order sensitivity to the forward.</summary>
    public double GammaForward
    {
        get
        {
            if (stdDev <= NumericLimits.QL_EPSILON)
            {
                return 0.0;
            }

            if (plainVanilla)
            {
                return discount * nD / stdDev;
            }

            return discount * ((dAlphaDd / stdDev) - (d * WeightedDensity / variance));
        }
    }

    /// <summary>Gets the probability of being in the money in the bond martingale measure: N(d), 1 − N(d) for a
    /// put.</summary>
    public double ItmCashProbability => optionType == OptionType.Put ? 1.0 - cumD : cumD;

    /// <summary>Gets the probability of being in the money in the asset martingale measure: N(d), 1 − N(d) for a
    /// put.</summary>
    public double ItmAssetProbability => optionType == OptionType.Put ? 1.0 - cumD : cumD;

    /// <summary>Gets the sensitivity to the strike.</summary>
    public double StrikeSensitivity
    {
        get
        {
            if (plainVanilla)
            {
                // dV/dK = -N(d) for calls, N(-d) for puts
                return optionType == OptionType.Call ? -discount * cumD : discount * (1.0 - cumD);
            }

            return discount * ((beta * dXDstrike) - (WeightedDensity / DensityStdDev));
        }
    }

    /// <summary>Gets the second-order sensitivity to the strike.</summary>
    public double StrikeGamma
    {
        get
        {
            if (stdDev <= NumericLimits.QL_EPSILON)
            {
                return 0.0;
            }

            if (plainVanilla)
            {
                return discount * nD / stdDev;
            }

            return discount * ((-dAlphaDd / stdDev) - (d * WeightedDensity / variance)
                - (2.0 * dXDstrike * dBetaDd / stdDev));
        }
    }

    /// <summary>Gets alpha, the forward's coefficient in the value.</summary>
    public double Alpha => alpha;

    /// <summary>Gets beta, the strike's (or cash's) coefficient in the value.</summary>
    public double Beta => beta;

    // σ where the density terms count, 0 within QL_EPSILON (QuantLib's time value).
    private double Density => stdDev > NumericLimits.QL_EPSILON ? stdDev : 0.0;

    // The forward's sensitivity terms' divisor: σ, or infinity within QL_EPSILON, where those terms are 0.
    private double DensityStdDev => stdDev > NumericLimits.QL_EPSILON ? stdDev : double.PositiveInfinity;

    // K·∂α/∂d + x·∂β/∂d, the derivatives' common term.
    private double WeightedDensity => (strike * dAlphaDd) + (x * dBetaDd);

    /// <summary>The sensitivity to the spot.</summary>
    /// <param name="spot">The spot.</param>
    /// <returns>The delta, the forward delta times F/S.</returns>
    public double Delta(double spot)
    {
        double dForwardDs = forward / spot;
        double deltaFwd = DeltaForward;

        return deltaFwd * dForwardDs;
    }

    /// <summary>The sensitivity in percent to a percent change in the spot.</summary>
    /// <param name="spot">The spot.</param>
    /// <returns>The elasticity.</returns>
    public double Elasticity(double spot)
    {
        double val = Value;
        double del = Delta(spot);
        return ElasticityOf(val, del, spot);
    }

    /// <summary>The second-order sensitivity to the spot.</summary>
    /// <param name="spot">The spot.</param>
    /// <returns>The gamma, the forward gamma times (F/S)².</returns>
    public double Gamma(double spot)
    {
        if (stdDev <= NumericLimits.QL_EPSILON)
        {
            return 0.0;
        }

        double dForwardDs = forward / spot;
        if (plainVanilla)
        {
            // QuantLib's order: D·(n(d)/σ)·(F/S)²
            double gammaForward = nD / stdDev;
            return discount * gammaForward * dForwardDs * dForwardDs;
        }

        return GammaForward * dForwardDs * dForwardDs;
    }

    /// <summary>The sensitivity to the time to maturity.</summary>
    /// <param name="spot">The spot.</param>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The theta; 0 at a maturity close to 0.</returns>
    /// <exception cref="ArgumentException">The maturity is negative.</exception>
    public double Theta(double spot, double maturity)
    {
        if (!(maturity >= 0.0))
        {
            throw new ArgumentException($"maturity ({Show(maturity)}) must be non-negative", nameof(maturity));
        }

        if (ComparisonFunctions.Close(maturity, 0.0))
        {
            return 0.0;
        }

        // Theta = -dV/dt = -(r*V - r*S*Delta + 0.5*σ²*Gamma), the time decay reading the forward gamma, where
        // QuantLib reads the spot gamma
        double spotOverForward = spot / forward;
        return -((Math.Log(discount) * Value) + (Math.Log(forward / spot) * spot * Delta(spot))
            + (0.5 * variance * Gamma(spot) * spotOverForward * spotOverForward)) / maturity;
    }

    /// <summary>The sensitivity to the time to maturity per day, assuming 365 days a year.</summary>
    /// <param name="spot">The spot.</param>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The theta per day.</returns>
    /// <exception cref="ArgumentException">The maturity is negative.</exception>
    public double ThetaPerDay(double spot, double maturity) => Theta(spot, maturity) / 365.0;

    /// <summary>The sensitivity to the volatility σ, the standard deviation being σ√T.</summary>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The vega.</returns>
    /// <exception cref="ArgumentException">The maturity is negative.</exception>
    public double Vega(double maturity)
    {
        if (!(maturity >= 0.0))
        {
            throw new ArgumentException("negative maturity not allowed", nameof(maturity));
        }

        if (maturity <= NumericLimits.QL_EPSILON || stdDev <= NumericLimits.QL_EPSILON)
        {
            return 0.0;
        }

        if (plainVanilla)
        {
            return discount * Math.Sqrt(maturity) * nD;
        }

        // dV/dσ = √T·σ·∂²V/∂F², the heat equation's
        return Math.Sqrt(maturity) * stdDev * GammaForward;
    }

    /// <summary>The sensitivity to the risk-free rate.</summary>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The rho.</returns>
    /// <exception cref="ArgumentException">The maturity is negative.</exception>
    public double Rho(double maturity)
    {
        if (!(maturity >= 0.0))
        {
            throw new ArgumentException("negative maturity not allowed", nameof(maturity));
        }

        // Rho = dV/dr = T * (discount * delta_forward * forward - value)
        double deltaFwd = DeltaForward;
        double rhoValue = maturity * ((deltaFwd * forward) - Value);

        return rhoValue;
    }

    /// <summary>The sensitivity to the dividend yield.</summary>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The dividend rho.</returns>
    /// <exception cref="ArgumentException">The maturity is negative.</exception>
    public double DividendRho(double maturity)
    {
        if (!(maturity >= 0.0))
        {
            throw new ArgumentException("negative maturity not allowed", nameof(maturity));
        }

        if (!plainVanilla)
        {
            return -maturity * DeltaForward * forward;
        }

        // Dividend rho = -T * discount * delta_forward * forward
        double deltaFwd = optionType == OptionType.Call ? cumD : cumD - 1.0;
        return -maturity * discount * deltaFwd * forward;
    }

    /// <summary>
    /// The sensitivity of the vega to the spot (vanna): QuantLib's documented quantity, its forward form times D·F/S,
    /// which QuantLib's misses.
    /// </summary>
    /// <param name="spot">The spot.</param>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The vanna.</returns>
    public double Vanna(double spot, double maturity)
    {
        if (maturity <= NumericLimits.QL_EPSILON || stdDev <= NumericLimits.QL_EPSILON)
        {
            return 0.0;
        }

        double dForwardDs = forward / spot;
        if (plainVanilla)
        {
            // Vanna = -D * d * n(d) * sqrt(T) / stdDev * F/S
            return -discount * d * nD * Math.Sqrt(maturity) / stdDev * dForwardDs;
        }

        // √T·σ·∂/∂F of the forward gamma, times F/S
        double dGammaDf = discount * ((-d * dAlphaDd / variance)
            + (((d * d) - 1.0) * WeightedDensity / (variance * stdDev)));
        return Math.Sqrt(maturity) * stdDev * dGammaDf * dForwardDs;
    }

    /// <summary>The sensitivity of the vega to the volatility (volga): QuantLib's times √T, which QuantLib's misses.</summary>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The volga.</returns>
    public double Volga(double maturity)
    {
        if (maturity <= NumericLimits.QL_EPSILON || stdDev <= NumericLimits.QL_EPSILON)
        {
            return 0.0;
        }

        if (plainVanilla)
        {
            // Volga = d^2 * sqrt(T) / stdDev * Vega
            double vega = Vega(maturity);
            return d * d * Math.Sqrt(maturity) / stdDev * vega;
        }

        // T·(Γ + σ·∂Γ/∂σ), Γ the forward gamma
        double dGammaDs = discount * ((((d * d) - 1.0) * dAlphaDd / variance)
            + (d * (3.0 - (d * d)) * WeightedDensity / (variance * stdDev)));
        return maturity * (GammaForward + (stdDev * dGammaDs));
    }

    private static double ElasticityOf(double val, double del, double level)
    {
        if (val > NumericLimits.QL_EPSILON)
        {
            return del / val * level;
        }
        else if (Math.Abs(del) < NumericLimits.QL_EPSILON)
        {
            return 0.0;
        }
        else if (del > 0.0)
        {
            return NumericLimits.QL_MAX_REAL;
        }
        else
        {
            return NumericLimits.QL_MIN_REAL;
        }
    }

    private static StrikedTypePayoff PayoffOf(StrikedTypePayoff payoff)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return payoff;
    }

    // std::max's semantics, which keep or drop a NaN by position where Math.Max propagates it.
    private static double Max(double a, double b) => a < b ? b : a;

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);
}
