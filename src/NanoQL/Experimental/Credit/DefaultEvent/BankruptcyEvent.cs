// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/defaultevent.hpp ql/experimental/credit/defaultevent.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A bankruptcy: a default event stronger than every other, which matches every contract event type.</summary>
/// <remarks>
/// A settlement settles the seniorities it holds, or the one its single rate names, as any event's does (the S-xcr
/// plan's XR8 (iii)): QuantLib's map constructor refuses a settlement that doesn't hold all five ISDA seniorities
/// ("Bankruptcy event should have settled for all seniorities.", <c>defaultevent.cpp:210-213</c>), while its single-rate
/// constructor fills the others with ISDA's conventional recoveries, so the two forms disagree. A documented
/// divergence (<c>Divergences/SingleSenioritySettlementTests.cs</c>); no QuantLib code or test builds one.
/// </remarks>
[Experimental("NQL0001")]
public sealed class BankruptcyEvent : DefaultEvent
{
    /// <summary>Initializes a new instance of the <see cref="BankruptcyEvent"/> class, settled for the seniorities a
    /// map holds.</summary>
    /// <param name="creditEventDate">The credit event's date.</param>
    /// <param name="currency">The bonds' currency; <see langword="null"/> for QuantLib's empty currency.</param>
    /// <param name="bondsSeniority">The seniority of the bonds that triggered the event.</param>
    /// <param name="settleDate">The settlement date, or <see langword="null"/> for an event not settled.</param>
    /// <param name="recoveryRates">The realized recoveries by seniority (<see cref="DefaultEvent"/>'s map
    /// form).</param>
    /// <exception cref="ArgumentException">As <see cref="DefaultEvent"/>'s map constructor.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bondsSeniority"/> isn't a member of its
    /// type.</exception>
    public BankruptcyEvent(
        Date creditEventDate,
        Currency? currency,
        Seniority bondsSeniority,
        Date? settleDate,
        IReadOnlyDictionary<Seniority, double>? recoveryRates)
        : base(
            creditEventDate,
            new DefaultType(AtomicDefaultType.Bankruptcy, RestructuringType.XR),
            currency,
            bondsSeniority,
            settleDate,
            recoveryRates)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BankruptcyEvent"/> class, settled at one recovery
    /// rate.</summary>
    /// <param name="creditEventDate">The credit event's date.</param>
    /// <param name="currency">The bonds' currency; <see langword="null"/> for QuantLib's empty currency.</param>
    /// <param name="bondsSeniority">The seniority of the bonds that triggered the event, the seniority the recovery
    /// settles (every ISDA seniority for <see cref="Seniority.NoSeniority"/>).</param>
    /// <param name="settleDate">The settlement date, or <see langword="null"/> for an event not settled.</param>
    /// <param name="recoveryRate">The realized recovery.</param>
    /// <exception cref="ArgumentException">As <see cref="DefaultEvent"/>'s single-rate constructor.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bondsSeniority"/> isn't a member of its
    /// type.</exception>
    public BankruptcyEvent(
        Date creditEventDate,
        Currency? currency,
        Seniority bondsSeniority,
        Date? settleDate,
        double recoveryRate)
        : base(
            creditEventDate,
            new DefaultType(AtomicDefaultType.Bankruptcy, RestructuringType.XR),
            currency,
            bondsSeniority,
            settleDate,
            recoveryRate)
    {
    }

    /// <summary>A bankruptcy triggers every contract event type.</summary>
    /// <param name="contractEventType">The contract's event type.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public override bool MatchesEventType(DefaultType contractEventType, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(contractEventType);
        ArgumentNullException.ThrowIfNull(context);
        return true;
    }
}
