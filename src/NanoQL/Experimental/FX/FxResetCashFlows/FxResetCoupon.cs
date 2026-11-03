// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/fx/fxresetcashflows.hpp ql/experimental/fx/fxresetcashflows.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A floating-rate coupon of an FX-resetting leg.</summary>
/// <remarks>
/// <para>
/// The coupon wraps an underlying floating-rate coupon: its nominal is the constant leg's notional converted at the
/// period's FX-reset observation, by its FX-reset pricer; its rate is the underlying's, and its amounts the
/// underlying's rescaled to that nominal, which keeps the underlying's accrual logic, e.g. the partial-period
/// compounding of an overnight-indexed coupon. <see cref="Coupon.ContractNominal"/> is the underlying's.
/// </para>
/// <para>
/// Immutable, as every coupon is: <see cref="SetFxResetPricer"/> returns a copy holding the pricer, and a coupon
/// pricer set through <see cref="FloatingRateCouponExtensions.SetPricer"/> is forwarded to a copy of the underlying,
/// whose pricer drives the paid rate.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class FxResetCoupon : FloatingRateCoupon
{
    // Written only by the constructor and on a fresh copy (WithPricer, SetFxResetPricer).
    private FloatingRateCoupon underlying;
    private FxResetPricer? fxResetPricer;

    /// <summary>Initializes a new instance of the <see cref="FxResetCoupon"/> class.</summary>
    /// <param name="underlying">The underlying floating-rate coupon.</param>
    /// <param name="constantLegNotional">The constant leg's notional, in its own currency.</param>
    /// <param name="fxReset">The FX-reset observation of the coupon's period.</param>
    /// <exception cref="ArgumentNullException"><paramref name="underlying"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The underlying coupon's nominal is zero.</exception>
    public FxResetCoupon(FloatingRateCoupon underlying, double constantLegNotional, FxReset fxReset)
        : base(
            Checked(underlying).Date,
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
        ConstantLegNotional = constantLegNotional;
        FxReset = fxReset;
    }

    /// <summary>Gets the underlying floating-rate coupon.</summary>
    public FloatingRateCoupon Underlying => underlying;

    /// <summary>Gets the constant leg's notional, in its own currency.</summary>
    public double ConstantLegNotional { get; }

    /// <summary>Gets the FX-reset observation of the coupon's period.</summary>
    public FxReset FxReset { get; }

    /// <summary>Gets the date on which the FX rate is observed.</summary>
    public Date FxResetDate => FxReset.FixingDate;

    /// <summary>Gets the settlement date of the FX observation.</summary>
    public Date FxResetValueDate => FxReset.ValueDate;

    /// <summary>Gets the FX-reset pricer, or <see langword="null"/> when none is set.</summary>
    public FxResetPricer? FxResetPricer => fxResetPricer;

    /// <summary>The nominal: the constant leg's notional converted at the observation's FX rate.</summary>
    /// <param name="context">The evaluation context, which the FX-reset pricer reads.</param>
    /// <returns>The nominal.</returns>
    /// <exception cref="NanoQLException">No FX-reset pricer is set.</exception>
    public override double Nominal(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        FxResetPricer pricer = fxResetPricer ?? throw new NanoQLException(
            "no FX reset pricer set; the coupon nominal is available only after assigning an FX reset pricer");
        return ConstantLegNotional * pricer.FxRate(FxReset, context);
    }

    /// <inheritdoc/>
    public override double Rate(EvaluationContext context) => underlying.Rate(context);

    /// <summary>The underlying coupon's amount, rescaled to the FX-reset nominal.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The amount.</returns>
    public override double Amount(EvaluationContext context) => underlying.Amount(context) * NominalScale(context);

    /// <summary>The underlying coupon's accrued amount, rescaled to the FX-reset nominal.</summary>
    /// <param name="d">The date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The accrued amount.</returns>
    public override double AccruedAmount(Date d, EvaluationContext context) =>
        underlying.AccruedAmount(d, context) * NominalScale(context);

    /// <inheritdoc/>
    public override Date FixingDate() => underlying.FixingDate();

    /// <inheritdoc/>
    public override double IndexFixing(EvaluationContext context) => underlying.IndexFixing(context);

    /// <summary>A copy of the coupon holding an FX-reset pricer: QuantLib's <c>setFxResetPricer</c>.</summary>
    /// <param name="pricer">The pricer, or <see langword="null"/> for none.</param>
    /// <returns>The copy; the receiver is unchanged.</returns>
    public FxResetCoupon SetFxResetPricer(FxResetPricer? pricer)
    {
        var copy = (FxResetCoupon)MemberwiseClone();
        copy.fxResetPricer = pricer;
        return copy;
    }

    /// <inheritdoc/>
    /// <remarks>The pricer is forwarded to a copy of the underlying coupon, whose pricer drives the paid
    /// rate.</remarks>
    protected internal override FloatingRateCoupon WithPricer(FloatingRateCouponPricer? pricer)
    {
        var copy = (FxResetCoupon)base.WithPricer(pricer);
        copy.underlying = underlying.WithPricer(pricer);
        return copy;
    }

    /// <inheritdoc/>
    internal override bool SpreadIsCompoundedDaily => underlying.SpreadIsCompoundedDaily;

    /// <inheritdoc/>
    internal override FloatingRateCoupon? WithSpreadShift(double shift) =>
        underlying.WithSpreadShift(shift) is { } shifted
            ? new FxResetCoupon(shifted, ConstantLegNotional, FxReset).SetFxResetPricer(fxResetPricer)
            : null;

    private static FloatingRateCoupon Checked(FloatingRateCoupon underlying)
    {
        ArgumentNullException.ThrowIfNull(underlying);
        if (underlying.ContractNominal == 0.0)
        {
            throw new ArgumentException("underlying coupon nominal cannot be zero", nameof(underlying));
        }

        return underlying;
    }

    private double NominalScale(EvaluationContext context) => Nominal(context) / underlying.Nominal(context);
}
