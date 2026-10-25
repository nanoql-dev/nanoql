// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/hybridhestonhullwhiteprocess.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Processes;

/// <summary>The schemes <see cref="HybridHestonHullWhiteProcess"/> steps by: QuantLib's
/// <c>HybridHestonHullWhiteProcess::Discretization</c>.</summary>
public enum HybridHestonHullWhiteProcessDiscretization
{
    /// <summary>Euler's step for the underlying, the short rate's Brownian motion correlated at the given correlation.</summary>
    Euler,

    /// <summary>The underlying's log-normal step over the step's exact Black-Scholes-Merton Hull-White variance, the
    /// short rate's Brownian motion correlated at the terminal correlation.</summary>
    BSMHullWhite,
}
