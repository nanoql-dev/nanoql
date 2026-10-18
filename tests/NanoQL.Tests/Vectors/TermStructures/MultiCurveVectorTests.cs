// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The multicurve vectors, <c>tests/vectors/termstructures/multicurve.json</c>: cycles of curves built by a
/// <see cref="MultiCurve"/> on <see cref="MultiCurveMember"/>s, whether each cycle builds and each member's nodes,
/// maximum date, discounts read through the member and helpers' quote errors; and QuantLib's refusals, each step's
/// result.
/// </summary>
/// <remarks>
/// A cycle's members are created first, their helpers built on them, then the curves added in order and the cycle
/// built. QuantLib links a member when it is added and nanoql when its multicurve is built, so in a refusal case a
/// multicurve is built before a step adds to another.
/// </remarks>
[Trait("Oracle", "QuantLib")]
public class MultiCurveVectorTests
{
    [Fact]
    public void MultiCurveVector()
    {
        var file = VectorFile.Load("termstructures/multicurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.String("kind") == "cycle")
            {
                CheckCycle(check, c, context);
            }
            else
            {
                CheckSteps(check, c, context);
            }
        }

        check.Complete();
    }

    private static void CheckCycle(VectorAssert check, VectorCase c, EvaluationContext context)
    {
        JsonElement[] members = [.. c.Input("members").EnumerateArray()];
        Dictionary<string, MultiCurveMember> handles = members.ToDictionary(
            m => m.GetProperty("name").GetString()!, m => HandleOf(m, context));
        var helpers = new BootstrapHelper<YieldTermStructure>[members.Length][];
        MultiCurve multiCurve = MultiCurveOf(c);
        for (int k = 0; k < members.Length; ++k)
        {
            JsonElement m = members[k];
            MultiCurveMember h = handles[m.GetProperty("name").GetString()!];
            JsonElement curve = m.GetProperty("curve");
            if (curve.TryGetProperty("traits", out _))
            {
                helpers[k] = RateHelperInputs.HelpersOf(m.GetProperty("helpers"), context, n => handles[n]);
                multiCurve = AddBootstrapped(multiCurve, h, curve, helpers[k]);
            }
            else
            {
                helpers[k] = [];
                multiCurve = multiCurve.AddNonBootstrappedCurve(h, CashFlowInputs.CurveOf(curve));
            }
        }

        ImmutableArray<YieldTermStructure> curves = default;
        check.Output(c, "built", () =>
        {
            curves = multiCurve.Build(context);
            return true;
        });
        if (curves.IsDefault)
        {
            return;
        }

        JsonElement[] dates = [.. c.Input("discountDates").EnumerateArray()];
        JsonElement[] flags = [.. c.Input("extrapolate").EnumerateArray()];
        MultiCurveMember[] members2 = [.. members.Select(m => handles[m.GetProperty("name").GetString()!])];
        check.Output(c, "dates", curves.Select(x => NodesOf(x).Dates).ToArray());
        check.Output(c, "data", curves.Select(x => NodesOf(x).Data).ToArray());
        check.Output(c, "maxDate", members2.Select(h => (object?)h.MaxDate).ToArray());
        check.Output(c, "discounts", members2.Select((h, k) => dates[k].EnumerateArray().Zip(flags[k].EnumerateArray())
            .Select(p => (Func<object?>)(() => h.Discount(new Date(p.First.GetInt32()), p.Second.GetBoolean())))
            .ToArray<object?>()).ToArray());
        check.Output(c, "quoteErrors", curves.Select((x, k) => helpers[k]
            .Select(h => (Func<object?>)(() => h.SetTermStructure(x).QuoteError(context)))
            .ToArray<object?>()).ToArray());
    }

    private static void CheckSteps(VectorAssert check, VectorCase c, EvaluationContext context)
    {
        var handles = new Dictionary<string, MultiCurveMember>();
        var curves = new Dictionary<string, YieldTermStructure?>();
        foreach (JsonElement m in c.Input("members").EnumerateArray())
        {
            string name = m.GetProperty("name").GetString()!;
            handles[name] = HandleOf(m, context);
            JsonElement curve = m.GetProperty("curve");
            curves[name] = curve.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CurveOf(curve);
        }

        var multiCurves = new Dictionary<int, MultiCurve>();
        var built = new HashSet<int>();
        var results = new List<Func<object?>>();
        foreach (JsonElement step in c.Input("steps").EnumerateArray())
        {
            int index = step.GetProperty("multiCurve").GetInt32();

            // QuantLib links a multicurve's members on adding them: build every other multicurve before this step
            foreach (KeyValuePair<int, MultiCurve> other in multiCurves)
            {
                if (other.Key != index && built.Add(other.Key))
                {
                    _ = other.Value.Build(context);
                }
            }

            MultiCurveMember h = handles[step.GetProperty("member").GetString()!];
            YieldTermStructure? curve = curves[step.GetProperty("curve").GetString()!];
            string op = step.GetProperty("op").GetString()!;
            if (op != "addNonBootstrappedCurve")
            {
                throw new InvalidOperationException($"unknown step {op}");
            }

            MultiCurve current = multiCurves.TryGetValue(index, out MultiCurve? m) ? m : new MultiCurve(1e-10);
            try
            {
                multiCurves[index] = current.AddNonBootstrappedCurve(h, curve!);
                results.Add(() => true);
            }
            catch (ArgumentException e)
            {
                results.Add(() => throw e);
            }
        }

        check.Output(c, "results", results.ToArray());
    }

    private static MultiCurveMember HandleOf(JsonElement member, EvaluationContext context)
    {
        JsonElement h = member.GetProperty("handle");
        DayCounter dc = CashFlowInputs.DayCounterOf(member.GetProperty("dayCounter"));
        return h.GetProperty("form").GetString() == "referenceDate"
            ? new MultiCurveMember(new Date(h.GetProperty("referenceDate").GetInt32()), dc)
            : MultiCurveMember.Create(
                h.GetProperty("settlementDays").GetInt32(), CashFlowInputs.CalendarOf(h.GetProperty("calendar")), dc, context);
    }

    private static MultiCurve MultiCurveOf(VectorCase c)
    {
        if (c.Inputs.TryGetProperty("accuracy", out JsonElement accuracy) && accuracy.ValueKind != JsonValueKind.Null)
        {
            return new MultiCurve(accuracy.GetDouble());
        }

        JsonElement e = c.Input("endCriteria");
        EndCriteria? endCriteria = e.ValueKind == JsonValueKind.Null
            ? null
            : new EndCriteria(
                e.GetProperty("maxIterations").GetInt32(),
                e.GetProperty("maxStationaryStateIterations").GetInt32(),
                e.GetProperty("rootEpsilon").GetDouble(),
                e.GetProperty("functionEpsilon").GetDouble(),
                e.GetProperty("gradientNormEpsilon").GetDouble());
        if (c.Input("optimizer").ValueKind != JsonValueKind.Null)
        {
            throw new InvalidOperationException("the multicurve vectors give no optimizer");
        }

        return new MultiCurve(endCriteria: endCriteria);
    }

    // The bootstrapped member a curve spec describes, added in its form.
    private static MultiCurve AddBootstrapped(
        MultiCurve multiCurve, MultiCurveMember h, JsonElement spec, BootstrapHelper<YieldTermStructure>[] helpers) =>
        (spec.GetProperty("traits").GetString(), spec.GetProperty("interpolator").GetProperty("type").GetString()) switch
        {
            ("Discount", "LogLinear") => Add<Discount, LogLinear>(multiCurve, h, spec, helpers),
            ("ZeroYield", "Linear") => Add<ZeroYield, Linear>(multiCurve, h, spec, helpers),
            ("ForwardRate", "BackwardFlat") => Add<ForwardRate, BackwardFlat>(multiCurve, h, spec, helpers),
            var t => throw new InvalidOperationException($"unknown curve {t}"),
        };

    private static MultiCurve Add<TTraits, TInterpolator>(
        MultiCurve multiCurve, MultiCurveMember h, JsonElement spec, BootstrapHelper<YieldTermStructure>[] helpers)
        where TTraits : struct, IYieldTraits
        where TInterpolator : struct, IInterpolator
    {
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        return spec.GetProperty("form").GetString() == "referenceDate"
            ? multiCurve.AddBootstrappedCurve<TTraits, TInterpolator>(
                h, new Date(spec.GetProperty("referenceDate").GetInt32()), helpers, dc)
            : multiCurve.AddBootstrappedCurve<TTraits, TInterpolator>(
                h,
                spec.GetProperty("settlementDays").GetInt32(),
                CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
                helpers,
                dc);
    }

    // A built member's nodes: none for a curve that isn't bootstrapped.
    private static (object?[] Dates, object?[] Data) NodesOf(YieldTermStructure curve) => curve switch
    {
        PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap> x => Nodes(x),
        PiecewiseYieldCurve<ZeroYield, Linear, GlobalBootstrap> x => Nodes(x),
        PiecewiseYieldCurve<ForwardRate, BackwardFlat, GlobalBootstrap> x => Nodes(x),
        _ => ([], []),
    };

    private static (object?[] Dates, object?[] Data) Nodes<TTraits, TInterpolator>(
        IInterpolatedCurve<TTraits, TInterpolator> curve)
        where TTraits : struct, IYieldTraits
        where TInterpolator : struct, IInterpolator =>
        (curve.Dates().Cast<object?>().ToArray(), curve.Data().Cast<object?>().ToArray());
}
