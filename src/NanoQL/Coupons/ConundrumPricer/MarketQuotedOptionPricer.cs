// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2023 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/conundrumpricer.hpp ql/cashflows/conundrumpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// A vanilla option on a swap rate priced on a swaption volatility's smile section: Black's formula on a zero-shift
/// lognormal volatility, Bachelier's on a normal one.
/// </summary>
/// <remarks>Immutable; the smile section is the volatility's at the expiry and swap tenor, read at
/// construction.</remarks>
public sealed class MarketQuotedOptionPricer : VanillaOptionPricer
{
    private readonly double forwardValue;
    private readonly SwaptionVolatilityStructure volatilityStructure;
    private readonly SmileSection smile;

    /// <summary>Initializes a new instance of the <see cref="MarketQuotedOptionPricer"/> class.</summary>
    /// <param name="forwardValue">The forward swap rate.</param>
    /// <param name="expiryDate">The option's expiry.</param>
    /// <param name="swapTenor">The underlying swap's tenor.</param>
    /// <param name="volatilityStructure">The swaption volatility.</param>
    /// <exception cref="ArgumentNullException"><paramref name="volatilityStructure"/> is null.</exception>
    /// <exception cref="ArgumentException">The volatility is neither normal nor a zero-shift lognormal one.</exception>
    public MarketQuotedOptionPricer(
        double forwardValue,
        Date expiryDate,
        Period swapTenor,
        SwaptionVolatilityStructure volatilityStructure)
    {
        ArgumentNullException.ThrowIfNull(volatilityStructure);
        this.forwardValue = forwardValue;
        this.volatilityStructure = volatilityStructure;
        smile = volatilityStructure.SmileSection(expiryDate, swapTenor);
        if (!((volatilityStructure.VolatilityType == VolatilityType.Normal)
            || (volatilityStructure.VolatilityType == VolatilityType.ShiftedLognormal
                && ComparisonFunctions.CloseEnough(volatilityStructure.Shift(expiryDate, swapTenor), 0.0))))
        {
            throw new ArgumentException(
                "VanillaOptionPricer: a normal or a zero-shift lognormal volatility is required",
                nameof(volatilityStructure));
        }
    }

    /// <inheritdoc/>
    public override double Evaluate(double strike, OptionType optionType, double deflator)
    {
        double variance = smile.Variance(strike);
        if (volatilityStructure.VolatilityType == VolatilityType.ShiftedLognormal)
        {
            return deflator *
                BlackFormulaFunctions.BlackFormula(optionType, strike, forwardValue, Math.Sqrt(variance));
        }
        else
        {
            return deflator *
                BlackFormulaFunctions.BachelierBlackFormula(optionType, strike, forwardValue, Math.Sqrt(variance));
        }
    }
}
