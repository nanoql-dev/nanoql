// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/cliquetoption.hpp ql/instruments/cliquetoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A cliquet option, or ratchet option: a series of forward-starting options, each struck at a fixed percentage of the
/// spot at the start of its period.
/// </summary>
/// <remarks>
/// Immutable. QuantLib's arguments carry an accrued coupon, a last fixing and local and global caps and floors, which
/// its instrument never sets ("add local/global caps/floors", its header's to-do), so they and the engines' refusals of
/// them aren't ported (the S-exo plan's ED8).
/// </remarks>
public sealed class CliquetOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="CliquetOption"/> class.</summary>
    /// <param name="payoff">The payoff, struck at a percentage of each period's starting spot.</param>
    /// <param name="maturity">The exercise at maturity.</param>
    /// <param name="resetDates">The dates each period starts on.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public CliquetOption(PercentageStrikePayoff payoff, EuropeanExercise maturity, IEnumerable<Date> resetDates)
        : base(payoff, maturity)
    {
        ArgumentNullException.ThrowIfNull(resetDates);
        ResetDates = [.. resetDates];
    }

    /// <summary>Gets the dates each period starts on.</summary>
    public ImmutableArray<Date> ResetDates { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">
    /// The moneyness isn't positive, no reset date is given, or the reset dates aren't sorted before maturity.
    /// </exception>
    public override void Validate()
    {
        base.Validate();
        if (!(((PercentageStrikePayoff)Payoff!).Strike > 0.0))
        {
            throw new NanoQLException("negative or zero moneyness given");
        }

        if (ResetDates.IsEmpty)
        {
            throw new NanoQLException("no reset dates given");
        }

        for (int i = 0; i < ResetDates.Length; ++i)
        {
            if (!(Exercise.LastDate() > ResetDates[i]))
            {
                throw new NanoQLException("reset date greater or equal to maturity");
            }

            if (!(i == 0 || ResetDates[i] > ResetDates[i - 1]))
            {
                throw new NanoQLException("unsorted reset dates");
            }
        }
    }
}
