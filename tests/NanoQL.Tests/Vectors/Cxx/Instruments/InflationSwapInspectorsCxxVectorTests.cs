// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's inflation swap and bond inspector vectors,
/// <c>tests/vectors/cxx/instruments/inflationswapinspectors.json</c>: the inspectors of
/// <see cref="ZeroCouponInflationSwap"/> (the inflation leg's calendar and convention defaulting to the fixed leg's),
/// <see cref="YearOnYearInflationSwap"/>, <see cref="CPISwap"/> and <see cref="CPIBond"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InflationSwapInspectorsCxxVectorTests
{
    [Fact]
    public void InflationSwapInspectorsCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/inflationswapinspectors");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            InflationIndex index = InflationInputs.IndexOf(c.Input("index"));
            switch (c.String("kind"))
            {
                case "zeroCouponInflationSwap":
                    ZeroCouponInflationSwap zc = ZeroCouponInflationSwapVectorTests.SwapOf(c, (ZeroInflationIndex)index);
                    check.Output(c, "type", zc.Type);
                    check.Output(c, "nominal", zc.Nominal);
                    check.Output(c, "fixedCalendar", zc.FixedCalendar?.Name);
                    check.Output(c, "fixedConvention", zc.FixedConvention);
                    check.Output(c, "dayCounter", zc.DayCounter.Name);
                    check.Output(c, "fixedRate", zc.FixedRate);
                    check.Output(c, "inflationIndex", zc.InflationIndex.Name);
                    check.Output(c, "observationLag", Lag(zc.ObservationLag));
                    check.Output(c, "observationInterpolation", zc.ObservationInterpolation);
                    check.Output(c, "adjustObservationDates", zc.AdjustObservationDates);
                    check.Output(c, "inflationCalendar", zc.InflationCalendar?.Name);
                    check.Output(c, "inflationConvention", zc.InflationConvention);
                    break;
                case "yearOnYearInflationSwap":
                    YearOnYearInflationSwap yoy =
                        YearOnYearInflationSwapVectorTests.SwapOf(c, (YoYInflationIndex)index, context);
                    check.Output(c, "type", yoy.Type);
                    check.Output(c, "nominal", yoy.Nominal);
                    check.Output(c, "fixedSchedule", yoy.FixedSchedule.Dates);
                    check.Output(c, "fixedRate", yoy.FixedRate);
                    check.Output(c, "fixedDayCount", yoy.FixedDayCount.Name);
                    check.Output(c, "yoySchedule", yoy.YoYSchedule.Dates);
                    check.Output(c, "yoyInflationIndex", yoy.YoYInflationIndex.Name);
                    check.Output(c, "observationLag", Lag(yoy.ObservationLag));
                    check.Output(c, "spread", yoy.Spread);
                    check.Output(c, "yoyDayCount", yoy.YoYDayCount?.Name);
                    check.Output(c, "paymentCalendar", yoy.PaymentCalendar?.Name);
                    check.Output(c, "paymentConvention", yoy.PaymentConvention);
                    break;
                case "cpiSwap":
                    CPISwap cpi = CPISwapVectorTests.SwapOf(c, (ZeroInflationIndex)index, context);
                    check.Output(c, "type", cpi.Type);
                    check.Output(c, "nominal", cpi.Nominal);
                    check.Output(c, "subtractInflationNominal", cpi.SubtractInflationNominal);
                    check.Output(c, "spread", cpi.Spread);
                    check.Output(c, "floatDayCount", cpi.FloatDayCount?.Name);
                    check.Output(c, "floatSchedule", cpi.FloatSchedule.Dates);
                    check.Output(c, "floatPaymentRoll", cpi.FloatPaymentRoll);
                    check.Output(c, "fixingDays", cpi.FixingDays);
                    check.Output(c, "floatIndex", cpi.FloatIndex?.Name);
                    check.Output(c, "fixedRate", cpi.FixedRate);
                    check.Output(c, "baseCPI", cpi.BaseCPI);
                    check.Output(c, "fixedDayCount", cpi.FixedDayCount?.Name);
                    check.Output(c, "fixedSchedule", cpi.FixedSchedule.Dates);
                    check.Output(c, "fixedPaymentRoll", cpi.FixedPaymentRoll);
                    check.Output(c, "observationLag", Lag(cpi.ObservationLag));
                    check.Output(c, "fixedIndex", cpi.FixedIndex.Name);
                    check.Output(c, "observationInterpolation", cpi.ObservationInterpolation);
                    check.Output(c, "inflationNominal", cpi.InflationNominal);
                    break;
                default:
                    CPIBond bond = CPIBondVectorTests.BondOf(c.Input("bond"), (ZeroInflationIndex)index, context);
                    check.Output(c, "frequency", bond.Frequency);
                    check.Output(c, "dayCounter", bond.DayCounter.Name);
                    check.Output(c, "growthOnly", bond.GrowthOnly);
                    check.Output(c, "baseCPI", bond.BaseCPI);
                    check.Output(c, "observationLag", Lag(bond.ObservationLag));
                    check.Output(c, "cpiIndex", bond.CpiIndex.Name);
                    check.Output(c, "observationInterpolation", bond.ObservationInterpolation);
                    break;
            }
        }

        check.Complete();
    }

    private static int[] Lag(Period p) => [p.Length, (int)p.Units];
}
