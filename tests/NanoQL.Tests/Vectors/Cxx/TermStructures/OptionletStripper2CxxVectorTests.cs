// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="OptionletStripper2"/>'s harness vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/optionlet/optionletstripper2.json</c>: strips of QuantLib's stripper
/// tests' cases, their at-the-money strikes, prices and spreads (S7R6's filter's dropped ones null), the strikes and
/// volatilities with the at-the-money ones inserted, the dates, times and rates; and the 6M Ibor cap QuantLib refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OptionletStripper2CxxVectorTests
{
    [Fact]
    public void OptionletStripper2CxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/optionlet/optionletstripper2");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            OptionletStripper2? stripper = null;
            check.Output(c, "built", () =>
            {
                stripper = new OptionletStripper2(
                    OptionletStripperCxxVectorTests.StripperOf(c.Input("stripper"), context),
                    CapFloorTermVolCurveVectorTests.CurveOf(c.Input("curve"), context),
                    context);
                return true;
            });
            if (stripper is null || !c.Expected.TryGetProperty("spreadsVol", out _))
            {
                continue;
            }

            OptionletStripper2 s = stripper;
            HashSet<int> dropped = [.. c.Input("droppedSpreads").EnumerateArray().Select(j => j.GetInt32())];
            int n = s.OptionletMaturities;
            check.Output(c, "atmCapFloorStrikes", s.AtmCapFloorStrikes);
            check.Output(c, "atmCapFloorPrices", s.AtmCapFloorPrices);
            check.Output(c, "spreadsVol", s.SpreadsVol.Select((x, j) => dropped.Contains(j) ? null : (object?)x).ToArray());
            check.Output(c, "optionletStrikes", Enumerable.Range(0, n).Select(i => s.OptionletStrikes(i)).ToArray());
            check.Output(c, "optionletVolatilities", Enumerable.Range(0, n).Select(i => s.OptionletVolatilities(i)).ToArray());
            check.Output(c, "optionletFixingDates", s.OptionletFixingDates);
            check.Output(c, "optionletFixingTimes", s.OptionletFixingTimes);
            check.Output(c, "atmOptionletRates", s.AtmOptionletRates(context));
        }

        check.Complete();
    }
}
