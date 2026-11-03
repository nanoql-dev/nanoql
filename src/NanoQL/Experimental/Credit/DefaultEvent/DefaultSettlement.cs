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
/// A default event's settlement: its date and the realized recovery rates of the seniorities it settled. QuantLib's
/// <c>DefaultEvent::DefaultSettlement</c>.
/// </summary>
/// <remarks>
/// <para>
/// Immutable; built by <see cref="DefaultEvent"/>'s constructors, as QuantLib's are protected and the event a friend.
/// An event that hasn't settled has none (<see cref="DefaultEvent.Settlement"/> is <see langword="null"/>), where
/// QuantLib's holds a settlement with a null date.
/// </para>
/// <para>
/// A settlement from a single recovery rate settles the seniority it names, and every ISDA seniority for
/// <see cref="Seniority.NoSeniority"/>, as a settlement from a map settles the seniorities it holds. QuantLib's fills
/// every other seniority with ISDA's conventional recovery (<c>defaultevent.cpp:63-75</c>), so an event settled for
/// <see cref="Seniority.SubLT2"/> at 0.3 reads 0.65 for <see cref="Seniority.SecDom"/>, where each seniority settles at
/// its own auction (ISDA's 2008 Icelandic protocols). A documented divergence
/// (<c>Divergences/SingleSenioritySettlementTests.cs</c>); a seniority not settled reads <see langword="null"/>.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class DefaultSettlement : Event
{
    // A settlement of the seniorities a map holds.
    internal DefaultSettlement(Date date, ImmutableSortedDictionary<Seniority, double> recoveryRates, string paramName)
    {
        if (recoveryRates.ContainsKey(Seniority.NoSeniority))
        {
            throw new ArgumentException("NoSeniority is not a valid realized seniority.", paramName);
        }

        Date = date;
        RecoveryRates = recoveryRates;
    }

    // A settlement of one seniority at a recovery rate, or of every ISDA seniority for NoSeniority.
    internal DefaultSettlement(Date date, Seniority seniority, double recoveryRate)
    {
        Date = date;
        if (seniority == Seniority.NoSeniority)
        {
            var rates = RecoveryRateQuote.MakeIsdaConvMap().ToBuilder();
            foreach (Seniority s in rates.Keys.ToList())
            {
                rates[s] = recoveryRate;
            }

            RecoveryRates = rates.ToImmutable();
        }
        else
        {
            RecoveryRates = ImmutableSortedDictionary<Seniority, double>.Empty.Add(seniority, recoveryRate);
        }
    }

    /// <summary>Gets the settlement date.</summary>
    public override Date Date { get; }

    /// <summary>Gets the realized recovery rates of the seniorities the event settled.</summary>
    public ImmutableSortedDictionary<Seniority, double> RecoveryRates { get; }

    /// <summary>The realized recovery rate of a seniority.</summary>
    /// <param name="seniority">The seniority.</param>
    /// <returns>The recovery, or <see langword="null"/> where the event didn't settle the seniority.</returns>
    /// <exception cref="ArgumentException"><paramref name="seniority"/> is <see cref="Seniority.NoSeniority"/>
    /// ("NoSeniority is not valid for recovery rate request.").</exception>
    public double? RecoveryRate(Seniority seniority)
    {
        // expensive require cause called often...... fix me
        if (seniority == Seniority.NoSeniority)
        {
            throw new ArgumentException("NoSeniority is not valid for recovery rate request.", nameof(seniority));
        }

        return RecoveryRates.TryGetValue(seniority, out double r) ? r : null;
    }
}
