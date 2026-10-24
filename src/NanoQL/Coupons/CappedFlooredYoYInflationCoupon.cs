// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/capflooredinflationcoupon.hpp ql/cashflows/capflooredinflationcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>A year-on-year inflation coupon with a cap, a floor or both on its rate.</summary>
/// <remarks>
/// <para>
/// The payoff of a capped coupon is N T min(a L + b, C), of a floored one N T max(a L + b, F), and of a collared one
/// N T min(max(a L + b, F), C), where N is the nominal, T the accrual time, L the inflation rate, a its gearing, b the
/// spread, and C and F the strikes; at a gearing that isn't positive the cap becomes a floor on the fixing, and the
/// floor a cap. The rate is the swaplet's plus the floorlet's minus the caplet's, through the pricer of the underlying
/// when there is one, as QuantLib's.
/// </para>
/// <para>
/// A copy with another pricer (<see cref="InflationCouponExtensions.SetPricer"/>) carries the pricer into a copy of the
/// underlying too, as QuantLib's own <c>setPricer</c> sets both; QuantLib's <c>setCouponPricer</c> calls the
/// non-virtual <c>InflationCoupon::setPricer</c>, so a coupon built on an underlying keeps the underlying's old pricer
/// there (the S-cms plan's CMR5 (xvii)), which the copy fixes by construction.
/// </para>
/// </remarks>
public sealed class CappedFlooredYoYInflationCoupon : YoYInflationCoupon
{
    // Written only by the constructors and on a fresh copy (WithPricer).
    private YoYInflationCoupon? underlying;
    private readonly bool isCapped;
    private readonly bool isFloored;
    private readonly double capValue;
    private readonly double floorValue;

    /// <summary>Initializes a new instance of the <see cref="CappedFlooredYoYInflationCoupon"/> class on an
    /// underlying coupon, whose terms it copies and whose pricer prices it.</summary>
    /// <param name="underlying">The coupon whose rate is capped or floored.</param>
    /// <param name="cap">The cap on the coupon's rate, or <see langword="null"/> for none.</param>
    /// <param name="floor">The floor on the coupon's rate, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="underlying"/> is null.</exception>
    /// <exception cref="ArgumentException">The coupon is capped and floored with the cap below the floor, with
    /// QuantLib's text.</exception>
    public CappedFlooredYoYInflationCoupon(YoYInflationCoupon underlying, double? cap = null, double? floor = null)
        : base(
            NotNull(underlying).Date,
            underlying.ContractNominal,
            underlying.AccrualStartDate,
            underlying.AccrualEndDate,
            underlying.FixingDays,
            underlying.YoYIndex,
            underlying.ObservationLag,
            underlying.Interpolation,
            underlying.DayCounter,
            underlying.Gearing,
            underlying.Spread,
            underlying.ReferencePeriodStart,
            underlying.ReferencePeriodEnd)
    {
        this.underlying = underlying;
        (isCapped, isFloored, capValue, floorValue) = Common(Gearing, cap, floor);
    }

    /// <summary>Initializes a new instance of the <see cref="CappedFlooredYoYInflationCoupon"/> class on its own
    /// terms.</summary>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The accrual start date.</param>
    /// <param name="endDate">The accrual end date.</param>
    /// <param name="fixingDays">The fixing days.</param>
    /// <param name="index">The year-on-year inflation index.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <param name="interpolation">How the index is observed.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="gearing">The gearing, the multiplier of the fixing.</param>
    /// <param name="spread">The spread over the geared fixing.</param>
    /// <param name="cap">The cap on the coupon's rate, or <see langword="null"/> for none.</param>
    /// <param name="floor">The floor on the coupon's rate, or <see langword="null"/> for none.</param>
    /// <param name="refPeriodStart">The reference period's start, the accrual start by default.</param>
    /// <param name="refPeriodEnd">The reference period's end, the accrual end by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> or <paramref name="dayCounter"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative, or
    /// <paramref name="interpolation"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, or the coupon is capped and floored with the
    /// cap below the floor, with QuantLib's text.</exception>
    public CappedFlooredYoYInflationCoupon(
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        int fixingDays,
        YoYInflationIndex index,
        Period observationLag,
        CPIInterpolationType interpolation,
        DayCounter dayCounter,
        double gearing = 1.0,
        double spread = 0.0,
        double? cap = null,
        double? floor = null,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null)
        : base(
            paymentDate,
            nominal,
            startDate,
            endDate,
            fixingDays,
            index,
            observationLag,
            interpolation,
            dayCounter,
            gearing,
            spread,
            refPeriodStart,
            refPeriodEnd)
    {
        (isCapped, isFloored, capValue, floorValue) = Common(Gearing, cap, floor);
    }

    /// <summary>Gets the underlying coupon, or <see langword="null"/> for a coupon built on its own terms.</summary>
    public YoYInflationCoupon? Underlying => underlying;

    /// <summary>Gets a value indicating whether the fixing is capped, the coupon's floor at a gearing that isn't
    /// positive.</summary>
    public bool IsCapped => isCapped;

    /// <summary>Gets a value indicating whether the fixing is floored, the coupon's cap at a gearing that isn't
    /// positive.</summary>
    public bool IsFloored => isFloored;

    /// <summary>Gets the cap on the coupon's rate, or <see langword="null"/> for none, QuantLib's
    /// <c>Null&lt;Rate&gt;()</c>, which it is at a zero gearing too.</summary>
    public double? Cap => (Gearing > 0 && isCapped) ? capValue : (Gearing < 0 && isFloored) ? floorValue : null;

    /// <summary>Gets the floor on the coupon's rate, or <see langword="null"/> for none, which it is at a zero gearing
    /// too.</summary>
    public double? Floor => (Gearing > 0 && isFloored) ? floorValue : (Gearing < 0 && isCapped) ? capValue : null;

    /// <summary>Gets the cap on the fixing, (C − b) / a, or <see langword="null"/> when the fixing isn't capped, where
    /// QuantLib reads an unset member.</summary>
    public double? EffectiveCap => isCapped ? (capValue - Spread) / Gearing : null;

    /// <summary>Gets the floor on the fixing, (F − b) / a, or <see langword="null"/> when the fixing isn't floored,
    /// where QuantLib reads an unset member.</summary>
    public double? EffectiveFloor => isFloored ? (floorValue - Spread) / Gearing : null;

    /// <summary>The rate before the cap and floor: the underlying's, or the coupon's own swaplet rate.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    public double UnderlyingRate(EvaluationContext context) => underlying is not null ? underlying.Rate(context) : base.Rate(context);

    /// <summary>The rate: the swaplet's plus the floorlet's minus the caplet's.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="NanoQLException">The coupon is capped or floored and has no pricer, QuantLib's "pricer not
    /// set".</exception>
    public override double Rate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        double swapletRate = UnderlyingRate(context);

        // the coupon QuantLib's pricer was initialized with, by the swaplet's rate
        InflationCoupon priced = (InflationCoupon?)underlying ?? this;
        InflationCouponPricer? couponPricer = priced.Pricer;
        if (isFloored || isCapped)
        {
            if (couponPricer is null)
            {
                throw new NanoQLException("pricer not set");
            }
        }

        double floorletRate = isFloored ? couponPricer!.FloorletRate(priced, EffectiveFloor!.Value, context) : 0.0;
        double capletRate = isCapped ? couponPricer!.CapletRate(priced, EffectiveCap!.Value, context) : 0.0;
        return swapletRate + floorletRate - capletRate;
    }

    /// <summary>A copy holding another pricer, its underlying's copy holding it too.</summary>
    /// <param name="pricer">The pricer.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentException">The coupon refuses the pricer, a null one among them.</exception>
    protected internal override InflationCoupon WithPricer(InflationCouponPricer? pricer)
    {
        var copy = (CappedFlooredYoYInflationCoupon)base.WithPricer(pricer);
        if (underlying is not null)
        {
            copy.underlying = (YoYInflationCoupon)underlying.WithPricer(pricer);
        }

        return copy;
    }

    // QuantLib's setCommon: a gearing that isn't positive swaps the cap and the floor on the fixing.
    private static (bool IsCapped, bool IsFloored, double Cap, double Floor) Common(double gearing, double? cap, double? floor)
    {
        bool capped = false, floored = false;
        double capValue = 0.0, floorValue = 0.0;
        if (gearing > 0)
        {
            if (cap is double c)
            {
                capped = true;
                capValue = c;
            }

            if (floor is double f)
            {
                floorValue = f;
                floored = true;
            }
        }
        else
        {
            if (cap is double c)
            {
                floorValue = c;
                floored = true;
            }

            if (floor is double f)
            {
                capped = true;
                capValue = f;
            }
        }

        if (capped && floored)
        {
            if (!(cap >= floor))
            {
                throw new ArgumentException(
                    $"cap level ({Show(cap!.Value)}) less than floor level ({Show(floor!.Value)})", nameof(cap));
            }
        }

        return (capped, floored, capValue, floorValue);
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    private static YoYInflationCoupon NotNull(YoYInflationCoupon underlying) =>
        underlying ?? throw new ArgumentNullException(nameof(underlying));
}
