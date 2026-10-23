// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/basketgeneratingengine.hpp ql/pricingengines/swaption/basketgeneratingengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The underlying a <see cref="BasketGeneratingEngine"/> matches its basket to: QuantLib's protected virtual
/// <c>underlyingNpv</c>, <c>underlyingType</c>, <c>underlyingLastDate</c> and <c>initialGuess</c>, which read the
/// derived engine's arguments, here bound to an instrument's.
/// </summary>
internal interface IBasketGeneratingUnderlying
{
    /// <summary>Gets the underlying's type.</summary>
    SwapType Type { get; }

    /// <summary>Gets the underlying's last payment date.</summary>
    Date LastDate { get; }

    /// <summary>The value of the underlying's cash flows exercised into at an expiry, in the model's state
    /// there.</summary>
    /// <param name="expiry">The expiry.</param>
    /// <param name="y">The model's state.</param>
    /// <returns>The value, the type's sign applied.</returns>
    double Npv(Date expiry, double y);

    /// <summary>The match's starting point at an expiry.</summary>
    /// <param name="expiry">The expiry.</param>
    /// <returns>The nominal, the maturity and the rate.</returns>
    Vector InitialGuess(Date expiry);
}
