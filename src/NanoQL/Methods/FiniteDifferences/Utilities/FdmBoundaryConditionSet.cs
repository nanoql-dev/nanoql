// SPDX-FileCopyrightText: Copyright (C) 2012 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdmboundaryconditionset.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

// QuantLib's FdmBoundaryConditionSet, the boundary conditions of a finite-difference operator, as a global alias so
// ported code reads as QuantLib's does. Public signatures write the type out.
global using FdmBoundaryConditionSet = System.Collections.Immutable.ImmutableArray<
    NanoQL.Methods.BoundaryCondition<NanoQL.Methods.FdmLinearOp>>;
