// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Yiping Chen
// SPDX-FileCopyrightText: Copyright (C) 2007 Neil Firth
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/pseudosqrt.hpp ql/math/matrixutilities/pseudosqrt.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The algorithm used for a matrix's pseudo square root where the matrix isn't positive semi-definite: QuantLib's
/// <c>SalvagingAlgorithm::Type</c>, with its numeric values.
/// </summary>
public enum SalvagingAlgorithmType
{
    /// <summary>No salvaging: the matrix must be positive semi-definite.</summary>
    None = 0,

    /// <summary>Spectral: negative eigenvalues set to zero.</summary>
    Spectral = 1,

    /// <summary>Hypersphere: the spectral root, optimized on the hypersphere where an eigenvalue is negative.</summary>
    Hypersphere = 2,

    /// <summary>Lower diagonal: the spectral root, optimized as a lower-diagonal root where an eigenvalue is negative.</summary>
    LowerDiagonal = 3,

    /// <summary>Higham's nearest correlation matrix; only for correlation matrices.</summary>
    Higham = 4,

    /// <summary>The principal (symmetric) square root.</summary>
    Principal = 5,
}
