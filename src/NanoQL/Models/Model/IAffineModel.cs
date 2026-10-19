// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/model.hpp ql/models/model.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>An affine model: the base for analytically tractable models, which engines test for with <c>is</c>.</summary>
/// <remarks>QuantLib's <c>AffineModel</c>, a base its models derive from beside their class base, is an interface.</remarks>
public interface IAffineModel
{
    /// <summary>The implied discount curve: the discount factor at a time.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The discount factor.</returns>
    double Discount(double t);

    /// <summary>The price at a time of a unit discount bond maturing later, given the model's factors then.</summary>
    /// <param name="now">The time.</param>
    /// <param name="maturity">The bond's maturity.</param>
    /// <param name="factors">The factors' values at <paramref name="now"/>.</param>
    /// <returns>The price.</returns>
    double DiscountBond(double now, double maturity, Vector factors);

    /// <summary>The price of an option on a discount bond.</summary>
    /// <param name="type">The option's type.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="maturity">The option's maturity.</param>
    /// <param name="bondMaturity">The bond's maturity.</param>
    /// <returns>The price.</returns>
    double DiscountBondOption(OptionType type, double strike, double maturity, double bondMaturity);

    /// <summary>The price of an option on a discount bond that starts later than the option's maturity.</summary>
    /// <param name="type">The option's type.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="maturity">The option's maturity.</param>
    /// <param name="bondStart">The bond's start.</param>
    /// <param name="bondMaturity">The bond's maturity.</param>
    /// <returns>The price.</returns>
    double DiscountBondOption(OptionType type, double strike, double maturity, double bondStart, double bondMaturity);
}
