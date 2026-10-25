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
/// The Heston operator's equity part, in direction 0 of a mesher of the log spot by the variance: the drift
/// r − q − ½vL², less the quanto adjustment where there is one, and the diffusion ½vL², L the leverage at each point,
/// discounting at half the rate.
/// </summary>
/// <remarks>
/// A working type (the S-hes plan's HD8): <see cref="SetTime"/> rewrites the part's operator and its leverage slice in
/// place through buffers it holds. The curves, the quanto helper's structures and the leverage function are read on
/// the engine's clock (<see cref="FdmHestonOp.Clock"/>), each on its own, QuantLib's reads where they share its times
/// (HR7 (ii)).
/// </remarks>
internal sealed class FdmHestonEquityPart
{
    private readonly double[] varianceValues;
    private readonly double[] volatilityValues;
    private readonly FirstDerivativeOp dxMap;
    private readonly TripleBandLinearOp dxxMap;
    private readonly TripleBandLinearOp mapT;

    private readonly TermStructure clock;
    private readonly YieldTermStructure rTS;
    private readonly YieldTermStructure qTS;
    private readonly FdmQuantoHelper? quantoHelper;
    private readonly LocalVolTermStructure? leverageFct;

    // a spread added to the dividend rate per unit of the clock's time (FdmBatesOp's compensator on a dividend curve
    // with its own clock), 0 otherwise
    private readonly double dividendSpread;

    // the leverage slice and its square at each point, the spot of each point of the first variance row and each
    // point's row position, the row's leverage, the step's drift and the quanto adjustment, held
    private readonly double[] l;
    private readonly double[] lSquare;
    private readonly double[] xRow;
    private readonly int[] nx;
    private readonly double[] lRow;
    private readonly double[] drift;
    private readonly double[] adjustment;

    internal FdmHestonEquityPart(
        FdmMesher mesher,
        YieldTermStructure rTS,
        YieldTermStructure qTS,
        FdmQuantoHelper? quantoHelper,
        LocalVolTermStructure? leverageFct,
        TermStructure clock,
        double dividendSpread)
    {
        this.dividendSpread = dividendSpread;
        Vector locations1 = mesher.Locations(1);
        varianceValues = [.. (0.5 * locations1).AsSpan()];
        dxMap = new FirstDerivativeOp(0, mesher);
        dxxMap = new SecondDerivativeOp(0, mesher).Mult(0.5 * locations1);
        mapT = new TripleBandLinearOp(0, mesher);
        this.rTS = rTS;
        this.qTS = qTS;
        this.quantoHelper = quantoHelper;
        this.leverageFct = leverageFct;
        this.clock = clock;

        int n = mesher.Layout.Size;
        int dim0 = mesher.Layout.Dim[0];
        nx = new int[n];
        xRow = new double[dim0];

        // on the boundary s_min and s_max the second derivative
        // d^2V/dS^2 is zero and due to Ito's Lemma the variance term
        // in the drift should vanish.
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int i = 0; i < n; ++i, iter.Increment())
        {
            int c0 = iter.Coordinates[0];
            if (c0 == 0 || c0 == dim0 - 1)
            {
                varianceValues[iter.Index] = 0.0;
            }

            nx[iter.Index] = c0;
            if (iter.Coordinates[1] == 0)
            {
                xRow[c0] = Math.Exp(mesher.Location(iter, 0));
            }
        }

        volatilityValues = new double[n];
        for (int i = 0; i < n; ++i)
        {
            volatilityValues[i] = Math.Sqrt(2 * varianceValues[i]);
        }

        // without a leverage function the slice is 1 at every point, QuantLib's Array(size, 1.0)
        l = new double[n];
        lSquare = new double[n];
        Array.Fill(l, 1.0);
        Array.Fill(lSquare, 1.0 * 1.0);
        lRow = new double[dim0];
        drift = new double[n];
        adjustment = new double[quantoHelper is not null ? n : 0];
    }

    internal TripleBandLinearOp Map => mapT;

    internal double[] L => l;

    internal void SetTime(double t1, double t2)
    {
        double r = rTS.ContinuousForwardRateAt(clock, t1, t2);
        double q = qTS.ContinuousForwardRateAt(clock, t1, t2);
        if (dividendSpread != 0.0)
        {
            q += dividendSpread;
        }

        if (leverageFct is not null)
        {
            SetLeverageFctSlice(t1, t2);
            for (int i = 0; i < l.Length; ++i)
            {
                lSquare[i] = l[i] * l[i];
            }
        }

        if (quantoHelper is not null)
        {
            for (int i = 0; i < adjustment.Length; ++i)
            {
                adjustment[i] = volatilityValues[i] * l[i];
            }

            quantoHelper.QuantoAdjustmentAt(clock, adjustment, t1, t2, adjustment);
            for (int i = 0; i < drift.Length; ++i)
            {
                drift[i] = r - q - (varianceValues[i] * lSquare[i]) - adjustment[i];
            }
        }
        else
        {
            for (int i = 0; i < drift.Length; ++i)
            {
                drift[i] = r - q - (varianceValues[i] * lSquare[i]);
            }
        }

        mapT.SetAxpybOfMult(drift, dxMap, dxxMap, lSquare, -0.5 * r);
    }

    // QuantLib's getLeverageFctSlice, into the held slice: the leverage at the step's midpoint on its own clock, at
    // each spot of the grid clamped to the function's strikes, at least 0.01, the same down every variance column
    private void SetLeverageFctSlice(double t1, double t2)
    {
        LocalVolTermStructure leverage = leverageFct!;
        double t = 0.5 * (t1 + t2);
        double ownTime = FdmHestonLocalVolatilityVarianceMesher.LeverageTime(leverage, clock, t);
        double time = Min(leverage.MaxTime, ownTime);

        for (int i = 0; i < lRow.Length; ++i)
        {
            double spot = Min(leverage.MaxStrike(), Max(leverage.MinStrike(), xRow[i]));
            lRow[i] = Max(0.01, leverage.LocalVol(time, spot, true));
        }

        for (int i = 0; i < l.Length; ++i)
        {
            l[i] = lRow[nx[i]];
        }
    }

    // std::max and std::min
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;
}
