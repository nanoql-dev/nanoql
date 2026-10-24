// SPDX-FileCopyrightText: Copyright (C) 2021 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/zerocouponswap.hpp ql/instruments/zerocouponswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The results an engine returns for a <see cref="ZeroCouponSwap"/>: the swap's, with the fixed payment that gives it
/// a zero NPV.
/// </summary>
/// <remarks>
/// QuantLib's swap derives the fair payment from its results on each call (<c>fairFixedPayment</c>); here it is a
/// member of the results, which <see cref="PricingEngines.DiscountingSwapEngine"/>'s <see cref="ZeroCouponSwap"/>
/// overload gives (the S-cms plan's CMD7), <see langword="null"/>, QuantLib's "result not available", from another
/// engine. QuantLib's <c>fairFixedRate</c>, which takes a day counter, is <see cref="ZeroCouponSwap.FairFixedRate"/>.
/// Equality compares the swap's results and the fair payment.
/// </remarks>
public sealed record ZeroCouponSwapResults : SwapResults
{
    /// <summary>Initializes a new instance of the <see cref="ZeroCouponSwapResults"/> class.</summary>
    public ZeroCouponSwapResults()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ZeroCouponSwapResults"/> class with a swap's results, and
    /// no fair payment.</summary>
    /// <param name="results">The swap's results.</param>
    internal ZeroCouponSwapResults(SwapResults results)
        : base(results)
    {
    }

    /// <summary>Gets the fixed payment that gives the swap a zero NPV.</summary>
    public double? FairFixedPayment { get; init; }

    /// <summary>Gets the fixed leg's NPV, signed by whether it is paid.</summary>
    public double? FixedLegNPV => !LegNPV.IsDefault && LegNPV.Length > 0 ? LegNPV[0] : null;

    /// <summary>Gets the floating leg's NPV, signed by whether it is paid.</summary>
    public double? FloatingLegNPV => !LegNPV.IsDefault && LegNPV.Length > 1 ? LegNPV[1] : null;

    /// <inheritdoc/>
    public bool Equals(ZeroCouponSwapResults? other) =>
        other is not null
        && base.Equals(other)
        && EqualityComparer<double?>.Default.Equals(FairFixedPayment, other.FairFixedPayment);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), FairFixedPayment);
}
