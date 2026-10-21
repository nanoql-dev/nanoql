// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/blackcalculator.hpp ql/pricingengines/blackcalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Instruments;
using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Black (1976) calculator: the value and sensitivities of a striked payoff on a forward with a log-normal
/// distribution of a given standard deviation, discounted. It supports plain vanilla, cash-or-nothing,
/// asset-or-nothing and gap payoffs.
/// </summary>
/// <remarks>
/// <para>
/// Four of QuantLib's results are fixed, documented divergences, each checked by QuantLib's own second route to the
/// value:
/// </para>
/// <list type="bullet">
/// <item><description>At zero volatility, QuantLib's delta, forward delta and strike sensitivity tell a call from a put
/// by <c>alpha &gt;= 0</c> (<c>blackcalculator.cpp:214-228</c>, <c>:256-270</c>, <c>:438-452</c>), but alpha is 0 for
/// an out-of-the-money put and for every cash-or-nothing payoff, which then get a call's delta. Here they are the
/// nonzero branch's formulas with the derivative terms zero, from alpha and beta, so the theta, the theta per day,
/// the elasticity, the rho and the dividend rho that read them are fixed too.</description></item>
/// <item><description>QuantLib's volga divides by the standard deviation σ√T where the derivative in σ divides by σ
/// (<c>:511-523</c>), so it is the volga over √T: here it is multiplied by √T.</description></item>
/// <item><description>QuantLib's vanna and volga use the plain vanilla's closed form for every payoff
/// (<c>:494-523</c>); for other payoffs they are here the derivatives of QuantLib's vega through the payoff's alpha and
/// beta. The plain vanilla's are QuantLib's formulas.</description></item>
/// <item><description>QuantLib's in-the-money probabilities are N(d2) and N(d1) for every payoff
/// (<c>blackcalculator.hpp:129-135</c>), a call's; a put's are here 1 − N(d2) and 1 − N(d1), the expressions
/// QuantLib's cash-or-nothing and asset-or-nothing puts value.</description></item>
/// </list>
/// <para>
/// Unsealed: QuantLib's Black-Scholes calculator derives from it (<c>blackscholescalculator.hpp:32</c>), reading only
/// its public members.
/// </para>
/// </remarks>
public class BlackCalculator
{
    // QuantLib's M_SQRT_2 (1/√2) and M_1_SQRTPI (1/√π).
    private const double M_SQRT_2 = 0.7071067811865475244008443621048490392848359376887;
    private const double M_1_SQRTPI = 0.564189583547756286948;

    private readonly double strike, forward, stdDev, discount, variance;
    private readonly double d1, d2;
    private readonly double alpha, beta, dAlphaDd1, dBetaDd2;
    private readonly double nD1, cumD1, nD2, cumD2;
    private readonly double x, dXDs, dXDstrike;
    private readonly OptionType optionType;
    private readonly bool plainVanilla;

    /// <summary>Initializes a new instance of the <see cref="BlackCalculator"/> class for a striked payoff.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation of the log of the underlying at expiry, σ√T.</param>
    /// <param name="discount">The discount factor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The strike is negative, the forward or the discount isn't positive, the standard deviation is negative, or the
    /// payoff isn't a plain vanilla, cash-or-nothing, asset-or-nothing or gap payoff.
    /// </exception>
    public BlackCalculator(StrikedTypePayoff payoff, double forward, double stdDev, double discount = 1.0)
        : this(PayoffOf(payoff), payoff.Strike, forward, stdDev, discount, nameof(payoff))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BlackCalculator"/> class for a plain vanilla
    /// payoff.</summary>
    /// <param name="optionType">The option's type.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation of the log of the underlying at expiry, σ√T.</param>
    /// <param name="discount">The discount factor.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="ArgumentException">
    /// The strike is negative, the forward or the discount isn't positive, or the standard deviation is negative.
    /// </exception>
    public BlackCalculator(OptionType optionType, double strike, double forward, double stdDev, double discount = 1.0)
        : this(new PlainVanillaPayoff(optionType, strike), strike, forward, stdDev, discount, nameof(strike))
    {
    }

    private BlackCalculator(
        StrikedTypePayoff p, double strike, double forward, double stdDev, double discount, string strikeParamName)
    {
        this.strike = strike;
        this.forward = forward;
        this.stdDev = stdDev;
        this.discount = discount;
        variance = stdDev * stdDev;

        // initialize
        if (!(strike >= 0.0))
        {
            throw new ArgumentException($"strike ({Show(strike)}) must be non-negative", strikeParamName);
        }

        if (!(forward > 0.0))
        {
            throw new ArgumentException($"forward ({Show(forward)}) must be positive", nameof(forward));
        }

        if (!(stdDev >= 0.0))
        {
            throw new ArgumentException($"stdDev ({Show(stdDev)}) must be non-negative", nameof(stdDev));
        }

        if (!(discount > 0.0))
        {
            throw new ArgumentException($"discount ({Show(discount)}) must be positive", nameof(discount));
        }

        if (stdDev >= NumericLimits.QL_EPSILON)
        {
            if (ComparisonFunctions.Close(strike, 0.0))
            {
                d1 = NumericLimits.QL_MAX_REAL;
                d2 = NumericLimits.QL_MAX_REAL;
                cumD1 = 1.0;
                cumD2 = 1.0;
                nD1 = 0.0;
                nD2 = 0.0;
            }
            else
            {
                d1 = (Math.Log(forward / strike) / stdDev) + (0.5 * stdDev);
                d2 = d1 - stdDev;
                var f = new CumulativeNormalDistribution();
                cumD1 = f.Evaluate(d1);
                cumD2 = f.Evaluate(d2);
                nD1 = f.Derivative(d1);
                nD2 = f.Derivative(d2);
            }
        }
        else
        {
            if (ComparisonFunctions.Close(forward, strike))
            {
                d1 = 0;
                d2 = 0;
                cumD1 = 0.5;
                cumD2 = 0.5;
                nD1 = M_SQRT_2 * M_1_SQRTPI;
                nD2 = M_SQRT_2 * M_1_SQRTPI;
            }
            else if (forward > strike)
            {
                d1 = NumericLimits.QL_MAX_REAL;
                d2 = NumericLimits.QL_MAX_REAL;
                cumD1 = 1.0;
                cumD2 = 1.0;
                nD1 = 0.0;
                nD2 = 0.0;
            }
            else
            {
                d1 = NumericLimits.QL_MIN_REAL;
                d2 = NumericLimits.QL_MIN_REAL;
                cumD1 = 0.0;
                cumD2 = 0.0;
                nD1 = 0.0;
                nD2 = 0.0;
            }
        }

        x = strike;
        dXDstrike = 1.0;

        // the following one will probably disappear as soon as
        // super-share will be properly handled
        dXDs = 0.0;

        // this part is always executed.
        // in case of plain-vanilla payoffs, it is also the only part
        // which is executed.
        optionType = p.OptionType;
        switch (optionType)
        {
            case OptionType.Call:
                alpha = cumD1; //  N(d1)
                dAlphaDd1 = nD1; //  n(d1)
                beta = -cumD2; // -N(d2)
                dBetaDd2 = -nD2; // -n(d2)
                break;
            case OptionType.Put:
                alpha = -1.0 + cumD1; // -N(-d1)
                dAlphaDd1 = nD1; //  n( d1)
                beta = 1.0 - cumD2; //  N(-d2)
                dBetaDd2 = -nD2; // -n( d2)
                break;
            default:
                throw new NanoQLException("invalid option type");
        }

        // now dispatch on type.
        switch (p)
        {
            case PlainVanillaPayoff:
                plainVanilla = true;
                break;
            case CashOrNothingPayoff payoff:
                alpha = dAlphaDd1 = 0.0;
                x = payoff.CashPayoff;
                dXDstrike = 0.0;
                switch (payoff.OptionType)
                {
                    case OptionType.Call:
                        beta = cumD2;
                        dBetaDd2 = nD2;
                        break;
                    case OptionType.Put:
                        beta = 1.0 - cumD2;
                        dBetaDd2 = -nD2;
                        break;
                    default:
                        throw new NanoQLException("invalid option type");
                }

                break;
            case AssetOrNothingPayoff payoff:
                beta = dBetaDd2 = 0.0;
                switch (payoff.OptionType)
                {
                    case OptionType.Call:
                        alpha = cumD1;
                        dAlphaDd1 = nD1;
                        break;
                    case OptionType.Put:
                        alpha = 1.0 - cumD1;
                        dAlphaDd1 = -nD1;
                        break;
                    default:
                        throw new NanoQLException("invalid option type");
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

    /// <summary>Gets the option's value.</summary>
    public double Value
    {
        get
        {
            double result = discount * ((forward * alpha) + (x * beta));
            return result;
        }
    }

    /// <summary>Gets the sensitivity to a change of the underlying's forward price.</summary>
    public double DeltaForward
    {
        get
        {
            // Handle zero volatility case: the nonzero branch's formula with its derivative terms zero.
            if (stdDev <= NumericLimits.QL_EPSILON)
            {
                return discount * alpha;
            }

            double temp = stdDev * forward;
            double dAlphaDforward = dAlphaDd1 / temp;
            double dBetaDforward = dBetaDd2 / temp;
            double temp2 = (dAlphaDforward * forward) + alpha + (dBetaDforward * x); // DXDforward = 0.0

            return discount * temp2;
        }
    }

    /// <summary>Gets the sensitivity in percent to a percent change of the underlying's forward price.</summary>
    public double ElasticityForward
    {
        get
        {
            double val = Value;
            double del = DeltaForward;
            if (val > NumericLimits.QL_EPSILON)
            {
                return del / val * forward;
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
    }

    /// <summary>Gets the second-order derivative with respect to the underlying's forward price.</summary>
    public double GammaForward
    {
        get
        {
            // Handle zero volatility case
            if (stdDev <= NumericLimits.QL_EPSILON)
            {
                // For zero volatility, gamma is always 0 (no convexity)
                return 0.0;
            }

            double temp = stdDev * forward;
            double dAlphaDforward = dAlphaDd1 / temp;
            double dBetaDforward = dBetaDd2 / temp;

            double d2AlphaDforward2 = -dAlphaDforward / forward * (1 + (d1 / stdDev));
            double d2BetaDforward2 = -dBetaDforward / forward * (1 + (d2 / stdDev));

            double temp2 = (d2AlphaDforward2 * forward) + (2.0 * dAlphaDforward) + (d2BetaDforward2 * x); // DXDforward = 0.0

            return discount * temp2;
        }
    }

    /// <summary>
    /// Gets the probability of expiring in the money in the bond martingale measure: N(d2) for a call, 1 − N(d2) for a
    /// put. A risk-neutral probability, not the real-world one.
    /// </summary>
    public double ItmCashProbability => optionType == OptionType.Put ? 1.0 - cumD2 : cumD2;

    /// <summary>
    /// Gets the probability of expiring in the money in the asset martingale measure: N(d1) for a call, 1 − N(d1) for
    /// a put. A risk-neutral probability, not the real-world one.
    /// </summary>
    public double ItmAssetProbability => optionType == OptionType.Put ? 1.0 - cumD1 : cumD1;

    /// <summary>Gets the sensitivity to the strike.</summary>
    public double StrikeSensitivity
    {
        get
        {
            // Handle zero volatility case: the nonzero branch's formula with its derivative terms zero.
            if (stdDev <= NumericLimits.QL_EPSILON)
            {
                return discount * (beta * dXDstrike);
            }

            double temp = stdDev * strike;
            double dAlphaDstrike = -dAlphaDd1 / temp;
            double dBetaDstrike = -dBetaDd2 / temp;

            double temp2 = (dAlphaDstrike * forward) + (dBetaDstrike * x) + (beta * dXDstrike);

            return discount * temp2;
        }
    }

    /// <summary>Gets the second-order derivative with respect to the strike.</summary>
    public double StrikeGamma
    {
        get
        {
            // Handle zero volatility case
            if (stdDev <= NumericLimits.QL_EPSILON)
            {
                // For zero volatility, strike gamma is 0 (no convexity)
                return 0.0;
            }

            double temp = stdDev * strike;
            double dAlphaDstrike = -dAlphaDd1 / temp;
            double dBetaDstrike = -dBetaDd2 / temp;

            double d2AlphaD2strike = -dAlphaDstrike / strike * (1 - (d1 / stdDev));
            double d2BetaD2strike = -dBetaDstrike / strike * (1 - (d2 / stdDev));

            double temp2 = (d2AlphaD2strike * forward) + (d2BetaD2strike * x) + (2.0 * dBetaDstrike * dXDstrike);

            return discount * temp2;
        }
    }

    /// <summary>Gets alpha: the coefficient of the forward in the value, before discounting.</summary>
    public double Alpha => alpha;

    /// <summary>Gets beta: the coefficient of the payoff's cash amount in the value, before discounting.</summary>
    public double Beta => beta;

    /// <summary>The sensitivity to a change of the underlying's spot price.</summary>
    /// <param name="spot">The spot.</param>
    /// <returns>The delta.</returns>
    /// <exception cref="ArgumentException"><paramref name="spot"/> isn't positive.</exception>
    public double Delta(double spot)
    {
        if (!(spot > 0.0))
        {
            throw new ArgumentException($"positive spot value required: {Show(spot)} not allowed", nameof(spot));
        }

        // Handle zero volatility case: the nonzero branch's formula with its derivative terms zero.
        if (stdDev <= NumericLimits.QL_EPSILON)
        {
            double dForwardDs0 = forward / spot;
            return discount * ((alpha * dForwardDs0) + (beta * dXDs));
        }

        double dForwardDs = forward / spot;

        double temp = stdDev * spot;
        double dAlphaDs = dAlphaDd1 / temp;
        double dBetaDs = dBetaDd2 / temp;
        double temp2 = (dAlphaDs * forward) + (alpha * dForwardDs) + (dBetaDs * x) + (beta * dXDs);

        return discount * temp2;
    }

    /// <summary>The sensitivity in percent to a percent change of the underlying's spot price.</summary>
    /// <param name="spot">The spot.</param>
    /// <returns>The elasticity.</returns>
    /// <exception cref="ArgumentException"><paramref name="spot"/> isn't positive.</exception>
    public double Elasticity(double spot)
    {
        double val = Value;
        double del = Delta(spot);
        if (val > NumericLimits.QL_EPSILON)
        {
            return del / val * spot;
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

    /// <summary>The second-order derivative with respect to the underlying's spot price.</summary>
    /// <param name="spot">The spot.</param>
    /// <returns>The gamma.</returns>
    /// <exception cref="ArgumentException"><paramref name="spot"/> isn't positive.</exception>
    public double Gamma(double spot)
    {
        if (!(spot > 0.0))
        {
            throw new ArgumentException($"positive spot value required: {Show(spot)} not allowed", nameof(spot));
        }

        // Handle zero volatility case
        if (stdDev <= NumericLimits.QL_EPSILON)
        {
            // For zero volatility, gamma is always 0 (no convexity)
            return 0.0;
        }

        double dForwardDs = forward / spot;

        double temp = stdDev * spot;
        double dAlphaDs = dAlphaDd1 / temp;
        double dBetaDs = dBetaDd2 / temp;

        double d2AlphaDs2 = -dAlphaDs / spot * (1 + (d1 / stdDev));
        double d2BetaDs2 = -dBetaDs / spot * (1 + (d2 / stdDev));

        double temp2 = (d2AlphaDs2 * forward) + (2.0 * dAlphaDs * dForwardDs) + (d2BetaDs2 * x) + (2.0 * dBetaDs * dXDs);

        return discount * temp2;
    }

    /// <summary>The sensitivity to the time to maturity.</summary>
    /// <param name="spot">The spot.</param>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The theta; 0 at a maturity close to 0.</returns>
    /// <exception cref="ArgumentException">The maturity is negative, or the spot isn't positive.</exception>
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

        return -((Math.Log(discount) * Value)
            + (Math.Log(forward / spot) * spot * Delta(spot))
            + (0.5 * variance * spot * spot * Gamma(spot))) / maturity;
    }

    /// <summary>
    /// The theta where the structures run on different times: each of <see cref="Theta(double, double)"/>'s terms over
    /// its own structure's time to maturity, the payoff's discount, the forward's risk-free and dividend discounts and
    /// the variance, none for a structure whose time is 0. QuantLib's <see cref="Theta(double, double)"/> where the
    /// four times agree.
    /// </summary>
    /// <param name="spot">The spot.</param>
    /// <param name="discountTime">The payoff's discount's time to maturity, not negative.</param>
    /// <param name="riskFreeDiscount">The forward's risk-free discount.</param>
    /// <param name="riskFreeTime">Its time to maturity, not negative.</param>
    /// <param name="dividendDiscount">The forward's dividend discount.</param>
    /// <param name="dividendTime">Its time to maturity, not negative.</param>
    /// <param name="varianceTime">The variance's time to maturity, not negative.</param>
    /// <returns>The theta, per unit of the times.</returns>
    /// <exception cref="ArgumentException">The times agree and are negative, or the spot isn't positive.</exception>
    internal double Theta(
        double spot,
        double discountTime,
        double riskFreeDiscount,
        double riskFreeTime,
        double dividendDiscount,
        double dividendTime,
        double varianceTime)
    {
        if (discountTime == riskFreeTime && riskFreeTime == dividendTime && dividendTime == varianceTime)
        {
            return Theta(spot, varianceTime);
        }

        return -((PerTime(Math.Log(discount), discountTime) * Value)
            + ((PerTime(Math.Log(dividendDiscount), dividendTime) - PerTime(Math.Log(riskFreeDiscount), riskFreeTime))
               * spot * Delta(spot))
            + (0.5 * PerTime(variance, varianceTime) * spot * spot * Gamma(spot)));
    }

    /// <summary>The sensitivity to the time to maturity per day, assuming 365 days a year.</summary>
    /// <param name="spot">The spot.</param>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The theta per day.</returns>
    /// <exception cref="ArgumentException">The maturity is negative, or the spot isn't positive.</exception>
    public double ThetaPerDay(double spot, double maturity) => Theta(spot, maturity) / 365.0;

    /// <summary>The sensitivity to the volatility.</summary>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The vega.</returns>
    /// <exception cref="ArgumentException">The maturity is negative.</exception>
    public double Vega(double maturity)
    {
        if (!(maturity >= 0.0))
        {
            throw new ArgumentException("negative maturity not allowed", nameof(maturity));
        }

        // Handle zero volatility case
        if (stdDev <= NumericLimits.QL_EPSILON)
        {
            return 0.0;
        }

        double temp = Math.Log(strike / forward) / variance;

        // actually DalphaDsigma / SQRT(T)
        double dAlphaDsigma = dAlphaDd1 * (temp + 0.5);
        double dBetaDsigma = dBetaDd2 * (temp - 0.5);

        double temp2 = (dAlphaDsigma * forward) + (dBetaDsigma * x);

        return discount * Math.Sqrt(maturity) * temp2;
    }

    /// <summary>The sensitivity to the discounting rate.</summary>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The rho.</returns>
    /// <exception cref="ArgumentException">The maturity is negative.</exception>
    public double Rho(double maturity)
    {
        if (!(maturity >= 0.0))
        {
            throw new ArgumentException("negative maturity not allowed", nameof(maturity));
        }

        // Handle zero volatility case
        if (stdDev <= NumericLimits.QL_EPSILON)
        {
            // For zero volatility, rho = T * (delta_forward * forward - value/discount)
            double deltaFwd = DeltaForward;
            return maturity * ((deltaFwd * forward) - Value);
        }

        // actually DalphaDr / T
        double dAlphaDr = dAlphaDd1 / stdDev;
        double dBetaDr = dBetaDd2 / stdDev;
        double temp = (dAlphaDr * forward) + (alpha * forward) + (dBetaDr * x);

        return maturity * ((discount * temp) - Value);
    }

    /// <summary>The sensitivity to the dividend or growth rate.</summary>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The dividend rho.</returns>
    /// <exception cref="ArgumentException">The maturity is negative.</exception>
    public double DividendRho(double maturity)
    {
        if (!(maturity >= 0.0))
        {
            throw new ArgumentException("negative maturity not allowed", nameof(maturity));
        }

        // Handle zero volatility case
        if (stdDev <= NumericLimits.QL_EPSILON)
        {
            // For zero volatility, dividend rho = -T * discount * delta_forward * forward
            double deltaFwd = DeltaForward / discount; // Remove discount to get pure delta
            return -maturity * discount * deltaFwd * forward;
        }

        // actually DalphaDq / T
        double dAlphaDq = -dAlphaDd1 / stdDev;
        double dBetaDq = -dBetaDd2 / stdDev;

        double temp = (dAlphaDq * forward) - (alpha * forward) + (dBetaDq * x);

        return maturity * discount * temp;
    }

    /// <summary>The sensitivity of the vega to the spot (vanna).</summary>
    /// <param name="spot">The spot.</param>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The vanna.</returns>
    /// <exception cref="ArgumentException">The spot isn't positive, or the maturity is negative.</exception>
    public double Vanna(double spot, double maturity)
    {
        if (!(spot > 0.0))
        {
            throw new ArgumentException($"positive spot value required: {Show(spot)} not allowed", nameof(spot));
        }

        if (!(maturity >= 0.0))
        {
            throw new ArgumentException("negative maturity not allowed", nameof(maturity));
        }

        if (stdDev <= NumericLimits.QL_EPSILON)
        {
            return 0.0;
        }

        if (plainVanilla)
        {
            // Calculate Vega
            double vega = Vega(maturity);

            // d2 = d1 - stdDev_
            // Vanna = -d2 / (spot * stdDev_) * Vega
            return -d2 / (spot * stdDev) * vega;
        }

        // The derivative in the spot of the vega's alpha and beta terms: dF/dS = F/S, dd1/dS = dd2/dS = 1/(S stdDev),
        // d temp/dS = -1/(S variance), and d(DalphaDd1)/dd1 = -d1 DalphaDd1, d(DbetaDd2)/dd2 = -d2 DbetaDd2.
        double temp = Math.Log(strike / forward) / variance;
        double dVegaDs =
            (-d1 * dAlphaDd1 / (spot * stdDev) * (temp + 0.5) * forward)
            + (dAlphaDd1 * (-1.0 / (spot * variance)) * forward)
            + (dAlphaDd1 * (temp + 0.5) * forward / spot)
            + (-d2 * dBetaDd2 / (spot * stdDev) * (temp - 0.5) * x)
            + (dBetaDd2 * (-1.0 / (spot * variance)) * x);
        return discount * Math.Sqrt(maturity) * dVegaDs;
    }

    /// <summary>The sensitivity of the vega to the volatility (volga).</summary>
    /// <param name="maturity">The time to maturity.</param>
    /// <returns>The volga.</returns>
    /// <exception cref="ArgumentException">The maturity is negative.</exception>
    public double Volga(double maturity)
    {
        if (!(maturity >= 0.0))
        {
            throw new ArgumentException("negative maturity not allowed", nameof(maturity));
        }

        if (stdDev <= NumericLimits.QL_EPSILON)
        {
            return 0.0;
        }

        if (plainVanilla)
        {
            // Calculate Vega
            double vega = Vega(maturity);

            // Volga = Vega * d1 * d2 / stdDev_, times √T: the derivative in σ, not in σ√T
            return vega * d1 * d2 / stdDev * Math.Sqrt(maturity);
        }

        // The derivative in the standard deviation of the vega's alpha and beta terms, times √T: dd1/d stdDev =
        // temp + 0.5, dd2/d stdDev = temp - 0.5, d temp/d stdDev = -2 temp/stdDev.
        double temp = Math.Log(strike / forward) / variance;
        double dVegaDstdDev =
            (-d1 * dAlphaDd1 * (temp + 0.5) * (temp + 0.5) * forward)
            + (dAlphaDd1 * (-2.0 * temp / stdDev) * forward)
            + (-d2 * dBetaDd2 * (temp - 0.5) * (temp - 0.5) * x)
            + (dBetaDd2 * (-2.0 * temp / stdDev) * x);
        return discount * maturity * dVegaDstdDev;
    }

    private static StrikedTypePayoff PayoffOf(StrikedTypePayoff payoff)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return payoff;
    }

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);

    // A structure's log discount or variance per unit of its own time to maturity, none where that time is 0, its
    // discount 1 and its variance 0 there
    private static double PerTime(double x, double time) => time > 0.0 ? x / time : 0.0;
}
