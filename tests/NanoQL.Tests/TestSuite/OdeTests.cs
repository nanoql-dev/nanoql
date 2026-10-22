// SPDX-FileCopyrightText: Copyright (C) 2012 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2013 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/ode.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testMatrixExponential and testMatrixExponentialOfZero, which wait for S-core's Expm

using System.Collections.Immutable;
using System.Numerics;
using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class OdeTests
{
    [Fact]
    public void TestAdaptiveRungeKutta()
    {
        // Testing adaptive Runge Kutta...

        var rk_real = new AdaptiveRungeKutta<Real>(1E-12, 1E-4, 0.0);
        var rk_complex = new AdaptiveRungeKutta<Complex>(1E-12, 1E-4, 0.0);
        Real tol1 = 5E-10, tol2 = 2E-12, tol3 = 2E-12, tol4 = 2E-12;

        // f'=f, f(0)=1
        Func<Real, Real, Real> ode1_ = Ode1;
        Real y10 = 1;

        // f'=f, f(0)=i
        Func<Real, Complex, Complex> ode2_ = Ode2;
        var y20 = new Complex(0.0, 1.0);

        // f''=-f, f(0)=0, f'(0)=1
        Func<Real, ImmutableArray<Real>, ImmutableArray<Real>> ode3_ = Ode3;
        Real[] y30 = [0.0, 1.0];

        // f''=-f, f(0)=1, f'(0)=i
        Func<Real, ImmutableArray<Complex>, ImmutableArray<Complex>> ode4_ = Ode4;
        Complex[] y40 = [1.0, new Complex(0.0, 1.0)];

        Real x = 0.0;
        Real y1 = y10;
        Complex y2 = y20;
        ImmutableArray<Real> y3 = [.. y30];
        ImmutableArray<Complex> y4 = [.. y40];

        while (x < 5.0)
        {
            Real exact1 = Math.Exp(x);
            Complex exact2 = Complex.Exp(new Complex(0.0, x)) * new Complex(0.0, 1.0);
            Real exact3 = Math.Sin(x);
            Complex exact4 = Complex.Exp(new Complex(0.0, x));

            if (Math.Abs(exact1 - y1) > tol1)
            {
                Assert.Fail(
                    $"Error in ode #1: exact solution at x={x} is {exact1}, numerical solution is {y1} difference "
                    + $"{Math.Abs(exact1 - y1)} outside tolerance {tol1}");
            }

            if (Complex.Abs(exact2 - y2) > tol2)
            {
                Assert.Fail(
                    $"Error in ode #2: exact solution at x={x} is {exact2}, numerical solution is {y2} difference "
                    + $"{Complex.Abs(exact2 - y2)} outside tolerance {tol2}");
            }

            if (Math.Abs(exact3 - y3[0]) > tol3)
            {
                Assert.Fail(
                    $"Error in ode #3: exact solution at x={x} is {exact3}, numerical solution is {y3[0]} difference "
                    + $"{Math.Abs(exact3 - y3[0])} outside tolerance {tol3}");
            }

            if (Complex.Abs(exact4 - y4[0]) > tol4)
            {
                Assert.Fail(
                    $"Error in ode #4: exact solution at x={x} is {exact4}, numerical solution is {y4[0]} difference "
                    + $"{Complex.Abs(exact4 - y4[0])} outside tolerance {tol4}");
            }

            x += 0.01;
            y1 = rk_real.Evaluate(ode1_, y10, 0.0, x);
            y2 = rk_complex.Evaluate(ode2_, y20, 0.0, x);
            y3 = rk_real.Evaluate(ode3_, y30, 0.0, x);
            y4 = rk_complex.Evaluate(ode4_, y40, 0.0, x);
        }
    }

    private static Real Ode1(Real x, Real y) => y;

    private static Complex Ode2(Real x, Complex y) => new Complex(0.0, 1.0) * y;

    private static ImmutableArray<Real> Ode3(Real x, ImmutableArray<Real> y) => [y[1], -y[0]];

    private static ImmutableArray<Complex> Ode4(Real x, ImmutableArray<Complex> y) => [y[1], -y[0]];
}
