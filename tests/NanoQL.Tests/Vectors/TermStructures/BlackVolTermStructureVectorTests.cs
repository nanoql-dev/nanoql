// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BlackVolTermStructure vectors,
/// <c>tests/vectors/termstructures/volatility/equityfx/blackvoltermstructure.json</c>: the base's volatilities,
/// variances, forward volatilities and variances and smile sections through a constant volatility, variance curves and
/// variance surfaces, extrapolated, with its refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackVolTermStructureVectorTests
{
    [Fact]
    public void BlackVolTermStructureVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/blackvoltermstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BlackVolTermStructure s = BlackVolOf(c.Input("structure"), context);
            if (c.Name == "refusals")
            {
                check.Output(c, "reversedTimes", Lazy(() => s.BlackForwardVol(1.0, 0.5, 100.0)));
                check.Output(c, "reversedDates", Lazy(() => s.BlackForwardVol(DateAt(s, 1.0), DateAt(s, 0.5), 100.0)));
                check.Output(c, "negativeTime", Lazy(() => s.BlackVol(-0.1, 100.0)));
                check.Output(c, "pastMaxTime", Lazy(() => s.BlackVol(10.0, 100.0)));
                check.Output(c, "pastMaxDate", Lazy(() => s.BlackVol(DateAt(s, 10.0), 100.0)));
                continue;
            }

            if (c.Name == "a decreasing variance")
            {
                check.Output(c, "blackVariance", new object?[] { s.BlackVariance(0.5, 100.0), s.BlackVariance(1.0, 100.0) });
                check.Output(c, "blackForwardVariance", Lazy(() => s.BlackForwardVariance(0.5, 1.0, 100.0)));
                check.Output(c, "blackForwardVol", Lazy(() => s.BlackForwardVol(0.5, 1.0, 100.0)));
                check.Output(c, "blackForwardVolEqual", Lazy(() => s.BlackForwardVol(0.75, 0.75, 100.0)));
                continue;
            }

            s = s.EnableExtrapolation();
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            double[][] forwards = [.. c.Input("forwards").EnumerateArray().Select(Doubles)];
            double[] smileTimes = c.Doubles("smileTimes");
            double[] smileStrikes = c.Doubles("smileStrikes");
            check.Output(c, "blackVol", points.Select(p => (object?)s.BlackVol(p[0], p[1], true)).ToArray());
            check.Output(c, "blackVariance", points.Select(p => (object?)s.BlackVariance(p[0], p[1], true)).ToArray());
            check.Output(c, "blackVolDate", points.Select(p => (object?)s.BlackVol(DateAt(s, p[0]), p[1], true)).ToArray());
            check.Output(
                c, "blackVarianceDate", points.Select(p => (object?)s.BlackVariance(DateAt(s, p[0]), p[1], true)).ToArray());
            check.Output(c, "blackForwardVol", forwards.Select(f => Lazy(() => s.BlackForwardVol(f[0], f[1], 100.0, true))).ToArray());
            check.Output(
                c, "blackForwardVariance", forwards.Select(f => Lazy(() => s.BlackForwardVariance(f[0], f[1], 100.0, true))).ToArray());
            check.Output(
                c,
                "blackForwardVolDate",
                forwards.Select(f => Lazy(() => s.BlackForwardVol(DateAt(s, f[0]), DateAt(s, f[1]), 100.0, true))).ToArray());
            check.Output(
                c,
                "blackForwardVarianceDate",
                forwards.Select(f => Lazy(() => s.BlackForwardVariance(DateAt(s, f[0]), DateAt(s, f[1]), 100.0, true))).ToArray());
            SmileSection[] sections = [.. smileTimes.Select(t => s.SmileSection(t, true))];
            check.Output(c, "smileExerciseTime", sections.Select(x => (object?)x.ExerciseTime).ToArray());
            check.Output(c, "smileMinStrike", sections.Select(x => (object?)x.MinStrike()).ToArray());
            check.Output(c, "smileMaxStrike", sections.Select(x => (object?)x.MaxStrike()).ToArray());
            check.Output(c, "smileAtmLevel", sections.Select(x => (object?)x.AtmLevel()).ToArray());
            check.Output(
                c,
                "smileVolatility",
                sections.Select(x => (object?)smileStrikes.Select(k => (object?)x.Volatility(k)).ToArray()).ToArray());
            check.Output(
                c,
                "smileVariance",
                sections.Select(x => (object?)smileStrikes.Select(k => (object?)x.Variance(k)).ToArray()).ToArray());
            check.Output(
                c,
                "smileVolatilityDate",
                smileTimes.Select(t => (object?)s.SmileSection(DateAt(s, t), true).Volatility(100.0)).ToArray());
        }

        check.Complete();
    }

    // The generator's date of a time: the reference date and round(365 t) days, Python's round, half to even.
    private static Date DateAt(BlackVolTermStructure s, double t) =>
        s.ReferenceDate + (int)Math.Round(365 * t, MidpointRounding.ToEven);
}
