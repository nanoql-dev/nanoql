// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2023 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/conundrumpricer.hpp ql/cashflows/conundrumpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>The yield-curve models of Hagan's g-functions: QuantLib's
/// <c>GFunctionFactory::YieldCurveModel</c>.</summary>
public enum GFunctionFactoryYieldCurveModel
{
    /// <summary>The standard model: equal periods, a flat yield.</summary>
    Standard,

    /// <summary>The exact-yield model: the fixed leg's own accrual periods.</summary>
    ExactYield,

    /// <summary>Parallel shifts of the forwarding curve.</summary>
    ParallelShifts,

    /// <summary>Non-parallel shifts of the forwarding curve, shaped by a mean reversion.</summary>
    NonParallelShifts,
}
