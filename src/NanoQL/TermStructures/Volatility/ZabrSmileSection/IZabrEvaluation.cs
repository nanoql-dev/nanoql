// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/zabrsmilesection.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// QuantLib's ZABR evaluation tags: the kernel a <see cref="ZabrSmileSection{TEvaluation}"/> prices with. Its static
/// members answer the section's members that QuantLib dispatches on the tag.
/// </summary>
/// <remarks>
/// QuantLib's tags are empty structs that select the section's private overloads. Here each tag carries its kernel, in
/// members internal to nanoql, so the tags are QuantLib's: <see cref="ZabrShortMaturityLognormal"/> and
/// <see cref="ZabrShortMaturityNormal"/>; the local-volatility and full finite-difference tags come with their kernels.
/// </remarks>
public interface IZabrEvaluation
{
    /// <summary>The kernel's price of an option on the section's forward.</summary>
    /// <typeparam name="TEvaluation">The section's kernel.</typeparam>
    /// <param name="section">The section.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="type">Call or put.</param>
    /// <param name="discount">The discount factor.</param>
    /// <returns>The price.</returns>
    internal static abstract double OptionPrice<TEvaluation>(
        ZabrSmileSection<TEvaluation> section, double strike, OptionType type, double discount)
        where TEvaluation : struct, IZabrEvaluation;

    /// <summary>The kernel's volatility at a strike.</summary>
    /// <typeparam name="TEvaluation">The section's kernel.</typeparam>
    /// <param name="section">The section.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    internal static abstract double VolatilityImpl<TEvaluation>(ZabrSmileSection<TEvaluation> section, double strike)
        where TEvaluation : struct, IZabrEvaluation;
}
