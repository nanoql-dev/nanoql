// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: Copyright (C) 2004, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/exchangerate.hpp ql/exchangerate.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Equality-tested: Conventions/ExchangeRatesValueTests.cs

namespace NanoQL;

/// <summary>The exchange rate between two currencies, given directly or derived by chaining two others.</summary>
/// <remarks>
/// <para>
/// Immutable. The rate <c>r</c> is given with the convention that a unit of the source is worth <c>r</c> units of the
/// target. QuantLib's default constructor, an empty rate whose value is <c>Null&lt;Real&gt;()</c>, isn't ported: no
/// member builds one.
/// </para>
/// <para>
/// Equality is nanoql's, where QuantLib has none: two rates are equal when they have the same source, target, rate
/// (by <see cref="double.Equals(double)"/>) and type, and, for derived rates, equal chains. The exchange-rate
/// registry's equality reads it.
/// </para>
/// <para>
/// <see cref="Chain"/> refuses two rates over one pair of currencies, which QuantLib chains into a rate from a currency
/// to itself (EUR→USD at 1.1 with EUR→USD at 1.2 gives USD→USD at 1.0909, which exchanges 100 USD into 109.09 USD): a
/// documented divergence (<c>Divergences/ExchangeRateChainRefusalTests.cs</c>). The exchange-rate registry's chained
/// lookups chain as QuantLib's, since their steps are never over one pair in its key.
/// </para>
/// </remarks>
public sealed class ExchangeRate : IEquatable<ExchangeRate>
{
    private readonly ExchangeRate? first;
    private readonly ExchangeRate? second;

    /// <summary>Initializes a new instance of the <see cref="ExchangeRate"/> class: a direct rate.</summary>
    /// <param name="source">The source currency; <see langword="null"/> for QuantLib's empty currency.</param>
    /// <param name="target">The target currency; <see langword="null"/> for QuantLib's empty currency.</param>
    /// <param name="rate">The units of the target a unit of the source is worth.</param>
    public ExchangeRate(Currency? source, Currency? target, double rate)
    {
        Source = source;
        Target = target;
        Rate = rate;
        Type = ExchangeRateType.Direct;
    }

    private ExchangeRate(Currency? source, Currency? target, double rate, ExchangeRate first, ExchangeRate second)
    {
        Source = source;
        Target = target;
        Rate = rate;
        Type = ExchangeRateType.Derived;
        this.first = first;
        this.second = second;
    }

    /// <summary>Gets the source currency.</summary>
    public Currency? Source { get; }

    /// <summary>Gets the target currency.</summary>
    public Currency? Target { get; }

    /// <summary>Gets the type: given directly, or derived by chaining.</summary>
    public ExchangeRateType Type { get; }

    /// <summary>Gets the rate: the units of the target a unit of the source is worth.</summary>
    public double Rate { get; }

    /// <summary>Chains two exchange rates that share a currency into a derived rate between the other two.</summary>
    /// <param name="r1">The first rate.</param>
    /// <param name="r2">The second rate.</param>
    /// <returns>The derived rate.</returns>
    /// <exception cref="ArgumentNullException">A rate is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The rates share no currency, or are over one pair of currencies, QuantLib's "exchange rates not chainable".
    /// </exception>
    public static ExchangeRate Chain(ExchangeRate r1, ExchangeRate r2)
    {
        ArgumentNullException.ThrowIfNull(r1);
        ArgumentNullException.ThrowIfNull(r2);

        // Two rates over one pair chain into a rate from a currency to itself, which QuantLib returns
        // (exchangerate.cpp:57-60, :61-64): refused with QuantLib's own text (:74). One pair by the currencies'
        // equality, the one the four cases below match by, so no chain gives a rate from a currency to itself.
        if ((r1.Source == r2.Source && r1.Target == r2.Target) || (r1.Source == r2.Target && r1.Target == r2.Source))
        {
            throw new ArgumentException("exchange rates not chainable", nameof(r2));
        }

        return ChainUnchecked(r1, r2);
    }

    /// <summary>
    /// QuantLib's <c>chain</c> as it is (<c>exchangerate.cpp:52-75</c>), with no one-pair refusal: the registry's
    /// chained lookups, whose head and tail are never one pair in its key, chain as QuantLib's, including through a
    /// currency equal to another but keyed apart.
    /// </summary>
    /// <param name="r1">The first rate.</param>
    /// <param name="r2">The second rate.</param>
    /// <returns>The derived rate.</returns>
    /// <exception cref="ArgumentException">The rates share no currency.</exception>
    internal static ExchangeRate ChainUnchecked(ExchangeRate r1, ExchangeRate r2)
    {
        if (r1.Source == r2.Source)
        {
            return new ExchangeRate(r1.Target, r2.Target, r2.Rate / r1.Rate, r1, r2);
        }

        if (r1.Source == r2.Target)
        {
            return new ExchangeRate(r1.Target, r2.Source, 1.0 / (r1.Rate * r2.Rate), r1, r2);
        }

        if (r1.Target == r2.Source)
        {
            return new ExchangeRate(r1.Source, r2.Target, r1.Rate * r2.Rate, r1, r2);
        }

        if (r1.Target == r2.Target)
        {
            return new ExchangeRate(r1.Source, r2.Source, r1.Rate / r2.Rate, r1, r2);
        }

        throw new ArgumentException("exchange rates not chainable", nameof(r2));
    }

    /// <summary>Applies the exchange rate to an amount in either of its currencies.</summary>
    /// <param name="amount">The amount.</param>
    /// <returns>The amount in the other currency.</returns>
    /// <exception cref="ArgumentException">The amount isn't in either of the rate's currencies.</exception>
    public Money Exchange(Money amount)
    {
        if (Type == ExchangeRateType.Direct)
        {
            if (amount.Currency == Source)
            {
                return new Money(amount.Value * Rate, Target);
            }

            if (amount.Currency == Target)
            {
                return new Money(amount.Value / Rate, Source);
            }

            throw new ArgumentException("exchange rate not applicable", nameof(amount));
        }

        if (amount.Currency == first!.Source || amount.Currency == first.Target)
        {
            return second!.Exchange(first.Exchange(amount));
        }

        if (amount.Currency == second!.Source || amount.Currency == second.Target)
        {
            return first.Exchange(second.Exchange(amount));
        }

        throw new ArgumentException("exchange rate not applicable", nameof(amount));
    }

    /// <summary>Whether this rate has another's source, target, rate and type, and, if derived, an equal
    /// chain.</summary>
    /// <param name="other">The other rate.</param>
    /// <returns><see langword="true"/> when they are equal.</returns>
    public bool Equals(ExchangeRate? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null
            && Source == other.Source
            && Target == other.Target
            && Rate.Equals(other.Rate)
            && Type == other.Type
            && Equals(first, other.first)
            && Equals(second, other.second);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ExchangeRate other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Source, Target, Rate, Type, first, second);
}
