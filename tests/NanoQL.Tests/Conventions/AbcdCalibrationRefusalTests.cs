// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using static NanoQL.Tests.Divergences.AbcdTermStructures;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The abcd calibration refuses, with QuantLib's text, the term structures whose total variance shrinks, which no valid
/// abcd curve fits: on sets 2 and 3 QuantLib's fit throws at its first trial point the abcd function's validation
/// refuses [the harness]; nanoql's guard carries the fit on to the zero floor, its lowest instantaneous volatility
/// below 1% of the smallest quote, and refuses it with that trial point's text.
/// </summary>
public class AbcdCalibrationRefusalTests
{
    [Fact]
    public void Set2IsRefusedWithQuantLibsText() =>
        Assert.Equal(
            "b (-0.532534) less than -0.201655: negative function value at stationary point 20.7838",
            Assert.Throws<NanoQLException>(() => new AbcdCalibration(T, Sets[2]).Compute()).Message);

    [Fact]
    public void Set3IsRefusedWithQuantLibsText() =>
        Assert.Equal(
            "b (-0.537421) less than -0.0624178: negative function value at stationary point 11.2936",
            Assert.Throws<NanoQLException>(() => new AbcdCalibration(T, Sets[3]).Compute()).Message);
}
