// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2008, 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmhestonhullwhiteop.hpp ql/methods/finitedifferences/operators/fdmhestonhullwhiteop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

// QuantLib's FdmHestonHullWhiteEquityPart: the equity direction's map, its drift the short rate x + phi(t) less half the
// variance and the dividend rate, on the model's clock; its step allocates nothing.
internal sealed class FdmHestonHullWhiteEquityPart
{
    private static readonly Vector None = new(0);

    private readonly double[] x;
    private readonly double[] varianceValues;
    private readonly FirstDerivativeOp dxMap;
    private readonly TripleBandLinearOp dxxMap;
    private readonly TripleBandLinearOp mapT;
    private readonly OneFactorModelShortRateDynamics dynamics;
    private readonly YieldTermStructure qTS;
    private readonly TermStructure clock;

    // the step's drift at each point, held
    private readonly Vector drift;

    internal FdmHestonHullWhiteEquityPart(FdmMesher mesher, HullWhite hwModel, YieldTermStructure qTS, TermStructure clock)
    {
        Vector locations1 = mesher.Locations(1);
        x = [.. mesher.Locations(2).AsSpan()];
        varianceValues = [.. (0.5 * locations1).AsSpan()];
        dxMap = new FirstDerivativeOp(0, mesher);
        dxxMap = new SecondDerivativeOp(0, mesher).Mult(0.5 * locations1);
        mapT = new TripleBandLinearOp(0, mesher);
        dynamics = hwModel.Dynamics();
        this.qTS = qTS;
        this.clock = clock;

        // on the boundary s_min and s_max the second derivative
        // d^2V/dS^2 is zero and due to Ito's Lemma the variance term
        // in the drift should vanish.
        int dim0 = mesher.Layout.Dim[0];
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int i = 0; i < mesher.Layout.Size; ++i, iter.Increment())
        {
            if (iter.Coordinates[0] == 0 || iter.Coordinates[0] == dim0 - 1)
            {
                varianceValues[iter.Index] = 0.0;
            }
        }

        drift = new Vector(x.Length);
    }

    internal TripleBandLinearOp Map => mapT;

    internal void SetTime(double t1, double t2)
    {
        double phi = 0.5 * (dynamics.ShortRate(t1, 0.0) + dynamics.ShortRate(t2, 0.0));

        // q over the step, at the instants its times denote on the model's clock, QuantLib's read where the curve shares
        // the clock's times (YieldTermStructure.ContinuousForwardRate, its arithmetic without its rate object)
        double q = qTS.ContinuousForwardRateAt(clock, t1, t2);

        // x + phi - varianceValues - q
        for (int i = 0; i < x.Length; ++i)
        {
            drift[i] = x[i] + phi - varianceValues[i] - q;
        }

        mapT.Axpyb(drift, dxMap, dxxMap, None);
    }
}
