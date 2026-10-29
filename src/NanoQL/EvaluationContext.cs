// SPDX-FileCopyrightText: Copyright (C) 2007, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/settings.hpp ql/settings.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL;

/// <summary>
/// The values QuantLib reads from its global <c>Settings</c>, as one explicit input: every function that reads the
/// evaluation date or a setting, directly or through a callee, takes the context.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EvaluationDate"/> is required: there is no "today" default. Every other field is an init-only property
/// whose default reproduces QuantLib's default setting. Variations are made with <c>with</c>.
/// </para>
/// <para>
/// Fields added later follow the same shape, never required, and no default changes once released, so callers keep
/// compiling and running.
/// </para>
/// <para>
/// Equality is the record's: field by field. <see cref="Fixings"/> and <see cref="ExchangeRates"/> compare by content,
/// since <see cref="IndexFixings"/> and <see cref="Currencies.ExchangeRates"/> define value equality, so two contexts
/// holding the same fixings and rates are equal however their stores were built; <see cref="MoneySettings"/> is a
/// record. So are <see cref="CommoditySettings"/> and <see cref="QuantitySettings"/>, and
/// <see cref="UnitOfMeasureConversions"/> compares by content.
/// </para>
/// </remarks>
public sealed record EvaluationContext
{
    // QuantLib's defaults, immutable values shared by every context that keeps them.
    private static readonly CommoditySettings DefaultCommoditySettings = new();
    private static readonly QuantitySettings DefaultQuantitySettings = new();

    private readonly Date evaluationDate;
    private readonly IndexFixings fixings = IndexFixings.Empty;
    private readonly ExchangeRates exchangeRates = Currencies.ExchangeRates.Default;
    private readonly MoneySettings moneySettings = new();
    private readonly CommoditySettings commoditySettings = DefaultCommoditySettings;
    private readonly UnitOfMeasureConversions unitOfMeasureConversions = Experimental.UnitOfMeasureConversions.Default;
    private readonly QuantitySettings quantitySettings = DefaultQuantitySettings;

    /// <summary>Gets the evaluation date; required.</summary>
    /// <exception cref="ArgumentException">Set to <c>default(Date)</c>, which is not a valid date.</exception>
    public required Date EvaluationDate
    {
        get => evaluationDate;
        init => evaluationDate = value.SerialNumber != 0
            ? value
            : throw new ArgumentException("default(Date) is not a valid evaluation date", nameof(value));
    }

    /// <summary>
    /// Gets a value indicating whether events on the reference date count as occurred; QuantLib's default is
    /// <see langword="false"/>.
    /// </summary>
    public bool IncludeReferenceDateEvents { get; init; }

    /// <summary>
    /// Gets a value indicating whether cash flows on the evaluation date are included, overriding
    /// <see cref="IncludeReferenceDateEvents"/> for them; <see langword="null"/>, QuantLib's default, leaves it to that
    /// setting.
    /// </summary>
    public bool? IncludeTodaysCashFlows { get; init; }

    /// <summary>
    /// Gets a value indicating whether today's fixings must be historic ones; QuantLib's default is
    /// <see langword="false"/>.
    /// </summary>
    public bool EnforcesTodaysHistoricFixings { get; init; }

    /// <summary>
    /// Gets the stored index fixings, QuantLib's <c>IndexManager</c> histories; by default none.
    /// </summary>
    /// <exception cref="ArgumentNullException">Set to <see langword="null"/>.</exception>
    public IndexFixings Fixings
    {
        get => fixings;
        init => fixings = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets a value indicating whether Ibor coupons are priced as par coupons, QuantLib's
    /// <c>IborCoupon::Settings::usingAtParCoupons()</c>; QuantLib-Python's default is <see langword="true"/>. It is read
    /// where QuantLib reads its switch: when an Ibor coupon pricer is built without an explicit choice, so a leg keeps
    /// the choice it was built with.
    /// </summary>
    public bool UseAtParCoupons { get; init; } = true;

    /// <summary>
    /// Gets the exchange-rate registry, QuantLib's <c>ExchangeRateManager</c>; by default
    /// <see cref="Currencies.ExchangeRates.Default"/>, QuantLib's known rates alone. <see cref="Money"/>'s conversions
    /// read it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Set to <see langword="null"/>.</exception>
    public ExchangeRates ExchangeRates
    {
        get => exchangeRates;
        init => exchangeRates = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets the settings that say how <see cref="Money"/> amounts in different currencies combine, QuantLib's
    /// <c>Money::Settings</c>; by default QuantLib's, no conversion and no base currency.
    /// </summary>
    /// <exception cref="ArgumentNullException">Set to <see langword="null"/>.</exception>
    public MoneySettings MoneySettings
    {
        get => moneySettings;
        init => moneySettings = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets the base currency and unit of measure the energy instruments value in, QuantLib's
    /// <c>CommoditySettings</c>; by default QuantLib's, U.S. dollars and barrels.
    /// </summary>
    /// <exception cref="ArgumentNullException">Set to <see langword="null"/>.</exception>
    public CommoditySettings CommoditySettings
    {
        get => commoditySettings;
        init => commoditySettings = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets the registry of conversion factors between units of measure, QuantLib's
    /// <c>UnitOfMeasureConversionManager</c>; by default <see cref="Experimental.UnitOfMeasureConversions.Default"/>,
    /// QuantLib's known factors alone. <see cref="Quantity"/>'s conversions and the energy instruments read it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Set to <see langword="null"/>.</exception>
    public UnitOfMeasureConversions UnitOfMeasureConversions
    {
        get => unitOfMeasureConversions;
        init => unitOfMeasureConversions = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets the settings that say how <see cref="Quantity"/> amounts in different units combine, QuantLib's static
    /// <c>Quantity::conversionType</c> and <c>Quantity::baseUnitOfMeasure</c>; by default QuantLib's, no conversion and
    /// no base unit.
    /// </summary>
    /// <exception cref="ArgumentNullException">Set to <see langword="null"/>.</exception>
    public QuantitySettings QuantitySettings
    {
        get => quantitySettings;
        init => quantitySettings = value ?? throw new ArgumentNullException(nameof(value));
    }
}
