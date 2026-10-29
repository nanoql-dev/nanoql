// SPDX-FileCopyrightText: Copyright (C) 2008 Frank Hövermann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/processes/extendedblackscholesprocess.hpp ql/experimental/processes/extendedblackscholesprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// The extended Black-Scholes-Merton process's built-in schemes: QuantLib's
/// <c>ExtendedBlackScholesMertonProcess::Discretization</c>.
/// </summary>
[Experimental("NQL0001")]
public enum ExtendedBlackScholesMertonProcessDiscretization
{
    /// <summary>The log-Euler step.</summary>
    Euler,

    /// <summary>Milstein's scheme in the log price, QuantLib's default.</summary>
    Milstein,

    /// <summary>A predictor-corrector with equal weights.</summary>
    PredictorCorrector,
}
