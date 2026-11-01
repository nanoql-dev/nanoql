// SPDX-FileCopyrightText: Copyright (C) 2020 Lew Wei Hao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmcirop.hpp ql/methods/finitedifferences/operators/fdmcirop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The CIR operator's equity part, in direction 0 of a mesher of the log spot by the short rate: the drift r − q − ½v,
/// less the quanto adjustment where there is one, and the diffusion ½v, at the Black forward variance v, discounting at
/// half the short rate.
/// </summary>
/// <remarks>
/// A working type: <see cref="SetTime"/> rewrites the part's operator in place through buffers it holds. The dividend
/// curve and the volatility are read on the engine's clock (<see cref="FdmBlackScholesOp.Clock"/>), each on its own,
/// QuantLib's reads where they share its times. With a quanto helper the short rate is the domestic one: the drift
/// less the helper's adjustment with the grid's rate as the domestic rate, r − q − ½v − (r − r_f + ρσσ_fx), where
/// QuantLib's operator takes no helper and its engine passes it to the mesher alone, so that a quanto prices without
/// the quanto drift (a run find of the S-van plan's C4, under R8).
/// </remarks>
[WorkingType]
internal sealed class FdmCIREquityPart
{
    private readonly FirstDerivativeOp dxMap;
    private readonly TripleBandLinearOp dxxMap;
    private readonly TripleBandLinearOp mapT;

    private readonly TermStructure clock;
    private readonly YieldTermStructure qTS;
    private readonly double strike;
    private readonly BlackVolTermStructure sigma1;
    private readonly FdmQuantoHelper? quantoHelper;

    // the short rate at each point, -0.5 times it, and the step's drift, held
    private readonly double[] r;
    private readonly double[] minusHalfR;
    private readonly double[] drift;

    internal FdmCIREquityPart(
        FdmMesher mesher,
        GeneralizedBlackScholesProcess bsProcess,
        double strike,
        TermStructure clock,
        FdmQuantoHelper? quantoHelper)
    {
        dxMap = new FirstDerivativeOp(0, mesher);
        dxxMap = new SecondDerivativeOp(0, mesher);
        mapT = new TripleBandLinearOp(0, mesher);
        this.clock = clock;
        qTS = bsProcess.DividendYield;
        this.strike = strike;
        sigma1 = bsProcess.BlackVolatility;
        this.quantoHelper = quantoHelper;

        r = [.. mesher.Locations(1).AsSpan()];
        minusHalfR = new double[r.Length];
        for (int i = 0; i < r.Length; ++i)
        {
            minusHalfR[i] = -0.5 * r[i];
        }

        drift = new double[r.Length];
    }

    internal TripleBandLinearOp Map => mapT;

    internal void SetTime(double t1, double t2)
    {
        double q = qTS.ContinuousForwardRateAt(clock, t1, t2);

        double v = sigma1.BlackForwardVarianceAt(clock, t1, t2, strike) / (t2 - t1);

        // mesher_->locations(1) - q - 0.5*v, less the quanto adjustment at the grid's rate
        if (quantoHelper is not null)
        {
            (double rForeign, double fxVol) = quantoHelper.ForeignAt(clock, t1, t2);
            double sqrtV = Math.Sqrt(v);
            for (int i = 0; i < r.Length; ++i)
            {
                double adjustment = r[i] - rForeign + (sqrtV * fxVol * quantoHelper.EquityFxCorrelation);
                drift[i] = r[i] - q - (0.5 * v) - adjustment;
            }
        }
        else
        {
            for (int i = 0; i < r.Length; ++i)
            {
                drift[i] = r[i] - q - (0.5 * v);
            }
        }

        double h = v / 2;
        mapT.SetAxpybOfMult(drift, dxMap, dxxMap, new ReadOnlySpan<double>(in h), minusHalfR);
    }
}
