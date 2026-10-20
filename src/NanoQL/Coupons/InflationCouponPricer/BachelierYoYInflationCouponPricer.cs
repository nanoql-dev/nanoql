// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/inflationcouponpricer.hpp ql/cashflows/inflationcouponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Coupons;

/// <summary>The Bachelier-formula pricer of capped and floored year-on-year inflation coupons.</summary>
public sealed class BachelierYoYInflationCouponPricer : YoYInflationCouponPricer
{
    /// <summary>Initializes a new instance of the <see cref="BachelierYoYInflationCouponPricer"/> class.</summary>
    /// <param name="nominalTermStructure">The nominal curve the prices discount on, or <see langword="null"/> for
    /// none.</param>
    public BachelierYoYInflationCouponPricer(YieldTermStructure? nominalTermStructure = null)
        : base(nominalTermStructure)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BachelierYoYInflationCouponPricer"/> class.</summary>
    /// <param name="capletVol">The optionlet volatility, or <see langword="null"/> for none.</param>
    /// <param name="nominalTermStructure">The nominal curve the prices discount on, or <see langword="null"/> for
    /// none.</param>
    public BachelierYoYInflationCouponPricer(YoYOptionletVolatilitySurface? capletVol, YieldTermStructure? nominalTermStructure)
        : base(capletVol, nominalTermStructure)
    {
    }

    /// <inheritdoc/>
    /// <remarks>Bachelier's formula on the strike and the forward.</remarks>
    protected override double OptionletPriceImp(OptionType optionType, double strike, double forward, double stdDev) =>
        BachelierBlackFormula(optionType, strike, forward, stdDev);
}
