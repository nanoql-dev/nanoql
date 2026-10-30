// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/InstrumentSetTests.cs (a kind with no QuantLib defaults, its routes checked by hand)

using System.Diagnostics.CodeAnalysis;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Extensions.Conventions.ConventionRoutes;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A cross-currency fixed-against-floating swap convention resolved in a set
/// (<see cref="ConventionSet.CrossCurrencySwap"/>): its record, its base chain merged, and the rate helper it feeds, on
/// its floating index built in the set.
/// </summary>
[Experimental("NQL0001")]
public sealed class CrossCurrencySwapConvention
{
    internal CrossCurrencySwapConvention(ConventionSet set, CrossCurrencySwapConventionRecord record)
    {
        Set = set;
        Record = record;
    }

    /// <summary>Gets the set the convention was resolved in, whose names its record's fields are.</summary>
    public ConventionSet Set { get; }

    /// <summary>Gets the record with every field its base chain gives, under the convention's name.</summary>
    public CrossCurrencySwapConventionRecord Record { get; }

    /// <summary>A constant-notional cross-currency swap rate helper on the record's floating index built in the
    /// set.</summary>
    /// <param name="fixedRate">The fixed rate quoted.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="collateralCurve">The collateral currency's discount curve.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="floatingCurve">The floating index's forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>The helper.</returns>
    /// <exception cref="ArgumentException">The record leaves out a field the helper needs, or a name it gives is
    /// unknown.</exception>
    public ConstNotionalCrossCurrencySwapRateHelper RateHelper(
        double fixedRate,
        Period tenor,
        YieldTermStructure? collateralCurve,
        EvaluationContext context,
        YieldTermStructure? floatingCurve = null)
    {
        CrossCurrencySwapConventionRecord r = Record;
        return new ConstNotionalCrossCurrencySwapRateHelper(
            fixedRate,
            tenor,
            Required(r, nameof(r.FixingDays), r.FixingDays),
            Set.Calendar(Required(r, nameof(r.Calendar), r.Calendar)),
            Required(r, nameof(r.Convention), r.Convention),
            Required(r, nameof(r.EndOfMonth), r.EndOfMonth),
            Required(r, nameof(r.FixedFrequency), r.FixedFrequency),
            Set.DayCounter(Required(r, nameof(r.FixedDayCounter), r.FixedDayCounter)),
            Ibor(Set, Required(r, nameof(r.FloatingIndex), r.FloatingIndex), r.FloatingIndexTenor, floatingCurve),
            collateralCurve,
            Required(r, nameof(r.CollateralOnFixedLeg), r.CollateralOnFixedLeg),
            context,
            r.PaymentLag ?? 0,
            r.IndexedCoupons,
            r.FloatingPaymentFrequency);
    }

    // Every name the record gives resolves.
    internal void Validate()
    {
        CrossCurrencySwapConventionRecord r = Record;
        if (r.FloatingIndex is not null)
        {
            _ = Ibor(Set, r.FloatingIndex, r.FloatingIndexTenor, null);
        }

        _ = CalendarOf(Set, r.Calendar);
        _ = DayCounterOf(Set, r.FixedDayCounter);
    }
}
