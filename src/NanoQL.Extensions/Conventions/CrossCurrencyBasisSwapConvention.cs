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
/// A cross-currency basis swap convention resolved in a set (<see cref="ConventionSet.CrossCurrencyBasisSwap"/>): its
/// record, its base chain merged, and the rate helper it feeds, on its indexes built in the set.
/// </summary>
/// <remarks>
/// The record's <see cref="CrossCurrencyBasisSwapConventionRecord.MarkToMarket"/> chooses the helper: the
/// mark-to-market one, which takes the resettable leg and the FX reset's days and calendar but no lag on the notional
/// exchanges, or the constant-notional one, which takes the reverse; a record giving a field its helper can't take is
/// refused.
/// </remarks>
[Experimental("NQL0001")]
public sealed class CrossCurrencyBasisSwapConvention
{
    internal CrossCurrencyBasisSwapConvention(ConventionSet set, CrossCurrencyBasisSwapConventionRecord record)
    {
        Set = set;
        Record = record;
    }

    /// <summary>Gets the set the convention was resolved in, whose names its record's fields are.</summary>
    public ConventionSet Set { get; }

    /// <summary>Gets the record with every field its base chain gives, under the convention's name.</summary>
    public CrossCurrencyBasisSwapConventionRecord Record { get; }

    /// <summary>A basis swap rate helper on the record's indexes built in the set.</summary>
    /// <param name="basis">The basis quoted.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="collateralCurve">The collateral currency's discount curve.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="baseCurrencyCurve">The FX base currency index's forwarding curve, or <see langword="null"/> for
    /// none.</param>
    /// <param name="quoteCurrencyCurve">The FX quote currency index's forwarding curve, or <see langword="null"/> for
    /// none.</param>
    /// <returns>The helper: a <see cref="MtMCrossCurrencyBasisSwapRateHelper"/> where the record marks the swap to
    /// market, a <see cref="ConstNotionalCrossCurrencyBasisSwapRateHelper"/> otherwise.</returns>
    /// <exception cref="ArgumentException">The record leaves out a field the helper needs, a name it gives is unknown, or
    /// it gives a value the helper can't take.</exception>
    public CrossCurrencyBasisSwapRateHelperBase RateHelper(
        double basis,
        Period tenor,
        YieldTermStructure? collateralCurve,
        EvaluationContext context,
        YieldTermStructure? baseCurrencyCurve = null,
        YieldTermStructure? quoteCurrencyCurve = null)
    {
        CrossCurrencyBasisSwapConventionRecord r = Record;
        int fixingDays = Required(r, nameof(r.FixingDays), r.FixingDays);
        Calendar calendar = Set.Calendar(Required(r, nameof(r.Calendar), r.Calendar));
        BusinessDayConvention convention = Required(r, nameof(r.Convention), r.Convention);
        bool endOfMonth = Required(r, nameof(r.EndOfMonth), r.EndOfMonth);
        var baseIndex = Ibor(Set, Required(r, nameof(r.BaseCurrencyIndex), r.BaseCurrencyIndex), r.BaseCurrencyIndexTenor, baseCurrencyCurve);
        var quoteIndex = Ibor(Set, Required(r, nameof(r.QuoteCurrencyIndex), r.QuoteCurrencyIndex), r.QuoteCurrencyIndexTenor, quoteCurrencyCurve);
        bool collateralOnBase = Required(r, nameof(r.CollateralOnBaseCurrency), r.CollateralOnBaseCurrency);
        bool basisOnBase = Required(r, nameof(r.BasisOnBaseCurrencyLeg), r.BasisOnBaseCurrencyLeg);
        if (r.MarkToMarket is true)
        {
            const string mtm = nameof(MtMCrossCurrencyBasisSwapRateHelper);
            Untaken(mtm, r, nameof(r.PaymentLagOnNotionalExchanges), r.PaymentLagOnNotionalExchanges, false);
            return new MtMCrossCurrencyBasisSwapRateHelper(
                basis,
                tenor,
                fixingDays,
                calendar,
                convention,
                endOfMonth,
                baseIndex,
                quoteIndex,
                collateralCurve,
                collateralOnBase,
                basisOnBase,
                Required(r, nameof(r.BaseCurrencyLegResettable), r.BaseCurrencyLegResettable),
                context,
                r.PaymentFrequency,
                r.PaymentLag ?? 0,
                r.QuoteCurrencyPaymentFrequency,
                r.FxResetFixingDays ?? 0,
                CalendarOf(Set, r.FxResetFixingCalendar),
                r.IndexedCoupons);
        }

        const string constant = nameof(ConstNotionalCrossCurrencyBasisSwapRateHelper);
        Untaken(constant, r, nameof(r.BaseCurrencyLegResettable), r.BaseCurrencyLegResettable);
        Untaken(constant, r, nameof(r.FxResetFixingDays), r.FxResetFixingDays);
        Untaken(constant, r, nameof(r.FxResetFixingCalendar), r.FxResetFixingCalendar);
        return new ConstNotionalCrossCurrencyBasisSwapRateHelper(
            basis,
            tenor,
            fixingDays,
            calendar,
            convention,
            endOfMonth,
            baseIndex,
            quoteIndex,
            collateralCurve,
            collateralOnBase,
            basisOnBase,
            context,
            r.PaymentFrequency,
            r.PaymentLag ?? 0,
            r.QuoteCurrencyPaymentFrequency,
            r.IndexedCoupons,
            r.PaymentLagOnNotionalExchanges ?? false);
    }

    // Every name the record gives resolves.
    internal void Validate()
    {
        CrossCurrencyBasisSwapConventionRecord r = Record;
        if (r.BaseCurrencyIndex is not null)
        {
            _ = Ibor(Set, r.BaseCurrencyIndex, r.BaseCurrencyIndexTenor, null);
        }

        if (r.QuoteCurrencyIndex is not null)
        {
            _ = Ibor(Set, r.QuoteCurrencyIndex, r.QuoteCurrencyIndexTenor, null);
        }

        _ = CalendarOf(Set, r.Calendar);
        _ = CalendarOf(Set, r.FxResetFixingCalendar);
    }
}
