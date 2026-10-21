// SPDX-FileCopyrightText: Copyright (C) 2009 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdmbackwardsolver.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>The type of a finite-difference scheme: QuantLib's <c>FdmSchemeDesc::FdmSchemeType</c>.</summary>
public enum FdmSchemeDescFdmSchemeType
{
    /// <summary>The Hundsdorfer-Verwer scheme.</summary>
    HundsdorferType,

    /// <summary>The Douglas scheme.</summary>
    DouglasType,

    /// <summary>The Craig-Sneyd scheme.</summary>
    CraigSneydType,

    /// <summary>The modified Craig-Sneyd scheme.</summary>
    ModifiedCraigSneydType,

    /// <summary>The implicit Euler scheme.</summary>
    ImplicitEulerType,

    /// <summary>The explicit Euler scheme.</summary>
    ExplicitEulerType,

    /// <summary>The method of lines.</summary>
    MethodOfLinesType,

    /// <summary>The TR-BDF2 scheme.</summary>
    TrBDF2Type,

    /// <summary>The Crank-Nicolson scheme.</summary>
    CrankNicolsonType,
}
