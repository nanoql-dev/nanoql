// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2023 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/conundrumpricer.hpp ql/cashflows/conundrumpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>
/// The numeric Hagan pricer's integration limits after an optionlet's price: QuantLib's <c>lowerLimit()</c>,
/// <c>upperLimit()</c> and <c>stdDeviations()</c>, which that price sets in the pricer's mutable members.
/// </summary>
/// <param name="LowerLimit">The lower limit.</param>
/// <param name="UpperLimit">The upper limit.</param>
/// <param name="StdDeviations">The standard deviations of the upper limit, or <see langword="null"/> where no optionlet
/// is integrated, QuantLib's member then unset.</param>
public sealed record NumericHaganPricerIntegrationLimits(double LowerLimit, double UpperLimit, double? StdDeviations);
