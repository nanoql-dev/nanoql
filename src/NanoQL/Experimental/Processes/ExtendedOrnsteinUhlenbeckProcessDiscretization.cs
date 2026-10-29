// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/processes/extendedornsteinuhlenbeckprocess.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>How an <see cref="ExtendedOrnsteinUhlenbeckProcess"/> integrates its time-dependent level over a step.</summary>
[Experimental("NQL0001")]
public enum ExtendedOrnsteinUhlenbeckProcessDiscretization
{
    /// <summary>The level at the step's mid-point.</summary>
    MidPoint = 0,

    /// <summary>The trapezoidal rule on the level at the step's ends, QuantLib's spelling (XED8).</summary>
    Trapezodial = 1,

    /// <summary>The adaptive Gauss-Lobatto rule on the level over the step.</summary>
    GaussLobatto = 2,
}
