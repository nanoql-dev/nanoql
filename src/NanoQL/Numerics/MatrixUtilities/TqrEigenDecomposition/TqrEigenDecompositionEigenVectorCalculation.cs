// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/tqreigendecomposition.hpp ql/math/matrixutilities/tqreigendecomposition.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>Which eigenvectors <see cref="TqrEigenDecomposition"/> computes.</summary>
public enum TqrEigenDecompositionEigenVectorCalculation
{
    /// <summary>Every eigenvector.</summary>
    WithEigenVector = 0,

    /// <summary>No eigenvector.</summary>
    WithoutEigenVector = 1,

    /// <summary>The first component of each eigenvector, all a Gaussian quadrature's weights need.</summary>
    OnlyFirstRowEigenVector = 2,
}
