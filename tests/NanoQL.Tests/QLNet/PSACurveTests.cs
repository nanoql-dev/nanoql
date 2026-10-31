// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 , 2010, 2011, 2012  Andrea Maggiulli (a.maggiulli@gmail.com)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: tests/QLNet.Tests/T_PSACurve.cs @ QLNet-v1.13.2
// Ported from QLNet and modified by the nanoql Authors.

using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.QLNet;

[Trait("Oracle", "QLNet")]
public class PSACurveTests
{
    [Fact]
    public void TestCashedValues()
    {
        Date startDate = new Date(1, March, 2007);
        Period period = new Period(360, TimeUnit.Months);
        Calendar calendar = new TARGET();
        Date endDate = calendar.Advance(startDate, period, BusinessDayConvention.Unadjusted);

        // QLNet's case sets no evaluation date, and the schedule, its effective date given, reads none.
        var context = new EvaluationContext { EvaluationDate = new Date(16, September, 2015) };
        Schedule schedule = new Schedule(
            startDate,
            endDate,
            new Period(1, TimeUnit.Months),
            calendar,
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            false,
            context);

        // PSA 100%
        PSACurve psa100 = new PSACurve(startDate);
        double[] listCPR =
        [
            0.2000, 0.4000, 0.6000, 0.8000, 1.0000, 1.2000, 1.4000, 1.6000, 1.8000, 2.0000, 2.2000, 2.4000, 2.6000,
            2.8000, 3.0000, 3.2000, 3.4000, 3.6000, 3.8000, 4.0000, 4.2000, 4.4000, 4.6000, 4.8000, 5.0000, 5.2000,
            5.4000, 5.6000, 5.8000, 6.0000,
        ];

        for (int i = 0; i < schedule.Size; i++)
        {
            if (i <= 29)
            {
                Assert.True(Math.Abs(listCPR[i] - (psa100.GetCPR(schedule[i]) * 100)) <= 0.001);
            }
            else
            {
                Assert.Equal(6.0000, psa100.GetCPR(schedule[i]) * 100);
            }
        }
    }
}
