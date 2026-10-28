// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/latentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>A latent model's integration algorithm over its systemic factors.</summary>
[Experimental("NQL0001")]
public enum LatentModelIntegrationType
{
    /// <summary>A 25-point Gauss-Hermite rule per dimension.</summary>
    GaussianQuadrature,

    /// <summary>A trapezoid rule per dimension on [−35, 35], to 1e-4 in at most 20 refinements: a domain tailored for
    /// Student t factors, too wide for normal ones or t's of high order.</summary>
    Trapezoid,
}
