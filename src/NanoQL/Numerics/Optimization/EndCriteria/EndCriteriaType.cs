// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/endcriteria.hpp ql/math/optimization/endcriteria.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>Why an optimization ended: QuantLib's <c>EndCriteria::Type</c>.</summary>
/// <remarks>
/// QuantLib's <c>operator&lt;&lt;</c> writes each member's name, and no member shares another's value, so it isn't
/// ported: <see cref="Enum.ToString()"/> gives QuantLib's text.
/// </remarks>
public enum EndCriteriaType
{
    /// <summary>No criterion has been met.</summary>
    None,

    /// <summary>The maximum number of iterations was reached.</summary>
    MaxIterations,

    /// <summary>The independent variable stopped moving.</summary>
    StationaryPoint,

    /// <summary>The function value stopped changing.</summary>
    StationaryFunctionValue,

    /// <summary>The function value is below the function epsilon.</summary>
    StationaryFunctionAccuracy,

    /// <summary>The gradient's norm is below the gradient-norm epsilon.</summary>
    ZeroGradientNorm,

    /// <summary>The function epsilon is too small: no further reduction is possible.</summary>
    FunctionEpsilonTooSmall,

    /// <summary>Unknown.</summary>
    Unknown,
}
