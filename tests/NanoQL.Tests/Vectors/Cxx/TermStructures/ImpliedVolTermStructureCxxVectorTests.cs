// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="ImpliedVolTermStructure"/>'s harness vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/equityfx/impliedvoltermstructure.json</c>: the structure a
/// <see cref="BlackVarianceSurface"/> implies at a later date, on day counters whose year fractions add, its maximum
/// date and strike range, and its variances, volatilities and forward variances by date and by time, extrapolating.
/// The wheel doesn't expose it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ImpliedVolTermStructureCxxVectorTests
{
    [Fact]
    public void ImpliedVolTermStructureCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/equityfx/impliedvoltermstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Date[] dates = [.. c.Ints("dates").Select(s => new Date(s))];
            Date[] queries = [.. c.Ints("queryDates").Select(s => new Date(s))];
            double[] strikes = c.Doubles("strikes"), times = c.Doubles("queryTimes"), ks = c.Doubles("queryStrikes");
            var original = new BlackVarianceSurface(
                new Date(c.Int("originalReference")),
                null,
                dates,
                strikes,
                InterpolationInputs.MatrixOf(c.Input("vols")),
                CashFlowInputs.DayCounterOf(c.Input("dayCounter")));
            var implied = new ImpliedVolTermStructure(original, new Date(c.Int("referenceDate")));

            check.Output(c, "maxDate", implied.MaxDate);
            check.Output(c, "minStrike", implied.MinStrike());
            check.Output(c, "maxStrike", implied.MaxStrike());
            check.Output(c, "varianceAtDate", queries.Select(d => ks.Select(k => implied.BlackVariance(d, k, true)).ToArray()).ToArray());
            check.Output(c, "volAtDate", queries.Select(d => ks.Select(k => implied.BlackVol(d, k, true)).ToArray()).ToArray());
            check.Output(
                c,
                "forwardVariance",
                Enumerable.Range(0, queries.Length - 1)
                    .Select(i => ks.Select(k => implied.BlackForwardVariance(queries[i], queries[i + 1], k, true)).ToArray()).ToArray());
            check.Output(c, "varianceAtTime", times.Select(t => ks.Select(k => implied.BlackVariance(t, k, true)).ToArray()).ToArray());
            check.Output(c, "volAtTime", times.Select(t => ks.Select(k => implied.BlackVol(t, k, true)).ToArray()).ToArray());
        }

        check.Complete();
    }
}
