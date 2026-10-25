// SPDX-FileCopyrightText: Copyright (C) 2014 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdmhestongreensfct.hpp ql/methods/finitedifferences/utilities/fdmhestongreensfct.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>How an <see cref="FdmHestonGreensFct"/> approximates the Heston density at a short time.</summary>
public enum FdmHestonGreensFctAlgorithm
{
    /// <summary>The log spot's and the variance's densities as if they were independent: a normal and the square-root
    /// process's exact density.</summary>
    ZeroCorrelation,

    /// <summary>A correlated bivariate normal.</summary>
    Gaussian,

    /// <summary>The Heston process's semi-analytical density.</summary>
    SemiAnalytical,
}
