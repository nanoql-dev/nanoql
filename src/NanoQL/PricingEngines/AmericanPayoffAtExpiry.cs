// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/americanpayoffatexpiry.hpp ql/pricingengines/americanpayoffatexpiry.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic formula for American exercise with the payoff at expiry: a cash-or-nothing or asset-or-nothing payoff
/// paid at expiry if the underlying has reached the strike (knock-in: an up-and-in call or a down-and-in put), or if it
/// hasn't (knock-out: an up-and-out call or a down-and-out put).
/// </summary>
/// <remarks>
/// <para>
/// An immutable calculator over the spot, the discount and dividend discount to expiry and the variance to expiry, as
/// QuantLib's; its value. QuantLib's master after the pin keeps the price at low volatility (#2855, taken under R8, the
/// S-van plan's VR5 (i)): where N(D₂) underflows or the power it multiplies overflows, their finite product is taken in
/// logs, through log N's asymptotic tail. Only where N(D₂) is the formula's: with the strike on the spot, a knock-out
/// is already knocked out and QuantLib sets N(D₂) to 0, which #2855's test reads as an underflow, pricing −D·K·N(D₂)
/// where QuantLib 1.44 prices 0; here 1.44's 0 stands, a documented divergence found by the S-van run
/// (<c>Divergences/AmericanPayoffAtExpiryKnockedOutTests.cs</c>).
/// </para>
/// <para>
/// Below a variance of ε, QuantLib takes the spot's side of the strike for both probabilities
/// (<c>americanpayoffatexpiry.cpp:112-121</c>), so a call whose forward passes the strike is worth nothing; here the
/// underlying follows its forward, which reaches the strike by expiry where the forward is past it, and the payoff is
/// paid at expiry if it did (knock-in) or didn't (knock-out): a documented divergence (the S-van plan's VR4 (ii),
/// <c>Divergences/AmericanPayoffZeroVarianceTests.cs</c>).
/// </para>
/// <para>
/// A payoff other than a cash-or-nothing or asset-or-nothing one is refused, where QuantLib leaves the paid amount
/// unset.
/// </para>
/// </remarks>
public sealed class AmericanPayoffAtExpiry
{
    private readonly double discount;
    private readonly double k;
    private readonly double cumD1;
    private readonly double cumD2;
    private readonly double x;
    private readonly double y;

    // The value below a variance of ε (the S-van plan's VR4 (ii)).
    private readonly double? deterministic;

    /// <summary>Initializes a new instance of the <see cref="AmericanPayoffAtExpiry"/> class.</summary>
    /// <param name="spot">The underlying's value.</param>
    /// <param name="discount">The risk-free discount to expiry.</param>
    /// <param name="dividendDiscount">The dividend discount to expiry.</param>
    /// <param name="variance">The variance to expiry.</param>
    /// <param name="payoff">The payoff, cash-or-nothing or asset-or-nothing.</param>
    /// <param name="knockIn">Whether the payoff is paid if the strike was reached (otherwise if it wasn't).</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The spot, the discount or the dividend discount isn't positive, the variance is negative, or the payoff is
    /// neither cash-or-nothing nor asset-or-nothing.
    /// </exception>
    public AmericanPayoffAtExpiry(
        double spot,
        double discount,
        double dividendDiscount,
        double variance,
        StrikedTypePayoff payoff,
        bool knockIn = true)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        this.discount = discount;

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

        double stdDev = Math.Sqrt(variance);

        OptionType type = payoff.OptionType;
        double strike = payoff.Strike;
        double forward = spot * dividendDiscount / discount;

        double mu = (Math.Log(dividendDiscount / discount) / variance) - 0.5;

        // binary cash-or-nothing payoff? binary asset-or-nothing payoff? QuantLib leaves the amount unset otherwise.
        switch (payoff)
        {
            case CashOrNothingPayoff coo:
                k = coo.CashPayoff;
                break;
            case AssetOrNothingPayoff:
                k = forward;
                mu += 1.0;
                break;
            default:
                throw new ArgumentException(
                    $"{payoff.Name} payoff given: a cash-or-nothing or asset-or-nothing payoff required", nameof(payoff));
        }

        double logHS = Math.Log(strike / spot);
        double logSH = Math.Log(spot / strike);

        double eta;
        double phi;
        switch (type)
        {
            case OptionType.Call:
                if (knockIn)
                {
                    // up-and-in cash-(at-expiry)-or-nothing option
                    // a.k.a. american call with cash-or-nothing payoff
                    eta = -1.0;
                    phi = 1.0;
                }
                else
                {
                    // up-and-out cash-(at-expiry)-or-nothing option
                    eta = -1.0;
                    phi = -1.0;
                }

                break;
            case OptionType.Put:
                if (knockIn)
                {
                    // down-and-in cash-(at-expiry)-or-nothing option
                    // a.k.a. american put with cash-or-nothing payoff
                    eta = 1.0;
                    phi = -1.0;
                }
                else
                {
                    // down-and-out cash-(at-expiry)-or-nothing option
                    eta = 1.0;
                    phi = 1.0;
                }

                break;
            default:
                throw new NanoQLException("invalid option type");
        }

        double d2Value = 0.0;
        if (variance >= NumericLimits.QL_EPSILON)
        {
            double d1Value = phi * ((logSH / stdDev) + (mu * stdDev));
            d2Value = eta * ((logHS / stdDev) + (mu * stdDev));

            var f = new CumulativeNormalDistribution();
            cumD1 = f.Evaluate(d1Value);
            cumD2 = f.Evaluate(d2Value);
        }
        else
        {
            cumD1 = logSH * phi > 0 ? 1.0 : 0.0;
            cumD2 = logHS * eta > 0 ? 1.0 : 0.0;
        }

        bool reached;
        switch (type)
        {
            case OptionType.Call:
                reached = strike <= spot;
                if (reached)
                {
                    // up-and-in: a.k.a. american call with cash-or-nothing payoff; up-and-out: already knocked out
                    cumD1 = knockIn ? 0.5 : 0.0;
                    cumD2 = knockIn ? 0.5 : 0.0;
                }

                break;
            default:
                reached = strike >= spot;
                if (reached)
                {
                    // down-and-in: a.k.a. american put with cash-or-nothing payoff; down-and-out: already knocked out
                    cumD1 = knockIn ? 0.5 : 0.0;
                    cumD2 = knockIn ? 0.5 : 0.0;
                }

                break;
        }

        bool inTheMoney = (type == OptionType.Call && strike < spot) || (type == OptionType.Put && strike > spot);
        if (inTheMoney)
        {
            x = 1.0;
            y = 1.0;
        }
        else
        {
            x = 1.0;
            y = Math.Pow(strike / spot, 2.0 * mu);

            // #2855's log form only where N(D2) is the formula's: at the strike, a knock-out already knocked out has
            // N(D2) set to 0, which #2855's test reads as an underflow, pricing −D·K·N(D2) where 1.44 prices 0
            if (!reached && variance >= NumericLimits.QL_EPSILON && (cumD2 == 0.0 || !double.IsFinite(y)))
            {
                // the power overflows where N(D2) underflows, but their product is finite
                cumD2 = Math.Exp((2.0 * mu * logHS) + LogCumNormalAtExpiry(d2Value));
                y = 1.0;
            }
            else if (cumD2 == 0.0)
            {
                y = 0.0;
            }
        }

        if (!knockIn)
        {
            y *= -1.0;
        }

        if (!(variance >= NumericLimits.QL_EPSILON) && !reached)
        {
            // the underlying on its forward (the S-van plan's VR4 (ii)): it reaches the strike by expiry where the forward
            // is past it; paid at expiry if it did (knock-in) or didn't (knock-out)
            bool reachesByExpiry = type == OptionType.Call ? forward >= strike : forward <= strike;
            deterministic = reachesByExpiry == knockIn ? discount * k : 0.0;
        }
    }

    /// <summary>The value.</summary>
    /// <returns>The value.</returns>
    public double Value() => deterministic ?? discount * k * ((x * cumD1) + (y * cumD2));

    // log N(x), with the asymptotic tail where N(x) underflows
    private static double LogCumNormalAtExpiry(double x) => AmericanPayoffAtHit.LogCumNormal(x);
}
