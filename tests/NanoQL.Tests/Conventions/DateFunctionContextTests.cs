// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="IMM"/>'s and <see cref="ASX"/>'s functions whose date QuantLib defaults to the evaluation date take the
/// context and the date as a nullable after it: the context's evaluation date is read only when the date is
/// <see langword="null"/>, so a date given gives the same answer under any context.
/// </summary>
public class DateFunctionContextTests
{
    [Fact]
    public void TheDateFunctionsReadTheContextOnlyForANullDate()
    {
        var january = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        var july = new EvaluationContext { EvaluationDate = new Date(15, Month.July, 2025) };
        var d = new Date(1, Month.March, 2025);

        // a date given
        Assert.Equal(IMM.NextDate(january, d), IMM.NextDate(july, d));
        Assert.Equal(IMM.NextCode(january, d, false), IMM.NextCode(july, d, false));
        Assert.Equal(IMM.Date("H5", january, d), IMM.Date("H5", july, d));
        Assert.Equal(IMM.NextDate("H5", january, true, d), IMM.NextDate("H5", july, true, d));
        Assert.Equal(IMM.NextCode("H5", january, true, d), IMM.NextCode("H5", july, true, d));
        Assert.Equal(ASX.NextDate(january, d), ASX.NextDate(july, d));
        Assert.Equal(ASX.NextCode(january, d, false), ASX.NextCode(july, d, false));
        Assert.Equal(ASX.Date("H5", january, d), ASX.Date("H5", july, d));
        Assert.Equal(ASX.NextDate("H5", january, true, d), ASX.NextDate("H5", july, true, d));
        Assert.Equal(ASX.NextCode("H5", january, true, d), ASX.NextCode("H5", july, true, d));

        // a null date: the context's evaluation date
        Assert.Equal(IMM.NextDate(january, january.EvaluationDate), IMM.NextDate(january));
        Assert.Equal(new Date(19, Month.March, 2025), IMM.NextDate(january));
        Assert.Equal(new Date(17, Month.September, 2025), IMM.NextDate(july));
        Assert.Equal(IMM.Date("H5", january, january.EvaluationDate), IMM.Date("H5", january));
        Assert.Equal(new Date(9, Month.March, 2035), ASX.Date("H5", july));
        Assert.Equal(ASX.NextDate(july, july.EvaluationDate, false), ASX.NextDate(july, mainCycle: false));
        Assert.Equal("U5", ASX.NextCode(july));

        // the context is checked first, whatever the date
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => IMM.NextDate(null!, d)).ParamName);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => ASX.Date("H5", null!, d)).ParamName);
    }
}
