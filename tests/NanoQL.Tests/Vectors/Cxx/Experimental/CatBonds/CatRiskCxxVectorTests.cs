// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's catastrophe risk vectors, <c>tests/vectors/cxx/experimental/catbonds/catrisk.json</c>: every path
/// of <see cref="EventSet"/>'s simulations, and <see cref="BetaRisk"/>'s first paths on MSVC's streams up to the first
/// where QuantLib's fresh event draws and the cumulated ones can part, and
/// <see cref="BetaRiskSimulation.GenerateBeta"/>'s losses at shapes reaching each of the gamma's branches.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CatRiskCxxVectorTests
{
    [Fact]
    public void CatRiskCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/catbonds/catrisk");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            if (c.Input("kind").GetString() == "eventSet")
            {
                var set = new EventSet(EventsOf(a[0]), DateOf(a[1]), DateOf(a[2]));
                check.Output(c, "paths", () =>
                {
                    CatSimulation simulation = set.NewSimulation(DateOf(a[3]), DateOf(a[4]));
                    var paths = new List<object[]>();
                    var path = new List<KeyValuePair<Date, double>>();
                    while (simulation.NextPath(path))
                    {
                        paths.Add(Encoded(path));
                    }

                    return paths.ToArray();
                });
                continue;
            }

            if (c.Input("kind").GetString() == "betas")
            {
                var simulation = new BetaRiskSimulation(
                    DateOf(a[0]), DateOf(a[1]), VectorCase.Real(a[2]), VectorCase.Real(a[3]), VectorCase.Real(a[4]), VectorCase.Real(a[5]));
                check.Output(c, "betas", () => Enumerable.Range(0, a[6].GetInt32()).Select(_ => simulation.GenerateBeta()).ToArray());
                continue;
            }

            var risk = new BetaRisk(VectorCase.Real(a[0]), VectorCase.Real(a[1]), VectorCase.Real(a[2]), VectorCase.Real(a[3]));
            check.Output(c, "paths", () =>
            {
                CatSimulation simulation = risk.NewSimulation(DateOf(a[4]), DateOf(a[5]));
                var paths = new List<object[]>();
                var path = new List<KeyValuePair<Date, double>>();
                for (int i = 0; i < a[6].GetInt32(); i++)
                {
                    simulation.NextPath(path);
                    paths.Add(Encoded(path));
                }

                return paths.ToArray();
            });
        }

        check.Complete();
    }

    // Events given as [date serial, loss] pairs.
    internal static KeyValuePair<Date, double>[] EventsOf(JsonElement spec) =>
        [.. spec.EnumerateArray().Select(e => new KeyValuePair<Date, double>(new Date((int)VectorCase.Real(e[0])), VectorCase.Real(e[1])))];

    internal static Date DateOf(JsonElement serial) => new(serial.GetInt32());

    // A path as the harness writes it, [date, loss] pairs.
    private static object[] Encoded(List<KeyValuePair<Date, double>> path) =>
        [.. path.Select(e => (object)new object[] { e.Key, e.Value })];
}
