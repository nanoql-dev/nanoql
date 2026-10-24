// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/assetswap.hpp ql/instruments/assetswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A bond's asset swap: the bond's coupons and final payment against a floating leg, at par (the bond bought at par,
/// an upfront paying its premium) or at market (the floating notional scaled by the bond's dirty price); D. O'Kane,
/// "Introduction to Asset Swaps", Lehman Brothers European Fixed Income Research, January 2000.
/// </summary>
/// <remarks>
/// <para>
/// The bond's clean price is its (forward) price at the floating schedule's start, the upfront date. On an overnight
/// index the floating schedule must be given, and the floating coupons are overnight-indexed. QuantLib warns that the
/// fair prices are wrong with indexed coupons.
/// </para>
/// <para>
/// A non-par repayment is per 100 of the bond's notional at the upfront date, as the fair repayment reads it, and paid
/// as that share of the notional, where QuantLib's pays the number given as an amount
/// (<c>assetswap.cpp:125-134</c>); with none given the swap repays the bond's redemption, and its repayment is that
/// redemption per 100 of notional, where QuantLib's stores 100 whatever the redemption (<c>:130</c>) (the S-cms plan's
/// CMR5 (xix), (xx)). On a bond of 100 redeemed at 100 the two agree.
/// </para>
/// <para>
/// The swap is priced by <see cref="PricingEngines.DiscountingSwapEngine"/>'s asset swap overload, its fair values
/// derived from the swap's results (the S-cms plan's CMD6); QuantLib's <c>AssetSwap::arguments</c>, which no QuantLib
/// engine reads, aren't ported.
/// </para>
/// </remarks>
public sealed class AssetSwap : Swap
{
    private const double BasisPoint = 1.0e-4;

    private readonly double bondNotional;
    private readonly double bondAccruedAtUpfront;

    /// <summary>Initializes a new instance of the <see cref="AssetSwap"/> class.</summary>
    /// <param name="payBondCoupon">Whether the bond's coupons are paid (the floating leg received).</param>
    /// <param name="bond">The bond.</param>
    /// <param name="bondCleanPrice">The bond's clean price at the upfront date, per 100 of notional.</param>
    /// <param name="iborIndex">The floating leg's index, an Ibor or an overnight one.</param>
    /// <param name="spread">The floating leg's spread.</param>
    /// <param name="context">The evaluation context, from which the bond's settlement date starts a default floating
    /// schedule, and whose at-par switch the floating coupons' default pricer reads.</param>
    /// <param name="floatSchedule">The floating schedule, or <see langword="null"/> for one from the bond's
    /// settlement date to its maturity on the index's tenor, calendar and convention; required on an overnight
    /// index.</param>
    /// <param name="floatingDayCount">The floating coupons' day counter, or <see langword="null"/> for the
    /// index's.</param>
    /// <param name="parSwap">Whether the swap is at par, or at market.</param>
    /// <param name="gearing">The floating leg's gearing.</param>
    /// <param name="nonParRepayment">The final repayment per 100 of notional, or <see langword="null"/> for the bond's
    /// redemption.</param>
    /// <param name="dealMaturity">The deal's maturity, or <see langword="null"/> for the floating schedule's
    /// end.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bond"/>, <paramref name="iborIndex"/> or
    /// <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">The deal maturity is after the schedule's end or not after its start, each
    /// with QuantLib's text.</exception>
    /// <exception cref="NanoQLException">An overnight index without a floating schedule, or a bond without cash
    /// flows, each with QuantLib's text.</exception>
    public AssetSwap(
        bool payBondCoupon,
        Bond bond,
        double bondCleanPrice,
        IborIndex iborIndex,
        double spread,
        EvaluationContext context,
        Schedule? floatSchedule = null,
        DayCounter? floatingDayCount = null,
        bool parSwap = true,
        double gearing = 1.0,
        double? nonParRepayment = null,
        Date? dealMaturity = null)
        : this(Build(
            payBondCoupon,
            bond,
            bondCleanPrice,
            iborIndex,
            spread,
            context,
            floatSchedule,
            floatingDayCount,
            parSwap,
            gearing,
            nonParRepayment,
            dealMaturity))
    {
    }

    private AssetSwap(Terms terms)
        : base([terms.BondLeg, terms.FloatingLeg], [terms.PayBondCoupon, !terms.PayBondCoupon])
    {
        Bond = terms.Bond;
        CleanPrice = terms.BondCleanPrice;
        NonParRepayment = terms.NonParRepayment;
        Spread = terms.Spread;
        ParSwap = terms.ParSwap;
        UpfrontDate = terms.UpfrontDate;
        bondNotional = terms.BondNotional;
        bondAccruedAtUpfront = terms.BondAccruedAtUpfront;
    }

    /// <summary>Gets the bond.</summary>
    public Bond Bond { get; }

    /// <summary>Gets the bond's clean price at the upfront date.</summary>
    public double CleanPrice { get; }

    /// <summary>Gets the final repayment per 100 of notional: the one given, or the bond's redemption's.</summary>
    public double NonParRepayment { get; }

    /// <summary>Gets the floating leg's spread.</summary>
    public double Spread { get; }

    /// <summary>Gets whether the swap is at par, or at market.</summary>
    public bool ParSwap { get; }

    /// <summary>Gets whether the bond's coupons are paid.</summary>
    public bool PayBondCoupon => Payer(0);

    /// <summary>Gets the upfront date, the floating schedule's start.</summary>
    public Date UpfrontDate { get; }

    /// <summary>Gets the bond leg: its coupons up to the deal's maturity, any accrued coupon, and the final
    /// payment.</summary>
    public ImmutableArray<CashFlow> BondLeg => Leg(0);

    /// <summary>Gets the floating leg, with the upfront and the back payment of a par swap, or the final notional
    /// exchange of a market one.</summary>
    public ImmutableArray<CashFlow> FloatingLeg => Leg(1);

    /// <inheritdoc/>
    /// <remarks>An expired swap has zero leg NPVs, BPS and discounts, so fair values of NaN, the quotients of zeros
    /// QuantLib derives (<c>assetswap.cpp:236-304</c>).</remarks>
    public override AssetSwapResults SetupExpired() => FetchResults(base.SetupExpired());

    /// <inheritdoc/>
    /// <remarks>QuantLib's <c>fairSpread</c>, <c>fairCleanPrice</c> and <c>fairNonParRepayment</c>, each derived from
    /// the swap's results unless the results hold it: the fair clean price needs the floating leg's start discount,
    /// unknown for a seasoned deal, and the fair repayment its end discount.</remarks>
    public override AssetSwapResults FetchResults(InstrumentResults results)
    {
        SwapResults r = base.FetchResults(results);
        var fetched = r as AssetSwapResults;
        double? fairSpread = fetched?.FairSpread;
        double? fairCleanPrice = fetched?.FairCleanPrice;
        double? fairNonParRepayment = fetched?.FairNonParRepayment;
        double npv = r.NPV ?? 0.0;

        if (fairSpread is null && r.LegBPS.Length > 1 && r.LegBPS[1] is double bps)
        {
            fairSpread = Spread - (npv / bps * BasisPoint);
        }

        if (fairCleanPrice is null && r.StartDiscounts.Length > 1 && r.StartDiscounts[1] is double startDiscount)
        {
            if (ParSwap)
            {
                fairCleanPrice = CleanPrice
                    - (PayerSign(1) * npv * (r.NpvDateDiscount ?? 1.0) / startDiscount / (bondNotional / 100.0));
            }
            else if (r.LegNPV[0] is double bondNpv && r.LegNPV[1] is double floatingNpv)
            {
                double dirtyPrice = CleanPrice + bondAccruedAtUpfront;
                double fairDirtyPrice = -bondNpv / floatingNpv * dirtyPrice;
                fairCleanPrice = fairDirtyPrice - bondAccruedAtUpfront;
            }
        }

        if (fairNonParRepayment is null && r.EndDiscounts.Length > 1 && r.EndDiscounts[1] is double endDiscount)
        {
            fairNonParRepayment = NonParRepayment
                - (PayerSign(0) * npv * (r.NpvDateDiscount ?? 1.0) / endDiscount / (bondNotional / 100.0));
        }

        return (fetched ?? new AssetSwapResults(r)) with
        {
            FairSpread = fairSpread,
            FairCleanPrice = fairCleanPrice,
            FairNonParRepayment = fairNonParRepayment,
        };
    }

    /// <summary>The spread that gives the swap a zero NPV.</summary>
    /// <param name="results">The swap's results.</param>
    /// <returns>The fair spread.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    /// <exception cref="NanoQLException">The results hold none, QuantLib's "fair spread not available".</exception>
    public static double FairSpread(AssetSwapResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.FairSpread ?? throw new NanoQLException("fair spread not available");
    }

    /// <summary>The floating leg's BPS.</summary>
    /// <param name="results">The swap's results.</param>
    /// <returns>The BPS.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    /// <exception cref="NanoQLException">The results hold none, QuantLib's "floating-leg BPS not
    /// available".</exception>
    public static double FloatingLegBPS(AssetSwapResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.LegBPS.Length > 1 && results.LegBPS[1] is double bps
            ? bps
            : throw new NanoQLException("floating-leg BPS not available");
    }

    /// <summary>The floating leg's NPV.</summary>
    /// <param name="results">The swap's results.</param>
    /// <returns>The NPV.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    /// <exception cref="NanoQLException">The results hold none, QuantLib's "floating-leg NPV not
    /// available".</exception>
    public static double FloatingLegNPV(AssetSwapResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.LegNPV.Length > 1 && results.LegNPV[1] is double npv
            ? npv
            : throw new NanoQLException("floating-leg NPV not available");
    }

    /// <summary>The bond's clean price that gives the swap a zero NPV.</summary>
    /// <param name="results">The swap's results.</param>
    /// <returns>The fair clean price.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    /// <exception cref="NanoQLException">The results hold none, a seasoned deal's, QuantLib's "fair clean price not
    /// available for seasoned deal".</exception>
    public static double FairCleanPrice(AssetSwapResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.FairCleanPrice ?? throw new NanoQLException("fair clean price not available for seasoned deal");
    }

    /// <summary>The final repayment that gives the swap a zero NPV.</summary>
    /// <param name="results">The swap's results.</param>
    /// <returns>The fair repayment.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    /// <exception cref="NanoQLException">The results hold none, an expired leg's, QuantLib's "fair non par repayment
    /// not available for expired leg".</exception>
    public static double FairNonParRepayment(AssetSwapResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.FairNonParRepayment
            ?? throw new NanoQLException("fair non par repayment not available for expired leg");
    }

    // QuantLib's payer_[j]: -1 for a paid leg, +1 for a received one.
    private double PayerSign(int j) => Payer(j) ? -1.0 : 1.0;

    // QuantLib's constructor's body: the two legs and the terms the fair values read.
    private static Terms Build(
        bool payBondCoupon,
        Bond bond,
        double bondCleanPrice,
        IborIndex iborIndex,
        double spread,
        EvaluationContext context,
        Schedule? floatSchedule,
        DayCounter? floatingDayCount,
        bool parSwap,
        double gearing,
        double? nonParRepayment,
        Date? dealMaturity)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(iborIndex);
        ArgumentNullException.ThrowIfNull(context);
        var overnight = iborIndex as OvernightIndex;
        if (overnight is not null && (floatSchedule is null || floatSchedule.Size == 0))
        {
            throw new NanoQLException("floating schedule is needed when using an overnight index");
        }

        Schedule schedule = floatSchedule is null || floatSchedule.Size == 0
            ? new Schedule(
                bond.SettlementDate(context),
                bond.MaturityDate(),
                iborIndex.Tenor,
                iborIndex.FixingCalendar,
                iborIndex.BusinessDayConvention,
                iborIndex.BusinessDayConvention,
                DateGenerationRule.Backward,
                false, // endOfMonth
                context)
            : floatSchedule;

        Date maturity = dealMaturity ?? schedule.Back();
        if (!(maturity <= schedule.Back()))
        {
            throw new ArgumentException(
                $"deal maturity {maturity} cannot be later than (adjusted) bond maturity {schedule.Back()}",
                nameof(dealMaturity));
        }

        if (!(maturity > schedule.Front()))
        {
            throw new ArgumentException(
                $"deal maturity {maturity} must be later than swap start date {schedule.Front()}",
                nameof(dealMaturity));
        }

        // the following might become an input parameter
        const BusinessDayConvention paymentAdjustment = BusinessDayConvention.Following;
        Date finalDate = (schedule.Calendar ?? EmptyCalendar.Instance).Adjust(maturity, paymentAdjustment);
        schedule = schedule.Until(finalDate);

        // bondCleanPrice must be the (forward) clean price
        // at the floating schedule start date
        Date upfrontDate = schedule.StartDate();
        double accrued = bond.AccruedAmount(context, upfrontDate);
        double dirtyPrice = bondCleanPrice + accrued;
        double bondNotional = bond.Notional(context, upfrontDate);
        double notional = bondNotional;

        // In the market asset swap, the bond is purchased in return for
        // payment of the full price. The notional of the floating leg is
        // then scaled by the full price.
        if (!parSwap)
        {
            notional *= dirtyPrice / 100.0;
        }

        // Bond leg
        ImmutableArray<CashFlow> bondCashflows = bond.Cashflows;
        if (bondCashflows.IsEmpty)
        {
            throw new NanoQLException("no cashflows from bond");
        }

        var bondLeg = ImmutableArray.CreateBuilder<CashFlow>();

        // a cash flow on the upfront date must be discarded
        const bool includeOnUpfrontDate = false;

        // add coupons for the time being, not the redemption
        int i = 0;
        for (; i < bondCashflows.Length - 1 && bondCashflows[i].Date <= maturity; ++i)
        {
            if (!bondCashflows[i].HasOccurred(context, upfrontDate, includeOnUpfrontDate))
            {
                bondLeg.Add(bondCashflows[i]);
            }
        }

        // if we're skipping a cashflow before the redemption
        // and it's a coupon, then add the accrued coupon.
        if (i < bondCashflows.Length - 1 && bondCashflows[i] is Coupon c)
        {
            double accruedAmount = c.AccruedAmount(maturity, context);
            bondLeg.Add(new SimpleCashFlow(accruedAmount, finalDate));
        }

        // add the redemption, or whatever the final payment is, per 100 of the bond's notional (CMR5 (xix), (xx))
        double repayment;
        if (nonParRepayment is double given)
        {
            repayment = given;
            bondLeg.Add(new SimpleCashFlow(given * bondNotional / 100.0, finalDate));
        }
        else
        {
            double redemption = bondCashflows[^1].Amount(context);
            bondLeg.Add(new SimpleCashFlow(redemption, finalDate));
            repayment = redemption / bondNotional * 100.0;
        }

        // Floating leg
        ImmutableArray<CashFlow> floating = overnight is not null
            ? new OvernightLeg(schedule, overnight)
                .WithNotionals(notional)
                .WithPaymentAdjustment(paymentAdjustment)
                .WithGearings(gearing)
                .WithSpreads(spread)
                .WithPaymentDayCounter(floatingDayCount)
                .ToLeg()
            : new IborLeg(schedule, iborIndex)
                .WithNotionals(notional)
                .WithPaymentAdjustment(paymentAdjustment)
                .WithGearings(gearing)
                .WithSpreads(spread)
                .WithPaymentDayCounter(floatingDayCount)
                .ToLeg(context);

        if (parSwap)
        {
            // upfront
            double upfront = (dirtyPrice - 100.0) / 100.0 * notional;

            // backpayment (accounts for non-par redemption, if any)
            double backPayment = notional;
            floating =
                [new SimpleCashFlow(upfront, upfrontDate), .. floating, new SimpleCashFlow(backPayment, finalDate)];
        }
        else
        {
            // final notional exchange
            floating = [.. floating, new SimpleCashFlow(notional, finalDate)];
        }

        return new Terms(
            payBondCoupon,
            bond,
            bondCleanPrice,
            spread,
            parSwap,
            repayment,
            upfrontDate,
            bondNotional,
            accrued,
            bondLeg.ToImmutable(),
            floating);
    }

    private sealed record Terms(
        bool PayBondCoupon,
        Bond Bond,
        double BondCleanPrice,
        double Spread,
        bool ParSwap,
        double NonParRepayment,
        Date UpfrontDate,
        double BondNotional,
        double BondAccruedAtUpfront,
        ImmutableArray<CashFlow> BondLeg,
        ImmutableArray<CashFlow> FloatingLeg);
}
