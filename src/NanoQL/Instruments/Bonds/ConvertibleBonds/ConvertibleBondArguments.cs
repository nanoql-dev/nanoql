// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Theo Boafo
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/convertiblebonds.hpp ql/instruments/bonds/convertiblebonds.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// What a convertible bond's engine reads: QuantLib's <c>ConvertibleBond::arguments</c>, which its <c>setupArguments</c>
/// computes from the bond for the settlement date of an evaluation context (<see cref="ConvertibleBond.SetupArguments"/>).
/// </summary>
/// <remarks>
/// Computed data, not the bond's terms: the callabilities not yet occurred at settlement, with their prices made dirty
/// and their triggers, <see langword="null"/> for QuantLib's <c>Null&lt;Real&gt;()</c> where a callability isn't soft.
/// </remarks>
internal sealed class ConvertibleBondArguments
{
    // QuantLib's Null<Real>(), which its checks read as no value.
    private const double NullReal = 3.4028234663852886e38;

    /// <summary>Gets the conversion's exercise.</summary>
    public required Exercise Exercise { get; init; }

    /// <summary>Gets the number of shares one bond converts into.</summary>
    public required double ConversionRatio { get; init; }

    /// <summary>Gets the dates of the callabilities not yet occurred at settlement.</summary>
    public required ImmutableArray<Date> CallabilityDates { get; init; }

    /// <summary>Gets their types.</summary>
    public required ImmutableArray<CallabilityType> CallabilityTypes { get; init; }

    /// <summary>Gets their dirty prices, per 100 of notional.</summary>
    public required ImmutableArray<double> CallabilityPrices { get; init; }

    /// <summary>Gets their triggers: a soft call's, or <see langword="null"/>.</summary>
    public required ImmutableArray<double?> CallabilityTriggers { get; init; }

    /// <summary>Gets every cash flow of the bond, the redemption last.</summary>
    public required ImmutableArray<CashFlow> Cashflows { get; init; }

    /// <summary>Gets the issue date, or <see langword="null"/> for none.</summary>
    public required Date? IssueDate { get; init; }

    /// <summary>Gets the bond's settlement date for the context.</summary>
    public required Date SettlementDate { get; init; }

    /// <summary>Gets the settlement days.</summary>
    public required int SettlementDays { get; init; }

    /// <summary>Gets the redemption, per 100 of notional.</summary>
    public required double Redemption { get; init; }

    /// <summary>Checks the arguments, as QuantLib's <c>validate</c> does.</summary>
    /// <exception cref="NanoQLException">An argument is missing or inconsistent.</exception>
    /// <remarks>
    /// QuantLib's "no exercise given" and "null settlement days" can't fail here: the bond refuses a null exercise, and
    /// its settlement days are never QuantLib's null.
    /// </remarks>
    public void Validate()
    {
        if (ConversionRatio == NullReal)
        {
            throw new NanoQLException("null conversion ratio");
        }

        if (!(ConversionRatio > 0.0))
        {
            throw new NanoQLException($"positive conversion ratio required: {Show(ConversionRatio)} not allowed");
        }

        if (Redemption == NullReal)
        {
            throw new NanoQLException("null redemption");
        }

        if (!(Redemption >= 0.0))
        {
            throw new NanoQLException($"positive redemption required: {Show(Redemption)} not allowed");
        }

        if (SettlementDate == default)
        {
            throw new NanoQLException("null settlement date");
        }

        if (CallabilityDates.Length != CallabilityTypes.Length)
        {
            throw new NanoQLException("different number of callability dates and types");
        }

        if (CallabilityDates.Length != CallabilityPrices.Length)
        {
            throw new NanoQLException("different number of callability dates and prices");
        }

        if (CallabilityDates.Length != CallabilityTriggers.Length)
        {
            throw new NanoQLException("different number of callability dates and triggers");
        }

        if (Cashflows.IsDefaultOrEmpty)
        {
            throw new NanoQLException("no cashflows given");
        }
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}
