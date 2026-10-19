// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The swaption builder vectors, <c>tests/vectors/instruments/makeswaption.json</c>: builds on Euribor swap presets,
/// swap indexes and overnight-indexed swap indexes, by option tenor and by fixing date, at the money and at given
/// strikes, over every setter, and the refusals; the swaption's exercise dates, settlement terms and type, and its
/// underlying swap's fixed rate, nominal, dates, schedules and NPV.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MakeSwaptionVectorTests
{
    [Fact]
    public void MakeSwaptionVector()
    {
        var file = VectorFile.Load("instruments/makeswaption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            YieldTermStructure npvCurve = CashFlowInputs.CurveOf(c.Input("npvCurve"));
            Swaption? swaption = null;
            Func<object?> built = () =>
            {
                MakeSwaption builder = SwaptionInputs.MakeSwaptionOf(c.Input("build"), npvCurve);
                swaption = builder.ToSwaption(context);
                return null;
            };
            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", built);
                continue;
            }

            built();
            FixedVsFloatingSwap u = swaption!.Underlying;
            double scale = u.Nominal();
            check.Output(c, "exerciseDates", swaption.Exercise.Dates);
            check.Output(c, "settlementType", swaption.SettlementType);
            check.Output(c, "settlementMethod", swaption.SettlementMethod);
            check.Output(c, "type", swaption.Type);
            check.Output(c, "fixedRate", u.FixedRate);
            check.Output(c, "nominal", scale);
            check.Output(c, "startDate", u.StartDate());
            check.Output(c, "maturityDate", u.MaturityDate());
            check.Output(c, "fixedSchedule", u.FixedSchedule.Dates);
            check.Output(c, "floatingSchedule", u.FloatingSchedule.Dates);
            check.Output(
                c,
                "underlyingNPV",
                () => DiscountingSwapEngine.Calculate(u, context, npvCurve, includeSettlementDateFlows: false).NPV);
        }

        check.Complete();
    }
}
