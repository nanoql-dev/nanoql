// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/crosscurrencyswap.hpp ql/instruments/crosscurrencyswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A generic cross-currency swap: a swap whose legs carry their currencies.</summary>
/// <remarks>
/// Unsealed, as in QuantLib, where the constant-notional and mark-to-market swaps derive from it; a derived swap builds
/// its legs and passes them, with their currencies, to the multi-leg constructor. A leg's currency may be
/// <see langword="null"/>, QuantLib's empty currency, as QuantLib's fixed-vs-floating rate helper gives its fixed leg.
/// The in-currency results QuantLib's swap holds (<c>inCcyLegNPV</c>, <c>inCcyLegBPS</c>, <c>npvDateDiscounts</c>) are
/// members of the <see cref="CrossCurrencySwapResults"/> an engine returns.
/// </remarks>
public class CrossCurrencySwap : Swap
{
    private readonly ImmutableArray<Currency?> currencies;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrossCurrencySwap"/> class with two legs and their currencies: the
    /// first leg is paid and the second is received.
    /// </summary>
    /// <param name="firstLeg">The cash flows of the first leg.</param>
    /// <param name="firstLegCcy">The currency of the first leg's cash flows.</param>
    /// <param name="secondLeg">The cash flows of the second leg.</param>
    /// <param name="secondLegCcy">The currency of the second leg's cash flows.</param>
    /// <exception cref="ArgumentException">A leg is <c>default(ImmutableArray)</c> or holds a null cash
    /// flow.</exception>
    public CrossCurrencySwap(
        ImmutableArray<CashFlow> firstLeg,
        Currency? firstLegCcy,
        ImmutableArray<CashFlow> secondLeg,
        Currency? secondLegCcy)
        : base(firstLeg, secondLeg)
    {
        currencies = [firstLegCcy, secondLegCcy];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrossCurrencySwap"/> class with any number of legs, each with its
    /// direction and currency.
    /// </summary>
    /// <param name="legs">The legs.</param>
    /// <param name="payer">For each leg, whether it is paid.</param>
    /// <param name="currencies">For each leg, the currency of its cash flows.</param>
    /// <exception cref="ArgumentException">The three sequences differ in size.</exception>
    public CrossCurrencySwap(
        IEnumerable<ImmutableArray<CashFlow>> legs,
        IEnumerable<bool> payer,
        IEnumerable<Currency?> currencies)
        : base(legs, payer)
    {
        ArgumentNullException.ThrowIfNull(currencies);
        this.currencies = [.. currencies];
        if (this.currencies.Length != NumberOfLegs)
        {
            throw new ArgumentException(
                $"Size mismatch between payer ({NumberOfLegs}) and currencies ({this.currencies.Length})",
                nameof(currencies));
        }
    }

    /// <summary>Gets each leg's currency, <see langword="null"/> for QuantLib's empty currency.</summary>
    public ImmutableArray<Currency?> Currencies => currencies;

    /// <summary>The currency of the leg at an index.</summary>
    /// <param name="j">The index.</param>
    /// <returns>The currency, <see langword="null"/> for QuantLib's empty currency.</returns>
    /// <exception cref="ArgumentException">There's no leg at the index.</exception>
    public Currency? LegCurrency(int j)
    {
        if (!(j >= 0 && j < NumberOfLegs))
        {
            throw new ArgumentException($"leg# {j} doesn't exist!", nameof(j));
        }

        return currencies[j];
    }

    /// <inheritdoc/>
    public override CrossCurrencySwapResults SetupExpired()
    {
        ImmutableArray<double?> zeros = [.. Enumerable.Repeat<double?>(0.0, NumberOfLegs)];
        return new CrossCurrencySwapResults(base.SetupExpired())
        {
            InCcyLegBPS = zeros,
            InCcyLegNPV = zeros,
            NpvDateDiscounts = zeros,
        };
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The numbers of legs, multipliers and currencies differ.</exception>
    public override void Validate()
    {
        base.Validate();
        if (Legs.Length != currencies.Length)
        {
            throw new NanoQLException("Number of legs is not equal to number of currencies");
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// The results aren't a cross-currency swap's, or a per-leg result has the wrong number of legs.
    /// </exception>
    public override CrossCurrencySwapResults FetchResults(InstrumentResults results)
    {
        SwapResults fetched = base.FetchResults(results);
        if (fetched is not CrossCurrencySwapResults r)
        {
            throw new ArgumentException("The results are not of type cross currency swap", nameof(results));
        }

        return r with
        {
            InCcyLegNPV = PerLeg(r.InCcyLegNPV, "Wrong number of in currency leg NPVs returned by engine"),
            InCcyLegBPS = PerLeg(r.InCcyLegBPS, "Wrong number of in currency leg BPSs returned by engine"),
            NpvDateDiscounts = PerLeg(r.NpvDateDiscounts, "Wrong number of npv date discounts returned by engine"),
        };
    }

    /// <summary>
    /// A leg with its notional exchanges: the nominal paid at the start and received at maturity, each date advanced by
    /// the payment lag on the calendar and adjusted by the leg's convention, the leg then sorted by date.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="calendar">The calendar of the exchange dates.</param>
    /// <param name="earliestDate">The start of the swap.</param>
    /// <param name="maturityDate">The maturity of the swap.</param>
    /// <param name="legBdc">The leg's payment convention.</param>
    /// <param name="nominal">The exchanged nominal.</param>
    /// <param name="paymentLag">The lag of both exchanges, in business days; zero by default, which only adjusts the
    /// dates.</param>
    /// <returns>The leg with its exchanges.</returns>
    private protected static ImmutableArray<CashFlow> AddNotionalExchangesToLeg(
        ImmutableArray<CashFlow> leg,
        Calendar calendar,
        Date earliestDate,
        Date maturityDate,
        BusinessDayConvention legBdc,
        double nominal,
        int paymentLag = 0)
    {
        // By default principal exchanges settle on the effective and maturity dates: the payment lag belongs to coupon
        // payments only, and the dates are adjusted with the leg's payment convention. A non-zero paymentLag instead
        // lags both exchanges like the coupons, so the final exchange settles together with the final coupon and the
        // initial exchange falls on the lagged settlement date. (Advance with zero days reduces to Adjust, so the
        // default keeps the effective/maturity behaviour.)
        // Initial notional exchange
        Date initial = calendar.Advance(earliestDate, paymentLag, TimeUnit.Days, legBdc);

        // Final notional exchange
        Date final = calendar.Advance(maturityDate, paymentLag, TimeUnit.Days, legBdc);

        // A lagged final coupon can pay after the maturity-date exchange.
        return SortLegByDate([new SimpleCashFlow(-nominal, initial), .. leg, new SimpleCashFlow(nominal, final)]);
    }

    // Whether a leg compounds its spread daily, so the leg's amount isn't linear in its spread and its fair spread's
    // derivation from the BPS is inexact.
    internal bool CompoundsSpreadDaily(int j) =>
        Legs[j].Any(cf => cf is FloatingRateCoupon { SpreadIsCompoundedDaily: true });

    // A copy, of the swap's own type and terms, whose leg's coupons' spreads are shifted, each keeping its pricer.
    internal CrossCurrencySwap WithLegSpreadShift(int j, double shift) =>
        (CrossCurrencySwap)WithLeg(
            j, [.. Legs[j].Select(cf => cf is FloatingRateCoupon c && c.WithSpreadShift(shift) is { } s ? s : cf)]);

    /// <summary>A leg sorted by its cash flows' dates, cash flows on one date keeping their order.</summary>
    /// <param name="leg">The leg.</param>
    /// <returns>The sorted leg.</returns>
    private protected static ImmutableArray<CashFlow> SortLegByDate(ImmutableArray<CashFlow> leg) =>
        [.. leg.OrderBy(cf => cf.Date)];

    private ImmutableArray<double?> PerLeg(ImmutableArray<double?> values, string message)
    {
        if (values.IsDefaultOrEmpty)
        {
            return [.. Enumerable.Repeat<double?>(null, NumberOfLegs)];
        }

        if (values.Length != NumberOfLegs)
        {
            throw new ArgumentException(message, "results");
        }

        return values;
    }
}
