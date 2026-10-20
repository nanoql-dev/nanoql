// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BlackConstantVol vectors, <c>tests/vectors/termstructures/volatility/equityfx/blackconstantvol.json</c>: fixed
/// and moving structures, their dates, strikes, conventions and volatilities.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackConstantVolVectorTests
{
    [Fact]
    public void BlackConstantVolVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/blackconstantvol");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BlackVolTermStructure s = BlackVolOf(c.Input("structure"), context);
            check.Output(c, "referenceDate", s.ReferenceDate);
            if (c.Inputs.TryGetProperty("date", out _))
            {
                check.Output(c, "blackVariance", s.BlackVariance(c.DateInput("date"), 100.0));
                continue;
            }

            double[] times = c.Doubles("times"), strikes = c.Doubles("strikes");
            Date[] dates = c.DateInputs("dates");
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "calendar", s.Calendar!.Name);
            check.Output(c, "businessDayConvention", s.BusinessDayConvention);
            check.Output(c, "dayCounter", s.DayCounter.Name);
            check.Output(c, "blackVol", times.Select(t => (object?)strikes.Select(k => (object?)s.BlackVol(t, k)).ToArray()).ToArray());
            check.Output(
                c, "blackVariance", times.Select(t => (object?)strikes.Select(k => (object?)s.BlackVariance(t, k)).ToArray()).ToArray());
            check.Output(c, "blackVolDate", dates.Select(d => (object?)s.BlackVol(d, 100.0)).ToArray());
            check.Output(c, "blackVarianceDate", dates.Select(d => (object?)s.BlackVariance(d, 100.0)).ToArray());
        }

        check.Complete();
    }
}
