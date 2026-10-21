// SPDX-FileCopyrightText: Copyright (C) 2005, 2007, 2009, 2014 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/hestonprocess.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Processes;

/// <summary>The schemes <see cref="HestonProcess.Evolve"/> steps by: QuantLib's
/// <c>HestonProcess::Discretization</c>.</summary>
public enum HestonProcessDiscretization
{
    /// <summary>Euler's step, the variance floored at zero in the diffusion and the drift's square root.</summary>
    PartialTruncation,

    /// <summary>Euler's step, the variance floored at zero throughout.</summary>
    FullTruncation,

    /// <summary>Euler's step on the variance's absolute value.</summary>
    Reflection,

    /// <summary>The variance sampled exactly from its non-central chi-squared law.</summary>
    NonCentralChiSquareVariance,

    /// <summary>Andersen's quadratic-exponential scheme.</summary>
    QuadraticExponential,

    /// <summary>Andersen's quadratic-exponential scheme with its martingale correction.</summary>
    QuadraticExponentialMartingale,

    /// <summary>Broadie and Kaya's exact scheme, its integrated variance by Gauss-Lobatto integration.</summary>
    BroadieKayaExactSchemeLobatto,

    /// <summary>Broadie and Kaya's exact scheme, its integrated variance by Gauss-Laguerre integration.</summary>
    BroadieKayaExactSchemeLaguerre,

    /// <summary>Broadie and Kaya's exact scheme, its integrated variance by the trapezoid rule on sine
    /// integrals.</summary>
    BroadieKayaExactSchemeTrapezoidal,
}
