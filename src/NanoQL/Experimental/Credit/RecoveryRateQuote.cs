// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/recoveryratequote.hpp ql/experimental/credit/recoveryratequote.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>A recovery-rate market quote and the seniority it is quoted for.</summary>
/// <remarks>
/// <para>
/// Immutable (§2.2's quote row; the S-core plan's CR1): the value is required, so QuantLib's <c>isValid</c>,
/// <c>setValue</c> and <c>reset</c>, which a mutable quote with a null value needs, aren't ported; a new value is a new
/// quote.
/// </para>
/// <para>
/// ISDA's conventional recoveries cover the five ISDA seniorities. QuantLib's table read for
/// <see cref="Seniority.NoSeniority"/> reads past its five entries (<c>recoveryratequote.hpp:36-37</c>), which nanoql
/// refuses (the S-xcr plan's XR8 (xxviii); <c>Divergences/ConventionalRecoveryNoSeniorityTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class RecoveryRateQuote
{
    // Conventional recoveries for ISDA seniorities
    private static readonly double[] IsdaConvRecoveries =
    [
        0.65, // SECDOM
        0.4, // SNRFOR
        0.2, // SUBLT2
        0.2, // JRSUBUT2
        0.15, // PREFT1
    ];

    /// <summary>Initializes a new instance of the <see cref="RecoveryRateQuote"/> class.</summary>
    /// <param name="value">The recovery rate, a fraction.</param>
    /// <param name="seniority">The seniority it is quoted for; none by default.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is outside [0, 1] ("Recovery value must be a
    /// fractional unit.").</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seniority"/> isn't a member of its type.</exception>
    public RecoveryRateQuote(double value, Seniority seniority = Seniority.NoSeniority)
    {
        if (!(value >= 0.0 && value <= 1.0))
        {
            throw new ArgumentException("Recovery value must be a fractional unit.", nameof(value));
        }

        EnumArguments.Check(seniority, nameof(seniority));
        Value = value;
        Seniority = seniority;
    }

    /// <summary>Gets the recovery rate, a fraction.</summary>
    public double Value { get; }

    /// <summary>Gets the seniority the quote is for.</summary>
    public Seniority Seniority { get; }

    /// <summary>ISDA's conventional recovery rate for a seniority.</summary>
    /// <param name="seniority">One of ISDA's five seniorities.</param>
    /// <returns>The conventional recovery.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seniority"/> is
    /// <see cref="Seniority.NoSeniority"/>, which has no conventional recovery, or isn't a member of its
    /// type.</exception>
    public static double ConventionalRecovery(Seniority seniority)
    {
        if (seniority is < Seniority.SecDom or > Seniority.PrefT1)
        {
            throw new ArgumentOutOfRangeException(nameof(seniority), seniority, "no conventional recovery for this seniority");
        }

        return IsdaConvRecoveries[(int)seniority];
    }

    /// <summary>ISDA's conventional recovery rates for the five ISDA seniorities: QuantLib's
    /// <c>makeIsdaConvMap</c>.</summary>
    /// <returns>The map of seniorities to their recoveries.</returns>
    public static ImmutableSortedDictionary<Seniority, double> MakeIsdaConvMap() => MakeIsdaMap(IsdaConvRecoveries);

    /// <summary>A map of seniorities to recoveries, the i-th recovery for the i-th seniority, as an event's settlement
    /// takes it.</summary>
    /// <param name="recoveries">The recoveries, in the order of <see cref="Seniority"/>'s members.</param>
    /// <returns>The map.</returns>
    /// <exception cref="ArgumentOutOfRangeException">More recoveries than seniorities, six with
    /// <see cref="Seniority.NoSeniority"/>.</exception>
    public static ImmutableSortedDictionary<Seniority, double> MakeIsdaMap(ReadOnlySpan<double> recoveries)
    {
        if (recoveries.Length > (int)Seniority.NoSeniority + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(recoveries), recoveries.Length, "more recoveries than seniorities");
        }

        var isdaMap = ImmutableSortedDictionary.CreateBuilder<Seniority, double>();
        for (int i = 0; i < recoveries.Length; i++)
        {
            isdaMap[(Seniority)i] = recoveries[i];
        }

        return isdaMap.ToImmutable();
    }
}
