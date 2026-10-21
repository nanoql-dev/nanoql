// SPDX-FileCopyrightText: Copyright (C) 2006 Allen Kuo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/forwardrateagreement.hpp ql/instruments/forwardrateagreement.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A forward rate agreement (FRA).</summary>
/// <remarks>
/// <para>
/// Unlike the forward contract conventions on carryable financial assets (stocks, bonds, commodities), the value date
/// for a FRA is taken to be the day when the forward loan or deposit begins and when full settlement takes place (based
/// on the NPV of the contract on that date). The maturity date is the date when the forward loan or deposit ends. In
/// fact, the FRA settles and expires on the value date, not on the (later) maturity date. It follows that the time
/// from the value date to the maturity date is the tenor or term of the underlying loan or deposit.
/// </para>
/// <para>
/// Choose <see cref="PositionType.Long"/> for an "FRA purchase" (future long loan, short deposit, the borrower), and
/// <see cref="PositionType.Short"/> for an "FRA sale" (future short loan, long deposit, the lender).
/// </para>
/// <para>
/// Immutable. QuantLib's FRA computes in the instrument, with no engine: <see cref="Calculate"/> does, running
/// <c>Instrument::calculate</c>'s steps and returning the NPV, the payoff and the forward rate
/// (<see cref="ForwardRateAgreementResults"/>). Nothing is cached: each call computes again.
/// </para>
/// <para>
/// QuantLib's to-do list: preconditions and tests, and the BBA (British), AFB (French, assumed here) and ABA
/// (Australian) banker conventions told apart in the calculations. QuantLib warns that the class still needs to be
/// rigorously tested.
/// </para>
/// </remarks>
public sealed class ForwardRateAgreement : Instrument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForwardRateAgreement"/> class whose forward rate the index
    /// forecasts: its fixing for the value date, its maturity the index's. This corresponds to
    /// <c>useIndexedCoupon = true</c> in <see cref="FraRateHelper"/>.
    /// </summary>
    /// <param name="index">The index, which holds the forecasting curve.</param>
    /// <param name="valueDate">The date the underlying loan or deposit starts accruing and the FRA settles.</param>
    /// <param name="position">Long for an FRA purchase, short for an FRA sale.</param>
    /// <param name="strikeForwardRate">The contract rate, simple on the index's day counter.</param>
    /// <param name="notionalAmount">The notional amount.</param>
    /// <param name="discountCurve">The curve discounting the payoff; the index's forecasting curve when
    /// <see langword="null"/>, QuantLib's empty handle.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The notional isn't positive, or the value date isn't before the maturity
    /// date.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> isn't a member of its enum.
    /// </exception>
    public ForwardRateAgreement(
        IborIndex index,
        Date valueDate,
        PositionType position,
        double strikeForwardRate,
        double notionalAmount,
        YieldTermStructure? discountCurve = null)
        : this(
            index,
            valueDate,
            IndexMaturityDate(index, valueDate),
            position,
            strikeForwardRate,
            notionalAmount,
            discountCurve)
    {
        UseIndexedCoupon = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForwardRateAgreement"/> class whose forward rate is the par
    /// approximation: the simple rate from the value date to the maturity date on the index's forecasting curve. This
    /// corresponds to <c>useIndexedCoupon = false</c> in <see cref="FraRateHelper"/>.
    /// </summary>
    /// <param name="index">The index, which holds the forecasting curve.</param>
    /// <param name="valueDate">The date the underlying loan or deposit starts accruing and the FRA settles.</param>
    /// <param name="maturityDate">The date the underlying loan or deposit ends, adjusted on the index's fixing calendar
    /// by its convention.</param>
    /// <param name="position">Long for an FRA purchase, short for an FRA sale.</param>
    /// <param name="strikeForwardRate">The contract rate, simple on the index's day counter.</param>
    /// <param name="notionalAmount">The notional amount.</param>
    /// <param name="discountCurve">The curve discounting the payoff; the index's forecasting curve when
    /// <see langword="null"/>, QuantLib's empty handle.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The notional isn't positive, or the value date isn't before the adjusted
    /// maturity date.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> isn't a member of its enum.
    /// </exception>
    public ForwardRateAgreement(
        IborIndex index,
        Date valueDate,
        Date maturityDate,
        PositionType position,
        double strikeForwardRate,
        double notionalAmount,
        YieldTermStructure? discountCurve = null)
    {
        ArgumentNullException.ThrowIfNull(index);
        Date.ThrowIfDefault(valueDate, nameof(valueDate));
        Date.ThrowIfDefault(maturityDate, nameof(maturityDate));
        EnumArguments.Check(position, nameof(position));
        Position = position;
        NotionalAmount = notionalAmount;
        Index = index;
        DayCounter = index.DayCounter;
        Calendar = index.FixingCalendar;
        BusinessDayConvention = index.BusinessDayConvention;
        ValueDate = valueDate;
        MaturityDate = Calendar.Adjust(maturityDate, BusinessDayConvention);
        DiscountCurve = discountCurve;
        if (!(notionalAmount > 0.0))
        {
            throw new ArgumentException("notionalAmount must be positive", nameof(notionalAmount));
        }

        if (!(ValueDate < MaturityDate))
        {
            throw new ArgumentException("valueDate must be earlier than maturityDate", nameof(valueDate));
        }

        StrikeForwardRate = new InterestRate(strikeForwardRate, index.DayCounter, Compounding.Simple, Frequency.Once);
    }

    /// <summary>Gets the index, which holds the forecasting curve.</summary>
    public IborIndex Index { get; }

    /// <summary>Gets the position: long for an FRA purchase, short for an FRA sale.</summary>
    public PositionType Position { get; }

    /// <summary>Gets the contract rate, the FRA fixing rate: simple, on the index's day counter.</summary>
    public InterestRate StrikeForwardRate { get; }

    /// <summary>Gets the notional amount.</summary>
    public double NotionalAmount { get; }

    /// <summary>Gets a value indicating whether the forward rate is the index fixing (the value-date constructor)
    /// rather than the par approximation.</summary>
    public bool UseIndexedCoupon { get; }

    /// <summary>Gets the index's day counter.</summary>
    public DayCounter DayCounter { get; }

    /// <summary>Gets the index's fixing calendar.</summary>
    public Calendar Calendar { get; }

    /// <summary>Gets the index's business-day convention.</summary>
    public BusinessDayConvention BusinessDayConvention { get; }

    /// <summary>Gets the date the underlying index starts accruing and the FRA is settled.</summary>
    public Date ValueDate { get; }

    /// <summary>Gets the maturity date of the underlying index; not the date the FRA is settled.</summary>
    public Date MaturityDate { get; }

    /// <summary>Gets the curve discounting the payoff, the term structure relevant to the contract (a repo curve, for
    /// instance); <see langword="null"/> when the index's forecasting curve discounts.</summary>
    public YieldTermStructure? DiscountCurve { get; }

    /// <summary>The index's fixing date for the value date.</summary>
    /// <returns>The fixing date.</returns>
    public Date FixingDate() => Index.FixingDate(ValueDate);

    /// <inheritdoc/>
    /// <remarks>An FRA expires and settles on its value date.</remarks>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(ValueDate).HasOccurred(context);
    }

    /// <summary>
    /// Prices the FRA: QuantLib's <c>Instrument::calculate</c> with the FRA's own calculation, the payoff on the value
    /// date discounted to the curve's reference date.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The NPV, the payoff and the forward rate; an expired FRA's NPV is zero, and its payoff and forward
    /// rate the fixed ones, or <see langword="null"/> under the par approximation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">No curve discounts the payoff, the index has no forecasting curve where one is
    /// read, or a past fixing is missing.</exception>
    public ForwardRateAgreementResults Calculate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IsExpired(context))
        {
            return SetupExpired(context);
        }

        Validate();
        InterestRate forwardRate = CalculateForwardRate(context);
        double amount = CalculateAmount(forwardRate);
        YieldTermStructure discount = DiscountCurve
            ?? Index.ForwardingTermStructure
            ?? throw new NanoQLException("empty Handle cannot be dereferenced");
        return FetchResults(new ForwardRateAgreementResults
        {
            NPV = amount * discount.Discount(ValueDate),
            Amount = amount,
            ForwardRate = forwardRate,
        });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The payoff and the forward rate are <see langword="null"/> here: <see cref="Calculate"/> adds the indexed
    /// coupon's, which read the fixing from the context.
    /// </remarks>
    public override ForwardRateAgreementResults SetupExpired()
    {
        InstrumentResults expired = base.SetupExpired();
        return new ForwardRateAgreementResults
        {
            NPV = expired.NPV,
            ErrorEstimate = expired.ErrorEstimate,
            ValuationDate = expired.ValuationDate,
            AdditionalResults = expired.AdditionalResults,
        };
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The results aren't an FRA's.</exception>
    public override ForwardRateAgreementResults FetchResults(InstrumentResults results)
    {
        InstrumentResults fetched = base.FetchResults(results);
        if (fetched is not ForwardRateAgreementResults r)
        {
            throw new ArgumentException("wrong result type", nameof(results));
        }

        return r;
    }

    // QuantLib's first constructor delegates to the second with the index's maturity for the value date.
    private static Date IndexMaturityDate(IborIndex index, Date valueDate)
    {
        ArgumentNullException.ThrowIfNull(index);
        return index.MaturityDate(valueDate);
    }

    // QuantLib's setupExpired computes the forward rate (forwardrateagreement.cpp:84-87), which under the par
    // approximation reads the curve before its reference date and throws, and its amount() then returns an amount
    // never computed (:74-77). Fixed: the indexed coupon's forward rate is its fixing, and its payoff QuantLib's own
    // calculateAmount on it (:110-117), which reads only the fixing, the strike, the notional and the two dates; the
    // par approximation's are null.
    private ForwardRateAgreementResults SetupExpired(EvaluationContext context)
    {
        ForwardRateAgreementResults expired = SetupExpired();
        if (!UseIndexedCoupon)
        {
            return expired;
        }

        InterestRate forwardRate = CalculateForwardRate(context);
        return expired with { Amount = CalculateAmount(forwardRate), ForwardRate = forwardRate };
    }

    private InterestRate CalculateForwardRate(EvaluationContext context)
    {
        if (UseIndexedCoupon)
        {
            return new InterestRate(
                Index.Fixing(FixingDate(), context), Index.DayCounter, Compounding.Simple, Frequency.Once);
        }

        // par coupon approximation
        YieldTermStructure curve = Index.ForwardingTermStructure
            ?? throw new NanoQLException("empty Handle cannot be dereferenced");
        return new InterestRate(
            ((curve.Discount(ValueDate) / curve.Discount(MaturityDate)) - 1.0)
                / Index.DayCounter.YearFraction(ValueDate, MaturityDate),
            Index.DayCounter,
            Compounding.Simple,
            Frequency.Once);
    }

    private double CalculateAmount(InterestRate forwardRate)
    {
        int sign = Position == PositionType.Long ? 1 : -1;
        double f = forwardRate.Rate;
        double k = StrikeForwardRate.Rate;
        double t = forwardRate.DayCounter.YearFraction(ValueDate, MaturityDate);
        return NotionalAmount * sign * (f - k) * t / (1.0 + (f * t));
    }
}
