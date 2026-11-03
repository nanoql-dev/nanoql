// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/americanpayoffathit.hpp ql/pricingengines/americanpayoffathit.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic formula for American exercise with the payoff at hit: a cash-or-nothing or asset-or-nothing payoff
/// paid when the underlying first reaches the strike, an up-and-in call or a down-and-in put.
/// </summary>
/// <remarks>
/// <para>
/// An immutable calculator over the spot, the discount and dividend discount to expiry and the variance to expiry, as
/// QuantLib's; its value and its delta, gamma and rho.
/// </para>
/// <para>
/// Two documented divergences, each leaving QuantLib's arithmetic where it doesn't apply. Where the closed form has no
/// real λ (λ² = μ² − 2 ln D / σ²T &lt; 0, at negative rates), QuantLib's square root is NaN
/// (<c>americanpayoffathit.cpp:75</c>); here the value and its Greeks are the payoff's present value at the hit
/// integrated over the first-passage density of the log-price to the strike, which is the closed form where λ is real
/// (as the barrier engines' rebate; <c>Divergences/AmericanPayoffAtHitNegativeRateTests.cs</c>). Below a variance of ε,
/// QuantLib divides by the variance (<c>:87-102</c>), NaN out of the money; here the underlying follows its forward, ln
/// S(τ) = ln S + τ ln(D_q/D) over the fraction τ of the time to expiry, and the payoff is paid at the τ* where that
/// path reaches the strike, if it does by expiry, with the Greeks of that value
/// (<c>Divergences/AmericanPayoffZeroVarianceTests.cs</c>).
/// </para>
/// <para>
/// A payoff other than a cash-or-nothing or asset-or-nothing one is refused, where QuantLib leaves the paid amount
/// unset.
/// </para>
/// </remarks>
public sealed class AmericanPayoffAtHit
{
    private const int IntegralEvaluations = 100000;
    private const double IntegralAccuracy = 1e-15;

    private readonly double spot;
    private readonly double variance;
    private readonly double stdDev;
    private readonly double strike;
    private readonly double k;
    private readonly double mu;
    private readonly double lambda;
    private readonly double muPlusLambda;
    private readonly double muMinusLambda;
    private readonly double logHS;
    private readonly double d1;
    private readonly double d2;
    private readonly double alpha;
    private readonly double beta;
    private readonly double dAlphaDd1;
    private readonly double dBetaDd2;
    private readonly bool inTheMoney;
    private readonly double forward;
    private readonly double x;

    // Where the closed form doesn't apply (λ² < 0, or a variance below ε): the value, the Greeks and the rho per unit of
    // maturity, computed in the constructor.
    private readonly (double Value, double Delta, double Gamma, double RhoPerMaturity)? special;

    /// <summary>Initializes a new instance of the <see cref="AmericanPayoffAtHit"/> class.</summary>
    /// <param name="spot">The underlying's value.</param>
    /// <param name="discount">The risk-free discount to expiry.</param>
    /// <param name="dividendDiscount">The dividend discount to expiry.</param>
    /// <param name="variance">The variance to expiry.</param>
    /// <param name="payoff">The payoff, cash-or-nothing or asset-or-nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The spot, the discount or the dividend discount isn't positive, the variance is negative, or the payoff is
    /// neither cash-or-nothing nor asset-or-nothing.
    /// </exception>
    public AmericanPayoffAtHit(
        double spot, double discount, double dividendDiscount, double variance, StrikedTypePayoff payoff)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        this.spot = spot;
        this.variance = variance;

        if (!(spot > 0.0))
        {
            throw new ArgumentException("positive spot value required", nameof(spot));
        }

        if (!(discount > 0.0))
        {
            throw new ArgumentException("positive discount required", nameof(discount));
        }

        if (!(dividendDiscount > 0.0))
        {
            throw new ArgumentException("positive dividend discount required", nameof(dividendDiscount));
        }

        if (!(variance >= 0.0))
        {
            throw new ArgumentException("negative variance not allowed", nameof(variance));
        }

        stdDev = Math.Sqrt(variance);

        OptionType type = payoff.OptionType;
        strike = payoff.Strike;

        logHS = Math.Log(strike / spot);

        inTheMoney = (type == OptionType.Call && strike < spot) || (type == OptionType.Put && strike > spot);

        // Binary Cash-Or-Nothing payoff? Binary Asset-Or-Nothing payoff? QuantLib leaves the amount unset otherwise.
        k = payoff switch
        {
            CashOrNothingPayoff coo => coo.CashPayoff,
            AssetOrNothingPayoff => inTheMoney ? spot : strike,
            _ => throw new ArgumentException(
                $"{payoff.Name} payoff given: a cash-or-nothing or asset-or-nothing payoff required", nameof(payoff)),
        };

        bool hitNow = type == OptionType.Call ? !(strike > spot) : !(strike < spot);

        if (!(variance >= NumericLimits.QL_EPSILON))
        {
            // the underlying on its forward, where QuantLib divides by the variance
            special = hitNow ? (k, 0.0, 0.0, 0.0) : Deterministic(type, discount, dividendDiscount);
            return;
        }

        mu = (Math.Log(dividendDiscount / discount) / variance) - 0.5;
        double lambdaSquared = (mu * mu) - (2.0 * Math.Log(discount) / variance);
        if (lambdaSquared < 0.0)
        {
            // no real λ: the first-passage integral, where QuantLib's square root is NaN; where the payoff is due now,
            // its value and the λ-free terms of QuantLib's Greeks
            special = !hitNow
                ? FirstPassage(discount)
                : (k, inTheMoney ? 0.0 : -k * mu / spot, inTheMoney ? 0.0 : k * (mu + (mu * mu) + lambdaSquared) / (spot * spot), 0.0);
            return;
        }

        lambda = Math.Sqrt(lambdaSquared);
        d1 = (logHS / stdDev) + (lambda * stdDev);
        d2 = d1 - (2.0 * lambda * stdDev);
        var f = new CumulativeNormalDistribution();
        double cumD1 = f.Evaluate(d1);
        double cumD2 = f.Evaluate(d2);

        // N(-d) directly, since 1 - N(d) loses the tail to cancellation
        double cumMinusD1 = f.Evaluate(-d1);
        double cumMinusD2 = f.Evaluate(-d2);
        double nD1 = f.Derivative(d1);
        double nD2 = f.Derivative(d2);

        switch (type)
        {
            // up-and-in cash-(at-hit)-or-nothing option
            // a.k.a. american call with cash-or-nothing payoff
            case OptionType.Call:
                if (strike > spot)
                {
                    alpha = cumMinusD1; //  N(-d1)
                    dAlphaDd1 = -nD1; // -n( d1)
                    beta = cumMinusD2; //  N(-d2)
                    dBetaDd2 = -nD2; // -n( d2)
                }
                else
                {
                    alpha = 0.5;
                    dAlphaDd1 = 0.0;
                    beta = 0.5;
                    dBetaDd2 = 0.0;
                }

                break;

            // down-and-in cash-(at-hit)-or-nothing option
            // a.k.a. american put with cash-or-nothing payoff
            case OptionType.Put:
                if (strike < spot)
                {
                    alpha = cumD1; //  N(d1)
                    dAlphaDd1 = nD1; //  n(d1)
                    beta = cumD2; //  N(d2)
                    dBetaDd2 = nD2; //  n(d2)
                }
                else
                {
                    alpha = 0.5;
                    dAlphaDd1 = 0.0;
                    beta = 0.5;
                    dBetaDd2 = 0.0;
                }

                break;
            default:
                throw new NanoQLException("invalid option type");
        }

        muPlusLambda = mu + lambda;
        muMinusLambda = mu - lambda;

        if (inTheMoney)
        {
            forward = 1.0;
            x = 1.0;
        }
        else
        {
            forward = Math.Pow(strike / spot, muPlusLambda);
            x = Math.Pow(strike / spot, muMinusLambda);

            // at small variance a power can overflow where the probability it
            // multiplies underflows; the product is finite, so it is taken in logs
            double sign = (type == OptionType.Call) ? -1.0 : 1.0;
            if (!double.IsFinite(forward))
            {
                alpha = Math.Exp((muPlusLambda * logHS) + LogCumNormal(sign * d1));
                dAlphaDd1 = sign * Math.Exp((muPlusLambda * logHS) - (0.5 * d1 * d1) - (0.5 * Math.Log(MathConstants.M_TWOPI)));
                forward = 1.0;
            }

            if (!double.IsFinite(x))
            {
                beta = Math.Exp((muMinusLambda * logHS) + LogCumNormal(sign * d2));
                dBetaDd2 = sign * Math.Exp((muMinusLambda * logHS) - (0.5 * d2 * d2) - (0.5 * Math.Log(MathConstants.M_TWOPI)));
                x = 1.0;
            }
        }
    }

    /// <summary>The value.</summary>
    /// <returns>The value.</returns>
    public double Value() => special?.Value ?? k * ((forward * alpha) + (x * beta));

    /// <summary>The delta, the value's derivative by the spot.</summary>
    /// <returns>The delta.</returns>
    public double Delta()
    {
        if (special is { } s)
        {
            return s.Delta;
        }

        double tempDelta = -spot * stdDev;
        double dAlphaDs = dAlphaDd1 / tempDelta;
        double dBetaDs = dBetaDd2 / tempDelta;

        double dForwardDs, dXDs;
        if (inTheMoney)
        {
            dForwardDs = 0.0;
            dXDs = 0.0;
        }
        else
        {
            dForwardDs = -muPlusLambda * forward / spot;
            dXDs = -muMinusLambda * x / spot;
        }

        return k * ((dAlphaDs * forward) + (alpha * dForwardDs) + (dBetaDs * x) + (beta * dXDs));
    }

    /// <summary>The gamma, the value's second derivative by the spot.</summary>
    /// <returns>The gamma.</returns>
    public double Gamma()
    {
        if (special is { } s)
        {
            return s.Gamma;
        }

        double tempDelta = -spot * stdDev;
        double dAlphaDs = dAlphaDd1 / tempDelta;
        double dBetaDs = dBetaDd2 / tempDelta;
        double d2AlphaDs2 = -dAlphaDs / spot * (1 - (d1 / stdDev));
        double d2BetaDs2 = -dBetaDs / spot * (1 - (d2 / stdDev));

        double dForwardDs, dXDs, d2ForwardDs2, d2XDs2;
        if (inTheMoney)
        {
            dForwardDs = 0.0;
            dXDs = 0.0;
            d2ForwardDs2 = 0.0;
            d2XDs2 = 0.0;
        }
        else
        {
            dForwardDs = -muPlusLambda * forward / spot;
            dXDs = -muMinusLambda * x / spot;
            d2ForwardDs2 = muPlusLambda * forward / (spot * spot) * (1 + muPlusLambda);
            d2XDs2 = muMinusLambda * x / (spot * spot) * (1 + muMinusLambda);
        }

        return k * (
            (d2AlphaDs2 * forward) + (dAlphaDs * dForwardDs)
            + (dAlphaDs * dForwardDs) + (alpha * d2ForwardDs2)
            + (d2BetaDs2 * x) + (dBetaDs * dXDs)
            + (dBetaDs * dXDs) + (beta * d2XDs2));
    }

    /// <summary>The rho, the value's derivative by the risk-free rate.</summary>
    /// <param name="maturity">The time to expiry.</param>
    /// <returns>The rho.</returns>
    /// <exception cref="ArgumentException"><paramref name="maturity"/> is negative.</exception>
    public double Rho(double maturity)
    {
        if (!(maturity >= 0.0))
        {
            throw new ArgumentException("negative maturity not allowed", nameof(maturity));
        }

        if (special is { } s)
        {
            return maturity * s.RhoPerMaturity;
        }

        // actually D.Dr / T
        double dAlphaDr = -dAlphaDd1 / (lambda * stdDev) * (1.0 + mu);
        double dBetaDr = dBetaDd2 / (lambda * stdDev) * (1.0 + mu);
        double dForwardDr, dXDr;
        if (inTheMoney)
        {
            dForwardDr = 0.0;
            dXDr = 0.0;
        }
        else
        {
            dForwardDr = forward * (1.0 + ((1.0 + mu) / lambda)) * logHS / variance;
            dXDr = x * (1.0 - ((1.0 + mu) / lambda)) * logHS / variance;
        }

        return maturity * k * ((dAlphaDr * forward) + (alpha * dForwardDr) + (dBetaDr * x) + (beta * dXDr));
    }

    // log N(x), with the asymptotic tail where N(x) underflows
    internal static double LogCumNormal(double x)
    {
        if (x > -30.0)
        {
            return Math.Log(new CumulativeNormalDistribution().Evaluate(x));
        }

        double z2 = 1.0 / (x * x);
        return (-0.5 * x * x) - Math.Log(-x) - (0.5 * Math.Log(MathConstants.M_TWOPI))
            + Log1pFunctions.Log1p(-z2 * (1.0 - (z2 * (3.0 - (15.0 * z2)))));
    }

    // The underlying on its forward path, ln S(τ) = ln S + τ g with g = ln(D_q/D), over the fraction τ of the time to
    // expiry: the payoff paid at τ* = ln(H/S)/g where the path reaches the strike H by expiry, discounted by D^τ*, with
    // the value's derivatives by the spot and by the rate per unit of maturity (D = e^(−rT), g = ln D_q + rT).
    private (double Value, double Delta, double Gamma, double RhoPerMaturity) Deterministic(
        OptionType type, double discount, double dividendDiscount)
    {
        double g = Math.Log(dividendDiscount / discount);
        double c = Math.Log(discount);
        bool reaches = type == OptionType.Call ? g > 0.0 && logHS <= g : g < 0.0 && logHS >= g;
        if (!reaches)
        {
            return (0.0, 0.0, 0.0, 0.0);
        }

        double tau = logHS / g;
        double value = k * Math.Exp(c * tau);
        double delta = -value * c / (spot * g);
        double gamma = value * c / (spot * spot * g) * ((c / g) + 1.0);
        double rhoPerMaturity = -value * tau * (1.0 + (c / g));
        return (value, delta, gamma, rhoPerMaturity);
    }

    // The payoff's present value at the hit, K ∫₀^V D^(v/V) f(v) dv over the variance v accrued, f the first-passage
    // density of the log-price, drifting at μ per unit of variance, to the strike's level b = ln(H/S), with its
    // derivatives by the spot (through b) and by the rate per unit of maturity (through D and μ): QuantLib's closed form
    // where λ is real.
    private (double Value, double Delta, double Gamma, double RhoPerMaturity) FirstPassage(double discount)
    {
        double b = logHS;
        double logD = Math.Log(discount);
        double v = variance;
        double m = mu;

        double Density(double s) => s > 0.0
            ? Math.Exp(logD * s / v) * Math.Abs(b) / Math.Sqrt(2.0 * Math.PI * s * s * s) * Math.Exp(-((b - (m * s)) * (b - (m * s))) / (2.0 * s))
            : 0.0;

        // ∂f/∂b = f (1/b − (b − μv)/v); ∂²f/∂b² = f ((1/b − (b − μv)/v)² − 1/b² − 1/v)
        double DDensity(double s) => s > 0.0 ? Density(s) * ((1.0 / b) - ((b - (m * s)) / s)) : 0.0;
        double D2Density(double s)
        {
            if (!(s > 0.0))
            {
                return 0.0;
            }

            double h = (1.0 / b) - ((b - (m * s)) / s);
            return Density(s) * ((h * h) - (1.0 / (b * b)) - (1.0 / s));
        }

        // each integral's absolute accuracy on its own scale: the b-derivatives grow as 1/|b| and 1/b² as the strike
        // nears the spot, where the value's 1e-15 lies below their rounding and the quadrature never meets it
        double scale = Math.Max(1.0, 1.0 / Math.Abs(b));
        var integral = new GaussLobattoIntegral(IntegralEvaluations, IntegralAccuracy);
        double value = k * integral.Integrate(Density, 0.0, v);
        double dValueDb = k * new GaussLobattoIntegral(IntegralEvaluations, IntegralAccuracy * scale)
            .Integrate(DDensity, 0.0, v);
        double d2ValueDb2 = k * new GaussLobattoIntegral(IntegralEvaluations, IntegralAccuracy * scale * scale)
            .Integrate(D2Density, 0.0, v);
        double dValueDr = k / v * integral.Integrate(s => Density(s) * (b - (m * s) - s), 0.0, v);

        // b = ln(H/S): ∂b/∂S = −1/S
        double delta = -dValueDb / spot;
        double gamma = (dValueDb + d2ValueDb2) / (spot * spot);
        return (value, delta, gamma, dValueDr);
    }
}
