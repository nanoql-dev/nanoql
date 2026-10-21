// SPDX-FileCopyrightText: Copyright (C) 2009 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2009, 2017 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/schemes/impliciteulerscheme.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// The iterative solver an implicit step uses for a multi-directional operator: QuantLib's
/// <c>ImplicitEulerScheme::SolverType</c>.
/// </summary>
public enum ImplicitEulerSchemeSolverType
{
    /// <summary>BiCGstab.</summary>
    BiCGstab,

    /// <summary>GMRES.</summary>
    GMRES,
}
