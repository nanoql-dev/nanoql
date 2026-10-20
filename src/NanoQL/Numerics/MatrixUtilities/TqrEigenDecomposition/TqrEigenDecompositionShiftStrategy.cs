// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/tqreigendecomposition.hpp ql/math/matrixutilities/tqreigendecomposition.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The shift <see cref="TqrEigenDecomposition"/>'s QR steps take.</summary>
public enum TqrEigenDecompositionShiftStrategy
{
    /// <summary>No shift.</summary>
    NoShift = 0,

    /// <summary>The 2×2 block's closer eigenvalue, times 1.25 at the last row.</summary>
    Overrelaxation = 1,

    /// <summary>The 2×2 block's closer eigenvalue (Wilkinson's shift).</summary>
    CloseEigenValue = 2,
}
