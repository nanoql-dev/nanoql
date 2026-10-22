// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The crypto currencies' vectors, <c>tests/vectors/currencies/crypto.json</c>: each of the 8 currencies'
/// name, codes, symbols, fractions per unit, triangulation currency and rounding.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CryptoVectorTests
{
    private static readonly Dictionary<string, Func<Currency>> Currencies = new()
    {
        ["BTCCurrency"] = () => new BTCCurrency(),
        ["ETHCurrency"] = () => new ETHCurrency(),
        ["ETCCurrency"] = () => new ETCCurrency(),
        ["BCHCurrency"] = () => new BCHCurrency(),
        ["XRPCurrency"] = () => new XRPCurrency(),
        ["LTCCurrency"] = () => new LTCCurrency(),
        ["DASHCurrency"] = () => new DASHCurrency(),
        ["ZECCurrency"] = () => new ZECCurrency(),
    };

    [Fact]
    public void CryptoVector()
    {
        var file = VectorFile.Load("currencies/crypto");
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
