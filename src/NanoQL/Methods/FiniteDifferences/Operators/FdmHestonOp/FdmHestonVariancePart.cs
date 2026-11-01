// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2014, 2015 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmhestonop.hpp ql/methods/finitedifferences/operators/fdmhestonop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using Vector = NanoQL.Numerics.Vector;

namespace NanoQL.Methods;

/// <summary>
/// The Heston operator's variance part, in direction 1 of a mesher of the log spot by the variance: the diffusion
/// ½σ_m²v and the drift κ(θ − v), discounting at half the rate.
/// </summary>
/// <remarks>
/// A working type (the S-hes plan's HD8): its generator is built at construction, and <see cref="SetTime"/> rewrites
/// the part's operator in place. The risk-free curve is read on the engine's clock (<see cref="FdmHestonOp.Clock"/>),
/// QuantLib's read where it is the risk-free curve itself (HR7 (ii)).
/// </remarks>
[WorkingType]
internal sealed class FdmHestonVariancePart
{
    private readonly TripleBandLinearOp dyMap;
    private readonly TripleBandLinearOp mapT;

    private readonly TermStructure clock;
    private readonly YieldTermStructure rTS;

    internal FdmHestonVariancePart(
        FdmMesher mesher, YieldTermStructure rTS, double mixedSigma, double kappa, double theta, TermStructure clock)
    {
        Vector locations1 = mesher.Locations(1);
        dyMap = new SecondDerivativeOp(1, mesher)
            .Mult(0.5 * mixedSigma * mixedSigma * locations1)
            .Add(new FirstDerivativeOp(1, mesher).Mult(kappa * (theta - locations1)));
        mapT = new TripleBandLinearOp(1, mesher);
        this.rTS = rTS;
        this.clock = clock;
    }

    internal TripleBandLinearOp Map => mapT;

    internal void SetTime(double t1, double t2)
    {
        double r = rTS.ContinuousForwardRateAt(clock, t1, t2);
        double minusHalfR = -0.5 * r;
        mapT.SetSumWithDiagonal(dyMap, new ReadOnlySpan<double>(in minusHalfR));
    }
}
