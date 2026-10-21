// SPDX-FileCopyrightText: Copyright (C) 2004 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/money.hpp ql/money.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Utilities;

namespace NanoQL;

/// <summary>An amount of cash in a given currency.</summary>
/// <remarks>
/// <para>
/// A value: its zero value, <c>default(Money)</c>, is QuantLib's <c>Money()</c>, 0.0 in the empty currency, a
/// <see langword="null"/> <see cref="Currency"/>.
/// </para>
/// <para>
/// The operators combine amounts in one currency as QuantLib's do, and never convert: QuantLib's operators read the
/// global <c>Money::Settings</c>, and these see no context, so they behave as under its <c>NoConversion</c>. On a
/// currency mismatch every operator but <c>==</c> and <c>!=</c> throws QuantLib's "currency mismatch and no conversion
/// specified";
/// <c>==</c> returns <see langword="false"/> and <c>!=</c> <see langword="true"/>, where QuantLib's throw. The
/// conversion modes are methods that take the context, which holds the settings
/// (<see cref="EvaluationContext.MoneySettings"/>) and the exchange rates
/// (<see cref="EvaluationContext.ExchangeRates"/>): <see cref="Add(Money, EvaluationContext)"/>,
/// <see cref="Subtract(Money, EvaluationContext)"/>, <see cref="Divide(Money, EvaluationContext)"/>,
/// <see cref="Equals(Money, EvaluationContext)"/>, <see cref="LessThan"/>, <see cref="LessThanOrEqual"/>,
/// <see cref="GreaterThan"/>, <see cref="GreaterThanOrEqual"/>, and <see cref="MoneyFunctions"/>' <c>Close</c> and
/// <c>CloseEnough</c>.
/// </para>
/// <para>
/// <see cref="Equals(Money)"/> and <see cref="GetHashCode"/> compare the currency and the value, the value by
/// <see cref="double.Equals(double)"/>, so that equality is reflexive, as .NET's own <see cref="double"/> keeps
/// <c>==</c> IEEE's.
/// </para>
/// </remarks>
public readonly struct Money : IEquatable<Money>
{
    /// <summary>Initializes a new instance of the <see cref="Money"/> struct.</summary>
    /// <param name="currency">The currency; <see langword="null"/> for QuantLib's empty currency.</param>
    /// <param name="value">The amount.</param>
    public Money(Currency? currency, double value)
    {
        Value = value;
        Currency = currency;
    }

    /// <summary>Initializes a new instance of the <see cref="Money"/> struct.</summary>
    /// <param name="value">The amount.</param>
    /// <param name="currency">The currency; <see langword="null"/> for QuantLib's empty currency.</param>
    public Money(double value, Currency? currency)
    {
        Value = value;
        Currency = currency;
    }

    /// <summary>Gets the currency; <see langword="null"/> for QuantLib's empty currency.</summary>
    public Currency? Currency { get; }

    /// <summary>Gets the amount.</summary>
    public double Value { get; }

    /// <summary>The same amount itself.</summary>
    /// <param name="m">The amount.</param>
    /// <returns><paramref name="m"/>.</returns>
    public static Money operator +(Money m) => m;

    /// <summary>The negated amount, in the same currency.</summary>
    /// <param name="m">The amount.</param>
    /// <returns>The negated amount.</returns>
    public static Money operator -(Money m) => new(-m.Value, m.Currency);

    /// <summary>The sum of two amounts in one currency.</summary>
    /// <param name="m1">The first amount.</param>
    /// <param name="m2">The second amount.</param>
    /// <returns>The sum, in their currency.</returns>
    /// <exception cref="NanoQLException">The currencies differ.</exception>
    public static Money operator +(Money m1, Money m2) => Plus(m1, m2, null);

    /// <summary>The difference of two amounts in one currency.</summary>
    /// <param name="m1">The first amount.</param>
    /// <param name="m2">The amount subtracted.</param>
    /// <returns>The difference, in their currency.</returns>
    /// <exception cref="NanoQLException">The currencies differ.</exception>
    public static Money operator -(Money m1, Money m2) => Plus(m1, -m2, null);

    /// <summary>An amount multiplied by a number.</summary>
    /// <param name="m">The amount.</param>
    /// <param name="x">The number.</param>
    /// <returns>The product, in the amount's currency.</returns>
    public static Money operator *(Money m, double x) => new(m.Value * x, m.Currency);

    /// <summary>An amount multiplied by a number.</summary>
    /// <param name="x">The number.</param>
    /// <param name="m">The amount.</param>
    /// <returns>The product, in the amount's currency.</returns>
    public static Money operator *(double x, Money m) => m * x;

    /// <summary>An amount divided by a number.</summary>
    /// <param name="m">The amount.</param>
    /// <param name="x">The number.</param>
    /// <returns>The quotient, in the amount's currency.</returns>
    public static Money operator /(Money m, double x) => new(m.Value / x, m.Currency);

    /// <summary>The ratio of two amounts in one currency.</summary>
    /// <param name="m1">The dividend.</param>
    /// <param name="m2">The divisor.</param>
    /// <returns>The ratio of their values.</returns>
    /// <exception cref="NanoQLException">The currencies differ.</exception>
    public static double operator /(Money m1, Money m2) => Apply(m1, m2, null, static (x, y) => x / y);

    /// <summary>Whether two amounts are equal: the same currency and equal values.</summary>
    /// <param name="m1">The first amount.</param>
    /// <param name="m2">The second amount.</param>
    /// <returns>
    /// <see langword="true"/> when the currencies are the same and the values compare equal; <see langword="false"/> on
    /// a currency mismatch, where QuantLib's throws.
    /// </returns>
    public static bool operator ==(Money m1, Money m2) => m1.Currency == m2.Currency && m1.Value == m2.Value;

    /// <summary>Whether two amounts differ.</summary>
    /// <param name="m1">The first amount.</param>
    /// <param name="m2">The second amount.</param>
    /// <returns>The negation of <c>==</c>.</returns>
    public static bool operator !=(Money m1, Money m2) => !(m1 == m2);

    /// <summary>Whether one amount is less than another in the same currency.</summary>
    /// <param name="m1">The first amount.</param>
    /// <param name="m2">The second amount.</param>
    /// <returns><see langword="true"/> when the first value is less.</returns>
    /// <exception cref="NanoQLException">The currencies differ.</exception>
    public static bool operator <(Money m1, Money m2) => Apply(m1, m2, null, static (x, y) => x < y);

    /// <summary>Whether one amount is at most another in the same currency.</summary>
    /// <param name="m1">The first amount.</param>
    /// <param name="m2">The second amount.</param>
    /// <returns><see langword="true"/> when the first value is at most the second.</returns>
    /// <exception cref="NanoQLException">The currencies differ.</exception>
    public static bool operator <=(Money m1, Money m2) => Apply(m1, m2, null, static (x, y) => x <= y);

    /// <summary>Whether one amount is greater than another in the same currency: QuantLib's <c>m2 &lt;
    /// m1</c>.</summary>
    /// <param name="m1">The first amount.</param>
    /// <param name="m2">The second amount.</param>
    /// <returns><see langword="true"/> when the first value is greater.</returns>
    /// <exception cref="NanoQLException">The currencies differ.</exception>
    public static bool operator >(Money m1, Money m2) => m2 < m1;

    /// <summary>Whether one amount is at least another in the same currency: QuantLib's <c>m2 &lt;= m1</c>.</summary>
    /// <param name="m1">The first amount.</param>
    /// <param name="m2">The second amount.</param>
    /// <returns><see langword="true"/> when the first value is at least the second.</returns>
    /// <exception cref="NanoQLException">The currencies differ.</exception>
    public static bool operator >=(Money m1, Money m2) => m2 <= m1;

    /// <summary>The amount rounded by its currency's rounding convention.</summary>
    /// <returns>The rounded amount, in the same currency.</returns>
    /// <exception cref="NanoQLException">The currency is QuantLib's empty one, a <see langword="null"/>.</exception>
    public Money Rounded() => new(CurrencyOf(this).Rounding.Evaluate(Value), Currency);

    /// <summary>
    /// The sum of this amount and another, converting as the context's <see cref="EvaluationContext.MoneySettings"/>
    /// say: QuantLib's <c>operator+</c>.
    /// </summary>
    /// <param name="other">The amount added.</param>
    /// <param name="context">The evaluation context, whose money settings and exchange rates are read.</param>
    /// <returns>
    /// The sum: in this amount's currency, or in the base currency under
    /// <see cref="MoneyConversionType.BaseCurrencyConversion"/> when the currencies differ.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The currencies differ under <see cref="MoneyConversionType.NoConversion"/>; no base currency is set under
    /// <see cref="MoneyConversionType.BaseCurrencyConversion"/>; or an exchange rate can't be found.
    /// </exception>
    public Money Add(Money other, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Plus(this, other, context);
    }

    /// <summary>
    /// The difference of this amount and another, converting as <see cref="Add(Money, EvaluationContext)"/> does:
    /// QuantLib's <c>operator-</c>, which adds the negated amount.
    /// </summary>
    /// <param name="other">The amount subtracted.</param>
    /// <param name="context">The evaluation context, whose money settings and exchange rates are read.</param>
    /// <returns>The difference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">As <see cref="Add(Money, EvaluationContext)"/>.</exception>
    public Money Subtract(Money other, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Plus(this, -other, context);
    }

    /// <summary>The ratio of this amount to another, converting as the context's money settings say.</summary>
    /// <param name="other">The divisor.</param>
    /// <param name="context">The evaluation context, whose money settings and exchange rates are read.</param>
    /// <returns>The ratio of their values, once converted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">As <see cref="Add(Money, EvaluationContext)"/>.</exception>
    public double Divide(Money other, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Apply(this, other, context, static (x, y) => x / y);
    }

    /// <summary>
    /// Whether this amount equals another, converting as the context's money settings say: QuantLib's
    /// <c>operator==</c>.
    /// </summary>
    /// <param name="other">The other amount.</param>
    /// <param name="context">The evaluation context, whose money settings and exchange rates are read.</param>
    /// <returns>
    /// Whether the values, once converted, compare equal; <see langword="false"/> on a currency mismatch under
    /// <see cref="MoneyConversionType.NoConversion"/>, as <c>==</c> returns, where QuantLib's throws.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">No base currency is set, or an exchange rate can't be found.</exception>
    public bool Equals(Money other, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Currency != other.Currency && context.MoneySettings.ConversionType == MoneyConversionType.NoConversion)
        {
            return false;
        }

        return Apply(this, other, context, static (x, y) => x == y);
    }

    /// <summary>Whether this amount is less than another, converting as the context's money settings say.</summary>
    /// <param name="other">The other amount.</param>
    /// <param name="context">The evaluation context, whose money settings and exchange rates are read.</param>
    /// <returns>Whether this value is less, once converted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">As <see cref="Add(Money, EvaluationContext)"/>.</exception>
    public bool LessThan(Money other, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Apply(this, other, context, static (x, y) => x < y);
    }

    /// <summary>Whether this amount is at most another, converting as the context's money settings say.</summary>
    /// <param name="other">The other amount.</param>
    /// <param name="context">The evaluation context, whose money settings and exchange rates are read.</param>
    /// <returns>Whether this value is at most the other, once converted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">As <see cref="Add(Money, EvaluationContext)"/>.</exception>
    public bool LessThanOrEqual(Money other, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Apply(this, other, context, static (x, y) => x <= y);
    }

    /// <summary>
    /// Whether this amount is greater than another: QuantLib's <c>m1 &gt; m2</c>, which is <c>m2 &lt; m1</c>, so an
    /// automated conversion converts this amount into the other's currency.
    /// </summary>
    /// <param name="other">The other amount.</param>
    /// <param name="context">The evaluation context, whose money settings and exchange rates are read.</param>
    /// <returns>Whether this value is greater, once converted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">As <see cref="Add(Money, EvaluationContext)"/>.</exception>
    public bool GreaterThan(Money other, EvaluationContext context) => other.LessThan(this, context);

    /// <summary>
    /// Whether this amount is at least another: QuantLib's <c>m1 &gt;= m2</c>, which is <c>m2 &lt;= m1</c>, so an
    /// automated conversion converts this amount into the other's currency.
    /// </summary>
    /// <param name="other">The other amount.</param>
    /// <param name="context">The evaluation context, whose money settings and exchange rates are read.</param>
    /// <returns>Whether this value is at least the other, once converted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">As <see cref="Add(Money, EvaluationContext)"/>.</exception>
    public bool GreaterThanOrEqual(Money other, EvaluationContext context) => other.LessThanOrEqual(this, context);

    /// <summary>Whether this amount has another's currency and value, the value by
    /// <see cref="double.Equals(double)"/>.</summary>
    /// <param name="other">The other amount.</param>
    /// <returns><see langword="true"/> when both are the same.</returns>
    public bool Equals(Money other) => Currency == other.Currency && Value.Equals(other.Value);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Money other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Currency, Value);

    /// <summary>
    /// The amount as QuantLib's <c>operator&lt;&lt;</c> writes it: the rounded value in C++'s default stream format,
    /// six significant digits, then a space and the currency's code ("1.23457e+06 EUR", "0.12 EUR", "nan EUR",
    /// "-inf EUR").
    /// </summary>
    /// <returns>The text.</returns>
    /// <exception cref="NanoQLException">The currency is QuantLib's empty one, which has no rounding.</exception>
    public override string ToString()
    {
        return $"{DataFormattersFunctions.General(Rounded().Value)} {Currency!.Code}";
    }

    // QuantLib's Currency::rounding() and code() on an empty currency: "no currency data provided".
    internal static Currency CurrencyOf(Money m) =>
        m.Currency ?? throw new NanoQLException("no currency data provided");

    // QuantLib's apply on this amount and another, for MoneyFunctions' close and close_enough.
    internal T Apply<T>(Money other, EvaluationContext context, Func<double, double, T> f) =>
        Apply(this, other, context, f);

    // QuantLib's operator+=: within one currency the values' sum; otherwise converted as the settings say, or refused.
    private static Money Plus(Money m1, Money m2, EvaluationContext? context)
    {
        if (m1.Currency == m2.Currency)
        {
            return new Money(m1.Value + m2.Value, m1.Currency);
        }

        MoneyConversionType conversionType = context?.MoneySettings.ConversionType ?? MoneyConversionType.NoConversion;
        switch (conversionType)
        {
            case MoneyConversionType.BaseCurrencyConversion:
                Money tmp1 = ConvertToBase(m1, context!);
                Money tmp2 = ConvertToBase(m2, context!);
                return Plus(tmp1, tmp2, context);
            case MoneyConversionType.AutomatedConversion:
                Money tmp = ConvertTo(m2, m1.Currency, context!);
                return Plus(m1, tmp, context);
            default:
                throw new NanoQLException("currency mismatch and no conversion specified");
        }
    }

    // QuantLib's apply: f of the values within one currency, otherwise converted as the settings say, or refused.
    private static T Apply<T>(Money m1, Money m2, EvaluationContext? context, Func<double, double, T> f)
    {
        if (m1.Currency == m2.Currency)
        {
            return f(m1.Value, m2.Value);
        }

        MoneyConversionType conversionType = context?.MoneySettings.ConversionType ?? MoneyConversionType.NoConversion;
        switch (conversionType)
        {
            case MoneyConversionType.BaseCurrencyConversion:
                Money tmp1 = ConvertToBase(m1, context!);
                Money tmp2 = ConvertToBase(m2, context!);
                return f(tmp1.Value, tmp2.Value);
            case MoneyConversionType.AutomatedConversion:
                Money tmp = ConvertTo(m2, m1.Currency, context!);
                return f(m1.Value, tmp.Value);
            default:
                throw new NanoQLException("currency mismatch and no conversion specified");
        }
    }

    // QuantLib's convertTo: the amount exchanged at the context's rate on its evaluation date, then rounded.
    private static Money ConvertTo(Money m, Currency? target, EvaluationContext context)
    {
        if (m.Currency != target)
        {
            ExchangeRate rate = context.ExchangeRates.Lookup(m.Currency, target, context);
            return rate.Exchange(m).Rounded();
        }

        return m;
    }

    // QuantLib's convertToBase.
    private static Money ConvertToBase(Money m, EvaluationContext context)
    {
        Currency baseCurrency = context.MoneySettings.BaseCurrency
            ?? throw new NanoQLException("no base currency set");
        return ConvertTo(m, baseCurrency, context);
    }
}
