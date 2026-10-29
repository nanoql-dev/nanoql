// SPDX-FileCopyrightText: Copyright (C) 2008 Toyin Akin
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/coupons/quantocouponpricer.hpp ql/experimental/coupons/quantocouponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A Black Ibor coupon pricer whose fixing is quanto-adjusted for an index paid in another currency: QuantLib's
/// <c>BlackIborQuantoCouponPricer</c>.
/// </summary>
/// <remarks>
/// The fixing is adjusted first, then by the base pricer's timing adjustment: for a fixing after the optionlet
/// volatility's reference date, with σ the optionlet volatility and σ_FX the exchange rate's Black volatility at the
/// fixing date and the fixing, ρ their correlation and t the fixing time, the shifted fixing times e^(σσ_FXρt) on a
/// shifted lognormal volatility, and the fixing plus σσ_FXρt on a normal one (Hull, 6th edition, p. 642, as QuantLib's).
/// QuantLib's correlation quote is a <see cref="double"/>.
/// </remarks>
[Experimental("NQL0001")]
public sealed class BlackIborQuantoCouponPricer : BlackIborCouponPricer
{
    private readonly BlackVolTermStructure fxRateBlackVolatility;
    private readonly double underlyingFxCorrelation;

    /// <summary>Initializes a new instance of the <see cref="BlackIborQuantoCouponPricer"/> class.</summary>
    /// <param name="fxRateBlackVolatility">The exchange rate's Black volatility.</param>
    /// <param name="underlyingFxCorrelation">The correlation of the index with the exchange rate.</param>
    /// <param name="capletVolatility">The optionlet volatility.</param>
    /// <param name="context">The evaluation context, whose at-par switch the pricer takes, as QuantLib's takes the
    /// global one.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public BlackIborQuantoCouponPricer(
        BlackVolTermStructure fxRateBlackVolatility,
        double underlyingFxCorrelation,
        OptionletVolatilityStructure capletVolatility,
        EvaluationContext context)
        : base(context, capletVolatility ?? throw new ArgumentNullException(nameof(capletVolatility)))
    {
        this.fxRateBlackVolatility = fxRateBlackVolatility ?? throw new ArgumentNullException(nameof(fxRateBlackVolatility));
        this.underlyingFxCorrelation = underlyingFxCorrelation;
    }

    /// <inheritdoc/>
    /// <remarks>The quanto adjustment first, then the base pricer's.</remarks>
    protected override double AdjustedFixing(IborCoupon coupon, EvaluationContext context, double? fixing = null)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        double f = fixing ?? coupon.IndexFixing(context, UseIndexedCoupon);

        // Here we apply the quanto adjustment first, then delegate to
        // the parent class
        OptionletVolatilityStructure vol = CapletVolatility!;
        Date d1 = coupon.StoredFixingDate;
        Date referenceDate = vol.ReferenceDate;
        if (d1 > referenceDate)
        {
            double t1 = vol.TimeFromReference(d1);
            double fxsigma = fxRateBlackVolatility.BlackVol(d1, f, true);
            double sigma = vol.Volatility(d1, f);
            double rho = underlyingFxCorrelation;

            // Apply Quanto Adjustment.
            // Hull 6th Edition, page 642, generalised to
            // shifted lognormal and normal volatilities
            if (vol.VolatilityType == VolatilityType.ShiftedLognormal)
            {
                double dQuantoAdj = Math.Exp(sigma * fxsigma * rho * t1);
                double shift = vol.Displacement;
                f = ((f + shift) * dQuantoAdj) - shift;
            }
            else
            {
                double dQuantoAdj = sigma * fxsigma * rho * t1;
                f += dQuantoAdj;
            }
        }

        return base.AdjustedFixing(coupon, context, f);
    }
}
