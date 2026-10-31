// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.QLNet;

/// <summary>
/// The <see cref="AmortizingBond"/>'s vectors, <c>tests/vectors/qlnet/Instruments/Bonds/AmortizingBond.json</c>, QLNet
/// 1.13.2's, on purchases on a coupon date and on the first period's start: the effective rate nanoql solves, and at
/// QLNet's own rate, the amortisations but the last, the carrying amounts at coupon dates and between them.
/// </summary>
[Trait("Oracle", "QLNet")]
public class AmortizingBondVectorTests
{
    [Fact]
    public void AmortizingBondVector()
    {
        var file = VectorFile.Load("qlnet/Instruments/Bonds/AmortizingBond");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            AmortizingBond Build(double? yield) => new(
                c.Double("faceValue"),
                c.Double("marketValue"),
                c.Double("couponRate"),
                new Date(c.Int("issueDate")),
                new Date(c.Int("maturityDate")),
                new Date(c.Int("tradeDate")),
                (Frequency)c.Int("frequency"),
                QLNetBondInputs.DayCounter(c),
                AmortizingMethod.EffectiveInterestRate,
                QLNetBondInputs.Calendar(c),
                context,
                yield);

            // The rate nanoql solves; then everything at QLNet's own rate, its bits.
            AmortizingBond solved = Build(null);
            check.Output(c, "yield", solved.Yield());
            check.Output(c, "isPremium", solved.IsPremium);
            AmortizingBond bond = Build(c.Expected.GetProperty("yield").GetDouble());
            int n = bond.Amortizations.Length;
            check.Output(c, "amortizationDates", bond.Amortizations.Select(a => a.Date.SerialNumber).ToArray());
            check.Output(c, "amortizations", bond.Amortizations.Take(n - 1).Select(a => a.Amount(context)).ToArray());
            check.Output(
                c, "carrying", bond.Amortizations.Take(n - 1).Select(a => bond.AmortizationValue(a.Date)).ToArray());
            check.Output(c, "between", c.Input("betweenDates").EnumerateArray()
                .Select(d => bond.AmortizationValue(new Date(d.GetInt32()))).ToArray());
        }

        check.Complete();
    }
}
