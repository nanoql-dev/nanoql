// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's swaption volatility structure vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/swaption/swaptionvolstructure.json</c>: on constant structures and on
/// the harness's own structure, which keeps <see cref="SwaptionVolatilityStructure"/>'s defaults,
/// <see cref="SwaptionVolatilityStructure.MaxSwapTenor"/>, <see cref="SwaptionVolatilityStructure.MaxSwapLength"/>, the
/// volatility type and both <c>SwapLength</c> overloads; the volatility and Black variance overloads the wheel doesn't
/// bind, and on the harness's structure every overload of the volatility, Black variance, shift and smile section; and
/// the smile sections of the six overloads asked to extrapolate past the structure's range, three of which refuse.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwaptionVolStructureCxxVectorTests
{
    private static readonly string[] Overloads =
        ["(Period,Period)", "(Date,Period)", "(Time,Period)", "(Period,Time)", "(Date,Time)", "(Time,Time)"];

    [Fact]
    public void SwaptionVolStructureCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/swaption/swaptionvolstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            SwaptionVolatilityStructure s = VolatilityInputs.SwaptionVolatilityOf(c.Input("surface"), c.Context());
            switch (c.String("kind"))
            {
                case "limits":
                    CheckLimits(check, c, s);
                    break;
                case "overloads":
                    CheckOverloads(check, c, s);
                    break;
                case "extrapolation":
                    Pair[] pairs = [.. c.Input("pairs").EnumerateArray().Select(Pair.Of)];
                    double strike = c.Double("strike");
                    check.Output(
                        c,
                        "smileSections",
                        pairs.Select(p => Overloads.Select(o => Lazy(() => SmileSummary(s, o, p, true, strike))).ToArray()).ToArray());
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static void CheckLimits(VectorAssert check, VectorCase c, SwaptionVolatilityStructure s)
    {
        Period[] tenors = [.. c.Input("tenors").EnumerateArray().Select(VolatilityInputs.TenorOf)];
        Date[][] datePairs = [.. c.Input("datePairs").EnumerateArray().Select(p => new[] { new Date(p[0].GetInt32()), new Date(p[1].GetInt32()) })];

        check.Output(c, "maxSwapTenor", new object[] { s.MaxSwapTenor.Length, s.MaxSwapTenor.Units });
        check.Output(c, "maxSwapLength", () => s.MaxSwapLength());
        check.Output(c, "volatilityType", s.VolatilityType);
        check.Output(c, "swapLengthTenors", tenors.Select(t => Lazy(() => s.SwapLength(t))).ToArray());
        check.Output(c, "swapLengthDates", datePairs.Select(d => Lazy(() => s.SwapLength(d[0], d[1]))).ToArray());
    }

    private static void CheckOverloads(VectorAssert check, VectorCase c, SwaptionVolatilityStructure s)
    {
        Pair[] pairs = [.. c.Input("pairs").EnumerateArray().Select(Pair.Of)];
        foreach (JsonProperty output in c.Expected.EnumerateObject())
        {
            string name = output.Name;
            string overload = name[name.IndexOf('(', StringComparison.Ordinal)..];
            string member = name[..name.IndexOf('(', StringComparison.Ordinal)];
            Func<Pair, object?> f = member switch
            {
                "volatility" => p => Volatility(s, overload, p, false),
                "blackVariance" => p => Volatility(s, overload, p, true),
                "shift" => p => Shift(s, overload, p),
                "smileSection" => p => SmileSummary(s, overload, p, false, p.Strike),
                _ => throw new InvalidOperationException($"unexpected output '{name}'"),
            };
            check.Output(c, name, pairs.Select(p => Lazy(() => f(p))).ToArray());
        }
    }

    private static double Volatility(SwaptionVolatilityStructure s, string overload, Pair p, bool variance) => overload switch
    {
        "(Period,Period)" => variance
            ? s.BlackVariance(p.Tenor, p.SwapTenor, p.Strike, p.Extrapolate)
            : s.Volatility(p.Tenor, p.SwapTenor, p.Strike, p.Extrapolate),
        "(Date,Period)" => variance
            ? s.BlackVariance(p.Date, p.SwapTenor, p.Strike, p.Extrapolate)
            : s.Volatility(p.Date, p.SwapTenor, p.Strike, p.Extrapolate),
        "(Time,Period)" => variance
            ? s.BlackVariance(p.Time, p.SwapTenor, p.Strike, p.Extrapolate)
            : s.Volatility(p.Time, p.SwapTenor, p.Strike, p.Extrapolate),
        "(Period,Time)" => variance
            ? s.BlackVariance(p.Tenor, p.SwapLength, p.Strike, p.Extrapolate)
            : s.Volatility(p.Tenor, p.SwapLength, p.Strike, p.Extrapolate),
        "(Date,Time)" => variance
            ? s.BlackVariance(p.Date, p.SwapLength, p.Strike, p.Extrapolate)
            : s.Volatility(p.Date, p.SwapLength, p.Strike, p.Extrapolate),
        "(Time,Time)" => variance
            ? s.BlackVariance(p.Time, p.SwapLength, p.Strike, p.Extrapolate)
            : s.Volatility(p.Time, p.SwapLength, p.Strike, p.Extrapolate),
        _ => throw new InvalidOperationException($"unexpected overload '{overload}'"),
    };

    private static double Shift(SwaptionVolatilityStructure s, string overload, Pair p) => overload switch
    {
        "(Period,Period)" => s.Shift(p.Tenor, p.SwapTenor, p.Extrapolate),
        "(Date,Period)" => s.Shift(p.Date, p.SwapTenor, p.Extrapolate),
        "(Time,Period)" => s.Shift(p.Time, p.SwapTenor, p.Extrapolate),
        "(Period,Time)" => s.Shift(p.Tenor, p.SwapLength, p.Extrapolate),
        "(Date,Time)" => s.Shift(p.Date, p.SwapLength, p.Extrapolate),
        "(Time,Time)" => s.Shift(p.Time, p.SwapLength, p.Extrapolate),
        _ => throw new InvalidOperationException($"unexpected overload '{overload}'"),
    };

    private static object?[] SmileSummary(SwaptionVolatilityStructure s, string overload, Pair p, bool extrapolate, double strike)
    {
        SmileSection section = overload switch
        {
            "(Period,Period)" => s.SmileSection(p.Tenor, p.SwapTenor, extrapolate),
            "(Date,Period)" => s.SmileSection(p.Date, p.SwapTenor, extrapolate),
            "(Time,Period)" => s.SmileSection(p.Time, p.SwapTenor, extrapolate),
            "(Period,Time)" => s.SmileSection(p.Tenor, p.SwapLength, extrapolate),
            "(Date,Time)" => s.SmileSection(p.Date, p.SwapLength, extrapolate),
            "(Time,Time)" => s.SmileSection(p.Time, p.SwapLength, extrapolate),
            _ => throw new InvalidOperationException($"unexpected overload '{overload}'"),
        };
        return SmileSectionInputs.Summary(section, strike);
    }

    // A value computed when compared, so that a throw is checked.
    private static Func<object?> Lazy<T>(Func<T> f) => () => f();

    // An option-and-swap pair of the inputs.
    private sealed record Pair(
        Period Tenor, Date Date, double Time, Period SwapTenor, double SwapLength, double Strike, bool Extrapolate)
    {
        public static Pair Of(JsonElement p) => new(
            VolatilityInputs.TenorOf(p.GetProperty("optionTenor")),
            new Date(p.GetProperty("optionDate").GetInt32()),
            IndexInputs.Real(p.GetProperty("optionTime")),
            VolatilityInputs.TenorOf(p.GetProperty("swapTenor")),
            IndexInputs.Real(p.GetProperty("swapLength")),
            IndexInputs.Real(p.GetProperty("strike")),
            p.GetProperty("extrapolate").GetBoolean());
    }
}
