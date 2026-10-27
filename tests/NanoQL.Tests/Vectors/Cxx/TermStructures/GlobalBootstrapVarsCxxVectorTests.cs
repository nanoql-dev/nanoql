// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's vectors of the global bootstrap's additional variables,
/// <c>tests/vectors/cxx/termstructures/globalbootstrapvars.json</c>: <see cref="SimpleQuoteVariables"/>' guesses and
/// values; and QuantLib's testGlobalBootstrapVariables and variations, the futures built by
/// <see cref="GlobalBootstrap.VariableHelpers"/> on the volatility the bootstrap optimizes, after the curve's helpers,
/// the curve's nodes, the volatility at the optimum (<see cref="PiecewiseYieldCurve{TTraits, TInterpolator, TBootstrap}.AdditionalVariables"/>)
/// and the helpers' quote errors.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GlobalBootstrapVarsCxxVectorTests
{
    [Fact]
    public void GlobalBootstrapVarsCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/globalbootstrapvars");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.String("kind") == "variables")
            {
                var variables = new SimpleQuoteVariables(
                    c.Int("count"),
                    c.Doubles("initialGuesses"),
                    c.Input("lowerBounds").EnumerateArray()
                        .Select(b => b.ValueKind == JsonValueKind.Null ? (double?)null : b.GetDouble()));
                check.Output(c, "initialize", variables.Initialize(false).ToArray());
                check.Output(c, "values", variables.Values(c.Doubles("x")).ToArray());
                continue;
            }

            Bootstrap(check, c);
        }

        check.Complete();
    }

    private static void Bootstrap(VectorAssert check, VectorCase c)
    {
        EvaluationContext context = c.Context();
        JsonElement spec = c.Input("curve"), futures = c.Input("futures"), variable = c.Input("variable");
        BootstrapHelper<YieldTermStructure>[] helpers = RateHelperInputs.HelpersOf(c.Input("helpers"), context);
        BootstrapHelper<YieldTermStructure>[] additional = RateHelperInputs.HelpersOf(c.Input("additionalHelpers"), context);
        IborIndex index = IndexInputs.IborIndexOf(futures.GetProperty("index"));
        Date[] dates = [.. futures.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
        double[] prices = [.. futures.GetProperty("prices").EnumerateArray().Select(p => p.GetDouble())];
        double meanReversion = futures.GetProperty("meanReversion").GetDouble();
        double weight = c.Double("penaltyWeight");
        JsonElement bound = variable.GetProperty("lowerBound");

        ImmutableArray<IBootstrapHelper> Futures(ImmutableArray<double> vol, EvaluationContext x)
        {
            var built = ImmutableArray.CreateBuilder<IBootstrapHelper>(dates.Length);
            for (int i = 0; i < dates.Length; i++)
            {
                var convAdj = new FuturesConvAdjustmentQuote(index, dates[i], prices[i], vol[0], meanReversion, x);
                built.Add(new FuturesRateHelper(prices[i], dates[i], index, convAdj.Value, FuturesType.IMM));
            }

            return built.MoveToImmutable();
        }

        Vector Penalties(IBootstrapState state, EvaluationContext x)
        {
            var errors = new Vector(state.AdditionalHelpers.Count);
            for (int i = 0; i < errors.Size; i++)
            {
                errors[i] = weight * state.AdditionalHelpers[i].QuoteError(x);
            }

            return errors;
        }

        var curve = new PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>(
            new Date(spec.GetProperty("referenceDate").GetInt32()),
            helpers,
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            context,
            i: default(LogLinear),
            bootstrap: new GlobalBootstrap(
                accuracy: c.Double("accuracy"),
                additionalHelpers: additional,
                additionalPenalties: Penalties,
                additionalVariables: new SimpleQuoteVariables(
                    1,
                    [variable.GetProperty("guess").GetDouble()],
                    bound.ValueKind == JsonValueKind.Null ? [] : [bound.GetDouble()]),
                variableHelpers: Futures));
        check.Output(c, "dates", curve.Dates().Cast<object?>().ToArray());
        check.Output(c, "data", curve.Data().Cast<object?>().ToArray());
        check.Output(c, "variable", curve.AdditionalVariables[0]);
        ImmutableArray<IBootstrapHelper> optimumFutures = Futures(curve.AdditionalVariables, context);
        check.Output(
            c,
            "quoteErrors",
            helpers.Select(h => h.SetTermStructure(curve).QuoteError(context))
                .Concat(optimumFutures.Select(h => ((BootstrapHelper<YieldTermStructure>)h).SetTermStructure(curve).QuoteError(context)))
                .ToArray());
        check.Output(c, "additionalQuoteErrors", additional.Select(h => h.SetTermStructure(curve).QuoteError(context)).ToArray());
    }
}
