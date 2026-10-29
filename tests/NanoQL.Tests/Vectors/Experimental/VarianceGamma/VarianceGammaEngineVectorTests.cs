// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The variance gamma vectors, <c>tests/vectors/experimental/variancegamma/variancegammaengine.json</c>:
/// <see cref="VarianceGammaEngine"/>'s NPV and <see cref="FFTVarianceGammaEngine"/>'s on each option alone and on a
/// list, for European calls and puts on four processes; <see cref="VarianceGammaProcess"/>'s initial value and its drift
/// and diffusion, refused. The outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class VarianceGammaEngineVectorTests
{
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void VarianceGammaEngineVector()
    {
        var file = VectorFile.Load("experimental/variancegamma/variancegammaengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            VarianceGammaProcess process = ProcessOf(c.Input("process"), context.EvaluationDate);
            if (!c.Inputs.TryGetProperty("options", out JsonElement specs))
            {
                double s0 = c.Input("process").GetProperty("s0").GetDouble();
                check.Output(c, "x0", process.X0);
                check.Output(c, "drift", () => process.Drift(0.5, s0));
                check.Output(c, "diffusion", () => process.Diffusion(0.5, s0));
                continue;
            }

            VanillaOption[] options = [.. specs.EnumerateArray().Select(s => OptionOf(s, context.EvaluationDate))];
            check.Output(
                c,
                "NPV",
                options.Select(o => Lazy(() => VarianceGammaEngine.Calculate(o, context, process).NPV)).ToArray());
            check.Output(
                c,
                "fftNPV",
                options.Select(o => Lazy(() => FFTVarianceGammaEngine.Calculate(o, context, process).NPV)).ToArray());
            var listed = new Lazy<ImmutableArray<OneAssetOptionResults>>(
                () => FFTVarianceGammaEngine.Calculate(options, context, process));
            check.Output(
                c,
                "fftListNPV",
                Enumerable.Range(0, options.Length).Select(k => Lazy(() => listed.Value[k].NPV)).ToArray());
        }

        check.Complete();
    }

    // The process spec: flat continuous curves on Act/365 (Fixed) from the evaluation date
    private static VarianceGammaProcess ProcessOf(JsonElement p, Date today) => new(
        p.GetProperty("s0").GetDouble(),
        new FlatForward(today, p.GetProperty("dividendYield").GetDouble(), A365),
        new FlatForward(today, p.GetProperty("riskFreeRate").GetDouble(), A365),
        p.GetProperty("sigma").GetDouble(),
        p.GetProperty("nu").GetDouble(),
        p.GetProperty("theta").GetDouble());

    private static VanillaOption OptionOf(JsonElement s, Date today) => new(
        new PlainVanillaPayoff((OptionType)s.GetProperty("optionType").GetInt32(), s.GetProperty("strike").GetDouble()),
        new EuropeanExercise(today + s.GetProperty("expiryDays").GetInt32()));
}
