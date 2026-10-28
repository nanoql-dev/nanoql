// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/numericaldifferentiation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>The placement of a numerical derivative's equally spaced points around x.</summary>
public enum NumericalDifferentiationScheme
{
    /// <summary>Points on both sides, an odd number centred on x.</summary>
    Central,

    /// <summary>Points at x and below.</summary>
    Backward,

    /// <summary>Points at x and above.</summary>
    Forward,
}
