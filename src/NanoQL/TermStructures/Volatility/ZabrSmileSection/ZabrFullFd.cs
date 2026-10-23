// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/zabrsmilesection.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// QuantLib's full finite-difference kernel for <see cref="ZabrSmileSection{TEvaluation}"/>: calls priced at the
/// section's strike grid, each by finite differences on the model's two-dimensional equation
/// (<see cref="ZabrModel.FullFdPrice"/>), interpolated by a spline, and the volatility implied from those prices.
/// </summary>
public readonly struct ZabrFullFd : IZabrEvaluation
{
    /// <inheritdoc/>
    static double IZabrEvaluation.OptionPrice<TEvaluation>(
        ZabrSmileSection<TEvaluation> section, double strike, OptionType type, double discount) =>
        section.FdOptionPrice(strike, type, discount);

    /// <inheritdoc/>
    static double IZabrEvaluation.VolatilityImpl<TEvaluation>(ZabrSmileSection<TEvaluation> section, double strike) =>
        ZabrShortMaturityNormal.ImpliedVolatility(section, strike);

    /// <inheritdoc/>
    static double[]? IZabrEvaluation.CallPrices(ZabrModel model, ReadOnlySpan<double> strikes)
    {
        double[] callPrices = new double[strikes.Length];
        for (int i = 0; i < strikes.Length; i++)
        {
            callPrices[i] = model.FullFdPrice(strikes[i]);
        }

        return callPrices;
    }
}
