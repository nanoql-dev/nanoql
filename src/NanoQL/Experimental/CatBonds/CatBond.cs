// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/catbond.hpp ql/experimental/catbonds/catbond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A catastrophe bond: a bond whose notional a catastrophe risk reduces, priced by simulating the risk's events.
/// </summary>
/// <remarks>
/// On the bond contract's protected constructor: a derived bond writes the leg, as <see cref="FloatingCatBond"/> does.
/// An engine for plain bonds refuses it, as QuantLib's does with "wrong arguments type"; its engine is
/// <see cref="MonteCarloCatBondEngine"/>, whose results carry the loss statistics QuantLib's bond keeps. An expired
/// bond's statistics are null, which QuantLib's leaves at their last values. An extension point, as in QuantLib.
/// </remarks>
[Experimental("NQL0001")]
public class CatBond : Bond
{
    /// <summary>Initializes a new instance of the <see cref="CatBond"/> class, without a leg, which a derived bond
    /// writes.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">The settlement calendar, or <see langword="null"/> for QuantLib's empty one.</param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="notionalRisk">The notional risk.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notionalRisk"/> is <see langword="null"/>.</exception>
    protected CatBond(int settlementDays, Calendar? calendar, Date? issueDate, NotionalRisk notionalRisk)
        : base(settlementDays, calendar, issueDate)
    {
        ArgumentNullException.ThrowIfNull(notionalRisk);
        NotionalRisk = notionalRisk;
    }

    /// <summary>Gets the notional risk.</summary>
    public NotionalRisk NotionalRisk { get; }

    /// <inheritdoc/>
    /// <remarks>An expired bond's settlement value is 0 and its loss statistics null.</remarks>
    public override CatBondResults SetupExpired()
    {
        BondResults expired = base.SetupExpired();
        return new CatBondResults
        {
            NPV = expired.NPV,
            ErrorEstimate = expired.ErrorEstimate,
            ValuationDate = expired.ValuationDate,
            AdditionalResults = expired.AdditionalResults,
            SettlementValue = expired.SettlementValue,
        };
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The results aren't a cat bond's.</exception>
    public override CatBondResults FetchResults(InstrumentResults results)
    {
        BondResults fetched = base.FetchResults(results);
        if (fetched is not CatBondResults r)
        {
            throw new ArgumentException("wrong result type", nameof(results));
        }

        return r;
    }

    /// <inheritdoc/>
    internal override void CheckPlainBondArguments() => throw new NanoQLException("wrong arguments type");
}
