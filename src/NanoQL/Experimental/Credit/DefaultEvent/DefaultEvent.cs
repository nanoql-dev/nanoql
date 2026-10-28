// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/defaultevent.hpp ql/experimental/credit/defaultevent.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A credit event on the bonds of a seniority and currency, with its optional settlement: a realized event, of an
/// atomic type, which the bonds of that seniority and currency suffer together.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, and an extension point (<see cref="FailureToPayEvent"/>, <see cref="BankruptcyEvent"/>). An event that
/// affects every seniority is a set of events of the same type, or one settled for
/// <see cref="Seniority.NoSeniority"/>.
/// </para>
/// <para>
/// Equality is QuantLib's: two events are the same, whatever their settlements, when their currencies, types, dates and
/// seniorities are. It is sealed. An issuer keeps every event it is given, equal ones included
/// (<see cref="Issuer"/>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class DefaultEvent : Event, IEquatable<DefaultEvent>
{
    private readonly Date defaultDate;

    /// <summary>Initializes a new instance of the <see cref="DefaultEvent"/> class, settled for the seniorities a map
    /// holds.</summary>
    /// <param name="creditEventDate">The credit event's date.</param>
    /// <param name="atomicEventType">The event's atomic type.</param>
    /// <param name="currency">The bonds' currency; <see langword="null"/> for QuantLib's empty currency.</param>
    /// <param name="bondsSeniority">The seniority of the bonds that triggered the event.</param>
    /// <param name="settleDate">The settlement date, or <see langword="null"/> for an event not settled.</param>
    /// <param name="recoveryRates">The realized recoveries by seniority, which must hold the event's seniority when the
    /// event has settled; the seniorities settled are those it holds. ISDA's conventional recoveries when
    /// <see langword="null"/> or empty, which an unsettled event never reads.</param>
    /// <exception cref="ArgumentNullException"><paramref name="atomicEventType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bondsSeniority"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>; the settlement precedes the event
    /// ("Settlement date should be after default date."); the map lacks the event's seniority ("Settled events must
    /// contain the seniority of the default") or holds <see cref="Seniority.NoSeniority"/>.</exception>
    public DefaultEvent(
        Date creditEventDate,
        DefaultType atomicEventType,
        Currency? currency,
        Seniority bondsSeniority,
        Date? settleDate = null,
        IReadOnlyDictionary<Seniority, double>? recoveryRates = null)
    {
        Date.ThrowIfDefault(creditEventDate, nameof(creditEventDate));
        Date.ThrowIfDefault(settleDate, nameof(settleDate));
        ArgumentNullException.ThrowIfNull(atomicEventType);
        EnumArguments.Check(bondsSeniority, nameof(bondsSeniority));
        ImmutableSortedDictionary<Seniority, double> rates = recoveryRates is null
            ? ImmutableSortedDictionary<Seniority, double>.Empty
            : recoveryRates.ToImmutableSortedDictionary();
        defaultDate = creditEventDate;
        DefaultType = atomicEventType;
        Currency = currency;
        EventSeniority = bondsSeniority;
        if (settleDate is { } s)
        {
            // has settled
            if (s < creditEventDate)
            {
                throw new ArgumentException("Settlement date should be after default date.", nameof(settleDate));
            }

            if (!rates.ContainsKey(bondsSeniority))
            {
                throw new ArgumentException("Settled events must contain the seniority of the default", nameof(recoveryRates));
            }

            Settlement = new DefaultSettlement(s, rates, nameof(recoveryRates));
        }
        else if (rates.ContainsKey(Seniority.NoSeniority))
        {
            // QuantLib's settlement checks the map an unsettled event holds as well
            throw new ArgumentException("NoSeniority is not a valid realized seniority.", nameof(recoveryRates));
        }
    }

    /// <summary>Initializes a new instance of the <see cref="DefaultEvent"/> class, settled at one recovery rate.</summary>
    /// <param name="creditEventDate">The credit event's date.</param>
    /// <param name="atomicEventType">The event's atomic type.</param>
    /// <param name="currency">The bonds' currency; <see langword="null"/> for QuantLib's empty currency.</param>
    /// <param name="bondsSeniority">The seniority of the bonds that triggered the event, the seniority the recovery
    /// settles; <see cref="Seniority.NoSeniority"/> settles every ISDA seniority at the rate, the event then taken to
    /// have affected all of them.</param>
    /// <param name="settleDate">The settlement date, or <see langword="null"/> for an event not settled.</param>
    /// <param name="recoveryRate">The realized recovery.</param>
    /// <exception cref="ArgumentNullException"><paramref name="atomicEventType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bondsSeniority"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, or the settlement precedes the event
    /// ("Settlement date should be after default date.").</exception>
    public DefaultEvent(
        Date creditEventDate,
        DefaultType atomicEventType,
        Currency? currency,
        Seniority bondsSeniority,
        Date? settleDate,
        double recoveryRate)
    {
        Date.ThrowIfDefault(creditEventDate, nameof(creditEventDate));
        Date.ThrowIfDefault(settleDate, nameof(settleDate));
        ArgumentNullException.ThrowIfNull(atomicEventType);
        EnumArguments.Check(bondsSeniority, nameof(bondsSeniority));
        defaultDate = creditEventDate;
        DefaultType = atomicEventType;
        Currency = currency;
        EventSeniority = bondsSeniority;
        if (settleDate is { } s)
        {
            if (s < creditEventDate)
            {
                throw new ArgumentException("Settlement date should be after default date.", nameof(settleDate));
            }

            Settlement = new DefaultSettlement(s, bondsSeniority, recoveryRate);
        }
    }

    /// <summary>Gets the credit event's date.</summary>
    public override Date Date => defaultDate;

    /// <summary>Gets a value indicating whether the event is a restructuring.</summary>
    public bool IsRestructuring => DefaultType.IsRestructuring;

    /// <summary>Gets a value indicating whether the event is a default: not a restructuring.</summary>
    public bool IsDefault => !IsRestructuring;

    /// <summary>Gets a value indicating whether the event has settled.</summary>
    public bool HasSettled => Settlement is not null;

    /// <summary>Gets the settlement, or <see langword="null"/> where the event hasn't settled.</summary>
    public DefaultSettlement? Settlement { get; }

    /// <summary>Gets the event's atomic type.</summary>
    public DefaultType DefaultType { get; }

    /// <summary>Gets the currency of the bonds the event refers to; <see langword="null"/> for QuantLib's empty
    /// currency.</summary>
    public Currency? Currency { get; }

    /// <summary>Gets the seniority of the bonds that triggered the event.</summary>
    public Seniority EventSeniority { get; }

    /// <summary>Whether two events are the same: their currencies, types, dates and seniorities are.</summary>
    /// <param name="left">The first event.</param>
    /// <param name="right">The second event.</param>
    /// <returns><see langword="true"/> when they are, or both are <see langword="null"/>.</returns>
    public static bool operator ==(DefaultEvent? left, DefaultEvent? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Whether two events differ.</summary>
    /// <param name="left">The first event.</param>
    /// <param name="right">The second event.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(DefaultEvent? left, DefaultEvent? right) => !(left == right);

    /// <summary>The realized recovery of a seniority, if the event settled it.</summary>
    /// <param name="seniority">The seniority.</param>
    /// <returns>The recovery, or <see langword="null"/> where the event hasn't settled, or didn't settle the
    /// seniority.</returns>
    /// <exception cref="ArgumentException"><paramref name="seniority"/> is <see cref="Seniority.NoSeniority"/> on a
    /// settled event.</exception>
    public virtual double? RecoveryRate(Seniority seniority) => Settlement?.RecoveryRate(seniority);

    /// <summary>
    /// Whether the event would trigger a contract on an event type: by default, the type's restructuring and atomic
    /// types match the event's. Seniority and currency aren't checked.
    /// </summary>
    /// <param name="contractEventType">The contract's event type.</param>
    /// <param name="context">The evaluation context, whose date a derived event may read.</param>
    /// <returns><see langword="true"/> when it would.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public virtual bool MatchesEventType(DefaultType contractEventType, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(contractEventType);
        ArgumentNullException.ThrowIfNull(context);

        // remember we are made of an atomic type.
        // behaviour by default...
        return contractEventType.ContainsRestructuringType(DefaultType.RestructuringType)
            && contractEventType.ContainsDefaultType(DefaultType.AtomicType);
    }

    /// <summary>
    /// Whether the event would trigger a contract on a key: its currency, its seniority (any, for a key of
    /// <see cref="Seniority.NoSeniority"/>), and one of its event types match.
    /// </summary>
    /// <param name="contractKey">The contract's key.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/> when it would.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public virtual bool MatchesDefaultKey(DefaultProbKey contractKey, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(contractKey);
        ArgumentNullException.ThrowIfNull(context);
        if (Currency != contractKey.Currency)
        {
            return false;
        }

        // a contract with NoSeniority matches all events
        if (EventSeniority != contractKey.Seniority && contractKey.Seniority != Seniority.NoSeniority)
        {
            return false;
        }

        // loop on all event types in the contract and chek if we match any,
        //   calls derived types
        foreach (DefaultType t in contractKey.EventTypes)
        {
            if (MatchesEventType(t, context))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public bool Equals(DefaultEvent? other) =>
        other is not null && Currency == other.Currency && DefaultType == other.DefaultType && Date == other.Date
        && EventSeniority == other.EventSeniority;

    /// <inheritdoc/>
    public sealed override bool Equals(object? obj) => obj is DefaultEvent other && Equals(other);

    /// <inheritdoc/>
    public sealed override int GetHashCode() => HashCode.Combine(Currency, DefaultType, Date, EventSeniority);
}
