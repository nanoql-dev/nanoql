// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/gsrprocesscore.hpp ql/processes/gsrprocesscore.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Processes;

/// <summary>
/// The integrals one <see cref="GsrProcessCore"/> has computed during one call: QuantLib's six cache maps, keyed as
/// QuantLib keys them.
/// </summary>
/// <remarks>
/// A working type (the S-g1d plan's G1R1 (iii)): an engine call or a public member builds one and passes it through the
/// core's members, so a price computes each term once, as QuantLib's first price does, and nothing outlives the call.
/// QuantLib's maps live in the core and are flushed on every parameter or time change; here a parameter change builds
/// a new core, and no map outlives the call that filled it.
/// </remarks>
internal sealed class GsrProcessCoreMemo
{
    /// <summary>Gets QuantLib's <c>cache1_</c>: the state's decay A(w, t), keyed by (w, t).</summary>
    public Dictionary<(double W, double T), double> X0Dependent { get; } = [];

    /// <summary>Gets QuantLib's <c>cache2a_</c>: the risk-neutral part of the expectation, keyed by (w, t).</summary>
    public Dictionary<(double W, double T), double> RiskNeutral { get; } = [];

    /// <summary>Gets QuantLib's <c>cache2b_</c>: the expectation's forward-measure part, keyed by (w, t).</summary>
    public Dictionary<(double W, double T), double> ForwardMeasure { get; } = [];

    /// <summary>Gets QuantLib's <c>cache3_</c>: the conditional variance, keyed by (w, t).</summary>
    public Dictionary<(double W, double T), double> Variance { get; } = [];

    /// <summary>Gets QuantLib's <c>cache4_</c>: y(t), keyed by t.</summary>
    public Dictionary<double, double> Y { get; } = [];

    /// <summary>Gets QuantLib's <c>cache5_</c>: G(t, w), keyed by (w, t) as QuantLib keys it.</summary>
    public Dictionary<(double W, double T), double> G { get; } = [];
}
