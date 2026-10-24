// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/digitalcoupon.hpp ql/cashflows/digitalcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// A floating-rate coupon with a digital call, a digital put or both: QuantLib's <c>DigitalCoupon</c>, priced by
/// call and put spread replication through capped and floored coupons on its underlying.
/// </summary>
/// <remarks>
/// <para>
/// Payoffs: a cash-or-nothing call pays rate + ξ·payoff·H(rate − strike), a put rate + ξ·payoff·H(strike − rate); an
/// asset-or-nothing call pays rate + ξ·rate·H(rate − strike), a put rate + ξ·rate·H(strike − rate), with ξ = +1 for a
/// long option and −1 for a short one. A naked option's rate in the payoff is 0.
/// </para>
/// <para>
/// Each digital is the spread of two capped (call) or floored (put) coupons on the underlying, at the strike plus and
/// minus the replication's gap, so its rate reads the underlying's pricer through those coupons, each building the
/// pricer's state once, as QuantLib's coupons each initialize it. A fixed rate (a fixing before the evaluation date, or
/// on it when the context enforces today's historic fixings or the index holds it) pays the digital's payoff directly.
/// The coupon keeps its underlying's ex-coupon date and fixing convention, which QuantLib's drops to their defaults, no
/// ex-coupon date and Preceding (R8, the S-cms plan's CMR5 (ix)), so its fixing date is its underlying's.
/// </para>
/// <para>
/// Unsealed, as in QuantLib, where the digital CMS, Ibor and CMS spread coupons derive from it. A copy with another
/// pricer (<see cref="FloatingRateCouponExtensions.SetPricer"/>) carries it into a copy of the underlying, as
/// QuantLib's <c>setPricer</c> sets both.
/// </para>
/// </remarks>
public class DigitalCoupon : FloatingRateCoupon
{
    // Written only by the constructor and on a fresh copy (WithPricer).
    private FloatingRateCoupon underlying;
    private readonly double callStrike;
    private readonly double putStrike;
    private readonly double callCsi;
    private readonly double putCsi;
    private readonly bool isCallAtmIncluded;
    private readonly bool isPutAtmIncluded;
    private readonly bool isCallCashOrNothing;
    private readonly bool isPutCashOrNothing;
    private readonly double callDigitalPayoff;
    private readonly double putDigitalPayoff;
    private readonly double callLeftEps;
    private readonly double callRightEps;
    private readonly double putLeftEps;
    private readonly double putRightEps;
    private readonly bool hasPutStrike;
    private readonly bool hasCallStrike;
    private readonly bool nakedOption;

    /// <summary>Initializes a new instance of the <see cref="DigitalCoupon"/> class.</summary>
    /// <param name="underlying">The coupon whose rate the options are on.</param>
    /// <param name="callStrike">The call's strike, or <see langword="null"/> for no call.</param>
    /// <param name="callPosition">The call's position.</param>
    /// <param name="isCallAtmIncluded">Whether a fixed rate at the call's strike pays the call.</param>
    /// <param name="callDigitalPayoff">The cash-or-nothing call's payoff, or <see langword="null"/> for an
    /// asset-or-nothing call.</param>
    /// <param name="putStrike">The put's strike, or <see langword="null"/> for no put.</param>
    /// <param name="putPosition">The put's position.</param>
    /// <param name="isPutAtmIncluded">Whether a fixed rate at the put's strike pays the put.</param>
    /// <param name="putDigitalPayoff">The cash-or-nothing put's payoff, or <see langword="null"/> for an
    /// asset-or-nothing put.</param>
    /// <param name="replication">The replication; QuantLib's default, central with a gap of 1e-4, when
    /// <see langword="null"/>.</param>
    /// <param name="nakedOption">Whether the coupon pays the options alone, without the underlying's rate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="underlying"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The replication's gap isn't positive, or a payoff is given without its
    /// strike.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A position given with its strike isn't a member of its
    /// type.</exception>
    public DigitalCoupon(
        FloatingRateCoupon underlying,
        double? callStrike = null,
        PositionType callPosition = PositionType.Long,
        bool isCallAtmIncluded = false,
        double? callDigitalPayoff = null,
        double? putStrike = null,
        PositionType putPosition = PositionType.Long,
        bool isPutAtmIncluded = false,
        double? putDigitalPayoff = null,
        DigitalReplication? replication = null,
        bool nakedOption = false)
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
        this.isCallAtmIncluded = isCallAtmIncluded;
        this.isPutAtmIncluded = isPutAtmIncluded;
        this.nakedOption = nakedOption;

        replication ??= new DigitalReplication();
        if (!(replication.Gap > 0.0))
        {
            throw new ArgumentException("Non positive epsilon not allowed", nameof(replication));
        }

        callLeftEps = callRightEps = putLeftEps = putRightEps = replication.Gap / 2;

        if (putStrike is null && putDigitalPayoff is not null)
        {
            throw new ArgumentException("Put Cash rate non allowed if put strike is null", nameof(putDigitalPayoff));
        }

        if (callStrike is null && callDigitalPayoff is not null)
        {
            throw new ArgumentException("Call Cash rate non allowed if call strike is null", nameof(callDigitalPayoff));
        }

        if (callStrike is double c)
        {
            EnumArguments.Check(callPosition, nameof(callPosition));
            hasCallStrike = true;
            this.callStrike = c;
            callCsi = callPosition == PositionType.Long ? 1.0 : -1.0;
            if (callDigitalPayoff is double payoff)
            {
                this.callDigitalPayoff = payoff;
                isCallCashOrNothing = true;
            }
        }

        if (putStrike is double p)
        {
            EnumArguments.Check(putPosition, nameof(putPosition));
            hasPutStrike = true;
            this.putStrike = p;
            putCsi = putPosition == PositionType.Long ? 1.0 : -1.0;
            if (putDigitalPayoff is double payoff)
            {
                this.putDigitalPayoff = payoff;
                isPutCashOrNothing = true;
            }
        }

        double gap = replication.Gap;
        switch (replication.ReplicationType)
        {
            case ReplicationType.Central:
                // do nothing
                break;
            case ReplicationType.Sub:
                if (hasCallStrike)
                {
                    (callLeftEps, callRightEps) = callPosition == PositionType.Long ? (0.0, gap) : (gap, 0.0);
                }

                if (hasPutStrike)
                {
                    (putLeftEps, putRightEps) = putPosition == PositionType.Long ? (gap, 0.0) : (0.0, gap);
                }

                break;
            default:
                // Super
                if (hasCallStrike)
                {
                    (callLeftEps, callRightEps) = callPosition == PositionType.Long ? (gap, 0.0) : (0.0, gap);
                }

                if (hasPutStrike)
                {
                    (putLeftEps, putRightEps) = putPosition == PositionType.Long ? (0.0, gap) : (gap, 0.0);
                }

                break;
        }
    }

    /// <summary>Gets the coupon whose rate the options are on.</summary>
    public FloatingRateCoupon Underlying => underlying;

    /// <summary>Gets the call's strike, or <see langword="null"/> for no call.</summary>
    public double? CallStrike => hasCallStrike ? callStrike : null;

    /// <summary>Gets the put's strike, or <see langword="null"/> for no put.</summary>
    public double? PutStrike => hasPutStrike ? putStrike : null;

    /// <summary>Gets the cash-or-nothing call's payoff, or <see langword="null"/> for an asset-or-nothing call or
    /// none.</summary>
    public double? CallDigitalPayoff => isCallCashOrNothing ? callDigitalPayoff : null;

    /// <summary>Gets the cash-or-nothing put's payoff, or <see langword="null"/> for an asset-or-nothing put or
    /// none.</summary>
    public double? PutDigitalPayoff => isPutCashOrNothing ? putDigitalPayoff : null;

    /// <summary>Gets a value indicating whether the coupon has a put.</summary>
    public bool HasPut => hasPutStrike;

    /// <summary>Gets a value indicating whether the coupon has a call.</summary>
    public bool HasCall => hasCallStrike;

    /// <summary>Gets a value indicating whether the coupon has both a call and a put.</summary>
    public bool HasCollar => hasCallStrike && hasPutStrike;

    /// <summary>Gets a value indicating whether the put is long.</summary>
    public bool IsLongPut => putCsi == 1.0;

    /// <summary>Gets a value indicating whether the call is long.</summary>
    public bool IsLongCall => callCsi == 1.0;

    /// <summary>The rate: the underlying's (0 for a naked option) plus the call's and the put's, each at its
    /// position.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="NanoQLException">The underlying has no pricer.</exception>
    public override double Rate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (underlying.Pricer is null)
        {
            throw new NanoQLException("pricer not set");
        }

        Date fixingDate = underlying.FixingDate();
        Date today = context.EvaluationDate;
        double underlyingRate = nakedOption ? 0.0 : underlying.Rate(context);
        if (fixingDate < today || ((fixingDate == today) && context.EnforcesTodaysHistoricFixings))
        {
            // must have been fixed
            return underlyingRate + (callCsi * CallPayoff(context)) + (putCsi * PutPayoff(context));
        }
        else if (fixingDate == today && underlying.Index.HasHistoricalFixing(fixingDate, context))
        {
            // might have been fixed
            return underlyingRate + (callCsi * CallPayoff(context)) + (putCsi * PutPayoff(context));
        }
        else
        {
            return underlyingRate + (callCsi * CallOptionRate(context)) + (putCsi * PutOptionRate(context));
        }
    }

    /// <summary>The underlying's convexity adjustment.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The adjustment.</returns>
    public override double ConvexityAdjustment(EvaluationContext context) => underlying.ConvexityAdjustment(context);

    /// <summary>
    /// The call's rate, which times the nominal, the accrual period and the discount is the option's NPV: the cap
    /// spread's step at the strike, times the payoff (or the strike, asset-or-nothing, plus the call on the rate).
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate; 0 for no call.</returns>
    public double CallOptionRate(EvaluationContext context)
    {
        double callOptionRate = 0.0;
        if (hasCallStrike)
        {
            // Step function
            callOptionRate = isCallCashOrNothing ? callDigitalPayoff : callStrike;
            var next = new CappedFlooredCoupon(underlying, callStrike + callRightEps);
            var previous = new CappedFlooredCoupon(underlying, callStrike - callLeftEps);
            callOptionRate *= (next.Rate(context) - previous.Rate(context)) / (callLeftEps + callRightEps);
            if (!isCallCashOrNothing)
            {
                // Call
                var atStrike = new CappedFlooredCoupon(underlying, callStrike);
                double call = underlying.Rate(context) - atStrike.Rate(context);

                // Sum up
                callOptionRate += call;
            }
        }

        return callOptionRate;
    }

    /// <summary>
    /// The put's rate, which times the nominal, the accrual period and the discount is the option's NPV: the floor
    /// spread's step at the strike, times the payoff (or the strike, asset-or-nothing, less the put on the rate).
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate; 0 for no put.</returns>
    public double PutOptionRate(EvaluationContext context)
    {
        double putOptionRate = 0.0;
        if (hasPutStrike)
        {
            // Step function
            putOptionRate = isPutCashOrNothing ? putDigitalPayoff : putStrike;
            var next = new CappedFlooredCoupon(underlying, null, putStrike + putRightEps);
            var previous = new CappedFlooredCoupon(underlying, null, putStrike - putLeftEps);
            putOptionRate *= (next.Rate(context) - previous.Rate(context)) / (putLeftEps + putRightEps);
            if (!isPutCashOrNothing)
            {
                // Put
                var atStrike = new CappedFlooredCoupon(underlying, null, putStrike);
                double put = -underlying.Rate(context) + atStrike.Rate(context);

                // Sum up
                putOptionRate -= put;
            }
        }

        return putOptionRate;
    }

    /// <summary>A copy holding another pricer, with a copy of the underlying holding it too.</summary>
    /// <param name="pricer">The pricer, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    protected internal override FloatingRateCoupon WithPricer(FloatingRateCouponPricer? pricer)
    {
        var copy = (DigitalCoupon)base.WithPricer(pricer);
        copy.underlying = underlying.SetPricer(pricer);
        return copy;
    }

    private static FloatingRateCoupon NotNull(FloatingRateCoupon underlying)
    {
        ArgumentNullException.ThrowIfNull(underlying);
        return underlying;
    }

    // to use only if index has fixed
    private double CallPayoff(EvaluationContext context)
    {
        double payoff = 0.0;
        if (hasCallStrike)
        {
            double underlyingRate = underlying.Rate(context);
            if ((underlyingRate - callStrike) > 1.0e-16)
            {
                payoff = isCallCashOrNothing ? callDigitalPayoff : underlyingRate;
            }
            else if (isCallAtmIncluded && Math.Abs(callStrike - underlyingRate) <= 1.0e-16)
            {
                payoff = isCallCashOrNothing ? callDigitalPayoff : underlyingRate;
            }
        }

        return payoff;
    }

    // to use only if index has fixed
    private double PutPayoff(EvaluationContext context)
    {
        double payoff = 0.0;
        if (hasPutStrike)
        {
            double underlyingRate = underlying.Rate(context);
            if ((putStrike - underlyingRate) > 1.0e-16)
            {
                payoff = isPutCashOrNothing ? putDigitalPayoff : underlyingRate;
            }
            else if (isPutAtmIncluded && Math.Abs(putStrike - underlyingRate) <= 1.0e-16)
            {
                // putStrike <= underlyingRate
                payoff = isPutCashOrNothing ? putDigitalPayoff : underlyingRate;
            }
        }

        return payoff;
    }
}
