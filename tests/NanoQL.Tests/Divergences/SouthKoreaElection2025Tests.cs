// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// South Korea's 2025 presidential election day. QuantLib 1.44 misses June 3rd, 2025, when the snap election closed
/// the country and the Korea Exchange; QuantLib's master adds it to the settlement calendar, which KRX inherits
/// (#2894, <c>46c4d4ed9</c>), with the date in both test cases' 2025 lists, which nanoql takes ahead of the pin, as a
/// fix. The 2025 lists of <c>testSouthKoreanSettlement</c> and <c>testKoreaStockExchange</c>
/// (<c>test-suite/calendars.cpp:1772-1788</c>, <c>:2443-2460</c>) move here, with upstream's line; QuantLib 1.44's
/// answer is recorded beside them.
/// </summary>
public class SouthKoreaElection2025Tests
{
    [Fact]
    public void The2025ListsHoldTheElectionDay()
    {
        // upstream's 2025 settlement list, June 3rd added (QuantLib 1.44: a business day)
        Date[] settlement =
        [
            new(1, Month.January, 2025), new(27, Month.January, 2025), new(28, Month.January, 2025),
            new(29, Month.January, 2025), new(30, Month.January, 2025), new(3, Month.March, 2025),
            new(1, Month.May, 2025), new(5, Month.May, 2025), new(6, Month.May, 2025), new(3, Month.June, 2025),
            new(6, Month.June, 2025), new(15, Month.August, 2025), new(3, Month.October, 2025),
            new(6, Month.October, 2025), new(7, Month.October, 2025), new(8, Month.October, 2025),
            new(9, Month.October, 2025), new(25, Month.December, 2025),
        ];
        Date from = new(1, Month.January, 2025), to = new(31, Month.December, 2025);
        Assert.Equal(settlement, new SouthKorea(SouthKoreaMarket.Settlement).HolidayList(from, to));

        // upstream's 2025 Korea Exchange list: the settlement list and the year-end closing
        Date[] krx = [.. settlement, new(31, Month.December, 2025)];
        Assert.Equal(krx, new SouthKorea(SouthKoreaMarket.KRX).HolidayList(from, to));
    }
}
