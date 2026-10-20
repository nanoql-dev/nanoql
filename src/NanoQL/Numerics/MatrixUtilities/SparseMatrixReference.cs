// SPDX-FileCopyrightText: Copyright (C) 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/sparsematrix.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

// QuantLib's SparseMatrixReference, uBLAS's matrix_reference<SparseMatrix>, a reference to a sparse matrix through
// which it is read and written: a SparseMatrix is a reference type, so the alias names the matrix itself, and ported
// code reads as QuantLib's does. Public signatures write the type out.
global using SparseMatrixReference = NanoQL.Numerics.SparseMatrix;
