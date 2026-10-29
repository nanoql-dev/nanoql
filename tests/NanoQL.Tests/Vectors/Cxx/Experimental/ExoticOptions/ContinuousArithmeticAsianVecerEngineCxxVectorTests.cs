// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Vecer vectors,
/// <c>tests/vectors/cxx/experimental/exoticoptions/continuousarithmeticasianvecerengine.json</c>:
/// <see cref="ContinuousArithmeticAsianVecerEngine"/>'s NPV for continuous arithmetic average-price calls and puts, an
/// average starting ahead, other grids, and the refusals; the process and option as
/// <c>tools/cxx/src/experimental/exoticoptions/continuousarithmeticasianvecerengine.cpp</c> builds them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ContinuousArithmeticAsianVecerEngineCxxVectorTests
{
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void ContinuousArithmeticAsianVecerEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/exoticoptions/continuousarithmeticasianvecerengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement s = c.Input("option");
            Date today = context.EvaluationDate;
            var process = new BlackScholesMertonProcess(
                s.GetProperty("spot").GetDouble(),
                new FlatForward(today, s.GetProperty("dividendYield").GetDouble(), A365),
                new FlatForward(today, s.GetProperty("riskFreeRate").GetDouble(), A365),
                new BlackConstantVol(today, new NullCalendar(), s.GetProperty("vol").GetDouble(), A365));
            var option = new ContinuousAveragingAsianOption(
                AverageType.Arithmetic,
                new PlainVanillaPayoff((OptionType)s.GetProperty("optionType").GetInt32(), s.GetProperty("strike").GetDouble()),
                new EuropeanExercise(today + s.GetProperty("expiryDays").GetInt32()));
            check.Output(c, "NPV", () => ContinuousArithmeticAsianVecerEngine.Calculate(
                option,
                context,
                process,
                0.0,
                today + s.GetProperty("startDays").GetInt32(),
                s.GetProperty("timeSteps").GetInt32(),
                s.GetProperty("assetSteps").GetInt32(),
                s.GetProperty("zMin").GetDouble(),
                s.GetProperty("zMax").GetDouble()).NPV);
        }

        check.Complete();
    }
}
