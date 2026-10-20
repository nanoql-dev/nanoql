// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/zabrsmilesection.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.TermStructures;

/// <summary>
/// QuantLib's short-maturity normal kernel for <see cref="ZabrSmileSection{TEvaluation}"/>: the option prices are
/// Bachelier's on the model's normal volatility at the strike, and the volatility is the lognormal one implied from the
/// out-of-the-money price, 0 where it can't be implied.
/// </summary>
public readonly struct ZabrShortMaturityNormal : IZabrEvaluation
{
    /// <inheritdoc/>
    static double IZabrEvaluation.OptionPrice<TEvaluation>(
        ZabrSmileSection<TEvaluation> section, double strike, OptionType type, double discount) =>
        BachelierBlackFormula(
            type, strike, section.Model.Forward, section.Model.NormalVolatility(strike) * Math.Sqrt(section.ExerciseTime), discount);

    /// <inheritdoc/>
    static double IZabrEvaluation.VolatilityImpl<TEvaluation>(ZabrSmileSection<TEvaluation> section, double strike)
    {
        double impliedVol = 0.0;
        try
        {
            OptionType type;
            if (strike >= section.Model.Forward)
            {
                type = OptionType.Call;
            }
            else
            {
                type = OptionType.Put;
            }

            impliedVol = BlackFormulaImpliedStdDev(type, strike, section.Model.Forward, section.OptionPrice(strike, type, 1.0), 1.0)
                / Math.Sqrt(section.ExerciseTime);
        }
        catch (Exception e) when (e is ArgumentException or NanoQLException)
        {
            // QuantLib's catch (...): a price the Black formula can't invert leaves the volatility at 0.
        }

        return impliedVol;
    }
}
