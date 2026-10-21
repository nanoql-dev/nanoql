// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/schemes/trbdf2scheme.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// The iterative solver TR-BDF2's BDF2 stage uses for a multi-directional operator: QuantLib's
/// <c>TrBDF2Scheme::SolverType</c>.
/// </summary>
public enum TrBDF2SchemeSolverType
{
    /// <summary>BiCGstab.</summary>
    BiCGstab,

    /// <summary>GMRES.</summary>
    GMRES,
}
