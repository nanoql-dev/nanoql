// SPDX-FileCopyrightText: Copyright (C) 2021 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/zerocouponswap.hpp ql/instruments/zerocouponswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A zero-coupon interest-rate swap: at maturity a fixed amount N[(1 + R)^α(T₀, T_K) − 1], or a given one, is swapped
/// for the floating amount N[∏(1 + α(T_k, T_{k+1}) L(T_k, T_{k+1})) − 1], the index's fixings compounded over its own
/// periods.
/// </summary>
/// <remarks>
/// The legs carry one cash flow each, on unadjusted dates; only the payment date is adjusted, the payment delay's
/// business days after maturity. The fixed leg is the first, the floating leg the second. An engine's results are a
/// <see cref="ZeroCouponSwapResults"/>, whose fair payment <see cref="PricingEngines.DiscountingSwapEngine"/>'s
/// overload gives at the fixed flow's payment date, where QuantLib's reads the discount at the fixed leg's maturity
/// date, the accrual end for the fixed-rate constructor's coupon, so with a payment delay its fair payment fed back
/// leaves a non-zero NPV (the S-cms plan's CMR5 (xxiv)).
/// </remarks>
public sealed class ZeroCouponSwap : Swap
{
    private readonly Date startDate;
    private readonly Date maturityDate;

    /// <summary>Initializes a new instance of the <see cref="ZeroCouponSwap"/> class paying a fixed amount.</summary>
    /// <param name="type">Whether the fixed amount is paid (payer) or received.</param>
    /// <param name="baseNominal">The nominal before compounding.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="maturityDate">The maturity date.</param>
    /// <param name="fixedPayment">The fixed amount.</param>
    /// <param name="iborIndex">The index compounded on the floating leg.</param>
    /// <param name="paymentCalendar">The calendar the payment date is adjusted on.</param>
    /// <param name="paymentConvention">The payment date's convention.</param>
    /// <param name="paymentDelay">The payment delay, in business days after maturity.</param>
    /// <exception cref="ArgumentNullException">The index or the calendar is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The type or the convention isn't a member of its enum, or the
    /// delay is negative.</exception>
    /// <exception cref="ArgumentException">The base nominal is negative, or the start isn't before maturity, with
    /// QuantLib's texts.</exception>
    public ZeroCouponSwap(
        SwapType type,
        double baseNominal,
        Date startDate,
        Date maturityDate,
        double fixedPayment,
        IborIndex iborIndex,
        Calendar paymentCalendar,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following,
        int paymentDelay = 0)
        : this(
            type,
            baseNominal,
            startDate,
            maturityDate,
            iborIndex,
            PaymentDateOf(maturityDate, paymentCalendar, paymentConvention, paymentDelay),
            paymentDate => new SimpleCashFlow(fixedPayment, paymentDate))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ZeroCouponSwap"/> class paying a fixed rate, compounded
    /// annually over the swap's term.</summary>
    /// <param name="type">Whether the fixed amount is paid (payer) or received.</param>
    /// <param name="baseNominal">The nominal before compounding.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="maturityDate">The maturity date.</param>
    /// <param name="fixedRate">The fixed rate.</param>
    /// <param name="fixedDayCounter">The fixed rate's day counter.</param>
    /// <param name="iborIndex">The index compounded on the floating leg.</param>
    /// <param name="paymentCalendar">The calendar the payment date is adjusted on.</param>
    /// <param name="paymentConvention">The payment date's convention.</param>
    /// <param name="paymentDelay">The payment delay, in business days after maturity.</param>
    /// <exception cref="ArgumentNullException">The day counter, the index or the calendar is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The type or the convention isn't a member of its enum, or the
    /// delay is negative.</exception>
    /// <exception cref="ArgumentException">The base nominal is negative, or the start isn't before maturity, with
    /// QuantLib's texts.</exception>
    public ZeroCouponSwap(
        SwapType type,
        double baseNominal,
        Date startDate,
        Date maturityDate,
        double fixedRate,
        DayCounter fixedDayCounter,
        IborIndex iborIndex,
        Calendar paymentCalendar,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following,
        int paymentDelay = 0)
        : this(
            type,
            baseNominal,
            startDate,
            maturityDate,
            iborIndex,
            PaymentDateOf(maturityDate, paymentCalendar, paymentConvention, paymentDelay),
            paymentDate => new FixedRateCoupon(
                paymentDate,
                baseNominal,
                new InterestRate(fixedRate, NotNull(fixedDayCounter), Compounding.Compounded, Frequency.Annual),
                startDate,
                maturityDate))
    {
    }

    private ZeroCouponSwap(
        SwapType type,
        double baseNominal,
        Date startDate,
        Date maturityDate,
        IborIndex iborIndex,
        Date paymentDate,
        Func<Date, CashFlow> fixedFlow)
        : base(BuildLegs(type, baseNominal, startDate, maturityDate, iborIndex, paymentDate, fixedFlow), Payers(type))
    {
        Type = type;
        BaseNominal = baseNominal;
        IborIndex = iborIndex;
        this.startDate = startDate;
        this.maturityDate = maturityDate;
        PaymentDate = paymentDate;
    }

    /// <summary>Gets whether the fixed amount is paid (payer) or received.</summary>
    public SwapType Type { get; }

    /// <summary>Gets the nominal before compounding.</summary>
    public double BaseNominal { get; }

    /// <summary>Gets the index compounded on the floating leg.</summary>
    public IborIndex IborIndex { get; }

    /// <summary>Gets the payment date of both amounts.</summary>
    public Date PaymentDate { get; }

    /// <summary>Gets the fixed leg.</summary>
    public ImmutableArray<CashFlow> FixedLeg => Leg(0);

    /// <summary>Gets the floating leg.</summary>
    public ImmutableArray<CashFlow> FloatingLeg => Leg(1);

    /// <summary>The start date.</summary>
    /// <returns>The date the swap was given.</returns>
    public override Date StartDate() => startDate;

    /// <summary>The maturity date.</summary>
    /// <returns>The date the swap was given.</returns>
    public override Date MaturityDate() => maturityDate;

    /// <summary>The fixed amount.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The amount.</returns>
    public double FixedPayment(EvaluationContext context) => FixedLeg[0].Amount(context);

    /// <summary>The fixed rate, compounded annually over the swap's term on a day counter, that gives the swap a zero
    /// NPV: QuantLib's <c>fairFixedRate</c>, from the results' fair payment.</summary>
    /// <param name="results">The swap's results.</param>
    /// <param name="dayCounter">The rate's day counter.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The results have no fair payment, QuantLib's "result not
    /// available".</exception>
    public double FairFixedRate(ZeroCouponSwapResults results, DayCounter dayCounter)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(dayCounter);

        // Given the relation between the fixed payment (N^FIX) and the fixed rate (R),
        // N^FIX = N * [(1 + R)^T - 1],
        // the compound factor C = (1 + R)^T
        // can be equivalently expressed as:
        // C = N^FIX / N + 1
        double payment = results.FairFixedPayment ?? throw new NanoQLException("result not available");
        double compound = (payment / BaseNominal) + 1.0;
        return InterestRate.ImpliedRate(
            compound, dayCounter, Compounding.Compounded, Frequency.Annual, startDate, maturityDate).Rate;
    }

    /// <inheritdoc/>
    public override ZeroCouponSwapResults SetupExpired() => new(base.SetupExpired());

    /// <summary>The swap's results, with the engine's fair payment when it gave one.</summary>
    /// <param name="results">The engine's results.</param>
    /// <returns>The results.</returns>
    public override ZeroCouponSwapResults FetchResults(InstrumentResults results)
    {
        SwapResults r = base.FetchResults(results);
        return r as ZeroCouponSwapResults ?? new ZeroCouponSwapResults(r);
    }

    private static ImmutableArray<CashFlow>[] BuildLegs(
        SwapType type,
        double baseNominal,
        Date startDate,
        Date maturityDate,
        IborIndex iborIndex,
        Date paymentDate,
        Func<Date, CashFlow> fixedFlow)
    {
        EnumArguments.Check(type, nameof(type));
        ArgumentNullException.ThrowIfNull(iborIndex);
        if (baseNominal < 0.0)
        {
            throw new ArgumentException("base nominal cannot be negative", nameof(baseNominal));
        }

        if (!(startDate < maturityDate))
        {
            throw new ArgumentException(
                $"start date ({startDate}) later than or equal to maturity date ({maturityDate})", nameof(startDate));
        }

        Schedule schedule = new MakeSchedule()
            .From(startDate)
            .To(maturityDate)
            .WithTenor(iborIndex.Tenor)
            .WithCalendar(iborIndex.FixingCalendar)
            .WithConvention(iborIndex.BusinessDayConvention)
            .Backwards()
            .EndOfMonth(iborIndex.EndOfMonth)
            .ToSchedule();
        var floating = new MultipleResetsCoupon(
            paymentDate, baseNominal, schedule, iborIndex.FixingDays, iborIndex, pricer: new CompoundingMultipleResetsPricer());
        return [[fixedFlow(paymentDate)], [floating]];
    }

    // QuantLib's payer_: the payer pays the fixed leg and receives the floating one
    private static bool[] Payers(SwapType type) => type == SwapType.Payer ? [true, false] : [false, true];

    private static Date PaymentDateOf(Date maturityDate, Calendar paymentCalendar, BusinessDayConvention convention, int delay)
    {
        ArgumentNullException.ThrowIfNull(paymentCalendar);
        EnumArguments.Check(convention, "paymentConvention");
        ArgumentOutOfRangeException.ThrowIfNegative(delay, "paymentDelay");
        return paymentCalendar.Advance(maturityDate, delay, TimeUnit.Days, convention);
    }

    private static DayCounter NotNull(DayCounter dayCounter) =>
        dayCounter ?? throw new ArgumentNullException(nameof(dayCounter));
}
