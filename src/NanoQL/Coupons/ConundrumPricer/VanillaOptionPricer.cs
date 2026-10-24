// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2023 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/conundrumpricer.hpp ql/cashflows/conundrumpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>The price of a vanilla option on a swap rate, deflated: the replication's building block in Hagan's
/// pricers.</summary>
/// <remarks>Unsealed, as in QuantLib, where <see cref="MarketQuotedOptionPricer"/> derives from it.</remarks>
public abstract class VanillaOptionPricer
{
    /// <summary>The option's price: QuantLib's <c>operator()</c>.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="optionType">The option's type.</param>
    /// <param name="deflator">The deflator, the annuity of the swap.</param>
    /// <returns>The price.</returns>
    public abstract double Evaluate(double strike, OptionType optionType, double deflator);
}
