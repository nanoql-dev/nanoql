// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/basketgeneratingengine.hpp ql/pricingengines/swaption/basketgeneratingengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>
/// The kind of calibration basket a <see cref="BasketGeneratingEngine"/> generates: QuantLib's
/// <c>BasketGeneratingEngine::CalibrationBasketType</c>.
/// </summary>
public enum BasketGeneratingEngineCalibrationBasketType
{
    /// <summary>Per exercise date, the at-the-money swaption into the underlying's last date.</summary>
    Naive,

    /// <summary>Per exercise date, the standard swaption whose nominal, maturity and strike match the underlying's
    /// value, delta and gamma in the model's state.</summary>
    MaturityStrikeByDeltaGamma,
}
