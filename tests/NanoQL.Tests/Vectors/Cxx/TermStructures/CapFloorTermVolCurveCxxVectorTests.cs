// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="CapFloorTermVolCurve"/>'s harness vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/capfloor/capfloortermvolcurve.json</c>: the members the wheel doesn't
/// bind, its settlement days (null on a fixed date), option tenors, dates and times, and the option dates of tenors.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CapFloorTermVolCurveCxxVectorTests
{
    [Fact]
    public void CapFloorTermVolCurveCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/capfloor/capfloortermvolcurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CapFloorTermVolCurve s = CapFloorTermVolCurveVectorTests.CurveOf(c.Input("curve"), context);
            check.Output(c, "settlementDays", s.SettlementDays);
            check.Output(c, "optionTenors", s.OptionTenors.Select(t => new[] { t.Length, (int)t.Units }).ToArray());
            check.Output(c, "optionDates", s.OptionDates);
            check.Output(c, "optionTimes", s.OptionTimes);
            check.Output(
                c,
                "optionDateFromTenor",
                c.Input("tenors").EnumerateArray().Select(t => (Func<object?>)(() => s.OptionDateFromTenor(SwapInputs.PeriodOf(t)))).ToArray());
        }

        check.Complete();
    }
}
