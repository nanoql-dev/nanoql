// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A currency's fixed leg tenor at the swap tenors on one side of a threshold: QuantLib's GBP swaps take a 1Y fixed leg
/// at most a year long, and its AUD swaps a 6M one at least four years long.
/// </summary>
/// <param name="Comparison">How the swap's tenor compares with the threshold.</param>
/// <param name="SwapTenor">The threshold.</param>
/// <param name="FixedLegTenor">The fixed leg's tenor where the comparison holds.</param>
public sealed record SwapTenorThreshold(SwapTenorComparison Comparison, Period SwapTenor, Period FixedLegTenor)
{
    /// <summary>Whether a swap's tenor is on the threshold's side, by QuantLib's comparison of periods.</summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <returns>Whether the comparison holds.</returns>
    /// <exception cref="NanoQLException">The two periods can't be compared (days with months).</exception>
    public bool Holds(Period tenor) => Comparison switch
    {
        SwapTenorComparison.AtMost => tenor <= SwapTenor,
        SwapTenorComparison.AtLeast => tenor >= SwapTenor,
        _ => throw new ArgumentOutOfRangeException(nameof(Comparison), Comparison, "unknown comparison"),
    };
}
