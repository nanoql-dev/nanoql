// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/qrdecomposition.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>
/// A QR decomposition with column pivoting, A·P = Q·R: what QuantLib's <c>qrDecomposition</c> writes into the caller's
/// matrices and returns.
/// </summary>
/// <param name="Q">The m-by-n matrix Q, its columns orthonormal (as far as A's rank allows).</param>
/// <param name="R">The n-by-n upper triangular matrix R, its diagonal of nonincreasing magnitude when pivoting.</param>
/// <param name="Ipvt">The permutation: column j of P is column <c>Ipvt[j]</c> of the identity.</param>
public sealed record QRDecompositionResult(Matrix Q, Matrix R, ImmutableArray<int> Ipvt);
