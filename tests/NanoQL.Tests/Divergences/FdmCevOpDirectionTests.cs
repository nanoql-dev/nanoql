// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="FdmCEVOp"/> in a direction other than 0 (the S-van plan's VR4 (xiv)). QuantLib's operator builds its
/// second derivative on direction 0 whatever its direction (<c>fdmcevop.cpp:39-40</c>), so that in direction 1 of a
/// two-dimensional mesher it differentiates along the other axis, with the CEV coefficients of direction 1: it differs
/// from its own operator in direction 0 of the transposed mesher by 1.107 [executed by the S-van planner: VP25].
/// nanoql's operator differentiates along the direction given. QuantLib's check: its operator in direction 0, its
/// engine's, on the transposed mesher, whose bits nanoql's direction 0 keeps (the operator's vectors).
/// </summary>
public class FdmCevOpDirectionTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void InDirectionOneItIsDirectionZeroOnTheTransposedMesher()
    {
        // a CEV mesher (f0 2.1, α 0.75, β 0.45, a year) and a uniform one, either way round; a separable test function
        var cev = new FdmCEV1dMesher(30, 2.1, 0.75, 0.45, 1.0, 1e-4, 1.5, (2.3, 0.1));
        var uniform = new Uniform1dMesher(-1.0, 1.0, 7);
        var cevSecond = new FdmMesherComposite(uniform, cev);
        var cevFirst = new FdmMesherComposite(cev, uniform);
        var curve = new FlatForward(Today, 0.15, new Actual365Fixed());

        var op1 = new FdmCEVOp(cevSecond, curve, 2.1, 0.75, 0.45, 1);
        var op0 = new FdmCEVOp(cevFirst, curve, 2.1, 0.75, 0.45, 0);
        op1.SetTime(0.25, 0.5);
        op0.SetTime(0.25, 0.5);

        int nu = 7, nc = 30;
        double[] u1 = new double[nu * nc], u0 = new double[nu * nc];
        for (int i = 0; i < nu; ++i)
        {
            for (int j = 0; j < nc; ++j)
            {
                double x = uniform.Location(i), f = cev.Location(j);
                double value = Math.Max(f - 2.3, 0.0) * (1.0 + (0.3 * x * x));
                u1[i + (nu * j)] = value; // (uniform, cev): direction 0 runs fastest
                u0[j + (nc * i)] = value; // (cev, uniform)
            }
        }

        double[] r1 = new double[u1.Length], r0 = new double[u0.Length];
        op1.Apply(u1, r1);
        op0.Apply(u0, r0);
        for (int i = 0; i < nu; ++i)
        {
            for (int j = 0; j < nc; ++j)
            {
                Assert.Equal(r0[j + (nc * i)], r1[i + (nu * j)]);
            }
        }

        // and its splitting solve in its own direction, another direction's being zero as QuantLib's
        double[] s1 = new double[u1.Length], s0 = new double[u0.Length];
        op1.SolveSplitting(1, u1, 0.1, s1);
        op0.SolveSplitting(0, u0, 0.1, s0);
        for (int i = 0; i < nu; ++i)
        {
            for (int j = 0; j < nc; ++j)
            {
                Assert.Equal(s0[j + (nc * i)], s1[i + (nu * j)]);
            }
        }

        op1.SolveSplitting(0, u1, 0.1, s1);
        Assert.All(s1, x => Assert.Equal(0.0, x));
    }
}
