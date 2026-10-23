// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The GSR process vectors, <c>tests/vectors/processes/gsrprocess.json</c>: <see cref="GsrProcess"/> with constant and
/// piecewise volatilities and reversions, its x0, drift, diffusion, step expectation, standard deviation and variance,
/// evolve and apply, σ, the reversion, y and G; the same members of the copy at another forward measure time, against
/// QuantLib's process built at that time (#2842); and the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GsrProcessVectorTests
{
    [Fact]
    public void GsrProcessVector()
    {
        var file = VectorFile.Load("processes/gsrprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] times = Numbers(c.Input("times"));
            double[] vols = Numbers(c.Input("vols"));
            double[] reversions = Numbers(c.Input("reversions"));
            double bigT = c.Double("T");
            if (c.Name.StartsWith("refused / ", StringComparison.Ordinal))
            {
                check.Output(c, "result", () => Refused(c, times, vols, reversions, bigT));
                continue;
            }

            var p = new GsrProcess(times, vols, reversions, bigT);
            HullWhiteProcessVectorTests.CheckProcess(check, c, p);
            double[] pointTimes = Numbers(c.Input("pointTimes"));
            double[][] gPoints = ModelInputs.Points(c, "gPoints");
            check.Output(c, "sigma", pointTimes.Select(t => (object?)p.Sigma(t)).ToArray());
            check.Output(c, "reversion", pointTimes.Select(t => (object?)p.Reversion(t)).ToArray());
            check.Output(c, "y", pointTimes.Select(t => (object?)p.Y(t)).ToArray());
            check.Output(c, "G", gPoints.Select(g => (object?)p.G(g[0], g[1], 0.0)).ToArray());

            GsrProcess other = p.WithForwardMeasureTime(c.Double("otherT"));
            double[][] stepPoints = ModelInputs.Points(c, "stepPoints");
            check.Output(c, "atOtherTimeExpectation", stepPoints.Select(s => (object?)other.Expectation(s[0], s[1], s[2])).ToArray());
            check.Output(c, "atOtherTimeVariance", stepPoints.Select(s => (object?)other.Variance(s[0], s[1], s[2])).ToArray());
            check.Output(c, "atOtherTimeDrift", ModelInputs.Points(c, "processPoints").Select(x => (object?)other.Drift(x[0], x[1])).ToArray());
            check.Output(c, "atOtherTimeY", pointTimes.Select(t => (object?)other.Y(t)).ToArray());
            check.Output(c, "atOtherTimeG", gPoints.Select(g => (object?)other.G(g[0], g[1], 0.0)).ToArray());
        }

        check.Complete();
    }

    private static double Refused(VectorCase c, double[] times, double[] vols, double[] reversions, double bigT)
    {
        var p = new GsrProcess(times, vols, reversions, bigT);
        if (c.Input("member").ValueKind == System.Text.Json.JsonValueKind.Null)
        {
            return p.X0;
        }

        double[] a = Numbers(c.Input("arguments"));
        return c.Input("member").GetString() switch
        {
            "expectation" => p.Expectation(a[0], a[1], a[2]),
            "variance" => p.Variance(a[0], a[1], a[2]),
            "diffusion" => p.Diffusion(a[0], a[1]),
            "y" => p.Y(a[0]),
            "G" => p.G(a[0], a[1], a[2]),
            _ => throw new InvalidOperationException(c.Name),
        };
    }
}
