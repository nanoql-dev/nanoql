// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Theo Boafo
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/convertiblebonds.hpp ql/instruments/bonds/convertiblebonds.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// The convertible bond base class: a bond its holder may convert into shares at the conversion ratio, under an
/// exercise, with a schedule of calls, soft calls and puts.
/// </summary>
/// <remarks>
/// <para>
/// Its engine (<see cref="PricingEngines.BinomialConvertibleEngine"/>) reads what QuantLib's <c>setupArguments</c>
/// computes for the settlement date of the context it prices under: the callabilities not yet occurred there, their
/// clean prices made dirty with the bond's accrued amount at the call date, and each one's trigger, a soft call's own
/// or none. A plain bond's engine (<see cref="PricingEngines.DiscountingBondEngine"/>) refuses a convertible bond, as
/// QuantLib's refuses it when it sets up its arguments, with "wrong argument type".
/// </para>
/// <para>
/// Most members inherited from <see cref="Bond"/>, such as the yield and the yield-based prices, refer to the
/// underlying plain-vanilla bond and don't take convertibility and callability into account, as QuantLib warns of its
/// own.
/// </para>
/// <para>
/// Unsealed, an extension point, as QuantLib's base is: the zero-coupon, fixed-coupon and floating-rate convertibles
/// derive from it. Its protected constructor follows <see cref="Bond"/>'s construction contract: a derived constructor
/// writes the cash flows and builds the redemption, QuantLib's notional of 100.
/// </para>
/// </remarks>
public abstract class ConvertibleBond : Bond
{
    private readonly Exercise exercise;
    private readonly ImmutableArray<Callability> callability;
    private readonly double redemption;

    /// <summary>Initializes a new instance of the <see cref="ConvertibleBond"/> class, with no cash flows yet.</summary>
    /// <param name="exercise">The conversion's exercise.</param>
    /// <param name="conversionRatio">The number of shares one bond converts into.</param>
    /// <param name="callability">The calls, soft calls and puts, copied, the last one the latest.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="schedule">The schedule, whose calendar is the bond's and whose end date its maturity.</param>
    /// <param name="redemption">The redemption, per 100 of notional.</param>
    /// <exception cref="ArgumentNullException">The exercise, the callabilities or the schedule is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The issue date is <c>default(Date)</c>; a callability is <see langword="null"/>; or the last callability is after
    /// the maturity date.
    /// </exception>
    /// <exception cref="NanoQLException">The schedule is empty.</exception>
    /// <remarks>
    /// QuantLib checks the last callability's date only, not the latest's, as here. Its exercise and its conversion
    /// ratio are checked when the bond is priced, as QuantLib's are, but the exercise's null, which is refused here.
    /// </remarks>
    protected ConvertibleBond(
        Exercise exercise,
        double conversionRatio,
        IEnumerable<Callability> callability,
        Date? issueDate,
        int settlementDays,
        Schedule schedule,
        double redemption)
        : base(settlementDays, CalendarOf(schedule), issueDate)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(callability);
        this.exercise = exercise;
        ConversionRatio = conversionRatio;
        this.callability = [.. callability];
        this.redemption = redemption;

        // QuantLib dereferences a null callability where it reads one.
        if (this.callability.Any(c => c is null))
        {
            throw new ArgumentException("null callability in schedule", nameof(callability));
        }

        maturityDate = schedule.EndDate();

        if (!this.callability.IsEmpty)
        {
            Date last = this.callability[^1].Date;
            if (!(last <= maturityDate))
            {
                throw new ArgumentException(
                    $"last callability date ({last}) later than maturity ({maturityDate})", nameof(callability));
            }
        }
    }

    /// <summary>Gets the number of shares one bond converts into.</summary>
    public double ConversionRatio { get; }

    /// <summary>Gets the calls, soft calls and puts.</summary>
    public ImmutableArray<Callability> Callability => callability;

    /// <summary>
    /// The engine's arguments for the context's settlement date: QuantLib's <c>setupArguments</c>.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The arguments.</returns>
    /// <exception cref="NanoQLException">The settlement date or an accrued amount can't be computed.</exception>
    internal ConvertibleBondArguments SetupArguments(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date settlement = SettlementDate(context);

        int n = callability.Length;
        ImmutableArray<Date>.Builder callabilityDates = ImmutableArray.CreateBuilder<Date>(n);
        ImmutableArray<CallabilityType>.Builder callabilityTypes = ImmutableArray.CreateBuilder<CallabilityType>(n);
        ImmutableArray<double>.Builder callabilityPrices = ImmutableArray.CreateBuilder<double>(n);
        ImmutableArray<double?>.Builder callabilityTriggers = ImmutableArray.CreateBuilder<double?>(n);

        for (int i = 0; i < n; i++)
        {
            if (!callability[i].HasOccurred(context, settlement, false))
            {
                callabilityTypes.Add(callability[i].Type);
                callabilityDates.Add(callability[i].Date);
                double price = callability[i].Price.Amount;
                if (callability[i].Price.Type == BondPriceType.Clean)
                {
                    price += AccruedAmount(context, callability[i].Date);
                }

                callabilityPrices.Add(price);

                // QuantLib's Null<Real>() for a callability that isn't soft
                callabilityTriggers.Add(callability[i] is SoftCallability softCall ? softCall.Trigger : null);
            }
        }

        return new ConvertibleBondArguments
        {
            Exercise = exercise,
            ConversionRatio = ConversionRatio,
            CallabilityDates = callabilityDates.ToImmutable(),
            CallabilityTypes = callabilityTypes.ToImmutable(),
            CallabilityPrices = callabilityPrices.ToImmutable(),
            CallabilityTriggers = callabilityTriggers.ToImmutable(),
            Cashflows = Cashflows,
            IssueDate = IssueDate,
            SettlementDate = settlement,
            SettlementDays = SettlementDays,
            Redemption = redemption,
        };
    }

    /// <summary>Refuses a plain bond's engine, as QuantLib's <c>setupArguments</c> refuses a plain bond's arguments.</summary>
    /// <exception cref="NanoQLException">Always.</exception>
    internal override void CheckPlainBondArguments() => throw new NanoQLException("wrong argument type");

    // The bond's calendar, the schedule's.
    private static Calendar? CalendarOf(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return schedule.Calendar;
    }
}
