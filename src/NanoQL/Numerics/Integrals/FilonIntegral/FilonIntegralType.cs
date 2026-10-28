// SPDX-FileCopyrightText: Copyright (C) 2014 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/filonintegral.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The kind of Filon integral: of f(x)·sin(tx) or of f(x)·cos(tx).</summary>
public enum FilonIntegralType
{
    /// <summary>The integral of f(x)·sin(tx).</summary>
    Sine,

    /// <summary>The integral of f(x)·cos(tx).</summary>
    Cosine,
}
