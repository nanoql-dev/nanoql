// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/zabrsmilesection.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// QuantLib's local-volatility kernel for <see cref="ZabrSmileSection{TEvaluation}"/>: calls priced at the section's
/// strike grid by finite differences on the Dupire equation under the model's local volatility
/// (<see cref="ZabrModel.FdPrice(ReadOnlySpan{double})"/>), interpolated by a spline, and the volatility implied from
/// those prices.
/// </summary>
public readonly struct ZabrLocalVolatility : IZabrEvaluation
{
    /// <inheritdoc/>
    static double IZabrEvaluation.OptionPrice<TEvaluation>(
        ZabrSmileSection<TEvaluation> section, double strike, OptionType type, double discount) =>
        section.FdOptionPrice(strike, type, discount);

    /// <inheritdoc/>
    static double IZabrEvaluation.VolatilityImpl<TEvaluation>(ZabrSmileSection<TEvaluation> section, double strike) =>
        ZabrShortMaturityNormal.ImpliedVolatility(section, strike);

    /// <inheritdoc/>
    static double[]? IZabrEvaluation.CallPrices(ZabrModel model, ReadOnlySpan<double> strikes) => [.. model.FdPrice(strikes)];
}
