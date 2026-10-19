// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A stripper told not to throw, after a strip that failed. QuantLib's <c>OptionletStripper1</c> leaves 0 for an optionlet
/// whose volatility it can't imply and keeps it as that optionlet's next guess (<c>optionletstripper1.cpp:150-171</c>);
/// a guess of 0 is the solver's lower bound, which <c>blackFormulaImpliedStdDev</c> refuses ("guess (0) &lt; xMin_ (0)"),
/// so once its quotes are good again the re-strip leaves 0 there still: on the warm-start case's setup with the 1Y row
/// at 60% and the others at 10%, the second optionlet's volatilities are 0, 0, 0.08686109830399297 and 0, and with the
/// quotes back at the good surface the wheel's re-strip gives 0, 0, 0.21151008145592823 and 0, where a fresh stripper
/// gives 0.28960633277893066, 0.24306750297546387, 0.2115100814559281 and 0.20306367926200938. nanoql strips once, from
/// QuantLib's first guess: the failed strip gives QuantLib's zeros, and a stripper on the good quotes QuantLib's fresh
/// values.
/// </summary>
public class OptionletStripperDontThrowTests
{
    [Fact]
    public void AFailedOptionletLeavesNoGuessBehind()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(16, Month.June, 2025) };
        double[,] bad =
        {
            { 0.60, 0.60, 0.60, 0.60 }, { 0.10, 0.10, 0.10, 0.10 }, { 0.10, 0.10, 0.10, 0.10 }, { 0.10, 0.10, 0.10, 0.10 },
        };

        OptionletStripper1 failed = StripperFixture.Strip(context, bad, dontThrow: true);
        Assert.Equal([0.0, 0.0, 0.08686109830399297, 0.0], failed.OptionletVolatilities(1));

        // QuantLib's re-strip on the good quotes: 0, 0, 0.21151008145592823, 0.
        OptionletStripper1 good = StripperFixture.Strip(context, dontThrow: true);
        Assert.Equal(
            [0.28960633277893066, 0.24306750297546387, 0.2115100814559281, 0.20306367926200938],
            good.OptionletVolatilities(1));
    }
}
