// SPDX-FileCopyrightText: Copyright (C) 2009 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdmbackwardsolver.hpp ql/methods/finitedifferences/solvers/fdmbackwardsolver.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>
/// The backward solver of a finite-difference problem: rolls values back from maturity under the described scheme,
/// after implicit Euler damping steps, applying the step conditions at every step.
/// </summary>
/// <remarks>
/// A working type. QuantLib builds its schemes and model at each rollback; the solver here builds them at its first
/// rollback and reuses them, a scheme's state being only its step, which each rollback sets, and the buffers it holds,
/// so a later rollback of the same size allocates nothing.
/// </remarks>
[WorkingType]
public sealed class FdmBackwardSolver
{
    private readonly FdmLinearOpComposite map;
    private readonly ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet;
    private readonly FdmStepConditionComposite condition;
    private readonly FdmSchemeDesc schemeDesc;
    private FiniteDifferenceModel<ImplicitEulerScheme>? dampingModel;
    private Action<Span<double>, double, double, int>? model;

    /// <summary>Initializes a new instance of the <see cref="FdmBackwardSolver"/> class.</summary>
    /// <param name="map">The operator.</param>
    /// <param name="bcSet">The boundary conditions; <c>default</c> or empty for none.</param>
    /// <param name="condition">The step conditions, or <see langword="null"/> for none.</param>
    /// <param name="schemeDesc">The scheme.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A boundary condition is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The scheme's type isn't a member of its enum.</exception>
    public FdmBackwardSolver(
        FdmLinearOpComposite map,
        ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet,
        FdmStepConditionComposite? condition,
        FdmSchemeDesc schemeDesc)
    {
        ArgumentNullException.ThrowIfNull(map);
        EnumArguments.Check(schemeDesc.Type, nameof(schemeDesc));
        this.map = map;
        this.bcSet = bcSet.IsDefault ? [] : bcSet;
        if (this.bcSet.Any(c => c is null))
        {
            throw new ArgumentException("null boundary condition in set", nameof(bcSet));
        }

        this.condition = condition ?? new FdmStepConditionComposite([], []);
        this.schemeDesc = schemeDesc;
    }

    /// <summary>
    /// Rolls the values back from <paramref name="from"/> to <paramref name="to"/>, in place: the damping steps by
    /// implicit Euler first, unless the scheme is implicit Euler, which takes them as its own.
    /// </summary>
    /// <param name="rhs">The values.</param>
    /// <param name="from">The later time.</param>
    /// <param name="to">The earlier time.</param>
    /// <param name="steps">The number of time steps.</param>
    /// <param name="dampingSteps">The number of damping steps.</param>
    /// <exception cref="ArgumentOutOfRangeException">A number of steps is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="from"/> is before <paramref name="to"/>.</exception>
    /// <exception cref="NanoQLException">A step fails.</exception>
    public void Rollback(Span<double> rhs, double from, double to, int steps, int dampingSteps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(steps);
        ArgumentOutOfRangeException.ThrowIfNegative(dampingSteps);
        double deltaT = from - to;
        int allSteps = steps + dampingSteps;
        double dampingTo = from - (deltaT * dampingSteps / allSteps);

        if (dampingSteps != 0 && schemeDesc.Type != FdmSchemeDescFdmSchemeType.ImplicitEulerType)
        {
            dampingModel ??= new FiniteDifferenceModel<ImplicitEulerScheme>(
                new ImplicitEulerScheme(map, bcSet), condition.StoppingTimes);
            dampingModel.Rollback(rhs, from, dampingTo, dampingSteps, condition);
        }

        model ??= Model();
        if (schemeDesc.Type == FdmSchemeDescFdmSchemeType.ImplicitEulerType)
        {
            model(rhs, from, to, allSteps);
        }
        else
        {
            model(rhs, dampingTo, to, steps);
        }
    }

    // the scheme's model, its rollback with the step conditions
    private Action<Span<double>, double, double, int> Model()
    {
        ImmutableArray<double> stoppingTimes = condition.StoppingTimes;
        switch (schemeDesc.Type)
        {
            case FdmSchemeDescFdmSchemeType.HundsdorferType:
            {
                var hsModel = new FiniteDifferenceModel<HundsdorferScheme>(
                    new HundsdorferScheme(schemeDesc.Theta, schemeDesc.Mu, map, bcSet), stoppingTimes);
                return (a, from, to, n) => hsModel.Rollback(a, from, to, n, condition);
            }

            case FdmSchemeDescFdmSchemeType.DouglasType:
            {
                var dsModel = new FiniteDifferenceModel<DouglasScheme>(
                    new DouglasScheme(schemeDesc.Theta, map, bcSet), stoppingTimes);
                return (a, from, to, n) => dsModel.Rollback(a, from, to, n, condition);
            }

            case FdmSchemeDescFdmSchemeType.CrankNicolsonType:
            {
                var cnModel = new FiniteDifferenceModel<CrankNicolsonScheme>(
                    new CrankNicolsonScheme(schemeDesc.Theta, map, bcSet), stoppingTimes);
                return (a, from, to, n) => cnModel.Rollback(a, from, to, n, condition);
            }

            case FdmSchemeDescFdmSchemeType.CraigSneydType:
            {
                var csModel = new FiniteDifferenceModel<CraigSneydScheme>(
                    new CraigSneydScheme(schemeDesc.Theta, schemeDesc.Mu, map, bcSet), stoppingTimes);
                return (a, from, to, n) => csModel.Rollback(a, from, to, n, condition);
            }

            case FdmSchemeDescFdmSchemeType.ModifiedCraigSneydType:
            {
                var mcsModel = new FiniteDifferenceModel<ModifiedCraigSneydScheme>(
                    new ModifiedCraigSneydScheme(schemeDesc.Theta, schemeDesc.Mu, map, bcSet), stoppingTimes);
                return (a, from, to, n) => mcsModel.Rollback(a, from, to, n, condition);
            }

            case FdmSchemeDescFdmSchemeType.ImplicitEulerType:
            {
                var implicitModel = new FiniteDifferenceModel<ImplicitEulerScheme>(
                    new ImplicitEulerScheme(map, bcSet), stoppingTimes);
                return (a, from, to, n) => implicitModel.Rollback(a, from, to, n, condition);
            }

            case FdmSchemeDescFdmSchemeType.ExplicitEulerType:
            {
                var explicitModel = new FiniteDifferenceModel<ExplicitEulerScheme>(
                    new ExplicitEulerScheme(map, bcSet), stoppingTimes);
                return (a, from, to, n) => explicitModel.Rollback(a, from, to, n, condition);
            }

            case FdmSchemeDescFdmSchemeType.MethodOfLinesType:
            {
                var molModel = new FiniteDifferenceModel<MethodOfLinesScheme>(
                    new MethodOfLinesScheme(schemeDesc.Theta, schemeDesc.Mu, map, bcSet), stoppingTimes);
                return (a, from, to, n) => molModel.Rollback(a, from, to, n, condition);
            }

            case FdmSchemeDescFdmSchemeType.TrBDF2Type:
            {
                FdmSchemeDesc trDesc = FdmSchemeDesc.CraigSneyd();

                var hsEvolver = new CraigSneydScheme(trDesc.Theta, trDesc.Mu, map, bcSet);

                var trBDF2 = new TrBDF2Scheme<CraigSneydScheme>(schemeDesc.Theta, map, hsEvolver, bcSet, schemeDesc.Mu);

                var trBDF2Model = new FiniteDifferenceModel<TrBDF2Scheme<CraigSneydScheme>>(trBDF2, stoppingTimes);
                return (a, from, to, n) => trBDF2Model.Rollback(a, from, to, n, condition);
            }

            default:
                throw new NanoQLException("Unknown scheme type");
        }
    }
}
