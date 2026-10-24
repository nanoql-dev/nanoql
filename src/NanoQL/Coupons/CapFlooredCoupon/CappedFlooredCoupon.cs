// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/capflooredcoupon.hpp ql/cashflows/capflooredcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Coupons;

/// <summary>A floating-rate coupon with a cap, a floor or both on its rate.</summary>
/// <remarks>
/// <para>
/// The payoff of a capped coupon is N T min(a L + b, C), of a floored one N T max(a L + b, F), and of a collared one
/// N T min(max(a L + b, F), C), where N is the nominal, T the accrual time, L the floating rate, a its gearing, b the
/// spread, and C and F the strikes. A capped rate decomposes as (a L + b) + |a| min((C − b) / |a| − ξ L, 0), with
/// ξ = sgn(a): the swaplet's rate less a caplet on the fixing, struck at the effective cap (C − b) / a, and at a
/// negative gearing the cap becomes a floor on the fixing, and the floor a cap.
/// </para>
/// <para>
/// The rate is the underlying's swaplet rate plus the floorlet's minus the caplet's, all through the underlying's own
/// pricer, read from one view of its rates for the call, so a pricer whose per-coupon state costs more than its queries
/// builds it once, as QuantLib's one <c>initialize</c> serves the three. A copy with another pricer
/// (<see cref="FloatingRateCouponExtensions.SetPricer"/>) carries the pricer into a copy of the underlying too, as
/// QuantLib's <c>setPricer</c> sets both. The coupon copies the underlying's terms when it is built, its nominal from
/// <see cref="Coupon.ContractNominal"/>, since a constructor has no context.
/// </para>
/// <para>Unsealed, as in QuantLib, where the capped Ibor and CMS coupons derive from it.</para>
/// </remarks>
public class CappedFlooredCoupon : FloatingRateCoupon
{
    // Written only by the constructor and on a fresh copy (WithPricer).
    private FloatingRateCoupon underlying;
    private readonly bool isCapped;
    private readonly bool isFloored;
    private readonly double capValue;
    private readonly double floorValue;

    /// <summary>Initializes a new instance of the <see cref="CappedFlooredCoupon"/> class.</summary>
    /// <param name="underlying">The coupon whose rate is capped or floored.</param>
    /// <param name="cap">The cap on the coupon's rate, or <see langword="null"/> for none.</param>
    /// <param name="floor">The floor on the coupon's rate, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">Both a cap and a floor are given, and the cap is less than the floor.</exception>
    public CappedFlooredCoupon(FloatingRateCoupon underlying, double? cap = null, double? floor = null)
        : base(
            NotNull(underlying).Date,
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
            underlying.IsInArrears,
            underlying.ExCouponDate,
            underlying.FixingConvention)
    {
        this.underlying = underlying;

        if (Gearing > 0)
        {
            if (cap is double c)
            {
                isCapped = true;
                capValue = c;
            }

            if (floor is double f)
            {
                floorValue = f;
                isFloored = true;
            }
        }
        else
        {
            if (cap is double c)
            {
                floorValue = c;
                isFloored = true;
            }

            if (floor is double f)
            {
                isCapped = true;
                capValue = f;
            }
        }

        if (isCapped && isFloored)
        {
            if (!(cap >= floor))
            {
                throw new ArgumentException(
                    $"cap level ({Show(cap!.Value)}) less than floor level ({Show(floor!.Value)})", nameof(cap));
            }
        }
    }

    /// <summary>Gets the coupon whose rate is capped or floored.</summary>
    public FloatingRateCoupon Underlying => underlying;

    /// <summary>Gets a value indicating whether the fixing is capped.</summary>
    public bool IsCapped => isCapped;

    /// <summary>Gets a value indicating whether the fixing is floored.</summary>
    public bool IsFloored => isFloored;

    /// <summary>Gets the cap on the coupon's rate, or <see langword="null"/> for none.</summary>
    public double? Cap
    {
        get
        {
            if ((Gearing > 0) && isCapped)
            {
                return capValue;
            }

            if ((Gearing < 0) && isFloored)
            {
                return floorValue;
            }

            return null;
        }
    }

    /// <summary>Gets the floor on the coupon's rate, or <see langword="null"/> for none.</summary>
    public double? Floor
    {
        get
        {
            if ((Gearing > 0) && isFloored)
            {
                return floorValue;
            }

            if ((Gearing < 0) && isCapped)
            {
                return capValue;
            }

            return null;
        }
    }

    /// <summary>Gets the cap on the fixing, (cap − spread) / gearing, or <see langword="null"/> when uncapped.</summary>
    public double? EffectiveCap => isCapped ? (capValue - Spread) / Gearing : null;

    /// <summary>Gets the floor on the fixing, (floor − spread) / gearing, or <see langword="null"/> when unfloored.</summary>
    public double? EffectiveFloor => isFloored ? (floorValue - Spread) / Gearing : null;

    /// <summary>The rate: the underlying's swaplet rate, plus the floorlet's rate, less the caplet's.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="NanoQLException">The underlying has no pricer.</exception>
    public override double Rate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        FloatingRateCouponPricer pricer = underlying.Pricer ?? throw new NanoQLException("pricer not set");
        CouponRateView rates = pricer.RateView(underlying, context);
        double swapletRate = rates.SwapletRate();
        double floorletRate = 0.0;
        if (isFloored)
        {
            floorletRate = rates.FloorletRate(EffectiveFloor!.Value);
        }

        double capletRate = 0.0;
        if (isCapped)
        {
            capletRate = rates.CapletRate(EffectiveCap!.Value);
        }

        return swapletRate + floorletRate - capletRate;
    }

    /// <summary>The underlying's convexity adjustment.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The adjustment.</returns>
    public override double ConvexityAdjustment(EvaluationContext context) => underlying.ConvexityAdjustment(context);

    /// <summary>A copy holding another pricer, with a copy of the underlying holding it too.</summary>
    /// <param name="pricer">The pricer, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    protected internal override FloatingRateCoupon WithPricer(FloatingRateCouponPricer? pricer)
    {
        var copy = (CappedFlooredCoupon)base.WithPricer(pricer);
        copy.underlying = underlying.SetPricer(pricer);
        return copy;
    }

    private static FloatingRateCoupon NotNull(FloatingRateCoupon underlying)
    {
        ArgumentNullException.ThrowIfNull(underlying);
        return underlying;
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}
