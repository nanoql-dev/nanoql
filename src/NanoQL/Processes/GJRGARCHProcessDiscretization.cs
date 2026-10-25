// SPDX-FileCopyrightText: Copyright (C) 2008 Yee Man Chan
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/gjrgarchprocess.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Processes;

/// <summary>The schemes <see cref="GJRGARCHProcess"/> steps by: QuantLib's <c>GJRGARCHProcess::Discretization</c>.</summary>
public enum GJRGARCHProcessDiscretization
{
    /// <summary>Euler's step, the variance floored at zero in the diffusion and the drift's square root.</summary>
    PartialTruncation,

    /// <summary>Euler's step, the variance floored at zero throughout.</summary>
    FullTruncation,

    /// <summary>Euler's step on the variance's absolute value.</summary>
    Reflection,
}
