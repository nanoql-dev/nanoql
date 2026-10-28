// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/defaultevent.hpp ql/experimental/credit/defaultevent.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A failure to pay: a default event with the amount defaulted, which matches a contract's
/// <see cref="FailureToPay"/> type once its grace period has passed.</summary>
[Experimental("NQL0001")]
public sealed class FailureToPayEvent : DefaultEvent
{
    /// <summary>Initializes a new instance of the <see cref="FailureToPayEvent"/> class, settled for the seniorities a
    /// map holds.</summary>
    /// <param name="creditEventDate">The credit event's date.</param>
    /// <param name="currency">The bonds' currency; <see langword="null"/> for QuantLib's empty currency.</param>
    /// <param name="bondsSeniority">The seniority of the bonds that triggered the event.</param>
    /// <param name="defaultedAmount">The amount defaulted.</param>
    /// <param name="settleDate">The settlement date, or <see langword="null"/> for an event not settled.</param>
    /// <param name="recoveryRates">The realized recoveries by seniority (<see cref="DefaultEvent"/>'s map
    /// form).</param>
    /// <exception cref="ArgumentException">As <see cref="DefaultEvent"/>'s map constructor.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bondsSeniority"/> isn't a member of its
    /// type.</exception>
    public FailureToPayEvent(
        Date creditEventDate,
        Currency? currency,
        Seniority bondsSeniority,
        double defaultedAmount,
        Date? settleDate,
        IReadOnlyDictionary<Seniority, double>? recoveryRates)
        : base(
            creditEventDate,
            new DefaultType(AtomicDefaultType.FailureToPay, RestructuringType.XR),
            currency,
            bondsSeniority,
            settleDate,
            recoveryRates)
    {
        AmountDefaulted = defaultedAmount;
    }

    /// <summary>Initializes a new instance of the <see cref="FailureToPayEvent"/> class, settled at one recovery
    /// rate.</summary>
    /// <param name="creditEventDate">The credit event's date.</param>
    /// <param name="currency">The bonds' currency; <see langword="null"/> for QuantLib's empty currency.</param>
    /// <param name="bondsSeniority">The seniority of the bonds that triggered the event, the seniority the recovery
    /// settles (every ISDA seniority for <see cref="Seniority.NoSeniority"/>).</param>
    /// <param name="defaultedAmount">The amount defaulted.</param>
    /// <param name="settleDate">The settlement date, or <see langword="null"/> for an event not settled.</param>
    /// <param name="recoveryRate">The realized recovery.</param>
    /// <exception cref="ArgumentException">As <see cref="DefaultEvent"/>'s single-rate constructor.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bondsSeniority"/> isn't a member of its
    /// type.</exception>
    public FailureToPayEvent(
        Date creditEventDate,
        Currency? currency,
        Seniority bondsSeniority,
        double defaultedAmount,
        Date? settleDate,
        double recoveryRate)
        : base(
            creditEventDate,
            new DefaultType(AtomicDefaultType.FailureToPay, RestructuringType.XR),
            currency,
            bondsSeniority,
            settleDate,
            recoveryRate)
    {
        AmountDefaulted = defaultedAmount;
    }

    /// <summary>Gets the amount defaulted.</summary>
    public double AmountDefaulted { get; }

    /// <summary>
    /// Whether the event matches a contract's failure-to-pay type: the amount defaulted reaches the type's required
    /// amount, and the event occurred before the evaluation date less the grace period.
    /// </summary>
    /// <param name="contractEventType">The contract's event type.</param>
    /// <param name="context">The evaluation context, whose date the grace period runs back from.</param>
    /// <returns><see langword="true"/> when it matches; never for a type that isn't a <see cref="FailureToPay"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public override bool MatchesEventType(DefaultType contractEventType, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(contractEventType);
        ArgumentNullException.ThrowIfNull(context);

        // this chekcs the atomic types, no need to call parents method
        if (contractEventType is not FailureToPay eveType)
        {
            return false;
        }

        if (AmountDefaulted < eveType.AmountRequired)
        {
            return false;
        }

        Date today = context.EvaluationDate;
        return HasOccurred(context, today - eveType.GracePeriod, true);
    }
}
