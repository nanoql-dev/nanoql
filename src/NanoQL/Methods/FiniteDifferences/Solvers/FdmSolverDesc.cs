// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdmsolverdesc.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>
/// The description of a finite-difference problem a solver rolls back: its mesher, boundary conditions, step
/// conditions, inner value, maturity and time steps.
/// </summary>
/// <param name="Mesher">The mesher.</param>
/// <param name="BcSet">The boundary conditions.</param>
/// <param name="Condition">The step conditions.</param>
/// <param name="Calculator">The inner value's calculator, which gives the values at maturity.</param>
/// <param name="Maturity">
/// The maturity, as a time on the solver's clock: for the Black-Scholes solver,
/// <see cref="FdmBlackScholesOp.Clock"/>'s.
/// </param>
/// <param name="TimeSteps">The number of time steps.</param>
/// <param name="DampingSteps">The number of implicit damping steps before them.</param>
/// <remarks>
/// A record of the problem's parts, as QuantLib's struct is; the step conditions and the calculator are working types
/// it holds by reference, so a description serves one rollback.
/// </remarks>
public sealed record FdmSolverDesc(
    FdmMesher Mesher,
    ImmutableArray<BoundaryCondition<FdmLinearOp>> BcSet,
    FdmStepConditionComposite Condition,
    FdmInnerValueCalculator Calculator,
    double Maturity,
    int TimeSteps,
    int DampingSteps);
