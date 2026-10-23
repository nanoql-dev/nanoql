// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Dupire operator's mixed term (the S-g1d plan's G1R6 (xi)): a one-direction operator has none, but QuantLib's
/// <c>FdmDupire1dOp::apply_mixed</c> returns its input (on the wheel, [1, 2, 3, 4, 5] for [1, 2, 3, 4, 5]), which a
/// scheme adding the mixed term would count twice. nanoql's gives zeros, as every other one-direction operator's
/// does, QuantLib's <c>FdmBlackScholesOp</c>'s the check. ZABR's own price never reads it: Douglas runs on
/// <c>apply</c>.
/// </summary>
public class FdmDupireApplyMixedTests
{
    [Fact]
    public void TheMixedTermIsZero()
    {
        var today = new Date(15, Month.January, 2025);
        var mesher = new FdmMesherComposite(new Uniform1dMesher(0.0, 0.1, 5));
        double[] r = [1.0, 2.0, 3.0, 4.0, 5.0];
        var dupire = new FdmDupire1dOp(mesher, new Vector([0.2, 0.2, 0.2, 0.2, 0.2]));
        var process = new BlackScholesMertonProcess(
            0.05, new FlatForward(today, 0.0, new Actual365Fixed()), new FlatForward(today, 0.0, new Actual365Fixed()),
            new BlackConstantVol(today, null, 0.2, new Actual365Fixed()));
        var blackScholes = new FdmBlackScholesOp(mesher, process, 0.05);
        double[] dupireMixed = new double[5], blackScholesMixed = new double[5];

        dupire.ApplyMixed(r, dupireMixed);
        blackScholes.ApplyMixed(r, blackScholesMixed);

        Assert.Equal(new double[5], dupireMixed);
        Assert.Equal(blackScholesMixed, dupireMixed);
    }
}
