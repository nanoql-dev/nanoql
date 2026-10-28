// SPDX-FileCopyrightText: Copyright (C) 2007 Gang Liang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/histogram.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The algorithms a <see cref="Histogram"/> takes its number of bins by, QuantLib's
/// <c>Histogram::Algorithm</c>.</summary>
public enum HistogramAlgorithm
{
    /// <summary>None: the bins or breaks are given.</summary>
    None,

    /// <summary>Sturges' rule, ⌈log₂ n + 1⌉ bins.</summary>
    Sturges,

    /// <summary>The Freedman-Diaconis rule, a bin width of 2·IQR·n^(−1/3).</summary>
    FD,

    /// <summary>Scott's rule, a bin width of 3.5·σ·n^(−1/3).</summary>
    Scott,
}
