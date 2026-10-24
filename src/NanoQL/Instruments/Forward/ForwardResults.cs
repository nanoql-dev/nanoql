// SPDX-FileCopyrightText: Copyright (C) 2006 Allen Kuo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/forward.hpp ql/instruments/forward.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The results of a <see cref="Forward"/>'s calculation: the instrument's results, with the NPV the payoff on the
/// forward value discounted from the maturity date, and the underlying's spot value and income and the forward value.
/// </summary>
/// <remarks>QuantLib's mutable <c>underlyingSpotValue_</c> and <c>underlyingIncome_</c>, which its calculation sets,
/// and its <c>forwardValue()</c>, which calculates on reading, are results here (the S-cms plan's CMD5). A result that
/// can't be computed is <see langword="null"/>: an expired forward's.</remarks>
public sealed record ForwardResults : InstrumentResults
{
    /// <summary>Gets the underlying's spot value.</summary>
    public double? SpotValue { get; init; }

    /// <summary>Gets the underlying's income between the settlement and the maturity dates, discounted on the income
    /// curve.</summary>
    public double? SpotIncome { get; init; }

    /// <summary>Gets the forward value of the underlying: its spot value less its income, over the discount at the
    /// maturity date.</summary>
    public double? ForwardValue { get; init; }
}
