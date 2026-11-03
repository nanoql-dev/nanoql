// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A stripper's implied volatilities on a second strip. QuantLib's <c>OptionletStripper1</c> keeps its standard
/// deviations between calculations and passes them as the next calculation's guesses
/// (<c>optionletstripper1.cpp:58-59, 150-153</c>), so a recalculated stripper warm-starts: on 16 June 2025, a 4 × 4
/// surface (1Y, 2Y, 3Y, 5Y by 2%, 3%, 4%, 5%) on a flat 4% curve and Euribor 6M, a quote moved and moved back, the
/// wheel's re-strip differs from its first in 33 of 36 volatilities, by up to 6.8449e-7: the first optionlet at 2% is
/// 0.30000093033375125 on the first strip and 0.3000002879738929 on the second. nanoql strips once, at construction, from
/// QuantLib's first guess, 0.14: every stripper on those quotes gives QuantLib's first strip, which a fresh QuantLib
/// stripper gives too.
/// </summary>
public class OptionletStripperWarmStartTests
{
    [Fact]
    public void EveryStripGivesQuantLibsFirstStrip()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(16, Month.June, 2025) };
        OptionletStripper1 first = StripperFixture.Strip(context);

        // The wheel's first strip, and a fresh one's; its re-strip after the round trip gives 0.3000002879738929 and
        // 0.24999947208573145.
        Assert.Equal(0.30000093033375125, first.OptionletVolatilities(0)[0]);
        Assert.Equal(0.24999986802156732, first.OptionletVolatilities(0)[1]);

        // The quote moved and back is the same quote: a new stripper on it strips from the cold start again.
        OptionletStripper1 second = StripperFixture.Strip(context);
        for (int i = 0; i < first.OptionletMaturities; ++i)
        {
            Assert.Equal(first.OptionletVolatilities(i), second.OptionletVolatilities(i));
        }
    }
}
