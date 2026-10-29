// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's CLV model vectors, <c>tests/vectors/cxx/experimental/models/clvmodels.json</c>: the normal and
/// square-root models' collocation points, their cdf, and the mapping function.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CLVModelsCxxVectorTests
{
    [Fact]
    public void CLVModelsCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/models/clvmodels");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            var reference = new Date(a[0].GetInt32());
            DayCounter dc = new Actual365Fixed();
            var bs = new BlackScholesMertonProcess(
                a[1].GetDouble(),
                new FlatForward(reference, a[3].GetDouble(), dc),
                new FlatForward(reference, a[2].GetDouble(), dc),
                new BlackConstantVol(reference, new NullCalendar(), a[4].GetDouble(), dc));
            double[] k = [.. a[5].EnumerateArray().Select(e => e.GetDouble())];
            Date[] maturities = [.. a[6].EnumerateArray().Select(e => reference + e.GetInt32())];
            int order = a[7].GetInt32();
            double? pMax = a[8].ValueKind == JsonValueKind.Null ? null : a[8].GetDouble();
            double? pMin = a[9].ValueKind == JsonValueKind.Null ? null : a[9].GetDouble();
            Date[] dates = [.. a[10].EnumerateArray().Select(e => reference + e.GetInt32())];
            (Func<Date, Vector> x, Func<Date, Vector> y, Func<Date, double, double> cdf, Func<double, double, double> g) =
                c.String("model") == "normal"
                    ? Normal(new NormalCLVModel(bs, new OrnsteinUhlenbeckProcess(k[0], k[1], k[2], k[3]), maturities, order, pMax, pMin))
                    : SquareRoot(new SquareRootCLVModel(bs, new SquareRootProcess(k[0], k[1], k[2], k[3]), maturities, order, pMax, pMin));
            var xs = new List<double>();
            var ys = new List<double>();
            var cdfs = new List<double>();
            foreach (Date d in dates)
            {
                xs.AddRange(x(d));
                foreach (double value in y(d))
                {
                    ys.Add(value);
                    cdfs.Add(cdf(d, value));
                }
            }

            check.Output(c, "x", xs.ToArray());
            check.Output(c, "y", ys.ToArray());
            check.Output(c, "cdf", cdfs.ToArray());
            check.Output(c, "g", a[11].EnumerateArray().Select(p => g(p[0].GetDouble(), p[1].GetDouble())).ToArray());
        }

        check.Complete();
    }

    private static (Func<Date, Vector>, Func<Date, Vector>, Func<Date, double, double>, Func<double, double, double>) Normal(
        NormalCLVModel m) => (m.CollocationPointsX, m.CollocationPointsY, m.Cdf, m.G());

    private static (Func<Date, Vector>, Func<Date, Vector>, Func<Date, double, double>, Func<double, double, double>) SquareRoot(
        SquareRootCLVModel m) => (m.CollocationPointsX, m.CollocationPointsY, m.Cdf, m.G());
}
