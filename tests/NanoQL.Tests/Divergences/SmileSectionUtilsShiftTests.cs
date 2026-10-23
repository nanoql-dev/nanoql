// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The smile utilities' endpoints on a shifted section (the S-g1d plan's G1R6 (xii)): where a section's strike range is
/// limited, QuantLib puts the range's ends in its grid with a moneyness of the shifted strike over the unshifted level
/// (<c>smilesectionutils.cpp:100-101</c>, <c>:107-108</c>), where every other point's is over the shifted level. On the
/// harness, over a section shifted by 1% at a level of 5% with strikes from 0 to 10%, its grid's ends read 0.2 and 2.2
/// where its own formula gives 1/6 and 11/6, and with a larger shift its grid comes out unsorted. nanoql divides by the
/// shifted level. QuantLib's check: its own interior formula, (k + shift)/(f + shift).
/// </summary>
public class SmileSectionUtilsShiftTests
{
    [Fact]
    public void TheEndpointsTakeTheInteriorsMoneyness()
    {
        // The harness's own section (tests/NanoQL.Tests/Vectors/SmileSectionInputs.cs), the C++ probe's.
        const string spec = """
            {"type": "HarnessSmileSection", "form": "time", "exerciseTime": 1.0, "dayCounter": {"type": "Actual365Fixed"},
             "atmLevel": 0.05, "volatilityType": 0, "shift": 0.01, "smile": [0.22, 0.3, -1.0], "minStrike": 0.0,
             "maxStrike": 0.1, "overriddenType": null, "overriddenShift": null}
            """;
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        SmileSection section = SmileSectionInputs.SectionOf(JsonDocument.Parse(spec).RootElement, context);
        var utils = new SmileSectionUtils(section);
        double[] m = [.. utils.MoneyGrid()], k = [.. utils.StrikeGrid()];
        for (int i = 0; i < m.Length; i++)
        {
            Assert.True(Math.Abs(m[i] - ((k[i] + 0.01) / (0.05 + 0.01))) <= 1e-15, $"{i}: {m[i]} at {k[i]}");
        }

        for (int i = 1; i < m.Length; i++)
        {
            Assert.True(m[i - 1] < m[i], $"{i}: {m[i - 1]}, {m[i]}");
        }

        // QuantLib's ends: 0.2 at 0, 2.2 at 10%.
        Assert.Equal(0.0, k[1]);
        Assert.True(Math.Abs(m[1] - 0.19999999999999998) > 0.03);
        Assert.Equal(0.1, k[^1]);
        Assert.True(Math.Abs(m[^1] - 2.1999999999999997) > 0.3);
    }
}
