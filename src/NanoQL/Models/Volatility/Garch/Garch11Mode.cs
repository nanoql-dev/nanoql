// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2012 Liquidnet Holdings, Inc.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/volatility/garch.hpp ql/models/volatility/garch.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>The initial guess of a GARCH(1,1) calibration, QuantLib's <c>Garch11::Mode</c>.</summary>
public enum Garch11Mode
{
    /// <summary>The initial guess is a moment matching estimates for mean(r2), acf(0), and acf(1).</summary>
    MomentMatchingGuess,

    /// <summary>The initial guess is an estimate of gamma based on the property: acf(i+1) = gamma*acf(i) for i &gt; 1.</summary>
    GammaGuess,

    /// <summary>The best of the two above modes.</summary>
    BestOfTwo,

    /// <summary>Double optimization.</summary>
    DoubleOptimization,
}
