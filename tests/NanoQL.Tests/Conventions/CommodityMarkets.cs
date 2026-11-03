// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The commodity markets the commodities' facts share, on 15 January 2025.
/// </summary>
internal static class CommodityMarkets
{
    /// <summary>The evaluation date, 15 January 2025, a Wednesday.</summary>
    public static readonly Date Today = new(15, Month.January, 2025);

    /// <summary>The null commodity type of QuantLib's known factors.</summary>
    public static readonly CommodityType None = new NullCommodityType();

    /// <summary>Barrels' worth of one of each of QuantLib's six petroleum units, by its known factors: a barrel, a
    /// thousand, a 42nd, a 158.987th, and 6.28981 for both kilolitres.</summary>
    public static readonly ImmutableDictionary<string, double> BarrelsPerUnit = new Dictionary<string, double>
    {
        ["BBL"] = 1.0,
        ["MB"] = 1000.0,
        ["GAL"] = 1.0 / 42.0,
        ["l"] = 1.0 / 158.987,
        ["kl"] = 6.28981,
        ["KL_tk"] = 6.28981,
    }.ToImmutableDictionary();

    /// <summary>A context on <see cref="Today"/>.</summary>
    /// <returns>The context.</returns>
    public static EvaluationContext Context() => new() { EvaluationDate = Today };

    /// <summary>One of the six petroleum units, by code.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The unit.</returns>
    public static UnitOfMeasure Unit(string code) => code switch
    {
        "BBL" => new BarrelUnitOfMeasure(),
        "MB" => new MBUnitOfMeasure(),
        "GAL" => new GallonUnitOfMeasure(),
        "l" => new LitreUnitOfMeasure(),
        "kl" => new KilolitreUnitOfMeasure(),
        "KL_tk" => new TokyoKilolitreUnitOfMeasure(),
        "MT" => new MTUnitOfMeasure(),
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "unknown unit"),
    };

    /// <summary>A flat forward curve from <see cref="Today"/>, Actual/365 (Fixed), continuous.</summary>
    /// <param name="rate">The rate.</param>
    /// <returns>The curve.</returns>
    public static YieldTermStructure Flat(double rate) => new FlatForward(Today, rate, new Actual365Fixed());

    /// <summary>
    /// The tests' index: a flat curve at a price per its unit, from today for three years, on a null calendar, in
    /// dollars, its one fixing at the price five days ago.
    /// </summary>
    /// <param name="unit">The unit of the curve and the index.</param>
    /// <param name="price">The price per unit.</param>
    /// <param name="lot">The lot quantity.</param>
    /// <param name="name">The index's name, which keys its fixings.</param>
    /// <returns>The index.</returns>
    public static CommodityIndex Index(UnitOfMeasure unit, double price, double lot = 1.0, string? name = null) =>
        new(
            name ?? "IDX" + unit.Code,
            None,
            new USDCurrency(),
            unit,
            new NullCalendar(),
            lot,
            new CommodityCurve(
                "CRV" + unit.Code, None, new USDCurrency(), unit, new NullCalendar(), [Today, Today + (365 * 3)],
                [price, price], new Actual365Fixed()),
            ImmutableSortedDictionary<Date, ExchangeContract>.Empty,
            0);

    /// <summary>The context with an index's one fixing at its price five days ago, the probes'.</summary>
    /// <param name="context">The context.</param>
    /// <param name="index">The index.</param>
    /// <param name="price">The fixing.</param>
    /// <param name="date">The fixing's date; five days ago when <see langword="null"/>.</param>
    /// <returns>The context with the fixing.</returns>
    public static EvaluationContext WithFixing(EvaluationContext context, FinancialIndex index, double price, Date? date = null) =>
        context with { Fixings = context.Fixings.AddFixing(index, date ?? Today - 5, price, true) };

    /// <summary>Monthly periods from 1 March 2025 to 31 March 2025, one month's quantity each, paid five days after
    /// their ends on a null calendar (the probes').</summary>
    /// <param name="quantity">The monthly quantity.</param>
    /// <param name="end">The last period's start bound; 31 March 2025 when <see langword="null"/>.</param>
    /// <returns>The periods.</returns>
    public static ImmutableArray<PricingPeriod> MarchPeriods(Quantity quantity, Date? end = null) =>
        CommodityPricingHelperFunctions.CreatePricingPeriods(
            new Date(1, Month.March, 2025),
            end ?? new Date(31, Month.March, 2025),
            quantity,
            EnergyCommodityDeliverySchedule.Monthly,
            EnergyCommodityQuantityPeriodicity.PerMonth,
            new PaymentTerm("Pricing end + 5 days", PaymentTermEventType.PricingDate, 5, new NullCalendar()));

    /// <summary>A vanilla swap on the probes' terms: dollars on both legs, a null calendar, one curve for all three.</summary>
    /// <param name="payer">Whether the holder pays the fixed price.</param>
    /// <param name="fixedPrice">The fixed price per <paramref name="fixedUnit"/>.</param>
    /// <param name="fixedUnit">The fixed price's unit.</param>
    /// <param name="index">The index.</param>
    /// <param name="periods">The periods.</param>
    /// <param name="curve">The curve discounting everything.</param>
    /// <param name="costs">The secondary costs.</param>
    /// <param name="receiveCurrency">The currency the holder receives in; dollars when <see langword="null"/>.</param>
    /// <returns>The swap.</returns>
    public static EnergyVanillaSwap Vanilla(
        bool payer,
        double fixedPrice,
        UnitOfMeasure fixedUnit,
        CommodityIndex index,
        IReadOnlyList<PricingPeriod> periods,
        YieldTermStructure curve,
        IReadOnlyDictionary<string, object>? costs = null,
        Currency? receiveCurrency = null) =>
        new(
            payer,
            new NullCalendar(),
            new Money(new USDCurrency(), fixedPrice),
            fixedUnit,
            index,
            new USDCurrency(),
            receiveCurrency ?? new USDCurrency(),
            periods,
            None,
            costs,
            curve,
            curve,
            curve);
}
