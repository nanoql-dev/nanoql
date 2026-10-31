// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Extensions.Coupons;
using NanoQL.Extensions.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.QLNet;

/// <summary>
/// The <see cref="MBSFixedRateBond"/>'s vectors, <c>tests/vectors/qlnet/Instruments/Bonds/MBSFixedRateBond.json</c>,
/// QLNet 1.13.2's: each pool's flows, nominals, payment dates and bond factors, its yields and
/// <see cref="MBSFixedRateBondFunctions.PVDifference"/>. A case holds the flows QLNet computes, up to its first NaN.
/// </summary>
[Trait("Oracle", "QLNet")]
public class MBSFixedRateBondVectorTests
{
    [Fact]
    public void MBSFixedRateBondVector()
    {
        var file = VectorFile.Load("qlnet/Instruments/Bonds/MBSFixedRateBond");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var start = new Date(c.Int("startDate"));
            var bond = new MBSFixedRateBond(
                c.Int("settlementDays"),
                QLNetBondInputs.Calendar(c),
                c.Double("faceAmount"),
                start,
                new Period(c.Int("bondTenorMonths"), TimeUnit.Months),
                new Period(c.Int("originalLengthMonths"), TimeUnit.Months),
                (Frequency)c.Int("frequency"),
                c.Double("wacRate"),
                c.Double("passThroughRate"),
                new Thirty360(Thirty360Convention.BondBasis),
                QLNetBondInputs.Model(c),
                (BusinessDayConvention)c.Int("paymentConvention"));
            ImmutableArray<CashFlow> flows = bond.ExpectedCashflows;
            int n = c.Expected.GetProperty("paymentDates").GetArrayLength();
            var coupons = flows.OfType<FixedRateCoupon>().Take(n).ToList();
            check.Output(c, "periods", flows.Length / 3);
            check.Output(c, "paymentDates", coupons.Select(x => x.Date.SerialNumber).ToArray());
            check.Output(c, "coupons", coupons.Select(x => x.Amount(context)).ToArray());
            check.Output(c, "couponNominals", coupons.Select(x => x.ContractNominal).ToArray());
            var scheduled = flows.OfType<AmortizingPayment>().Take(n).Select(x => x.Amount(context)).ToArray();
            check.Output(c, "scheduledPrincipal", scheduled);
            check.Output(
                c, "prepayments", flows.OfType<VoluntaryPrepay>().Take(n).Select(x => x.Amount(context)).ToArray());
            check.Output(c, "bondFactors", bond.BondFactors.ToArray());
            if (c.Expected.TryGetProperty("monthlyYield", out _))
            {
                check.Output(c, "monthlyYield", () => bond.MonthlyYield(context));
                check.Output(c, "bondEquivalentYield", () => bond.BondEquivalentYield(context));
            }

            if (c.Expected.TryGetProperty("pvDifference", out _))
            {
                double face = c.Double("faceAmount");
                double[] yields = c.Input("pvDifferenceYields").EnumerateArray().Select(y => y.GetDouble()).ToArray();
                check.Output(c, "pvDifference", yields
                    .Select(y => MBSFixedRateBondFunctions.PVDifference(face, flows, y, start, context)).ToArray());
            }
        }

        check.Complete();
    }
}
