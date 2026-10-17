// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The interest-rate vectors, <c>tests/vectors/interestrate.json</c>: rates under four day counters, five compoundings
/// and seven frequencies. A rate that fails to construct has that failure as its one output; every other gives its
/// frequency, compound and discount factors at times and between dates, equivalent rates, an implied rate and its text.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterestRateVectorTests
{
    [Fact]
    public void InterestRateVector() => Check("interestrate");

    /// <summary>
    /// Compares nanoql with one interest-rate vector file: the wheel's, or its twin from the C++ harness
    /// (<see cref="InterestRateCxxVectorTests"/>).
    /// </summary>
    /// <param name="path">The file's path under <c>tests/vectors</c>, without <c>.json</c>.</param>
    internal static void Check(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double r = c.Double("rate");
            DayCounter dc = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
            var comp = (Compounding)c.Int("compounding");
            var freq = (Frequency)c.Int("frequency");
            if (c.Expected.TryGetProperty("interestRate", out _))
            {
                check.Output(c, "interestRate", () => new InterestRate(r, dc, comp, freq));
                continue;
            }

            var ir = new InterestRate(r, dc, comp, freq);
            double[] times = c.Doubles("times");
            Date d1 = c.DateInput("d1");
            double equivalentTime = c.Double("equivalentTime");
            double impliedCompound = c.Double("impliedCompound"), impliedTime = c.Double("impliedTime");
            check.Output(c, "frequency", ir.Frequency);
            check.Output(c, "compoundFactor", Each(times, t => ir.CompoundFactor(t)));
            check.Output(c, "discountFactor", Each(times, t => ir.DiscountFactor(t)));
            check.Output(c, "compoundFactorDates", Each(c.DateInputs("d2"), d2 => ir.CompoundFactor(d1, d2)));
            check.Output(
                c,
                "equivalentRate",
                c.Input("equivalents").EnumerateArray()
                    .Select(e => (Comp: (Compounding)e[0].GetInt32(), Freq: (Frequency)e[1].GetInt32()))
                    .Select(e => (Func<object?>)(() => ir.EquivalentRate(e.Comp, e.Freq, equivalentTime).Rate))
                    .ToArray());
            JsonElement equivalentDates = c.Input("equivalentDates");
            DayCounter other = CashFlowInputs.DayCounterOf(equivalentDates.GetProperty("dayCounter"));
            var otherComp = (Compounding)equivalentDates.GetProperty("compounding").GetInt32();
            var otherFreq = (Frequency)equivalentDates.GetProperty("frequency").GetInt32();
            check.Output(
                c,
                "equivalentRateDates",
                Each(c.DateInputs("d2"), d2 => ir.EquivalentRate(other, otherComp, otherFreq, d1, d2).Rate));
            check.Output(
                c,
                "impliedRate",
                () => InterestRate.ImpliedRate(impliedCompound, dc, comp, freq, impliedTime).Rate);
            check.Output(c, "toString", () => ir.ToString());
        }

        check.Complete();
    }

    private static Func<object?>[] Each<TIn, TOut>(TIn[] inputs, Func<TIn, TOut> f) =>
        inputs.Select(x => (Func<object?>)(() => f(x))).ToArray();
}
