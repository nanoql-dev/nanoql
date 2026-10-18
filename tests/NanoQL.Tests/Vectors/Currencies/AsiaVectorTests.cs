// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Asian currencies' vectors, <c>tests/vectors/currencies/asia.json</c>: each of the 29 currencies'
/// name, codes, symbols, fractions per unit, triangulation currency and rounding.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AsiaVectorTests
{
    private static readonly Dictionary<string, Func<Currency>> Currencies = new()
    {
        ["BDTCurrency"] = () => new BDTCurrency(),
        ["CNYCurrency"] = () => new CNYCurrency(),
        ["HKDCurrency"] = () => new HKDCurrency(),
        ["IDRCurrency"] = () => new IDRCurrency(),
        ["ILSCurrency"] = () => new ILSCurrency(),
        ["INRCurrency"] = () => new INRCurrency(),
        ["IQDCurrency"] = () => new IQDCurrency(),
        ["IRRCurrency"] = () => new IRRCurrency(),
        ["JPYCurrency"] = () => new JPYCurrency(),
        ["KRWCurrency"] = () => new KRWCurrency(),
        ["KWDCurrency"] = () => new KWDCurrency(),
        ["KZTCurrency"] = () => new KZTCurrency(),
        ["MYRCurrency"] = () => new MYRCurrency(),
        ["NPRCurrency"] = () => new NPRCurrency(),
        ["PKRCurrency"] = () => new PKRCurrency(),
        ["SARCurrency"] = () => new SARCurrency(),
        ["SGDCurrency"] = () => new SGDCurrency(),
        ["THBCurrency"] = () => new THBCurrency(),
        ["TWDCurrency"] = () => new TWDCurrency(),
        ["VNDCurrency"] = () => new VNDCurrency(),
        ["QARCurrency"] = () => new QARCurrency(),
        ["BHDCurrency"] = () => new BHDCurrency(),
        ["OMRCurrency"] = () => new OMRCurrency(),
        ["JODCurrency"] = () => new JODCurrency(),
        ["AEDCurrency"] = () => new AEDCurrency(),
        ["PHPCurrency"] = () => new PHPCurrency(),
        ["CNHCurrency"] = () => new CNHCurrency(),
        ["LKRCurrency"] = () => new LKRCurrency(),
        ["UZSCurrency"] = () => new UZSCurrency(),
    };

    [Fact]
    public void AsiaVector()
    {
        var file = VectorFile.Load("currencies/asia");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Currency currency = Currencies[c.String("currency")]();
            check.Output(c, "name", currency.Name);
            check.Output(c, "code", currency.Code);
            check.Output(c, "numericCode", currency.NumericCode);
            check.Output(c, "symbol", currency.Symbol);
            check.Output(c, "fractionSymbol", currency.FractionSymbol);
            check.Output(c, "fractionsPerUnit", currency.FractionsPerUnit);
            check.Output(c, "triangulationCurrency", currency.TriangulationCurrency?.Code);
            check.Output(c, "rounding", c.Doubles("rounded").Select(x => (object?)currency.Rounding.Evaluate(x)));
        }

        check.Complete();
    }
}
