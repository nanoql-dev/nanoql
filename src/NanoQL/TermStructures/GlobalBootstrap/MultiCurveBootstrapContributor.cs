// SPDX-FileCopyrightText: Copyright (C) 2019 SoftSolutions! S.r.l.
// SPDX-FileCopyrightText: Copyright (C) 2025 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/globalbootstrap.hpp ql/termstructures/globalbootstrap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.TermStructures;

// QuantLib's MultiCurveBootstrapContributor: one curve's part of a joint bootstrap, which sets up its cost function and
// gives its guess, takes its share of the optimizer's argument, gives its errors, and is told when the joint
// optimization has succeeded. QuantLib's setParentBootstrapper has no counterpart: a member curve hands its contributor
// to the multicurve's bootstrap when the multicurve builds it.
internal abstract class MultiCurveBootstrapContributor
{
    // Sets up the curve's nodes, binds its helpers and returns the guess for its share of the argument.
    public abstract Vector SetupCostFunction();

    // Writes the curve's share of the optimizer's argument into its nodes.
    public abstract void SetCostFunctionArgument(ReadOnlySpan<double> x);

    // The curve's errors: its helpers' weighted quote errors, then its additional penalties.
    public abstract Vector EvaluateCostFunction();

    // The joint optimization has succeeded: the curve's nodes are its result.
    public abstract void SetToValid();
}
