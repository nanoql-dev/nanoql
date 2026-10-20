// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/zabrsmilesection.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// QuantLib's short-maturity lognormal kernel for <see cref="ZabrSmileSection{TEvaluation}"/>: the volatility is the
/// model's lognormal volatility at the strike, floored at 1e-6, and the option prices are <see cref="SmileSection"/>'s,
/// Black's on that volatility.
/// </summary>
public readonly struct ZabrShortMaturityLognormal : IZabrEvaluation
{
    /// <inheritdoc/>
    static double IZabrEvaluation.OptionPrice<TEvaluation>(
        ZabrSmileSection<TEvaluation> section, double strike, OptionType type, double discount) =>
        section.SmileSectionOptionPrice(strike, type, discount);

    /// <inheritdoc/>
    static double IZabrEvaluation.VolatilityImpl<TEvaluation>(ZabrSmileSection<TEvaluation> section, double strike)
    {
        // std::max(1E-6, strike), which floors a NaN strike too
        strike = 1E-6 < strike ? strike : 1E-6;
        return section.Model.LognormalVolatility(strike);
    }
}
