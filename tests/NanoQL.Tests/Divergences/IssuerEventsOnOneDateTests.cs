// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An issuer keeps every event, two on one date in the order given (the S-xcr plan's XR8 (iv)): QuantLib's event set
/// orders by date alone (<c>issuer.hpp:36-38</c>), so it keeps the first event of a date and drops the second, where its
/// own documentation lets several events take place on one date. QuantLib's values are the S-xcr run's
/// <c>c1_facts.cpp</c>; the check is QuantLib's issuer with each event alone.
/// </summary>
public class IssuerEventsOnOneDateTests
{
    [Fact]
    public void BothEventsOnADateCount()
    {
        // QuantLib, EUR then USD bankruptcies on 1 June 2006: set size 1; the EUR key finds 1 event, the USD key 0
        var eur = new BankruptcyEvent(new Date(1, June, 2006), new EURCurrency(), Seniority.SeniorSec, null, 0.4);
        var usd = new BankruptcyEvent(new Date(1, June, 2006), new USDCurrency(), Seniority.SeniorSec, null, 0.4);
        var both = new Issuer(null, [eur, usd]);
        var start = new Date(1, January, 2006);
        var kEur = new NorthAmericaCorpDefaultKey(new EURCurrency(), Seniority.SeniorSec, new Period(0, TimeUnit.Days), 1.0);
        var kUsd = new NorthAmericaCorpDefaultKey(new USDCurrency(), Seniority.SeniorSec, new Period(0, TimeUnit.Days), 1.0);

        Assert.Equal(new DefaultEvent[] { eur, usd }, both.Events);
        Assert.Equal(new DefaultEvent[] { eur }, both.DefaultsBetween(start, Today, kEur, false, Context));
        Assert.Equal(new DefaultEvent[] { usd }, both.DefaultsBetween(start, Today, kUsd, false, Context));
        Assert.Equal(new Issuer(null, [usd]).DefaultsBetween(start, Today, kUsd, false, Context), both.DefaultsBetween(start, Today, kUsd, false, Context));
    }
}
