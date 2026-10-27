// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's GARCH vectors, <c>tests/vectors/cxx/models/volatility/garch.json</c>: <see cref="Garch11"/>'s
/// calibrations by its four modes (their log-likelihoods) and from a guess, its volatility series, cost, squared
/// returns and forecasts.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Garch11CxxVectorTests
{
    [Fact]
    public void Garch11CxxVector()
    {
        var file = VectorFile.Load("cxx/models/volatility/garch");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("call"))
            {
                case "calibrate":
                    check.Output(c, "logLikelihood", new Garch11(Consecutive(c.Doubles("values")), Enum.Parse<Garch11Mode>(c.String("mode"))).LogLikelihood);
                    break;
                case "calibrateFromGuess":
                    Model(check, c, new Garch11(0.0, 0.0, 0.0).Calibrate(
                        Consecutive(c.Doubles("values")), new Simplex(0.001), new EndCriteria(10000, 500, 1e-8, 1e-8, 1e-8), c.Doubles("guess")));
                    break;
                case "calculate":
                {
                    double alpha = c.Double("alpha"), beta = c.Double("beta"), omega = c.Double("omega");
                    double[] values = c.Doubles("values");
                    var series = new Lazy<TimeSeries<double>>(() => Garch11.Calculate(new TimeSeries<double>(c.DateInputs("dates"), values), alpha, beta, omega));
                    check.Output(c, "dates", Lazy(() => series.Value.Dates().Select(d => (object?)d).ToArray()));
                    check.Output(c, "values", Lazy(() => series.Value.Values()));
                    check.Output(c, "cost", Garch11.CostFunction(values, alpha, beta, omega));
                    break;
                }

                case "to_r2":
                {
                    (System.Collections.Immutable.ImmutableArray<double> r2, double mean) = Garch11.ToR2(c.Doubles("values"));
                    check.Output(c, "r2", r2);
                    check.Output(c, "mean", mean);
                    break;
                }

                default:
                    check.Output(c, "forecast", new Garch11(c.Double("alpha"), c.Double("beta"), c.Double("vl")).Forecast(c.Double("r"), c.Double("sigma2")));
                    break;
            }
        }

        check.Complete();
    }

    private static void Model(VectorAssert check, VectorCase c, Garch11 g)
    {
        check.Output(c, "alpha", g.Alpha);
        check.Output(c, "beta", g.Beta);
        check.Output(c, "omega", g.Omega);
        check.Output(c, "logLikelihood", g.LogLikelihood);
    }

    // the returns on consecutive dates from 7 July 1962, as the harness builds them
    private static TimeSeries<double> Consecutive(double[] values) =>
        new([.. Enumerable.Range(0, values.Length).Select(i => new Date(7, July, 1962) + i)], values);

    private static Func<object?> Lazy(Func<object?> f) => f;
}
