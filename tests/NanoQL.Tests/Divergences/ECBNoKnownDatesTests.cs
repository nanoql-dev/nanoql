// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// ECB's functions with no known dates. QuantLib's refusal "ECB dates after ... are unknown" reads
/// <c>*knownDates().rbegin()</c> (<c>ecb.cpp:233-234</c>, <c>:245-246</c>), which is undefined on an empty set, as
/// <c>removeDate</c> can leave it. nanoql refuses with "no ECB dates are known", the type QuantLib's refusal has.
/// </summary>
public class ECBNoKnownDatesTests
{
    [Fact]
    public void AnEmptySetIsRefused()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
        ECB empty = ECB.Default;
        foreach (Date d in ECB.Default.KnownDates)
        {
            empty = empty.RemoveDate(d);
        }

        Assert.Empty(empty.KnownDates);
        Action[] reads =
        [
            () => empty.NextDate(context, new Date(1, Month.January, 2005)),
            () => empty.NextDates(context, new Date(1, Month.January, 2005)),
            () => empty.IsECBdate(new Date(19, Month.January, 2005)),
            () => empty.Date(Month.January, 2005),
            () => empty.Code(new Date(19, Month.January, 2005)),
            () => empty.NextCode(context),
        ];
        foreach (Action read in reads)
        {
            NanoQLException e = Assert.Throws<NanoQLException>(read);
            Assert.Equal("no ECB dates are known", e.Message);
        }
    }
}
