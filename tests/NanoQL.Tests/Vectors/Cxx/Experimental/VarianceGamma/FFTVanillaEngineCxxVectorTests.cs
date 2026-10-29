// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's FFT vanilla vectors, <c>tests/vectors/cxx/experimental/variancegamma/fftvanillaengine.json</c>:
/// <see cref="FFTVanillaEngine"/>'s NPV for European calls and puts over four expiries on two markets, each option alone
/// and every option as one list; the process and options as
/// <c>tools/cxx/src/experimental/variancegamma/fftvanillaengine.cpp</c> builds them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FFTVanillaEngineCxxVectorTests
{
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void FFTVanillaEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/variancegamma/fftvanillaengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement m = c.Input("market");
            Date today = context.EvaluationDate;
            var process = new BlackScholesMertonProcess(
                m.GetProperty("spot").GetDouble(),
                new FlatForward(today, m.GetProperty("dividendYield").GetDouble(), A365),
                new FlatForward(today, m.GetProperty("riskFreeRate").GetDouble(), A365),
                new BlackConstantVol(today, new NullCalendar(), m.GetProperty("vol").GetDouble(), A365));
            VanillaOption[] options =
            [
                .. c.Input("options").EnumerateArray().Select(o => new VanillaOption(
                    new PlainVanillaPayoff((OptionType)o[0].GetInt32(), o[1].GetDouble()),
                    new EuropeanExercise(today + o[2].GetInt32()))),
            ];
            if (c.Input("list").GetBoolean())
            {
                var listed = new Lazy<ImmutableArray<OneAssetOptionResults>>(
                    () => FFTVanillaEngine.Calculate(options, context, process));
                check.Output(
                    c,
                    "NPV",
                    Enumerable.Range(0, options.Length).Select(k => Lazy(() => listed.Value[k].NPV)).ToArray());
            }
            else
            {
                check.Output(
                    c,
                    "NPV",
                    options.Select(o => Lazy(() => FFTVanillaEngine.Calculate(o, context, process).NPV)).ToArray());
            }
        }

        check.Complete();
    }
}
