// SPDX-FileCopyrightText: Copyright (C) 2006 Banca Profilo S.p.A.
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/capfloor/mchullwhiteengine.hpp ql/pricingengines/capfloor/mchullwhiteengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// <see cref="MCHullWhiteCapFloorEngine"/> on low-discrepancy draws with QuantLib's default statistics:
/// <c>MCLDHullWhiteCapFloorEngine</c>, named as QuantLib-Python names its siblings, QuantLib's
/// <c>MCHullWhiteCapFloorEngine&lt;LowDiscrepancy&gt;</c>, its calculations closed at <see cref="LowDiscrepancy"/> and
/// <see cref="GeneralStatistics"/>.
/// </summary>
public static class MCLDHullWhiteCapFloorEngine
{
    /// <summary>Prices a cap, floor or collar by Monte Carlo on a Hull-White model.</summary>
    /// <param name="capFloor">The cap, floor or collar.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Hull-White model.</param>
    /// <param name="brownianBridge">Whether the paths are drawn through a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>, QuantLib's null.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence over the
    /// number of samples.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The instrument's results, the NPV and, where the traits allow one, the error estimate: the expired
    /// results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The cap, the context or the model is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="requiredSamples"/> or <paramref name="maxSamples"/> is negative, or <paramref name="seed"/> is
    /// outside its range.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The cap's terms are inconsistent, a cash flow isn't a floating-rate coupon, a forward can't be computed, neither
    /// a tolerance nor a number of samples is given, or the samples can't reach the tolerance.
    /// </exception>
    public static InstrumentResults Calculate(
        CapFloor capFloor,
        EvaluationContext context,
        HullWhite model,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed) =>
        MCHullWhiteCapFloorEngine.Calculate<LowDiscrepancy, GeneralStatistics>(capFloor, context, model, brownianBridge, antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed);
}
