// SPDX-FileCopyrightText: Copyright (C) 2008 Allen Kuo
// SPDX-FileCopyrightText: Copyright (C) 2017 BN Algorithms Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/callablebonds/callablebond.hpp ql/experimental/callablebonds/callablebond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// What a callable bond's engines read: QuantLib's <c>CallableBond::arguments</c>, which its <c>setupArguments</c>
/// computes from the bond for the settlement date of an evaluation context (<see cref="CallableBond.SetupArguments"/>).
/// </summary>
/// <remarks>
/// <para>
/// Computed data, not the bond's terms: the coupons still to be paid, and the callabilities still to be exercised with
/// their prices made dirty. Each live callability's type is kept beside its date and price, as QuantLib's fix after
/// v1.44 keeps it (PR #2875), so the tree applies each by its own type.
/// </para>
/// <para>
/// QuantLib's <c>spread</c>, which only its NPVSpreadHelper writes, is the tree engine's parameter, not a member.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
internal sealed class CallableBondArguments
{
    // QuantLib's Null<Real>(), which its check reads as no redemption.
    private const double NullReal = 3.4028234663852886e38;

    /// <summary>Gets the bond's settlement date for the context.</summary>
    public required Date SettlementDate { get; init; }

    /// <summary>Gets every cash flow of the bond, the redemption last.</summary>
    public required ImmutableArray<CashFlow> Cashflows { get; init; }

    /// <summary>Gets the bond's calendar, or <see langword="null"/> for QuantLib's empty calendar.</summary>
    public required Calendar? Calendar { get; init; }

    /// <summary>Gets the dates of the cash flows, the redemption aside, neither paid nor trading ex-coupon at settlement.</summary>
    public required ImmutableArray<Date> CouponDates { get; init; }

    /// <summary>Gets the amounts of those cash flows.</summary>
    public required ImmutableArray<double> CouponAmounts { get; init; }

    /// <summary>Gets the bond's face amount.</summary>
    public required double FaceAmount { get; init; }

    /// <summary>Gets the redemption: the face amount times the redemption price over 100.</summary>
    public required double Redemption { get; init; }

    /// <summary>Gets the redemption's date.</summary>
    public required Date RedemptionDate { get; init; }

    /// <summary>Gets the bond's payment day counter, or <see langword="null"/> for QuantLib's empty one.</summary>
    public required DayCounter? PaymentDayCounter { get; init; }

    /// <summary>Gets the bond's coupon frequency.</summary>
    public required Frequency Frequency { get; init; }

    /// <summary>Gets the whole put/call schedule, the callabilities that have occurred among them.</summary>
    public required ImmutableArray<Callability> PutCallSchedule { get; init; }

    /// <summary>Gets the dirty prices of the callabilities not yet occurred at settlement, per 100 of notional.</summary>
    public required ImmutableArray<double> CallabilityPrices { get; init; }

    /// <summary>Gets the dates of those callabilities.</summary>
    public required ImmutableArray<Date> CallabilityDates { get; init; }

    /// <summary>Gets the types of those callabilities.</summary>
    public required ImmutableArray<CallabilityType> CallabilityTypes { get; init; }

    /// <summary>
    /// Checks the arguments, as QuantLib's <c>validate</c> does: not the plain bond's checks, which it doesn't call.
    /// </summary>
    /// <exception cref="NanoQLException">An argument is missing or inconsistent.</exception>
    public void Validate()
    {
        if (SettlementDate == default)
        {
            throw new NanoQLException("null settlement date");
        }

        if (Redemption == NullReal)
        {
            throw new NanoQLException("null redemption");
        }

        if (!(Redemption >= 0.0))
        {
            string redemption = Redemption.ToString(System.Globalization.CultureInfo.InvariantCulture);
            throw new NanoQLException($"positive redemption required: {redemption} not allowed");
        }

        if (CallabilityDates.Length != CallabilityPrices.Length)
        {
            throw new NanoQLException("different number of callability dates and prices");
        }

        // QuantLib's fix after v1.44 (PR #2875)
        if (CallabilityDates.Length != CallabilityTypes.Length)
        {
            throw new NanoQLException("different number of callability dates and types");
        }

        if (CouponDates.Length != CouponAmounts.Length)
        {
            throw new NanoQLException("different number of coupon dates and amounts");
        }
    }
}
