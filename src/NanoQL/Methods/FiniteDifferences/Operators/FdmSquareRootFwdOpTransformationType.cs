// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2014 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmsquarerootfwdop.hpp ql/methods/finitedifferences/operators/fdmsquarerootfwdop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>The variable a <see cref="FdmSquareRootFwdOp"/>'s density is written in.</summary>
public enum FdmSquareRootFwdOpTransformationType
{
    /// <summary>The density of the variance itself.</summary>
    Plain,

    /// <summary>The density scaled by a power of the variance, v^(1 − 2κθ/σ²).</summary>
    Power,

    /// <summary>The density of the variance's logarithm.</summary>
    Log,
}
