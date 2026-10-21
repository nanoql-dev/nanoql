// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008 Paul Farrington
// SPDX-FileCopyrightText: Copyright (C) 2014 Thema Consulting SA
// SPDX-FileCopyrightText: Copyright (C) 2019 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/quantooption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testValues, testGreeks, testForwardValues, testForwardGreeks, testForwardPerformanceValues, testBarrierValues, testPDEOptionValues (B4.8's quanto engine); testAmericanQuantoOption (B4.3's Heston engine); testDoubleBarrierValues (B7.6's quanto double-barrier option)

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class QuantoOptionTests
{
    [Fact]
    public void TestFDMQuantoHelper()
    {
        // Testing FDM quanto helper...

        DayCounter dc = new Actual360();
        var today = new Date(22, Month.April, 2019);

        Real s = 100;
        Rate domesticR = 0.1;
        Rate foreignR = 0.2;
        Rate q = 0.3;
        Volatility vol = 0.3;
        Volatility fxVol = 0.2;

        Real exchRateATMlevel = 1.0;
        Real equityFxCorrelation = -0.75;

        YieldTermStructure domesticTS = new FlatForward(today, domesticR, dc);

        YieldTermStructure divTS = new FlatForward(today, q, dc);

        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), vol, dc);

        var bsmProcess = new BlackScholesMertonProcess(s, divTS, domesticTS, volTS);

        YieldTermStructure foreignTS = new FlatForward(today, foreignR, dc);

        BlackVolTermStructure fxVolTS = new BlackConstantVol(today, new NullCalendar(), fxVol, dc);

        var fdmQuantoHelper =
            new FdmQuantoHelper(domesticTS, foreignTS, fxVolTS, equityFxCorrelation, exchRateATMlevel);

        Real calculatedQuantoAdj = fdmQuantoHelper.QuantoAdjustment(vol, 0.0, 1.0);

        Real expectedQuantoAdj = domesticR - foreignR + (equityFxCorrelation * vol * fxVol);

        Real tol = 1e-10;
        if (Math.Abs(calculatedQuantoAdj - expectedQuantoAdj) > tol)
        {
            Assert.Fail(
                $"failed to reproduce quanto drift rate\n    calculated: {calculatedQuantoAdj}\n    expected:   "
                + $"{expectedQuantoAdj}");
        }

        Date maturityDate = today + new Period(6, TimeUnit.Months);
        double maturityTime = dc.YearFraction(today, maturityDate);

        Real eps = 0.0002;
        Real scalingFactor = 1.25;

        var mesher = new FdmBlackScholesMesher(
            3, bsmProcess, maturityTime, s, null, null, eps, scalingFactor, (null, null), [], fdmQuantoHelper);

        Real normInvEps = new InverseCumulativeNormal().Evaluate(1 - eps);
        Real sigmaSqrtT = vol * Math.Sqrt(maturityTime);

        Real qQuanto = q + expectedQuantoAdj;
        Real expectedDriftRate = domesticR - qQuanto;

        Real logFwd = Math.Log(s) + (expectedDriftRate * maturityTime);
        Real xMin = logFwd - (sigmaSqrtT * normInvEps * scalingFactor);
        Real xMax = Math.Log(s) + (sigmaSqrtT * normInvEps * scalingFactor);

        System.Collections.Immutable.ImmutableArray<double> loc = mesher.Locations();

        if (Math.Abs(loc[0] - xMin) > tol || Math.Abs(loc[^1] - xMax) > tol)
        {
            Assert.Fail(
                $"failed to reproduce FDM grid boundaries\n    calculated: ({loc[0]}, {loc[^1]})\n    expected:   "
                + $"({xMin}, {xMax})");
        }
    }
}
