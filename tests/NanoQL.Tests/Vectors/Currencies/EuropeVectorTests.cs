// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The European currencies' vectors, <c>tests/vectors/currencies/europe.json</c>: each of the 42 currencies'
/// name, codes, symbols, fractions per unit, triangulation currency and rounding.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EuropeVectorTests
{
    private static readonly Dictionary<string, Func<Currency>> Currencies = new()
    {
        ["BGLCurrency"] = () => new BGLCurrency(),
        ["BGNCurrency"] = () => new BGNCurrency(),
        ["BYRCurrency"] = () => new BYRCurrency(),
        ["CHFCurrency"] = () => new CHFCurrency(),
        ["CZKCurrency"] = () => new CZKCurrency(),
        ["DKKCurrency"] = () => new DKKCurrency(),
        ["EEKCurrency"] = () => new EEKCurrency(),
        ["EURCurrency"] = () => new EURCurrency(),
        ["GBPCurrency"] = () => new GBPCurrency(),
        ["GELCurrency"] = () => new GELCurrency(),
        ["HUFCurrency"] = () => new HUFCurrency(),
        ["ISKCurrency"] = () => new ISKCurrency(),
        ["LTLCurrency"] = () => new LTLCurrency(),
        ["LVLCurrency"] = () => new LVLCurrency(),
        ["MKDCurrency"] = () => new MKDCurrency(),
        ["NOKCurrency"] = () => new NOKCurrency(),
        ["PLNCurrency"] = () => new PLNCurrency(),
        ["ROLCurrency"] = () => new ROLCurrency(),
        ["RONCurrency"] = () => new RONCurrency(),
        ["RSDCurrency"] = () => new RSDCurrency(),
        ["RUBCurrency"] = () => new RUBCurrency(),
        ["SEKCurrency"] = () => new SEKCurrency(),
        ["SITCurrency"] = () => new SITCurrency(),
        ["TRLCurrency"] = () => new TRLCurrency(),
        ["TRYCurrency"] = () => new TRYCurrency(),
        ["UAHCurrency"] = () => new UAHCurrency(),
        ["ATSCurrency"] = () => new ATSCurrency(),
        ["BEFCurrency"] = () => new BEFCurrency(),
        ["CYPCurrency"] = () => new CYPCurrency(),
        ["DEMCurrency"] = () => new DEMCurrency(),
        ["ESPCurrency"] = () => new ESPCurrency(),
        ["FIMCurrency"] = () => new FIMCurrency(),
        ["FRFCurrency"] = () => new FRFCurrency(),
        ["GRDCurrency"] = () => new GRDCurrency(),
        ["HRKCurrency"] = () => new HRKCurrency(),
        ["IEPCurrency"] = () => new IEPCurrency(),
        ["ITLCurrency"] = () => new ITLCurrency(),
        ["LUFCurrency"] = () => new LUFCurrency(),
        ["MTLCurrency"] = () => new MTLCurrency(),
        ["NLGCurrency"] = () => new NLGCurrency(),
        ["PTECurrency"] = () => new PTECurrency(),
        ["SKKCurrency"] = () => new SKKCurrency(),
    };

    [Fact]
    public void EuropeVector()
    {
        var file = VectorFile.Load("currencies/europe");
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
