// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's generic inflation index vectors, <c>tests/vectors/cxx/experimental/inflation/genericindexes.json</c>:
/// a generic CPI and a generic year-on-year CPI, their names, frequencies, flags, lags, region and currency.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GenericIndexesCxxVectorTests
{
    [Fact]
    public void GenericIndexesCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/inflation/genericindexes");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var frequency = (Frequency)c.Input("frequency").GetInt32();
            bool revised = c.Input("revised").GetBoolean();
            Period lag = InflationInputs.LagOf(c.Input("lag"));
            Currency ccy = c.String("currency") switch
            {
                "EUR" => new EURCurrency(),
                "USD" => new USDCurrency(),
                _ => new GBPCurrency(),
            };
            check.Output(c, "cpi", Describe(new GenericCPI(frequency, revised, lag, ccy)));
            check.Output(c, "yy", Describe(new YYGenericCPI(frequency, revised, lag, ccy)));
        }

        check.Complete();
    }

    private static object?[] Describe(InflationIndex i) =>
    [
        i.Name, i.FamilyName, (int)i.Frequency, i.Revised, i.AvailabilityLag.Length, (int)i.AvailabilityLag.Units,
        i.Region.Name, i.Region.Code, i.Currency.Code,
    ];
}
