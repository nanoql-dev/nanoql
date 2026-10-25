// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Processes;

namespace NanoQL.Tests.Vectors;

/// <summary>The harness's hybrid process spec, <c>tools/cxx/src/processes/hybridspec.hpp</c>'s, as nanoql builds
/// it.</summary>
internal static class HybridHestonHullWhiteCxxInputs
{
    /// <summary>
    /// The process a spec describes: the hybrid over <see cref="HestonCxxInputs.ProcessOf"/>'s Heston process and a
    /// forward Hull-White process on its risk-free curve with the spec's reversion, volatility and forward-measure time,
    /// at its correlation and discretization.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The process.</returns>
    public static HybridHestonHullWhiteProcess ProcessOf(JsonElement spec)
    {
        HestonProcess heston = HestonCxxInputs.ProcessOf(spec.GetProperty("heston"));
        HullWhiteForwardProcess hullWhite = new HullWhiteForwardProcess(
                heston.RiskFreeRate, spec.GetProperty("a").GetDouble(), spec.GetProperty("sigma").GetDouble())
            .WithForwardMeasureTime(spec.GetProperty("forwardMeasureTime").GetDouble());
        return new HybridHestonHullWhiteProcess(
            heston,
            hullWhite,
            spec.GetProperty("correlation").GetDouble(),
            (HybridHestonHullWhiteProcessDiscretization)spec.GetProperty("discretization").GetInt32());
    }
}
