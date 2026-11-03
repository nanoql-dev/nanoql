// SPDX-FileCopyrightText: Copyright (C) 2020 Lew Wei Hao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmcirop.hpp ql/methods/finitedifferences/operators/fdmcirop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// The CIR operator's rates part, in direction 1 of a mesher of the log spot by the short rate: the CIR generator's
/// diffusion ½σ²r and drift κ(θ − r), discounting at half the short rate.
/// </summary>
/// <remarks>
/// A working type: its generator is built at construction, and <see cref="SetTime"/> rewrites the part's operator in
/// place, the same at every step, as QuantLib's. The diffusion is ½σ²r, the CIR generator's, where QuantLib's is σ²r
/// (<c>fdmcirop.cpp:55-60</c>), the generator of a process with twice the variance.
/// </remarks>
[WorkingType]
internal sealed class FdmCIRRatesPart
{
    private readonly TripleBandLinearOp dyMap;
    private readonly TripleBandLinearOp mapT;

    // -0.5 times the short rate at each point, held
    private readonly double[] minusHalfR;

    internal FdmCIRRatesPart(FdmMesher mesher, double sigma, double kappa, double theta)
    {
        Numerics.Vector r = mesher.Locations(1);

        // the CIR generator's 0.5*sigma^2*r, where QuantLib's is sigma^2*r
        dyMap = new SecondDerivativeOp(1, mesher)
            .Mult(0.5 * sigma * sigma * r)
            .Add(new FirstDerivativeOp(1, mesher).Mult(kappa * (theta - r)));
        mapT = new TripleBandLinearOp(1, mesher);

        minusHalfR = new double[r.Size];
        for (int i = 0; i < minusHalfR.Length; ++i)
        {
            minusHalfR[i] = -0.5 * r[i];
        }
    }

    internal TripleBandLinearOp Map => mapT;

    internal void SetTime(double t1, double t2) => mapT.SetSumWithDiagonal(dyMap, minusHalfR);
}
