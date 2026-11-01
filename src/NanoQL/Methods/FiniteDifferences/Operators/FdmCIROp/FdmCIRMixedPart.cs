// SPDX-FileCopyrightText: Copyright (C) 2020 Lew Wei Hao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmcirop.hpp ql/methods/finitedifferences/operators/fdmcirop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The CIR operator's mixed part, the log spot by the short rate: the covariance ρ·σ_S·σ·√r of the two factors'
/// diffusions, σ_S the equity's Black forward volatility.
/// </summary>
/// <remarks>
/// A working type: <see cref="SetTime"/> rewrites the part's operator in place. The coefficient is ρσ_Sσ√r, the
/// covariance of the log spot's diffusion σ_S·dW₁ with the CIR rate's σ√r·dW₂, where QuantLib's is 2ρσ_Sσ
/// (<c>fdmcirop.cpp:74-82</c>) (the S-van plan's VR4 (xv)). The volatility is read on the engine's clock
/// (<see cref="FdmBlackScholesOp.Clock"/>), QuantLib's read where it shares its times.
/// </remarks>
[WorkingType]
internal sealed class FdmCIRMixedPart
{
    private readonly NinePointLinearOp dyMap;
    private readonly NinePointLinearOp mapT;
    private readonly TermStructure clock;
    private readonly BlackVolTermStructure sigma1;
    private readonly double strike;

    internal FdmCIRMixedPart(
        FdmMesher mesher,
        CoxIngersollRossProcess cirProcess,
        GeneralizedBlackScholesProcess bsProcess,
        double rho,
        double strike,
        TermStructure clock)
    {
        // rho*sigma*sqrt(r), where QuantLib's is 2*rho*sigma (VR4 (xv))
        dyMap = new SecondOrderMixedDerivativeOp(0, 1, mesher)
            .Mult(rho * cirProcess.Volatility * Numerics.ArrayFunctions.Sqrt(mesher.Locations(1)));
        mapT = new NinePointLinearOp(0, 1, mesher);
        this.clock = clock;
        sigma1 = bsProcess.BlackVolatility;
        this.strike = strike;
    }

    internal NinePointLinearOp Map => mapT;

    internal void SetTime(double t1, double t2)
    {
        double v = Math.Sqrt(sigma1.BlackForwardVarianceAt(clock, t1, t2, strike) / (t2 - t1));
        mapT.SetMult(dyMap, v);
    }
}
