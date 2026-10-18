// SPDX-FileCopyrightText: Copyright (C) 2019 SoftSolutions! S.r.l.
// SPDX-FileCopyrightText: Copyright (C) 2025 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/globalbootstrap.hpp ql/termstructures/globalbootstrap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.TermStructures;

// A bootstrap that can join a multicurve's joint bootstrap: QuantLib's GlobalBootstrap, which derives from
// MultiCurveBootstrapContributor. Its contributor works on the curve's state as its own bootstrap would, and the curve
// is told through setToValid when the joint optimization has succeeded.
internal interface IMultiCurveBootstrap
{
    MultiCurveBootstrapContributor Contributor<TTermStructure, TTraits, TInterpolator>(
        BootstrapState<TTermStructure> state, TInterpolator interpolator, EvaluationContext context, Action setToValid)
        where TTermStructure : TermStructure
        where TTraits : struct, IBootstrapTraits<TTermStructure>
        where TInterpolator : struct, IInterpolator;
}
