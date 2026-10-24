// SPDX-FileCopyrightText: Copyright (C) 2014, 2016 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/lineartsrpricer.hpp ql/cashflows/lineartsrpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>How the linear TSR pricer bounds its integration: QuantLib's
/// <c>LinearTsrPricer::Settings::Strategy</c>.</summary>
public enum LinearTsrPricerSettingsStrategy
{
    /// <summary>The explicit lower and upper rate bounds.</summary>
    RateBound,

    /// <summary>The strike where a vanilla swaption's vega is a given ratio of the at-the-money one's.</summary>
    VegaRatio,

    /// <summary>The strike where the undeflated payer or receiver price falls below a threshold.</summary>
    PriceThreshold,

    /// <summary>A number of standard deviations of a Black-Scholes process at the at-the-money volatility.</summary>
    BSStdDevs,
}
