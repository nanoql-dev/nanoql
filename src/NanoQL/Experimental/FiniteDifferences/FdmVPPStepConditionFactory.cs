// SPDX-FileCopyrightText: Copyright (C) 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmvppstepconditionfactory.hpp ql/experimental/finitedifferences/fdmvppstepconditionfactory.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;

namespace NanoQL.Experimental;

/// <summary>
/// Builds a <see cref="VanillaVPPOption"/>'s state mesher and step condition: the plain plant and the plant with a start
/// limit, as QuantLib's; a running-hour limit, which QuantLib's factory classifies and refuses to build, is refused.
/// </summary>
[Experimental("NQL0001")]
public sealed class FdmVPPStepConditionFactory
{
    private readonly VanillaVPPOption args;
    private readonly VppType type;

    /// <summary>Initializes a new instance of the <see cref="FdmVPPStepConditionFactory"/> class.</summary>
    /// <param name="args">The plant.</param>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">Both limits are given ("start and running hour limt together is not
    /// supported").</exception>
    public FdmVPPStepConditionFactory(VanillaVPPOption args)
    {
        ArgumentNullException.ThrowIfNull(args);
        this.args = args;
        if (args.NStarts is not null && args.NRunningHours is not null)
        {
            throw new NanoQLException("start and running hour limt together is not supported");
        }

        type = args.NRunningHours is null && args.NStarts is null
            ? VppType.Vanilla
            : args.NRunningHours is null ? VppType.StartLimit : VppType.RunningHourLimit;
    }

    private enum VppType
    {
        Vanilla,
        StartLimit,
        RunningHourLimit,
    }

    /// <summary>The mesher of the plant's states: uniform on [0, 1], one node per state.</summary>
    /// <returns>The mesher.</returns>
    /// <exception cref="NanoQLException">The plant has a running-hour limit ("vpp type is not supported").</exception>
    public Fdm1dMesher StateMesher()
    {
        int nStates = type switch
        {
            VppType.Vanilla => (2 * args.TMinUp) + args.TMinDown,
            VppType.StartLimit => FdmVPPStartLimitStepCondition.NStatesOf(args.TMinUp, args.TMinDown, args.NStarts),
            _ => throw new NanoQLException("vpp type is not supported"),
        };
        return new Uniform1dMesher(0.0, 1.0, nStates);
    }

    /// <summary>The plant's step condition on a mesher.</summary>
    /// <param name="mesh">The mesher and the states' direction.</param>
    /// <param name="fuelCostAddon">The cost added to each unit of fuel.</param>
    /// <param name="fuel">The fuel price.</param>
    /// <param name="spark">The spark spread.</param>
    /// <returns>The step condition.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The plant has a running-hour limit ("vpp type is not supported"), or the
    /// condition refuses the mesher or the plant.</exception>
    public FdmVPPStepCondition Build(
        FdmVPPStepConditionMesher mesh, double fuelCostAddon, FdmInnerValueCalculator fuel, FdmInnerValueCalculator spark)
    {
        var parameters = new FdmVPPStepConditionParams(
            args.HeatRate, args.PMin, args.PMax, args.TMinUp, args.TMinDown, args.StartUpFuel, args.StartUpFixCost, fuelCostAddon);
        return type switch
        {
            VppType.Vanilla or VppType.StartLimit => new FdmVPPStartLimitStepCondition(parameters, args.NStarts, mesh, fuel, spark),
            _ => throw new NanoQLException("vpp type is not supported"),
        };
    }
}
