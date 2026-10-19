// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The amortizing fixed-rate bond vectors, <c>tests/vectors/instruments/bonds/amortizingfixedratebond.json</c>: 10
/// bonds, their frequency and day counter, and the outputs every bond file shares (<see cref="BondInputs"/>), a refused
/// construction being the case's one output, "construct"; then <see cref="AmortizingFixedRateBondFunctions"/>' notionals
/// and schedules for each bond length, frequency and rate.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AmortizingFixedRateBondVectorTests
{
    [Fact]
    public void AmortizingFixedRateBondVector()
    {
        var file = VectorFile.Load("instruments/bonds/amortizingfixedratebond");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("sinkingNotionals", out _))
            {
                CheckSinking(check, c);
                continue;
            }

            if (c.Expected.TryGetProperty("construct", out _))
            {
                check.Output(c, "construct", () => BondInputs.BondOf(c.Input("bond"), context));
                continue;
            }

            var bond = (AmortizingFixedRateBond)BondInputs.BondOf(c.Input("bond"), context);
            BondInputs.CheckStatic(check, c, bond, context);
            check.Output(c, "frequency", bond.Frequency);
            check.Output(c, "dayCounter", bond.DayCounter.Name);
            BondInputs.CheckDated(check, c, bond, context);
            BondInputs.CheckEngine(check, c, bond, context);
        }

        check.Complete();
    }

    private static void CheckSinking(VectorAssert check, VectorCase c)
    {
        int[] length = c.Ints("bondLength");
        var bondLength = new Period(length[0], (TimeUnit)length[1]);
        Frequency[] frequencies = [.. c.Ints("frequencies").Select(f => (Frequency)f)];
        double[] rates = c.Doubles("rates");
        var start = c.DateInput("startDate");
        Calendar calendar = CashFlowInputs.CalendarOf(c.Input("calendar"));
        check.Output(c, "sinkingNotionals", frequencies.Select(f => BondInputs.Each(
            rates, r => AmortizingFixedRateBondFunctions.SinkingNotionals(bondLength, f, r, 100.0))));
        check.Output(c, "sinkingSchedule", BondInputs.Each(
            frequencies, f => AmortizingFixedRateBondFunctions.SinkingSchedule(start, bondLength, f, calendar).Dates));
    }
}
