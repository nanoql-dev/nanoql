// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/inflationcouponpricer.hpp ql/cashflows/inflationcouponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Coupons;

/// <summary>The Black-formula pricer of capped and floored year-on-year inflation coupons.</summary>
public sealed class BlackYoYInflationCouponPricer : YoYInflationCouponPricer
{
    /// <summary>Initializes a new instance of the <see cref="BlackYoYInflationCouponPricer"/> class.</summary>
    /// <param name="nominalTermStructure">The nominal curve the prices discount on, or <see langword="null"/> for
    /// none.</param>
    public BlackYoYInflationCouponPricer(YieldTermStructure? nominalTermStructure = null)
        : base(nominalTermStructure)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BlackYoYInflationCouponPricer"/> class.</summary>
    /// <param name="capletVol">The optionlet volatility, or <see langword="null"/> for none.</param>
    /// <param name="nominalTermStructure">The nominal curve the prices discount on, or <see langword="null"/> for
    /// none.</param>
    public BlackYoYInflationCouponPricer(YoYOptionletVolatilitySurface? capletVol, YieldTermStructure? nominalTermStructure)
        : base(capletVol, nominalTermStructure)
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Black's formula on the strike and the forward, unshifted, as QuantLib's. Where the formula refuses the strike,
    /// at or below 0, a caplet is the forward less the strike and a floorlet 0, the formula's limits there, where
    /// QuantLib refuses ("strike + displacement (… + 0) must be non-negative"); at 0 itself that is QuantLib's value.
    /// On a forward at or below 0 the model has no rate distribution, and the formula's refusal stands.
    /// </remarks>
    protected override double OptionletPriceImp(OptionType optionType, double strike, double forward, double stdDev)
    {
        if (strike <= 0.0 && forward > 0.0)
        {
            return optionType == OptionType.Call ? forward - strike : 0.0;
        }

        return BlackFormula(optionType, strike, forward, stdDev);
    }
}
