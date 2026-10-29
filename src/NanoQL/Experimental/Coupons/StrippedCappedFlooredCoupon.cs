// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/coupons/strippedcapflooredcoupon.hpp ql/experimental/coupons/strippedcapflooredcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;

namespace NanoQL.Experimental;

/// <summary>
/// The option embedded in a capped or floored coupon, stripped from it: QuantLib's
/// <c>StrippedCappedFlooredCoupon</c>.
/// </summary>
/// <remarks>
/// The rate is the embedded option's: a long floorlet, a long caplet, or for a collar the floorlet less the caplet, at
/// the underlying's effective strikes, priced by the underlying's own pricer; QuantLib caches it, and here it is computed
/// per call. A pricer set on the stripped coupon is set on its underlying too, as QuantLib's. The coupon's terms are the
/// underlying's, its ex-coupon date and fixing convention QuantLib's constructor's defaults.
/// </remarks>
[Experimental("NQL0001")]
public sealed class StrippedCappedFlooredCoupon : FloatingRateCoupon
{
    // Written only by the constructor and on a fresh copy (WithPricer).
    private CappedFlooredCoupon underlying;

    /// <summary>Initializes a new instance of the <see cref="StrippedCappedFlooredCoupon"/> class.</summary>
    /// <param name="underlying">The capped or floored coupon.</param>
    /// <exception cref="ArgumentNullException"><paramref name="underlying"/> is null.</exception>
    public StrippedCappedFlooredCoupon(CappedFlooredCoupon underlying)
        : base(
            (underlying ?? throw new ArgumentNullException(nameof(underlying))).Date,
            underlying.ContractNominal,
            underlying.AccrualStartDate,
            underlying.AccrualEndDate,
            underlying.FixingDays,
            underlying.Index,
            underlying.Gearing,
            underlying.Spread,
            underlying.ReferencePeriodStart,
            underlying.ReferencePeriodEnd,
            underlying.DayCounter,
            underlying.IsInArrears)
    {
        this.underlying = underlying;
    }

    /// <summary>Gets the capped or floored coupon.</summary>
    public CappedFlooredCoupon Underlying => underlying;

    /// <summary>Gets the underlying's cap.</summary>
    public double? Cap => underlying.Cap;

    /// <summary>Gets the underlying's floor.</summary>
    public double? Floor => underlying.Floor;

    /// <summary>Gets the underlying's effective cap.</summary>
    public double? EffectiveCap => underlying.EffectiveCap;

    /// <summary>Gets the underlying's effective floor.</summary>
    public double? EffectiveFloor => underlying.EffectiveFloor;

    /// <summary>Gets a value indicating whether the underlying is capped.</summary>
    public bool IsCap => underlying.IsCapped;

    /// <summary>Gets a value indicating whether the underlying is floored.</summary>
    public bool IsFloor => underlying.IsFloored;

    /// <summary>Gets a value indicating whether the underlying is both capped and floored.</summary>
    public bool IsCollar => IsCap && IsFloor;

    /// <inheritdoc/>
    /// <remarks>The embedded option's rate, by the underlying's own pricer.</remarks>
    /// <exception cref="NanoQLException">The underlying's own underlying has no pricer.</exception>
    public override double Rate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        FloatingRateCoupon inner = underlying.Underlying;
        FloatingRateCouponPricer pricer = inner.Pricer ?? throw new NanoQLException("pricer not set");
        CouponRateView rates = pricer.RateView(inner, context);
        double floorletRate = 0.0;
        if (underlying.IsFloored)
        {
            floorletRate = rates.FloorletRate(underlying.EffectiveFloor!.Value);
        }

        double capletRate = 0.0;
        if (underlying.IsCapped)
        {
            capletRate = rates.CapletRate(underlying.EffectiveCap!.Value);
        }

        // if the underlying is collared we return the value of the embedded
        // collar, otherwise the value of a long floor or a long cap respectively
        return underlying.IsFloored && underlying.IsCapped ? floorletRate - capletRate : floorletRate + capletRate;
    }

    /// <inheritdoc/>
    /// <remarks>The underlying's.</remarks>
    public override double ConvexityAdjustment(EvaluationContext context) => underlying.ConvexityAdjustment(context);

    /// <summary>A copy holding another pricer, with a copy of the underlying holding it too.</summary>
    /// <param name="pricer">The pricer, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    protected internal override FloatingRateCoupon WithPricer(FloatingRateCouponPricer? pricer)
    {
        var copy = (StrippedCappedFlooredCoupon)base.WithPricer(pricer);
        copy.underlying = (CappedFlooredCoupon)underlying.SetPricer(pricer);
        return copy;
    }
}
