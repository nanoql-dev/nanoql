// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's generalized Hull-White vectors,
/// <c>tests/vectors/cxx/experimental/shortrate/generalizedhullwhite.json</c>: the model's parameters, bond options,
/// discount bonds, analytical fitting parameter and fitted tree, on its constant, linear-flat and linear forms, with
/// its refused dynamics and the linear-flat traits, and its Ornstein-Uhlenbeck process's moments and parameters.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GeneralizedHullWhiteCxxVectorTests
{
    [Fact]
    public void GeneralizedHullWhiteCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/shortrate/generalizedhullwhite");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name.StartsWith("process", StringComparison.Ordinal))
            {
                double speed = c.Double("speed"), vol = c.Double("vol");
                var p = new GeneralizedOrnsteinUhlenbeckProcess(_ => speed, _ => vol, c.Double("x0"), c.Double("level"));
                var values = new List<double>();
                foreach (JsonElement r in c.Input("points").EnumerateArray())
                {
                    double t = r[0].GetDouble(), x = r[1].GetDouble(), dt = r[2].GetDouble();
                    values.AddRange([p.Drift(t, x), p.Diffusion(t, x), p.Expectation(t, x, dt), p.StdDeviation(t, x, dt), p.Variance(t, x, dt)]);
                }

                check.Output(c, "values", values.ToArray());
                double[] times = [.. c.Input("points").EnumerateArray().Select(r => r[0].GetDouble())];
                check.Output(c, "speed", times.Select(p.Speed).ToArray());
                check.Output(c, "volatility", times.Select(p.Volatility).ToArray());
                check.Output(c, "level", p.Level);
                continue;
            }

            YieldTermStructure curve = ModelInputs.CurveOf(c.Input("curve"));
            GeneralizedHullWhite m = Model(curve, c.Input("model"));
            JsonElement q = c.Input("queries");
            check.Output(c, "a", m.A());
            check.Output(c, "sigma", m.Sigma);
            check.Output(c, "fixedReversion", m.FixedReversion().Cast<object?>().ToArray());
            check.Output(c, "bondOptions", q.GetProperty("bondOptions").EnumerateArray().Select(o => m.DiscountBondOption(
                o[0].GetDouble() > 0 ? OptionType.Call : OptionType.Put, o[1].GetDouble(), o[2].GetDouble(), o[3].GetDouble())).ToArray());
            check.Output(c, "discountBonds", q.GetProperty("discountBonds").EnumerateArray().Select(b =>
                m.DiscountBond(b[0].GetDouble(), b[1].GetDouble(), b[2].GetDouble())).ToArray());
            OneFactorModelShortRateDynamics dynamics = m.HwDynamics();
            check.Output(c, "phi", q.GetProperty("phi").EnumerateArray().Select(t => dynamics.ShortRate(t.GetDouble(), 0.0)).ToArray());
            JsonElement tree = q.GetProperty("tree");
            var lattice = (OneFactorModelShortRateTree)m.Tree(new TimeGrid(tree[0].GetDouble(), tree[1].GetInt32()));
            var sizes = new List<object?>();
            var nodes = new List<double>();
            for (int i = 0; i + 1 < lattice.TimeGrid.Size; ++i)
            {
                sizes.Add(lattice.Size(i));
                for (int j = 0; j < lattice.Size(i); ++j)
                {
                    nodes.Add(lattice.Discount(i, j));
                }
            }

            check.Output(c, "treeSizes", sizes.ToArray());
            check.Output(c, "tree", nodes.ToArray());
            check.Output(c, "dynamics", () => m.Dynamics());
            if (c.Input("model").GetProperty("type").GetString() == "linearFlat")
            {
                check.Output(c, "global", LinearFlat.Global);
                check.Output(c, "requiredPoints", LinearFlat.RequiredPoints);
            }
        }

        check.Complete();
    }

    private static GeneralizedHullWhite Model(YieldTermStructure curve, JsonElement spec)
    {
        if (spec.GetProperty("type").GetString() == "constant")
        {
            return new GeneralizedHullWhite(curve, spec.GetProperty("a").GetDouble(), spec.GetProperty("sigma").GetDouble());
        }

        static Date[] Dates(JsonElement d) => [.. d.EnumerateArray().Select(e => new Date(e.GetInt32()))];
        static double[] Numbers(JsonElement d) => [.. d.EnumerateArray().Select(e => e.GetDouble())];
        if (spec.GetProperty("type").GetString() == "linear")
        {
            return GeneralizedHullWhite.Create(
                curve,
                Dates(spec.GetProperty("speedDates")),
                Dates(spec.GetProperty("volDates")),
                Numbers(spec.GetProperty("speeds")),
                Numbers(spec.GetProperty("vols")),
                new Linear(),
                new Linear());
        }

        return new GeneralizedHullWhite(
            curve,
            Dates(spec.GetProperty("speedDates")),
            Dates(spec.GetProperty("volDates")),
            Numbers(spec.GetProperty("speeds")),
            Numbers(spec.GetProperty("vols")));
    }
}
