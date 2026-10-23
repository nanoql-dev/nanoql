// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/zabr.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class ZabrTests
{
    [Fact]
    public void TestConsistency()
    {
        // Testing the consistency of ZABR interpolation...

        Real tol = 1E-4;

        Real alpha = 0.08;
        Real beta = 0.70;
        Real nu = 0.20;
        Real rho = -0.30;
        Real tau = 5.0;
        Real forward = 0.03;

        var sabr = new SabrSmileSection(tau, forward, [alpha, beta, nu, rho]);

        var zabr0 = new ZabrSmileSection<ZabrShortMaturityLognormal>(tau, forward, [alpha, beta, nu, rho, 1.0]);

        var zabr1 = new ZabrSmileSection<ZabrShortMaturityNormal>(tau, forward, [alpha, beta, nu, rho, 1.0]);

        var zabr2 = new ZabrSmileSection<ZabrLocalVolatility>(tau, forward, [alpha, beta, nu, rho, 1.0]);

        // for full finite prices reduce the number of intermediate points here
        // below the recommended value to speed up the test
        var zabr3 = new ZabrSmileSection<ZabrFullFd>(tau, forward, [alpha, beta, nu, rho, 1.0], [], 2);

        Real k = 0.0001;
        while (k <= 0.70)
        {
            Real c0 = sabr.OptionPrice(k);
            Real z0 = zabr0.OptionPrice(k);
            Real z1 = zabr1.OptionPrice(k);
            Real z2 = zabr2.OptionPrice(k);
            Real z3 = zabr3.OptionPrice(k);
            if (Math.Abs(z0 - c0) > tol)
            {
                Assert.Fail($"Zabr short maturity lognormal expansion price ({z0}) deviates from Sabr Hagan 2002 price by {z0 - c0}");
            }

            if (Math.Abs(z1 - c0) > tol)
            {
                Assert.Fail($"Zabr short maturity normal expansion price ({z1}) deviates from Sabr Hagan 2002 price by {z1 - c0}");
            }

            if (Math.Abs(z2 - c0) > tol)
            {
                Assert.Fail($"Zabr local volatility price ({z2}) deviates from Sabr Hagan 2002 price by {z2 - c0}");
            }

            if (Math.Abs(z3 - c0) > tol)
            {
                Assert.Fail($"Zabr full finite difference price ({z3}) deviates from Sabr Hagan 2002 price by {z3 - c0}");
            }

            k += 0.0001;
        }
    }
}
