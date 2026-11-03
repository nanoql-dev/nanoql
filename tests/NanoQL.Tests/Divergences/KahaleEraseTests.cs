// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Kahale's failed interpolation: a segment that fails to fit erases the grid's next point and, in QuantLib, the
/// function slot at the grid index after the segment's (<c>kahalesmilesection.cpp:163</c>), past the slots' end once
/// the core's left index is 2 or more, undefined behaviour: on a harness section whose smile is 0.2 − k + 10k² at a
/// level of 3% over a year, interpolated on the default grid, the core's left index is 7 and the C++ harness exits.
/// nanoql erases the unfitted slot after the segment's own. QuantLib's check: its own section on the grid cut to start
/// at the core, left index 1, where its erase is in range and every slot either erase removes is unfitted: from the
/// core to the segment where nanoql's fit also refuses a cancelled function (KahaleSegmentFitTests), the prices agree
/// bit for bit.
/// </summary>
public class KahaleEraseTests
{
    // The core starts at moneyness 0.6 on the default grid; the cut grid starts there.
    private static readonly double[] Cut = [0.6, 0.7, 0.8, 0.9, 1.0, 1.25, 1.5, 1.75, 2.0, 5.0, 7.5, 10.0, 15.0, 20.0];

    [Fact]
    public void ALeftIndexOfTwoOrMoreErasesTheSegmentsNextSlot()
    {
        const string spec = """
            {"type": "HarnessSmileSection", "form": "date", "exerciseDate": 46037, "referenceDate": 45672,
             "dayCounter": {"type": "Actual365Fixed"}, "atmLevel": 0.03, "volatilityType": 0, "shift": 0.0,
             "smile": [0.2, -1.0, 10.0], "minStrike": -1.7976931348623157e+308, "maxStrike": 1.7976931348623157e+308,
             "overriddenType": null, "overriddenShift": null}
            """;
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        SmileSection source = SmileSectionInputs.SectionOf(JsonDocument.Parse(spec).RootElement, context);
        var full = new KahaleSmileSection(source, null, true);
        var cut = new KahaleSmileSection(source, null, true, false, false, Cut);
        Assert.Equal(7, full.CoreIndices().Left);
        Assert.Equal(1, cut.CoreIndices().Left);

        foreach (double k in new[] { 0.005, 0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.1 })
        {
            Assert.Equal(cut.OptionPrice(k), full.OptionPrice(k));
        }

        // QuantLib's on the cut grid, on the harness.
        (double K, double Price)[] quantLib =
        [
            (0.005, 0.024999999999999998), (0.01, 0.019999999999999997), (0.02, 0.010020557389471836), (0.03, 0.0021394633924579857),
            (0.04, 0.0001252510295506259),
        ];
        foreach ((double k, double price) in quantLib)
        {
            Assert.Equal(price, full.OptionPrice(k));
        }
    }
}
