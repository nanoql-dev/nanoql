// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's spread-base vectors,
/// <c>tests/vectors/cxx/pricingengines/basket/spreadblackscholesvanillaengine.json</c>: what
/// <see cref="SpreadBlackScholesVanillaEngine"/> hands its derived engine's formula (the forwards, strike, type,
/// variances, discount factor and correlation), read through a recording engine that mirrors the harness's own, and the
/// base's refusals with QuantLib's messages.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SpreadBlackScholesVanillaEngineCxxVectorTests
{
    [Fact]
    public void SpreadBlackScholesVanillaEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/basket/spreadblackscholesvanillaengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Date today = context.EvaluationDate;
            var engine = new RecordingSpreadEngine(
                ProcessOf(c.Input("process1"), today), ProcessOf(c.Input("process2"), today), c.Double("correlation"));
            JsonElement p = c.Input("payoff");
            var type = (OptionType)p[2].GetInt32();
            double strike = p[3].GetDouble();
            StrikedTypePayoff basePayoff = p[1].GetString() == "PlainVanilla"
                ? new PlainVanillaPayoff(type, strike)
                : new CashOrNothingPayoff(type, strike, 1.0);
            BasketPayoff payoff = p[0].GetString() == "Spread" ? new SpreadBasketPayoff(basePayoff) : new MaxBasketPayoff(basePayoff);
            JsonElement e = c.Input("exercise");
            var date = new Date(e[1].GetInt32());
            Exercise exercise = e[0].GetString() == "European" ? new EuropeanExercise(date) : new AmericanExercise(today, date);
            var option = new BasketOption(payoff, exercise);
            if (c.Expected.TryGetProperty("error", out _))
            {
                check.Output(c, "error", () => Message(() => engine.Calculate(option, context)));
                continue;
            }

            var npv = new Lazy<double?>(() => engine.Calculate(option, context).NPV);
            check.Output(c, "NPV", () => npv.Value);
            check.Output(c, "f1", () => Recorded(npv, engine).F1);
            check.Output(c, "f2", () => Recorded(npv, engine).F2);
            check.Output(c, "strike", () => Recorded(npv, engine).Strike);
            check.Output(c, "optionType", () => (int)Recorded(npv, engine).Type);
            check.Output(c, "variance1", () => Recorded(npv, engine).Variance1);
            check.Output(c, "variance2", () => Recorded(npv, engine).Variance2);
            check.Output(c, "df", () => Recorded(npv, engine).Df);
            check.Output(c, "rho", () => Recorded(npv, engine).Rho);
        }

        check.Complete();
    }

    // The harness's process: [spot, r, q, volatility] on flat Act/365 (Fixed) forwards from the evaluation date, the
    // volatility a number or [dates, strikes, rows] (a row per strike, a volatility per date).
    private static BlackScholesMertonProcess ProcessOf(JsonElement s, Date today)
    {
        DayCounter dc = new Actual365Fixed();
        BlackVolTermStructure vol;
        JsonElement v = s[3];
        if (v.ValueKind == JsonValueKind.Number)
        {
            vol = new BlackConstantVol(today, new NullCalendar(), v.GetDouble(), dc);
        }
        else
        {
            Date[] dates = [.. v[0].EnumerateArray().Select(d => new Date(d.GetInt32()))];
            double[] strikes = Doubles(v[1]);
            double[][] rows = [.. v[2].EnumerateArray().Select(Doubles)];
            var m = new Matrix(strikes.Length, dates.Length);
            for (int i = 0; i < strikes.Length; ++i)
            {
                for (int j = 0; j < dates.Length; ++j)
                {
                    m[i, j] = rows[i][j];
                }
            }

            vol = new BlackVarianceSurface(today, new NullCalendar(), dates, strikes, m, dc);
        }

        return new BlackScholesMertonProcess(
            s[0].GetDouble(), new FlatForward(today, s[2].GetDouble(), dc), new FlatForward(today, s[1].GetDouble(), dc), vol);
    }

    private static Arguments Recorded(Lazy<double?> npv, RecordingSpreadEngine engine)
    {
        _ = npv.Value;
        return engine.Last ?? throw new InvalidOperationException("the formula wasn't called");
    }

    // The refusal's message, QuantLib's text.
    private static string Message(Action act)
    {
        try
        {
            act();
        }
        catch (NanoQLException e)
        {
            return e.Message;
        }

        return "no refusal";
    }

    private sealed record Arguments(
        double F1, double F2, double Strike, OptionType Type, double Variance1, double Variance2, double Df, double Rho);

    // The harness's derived engine: records the formula's arguments and returns df·(f1 − f2 − strike) + rho.
    private sealed class RecordingSpreadEngine(
        GeneralizedBlackScholesProcess process1, GeneralizedBlackScholesProcess process2, double correlation)
        : SpreadBlackScholesVanillaEngine(process1, process2, correlation)
    {
        public Arguments? Last { get; private set; }

        protected override double Calculate(
            double f1, double f2, double strike, OptionType optionType, double variance1, double variance2, double df)
        {
            Last = new Arguments(f1, f2, strike, optionType, variance1, variance2, df, Rho);
            return (df * (f1 - f2 - strike)) + Rho;
        }
    }
}
