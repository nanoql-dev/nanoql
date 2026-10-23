// SPDX-FileCopyrightText: Copyright (C) 2013, 2018 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodels/markovfunctional.hpp ql/models/shortrate/onefactormodels/markovfunctional.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using NanoQL.Time;
using NanoQL.Utilities;
using static NanoQL.Models.MarkovFunctionalModelSettingsAdjustments;

namespace NanoQL.Models;

/// <summary>
/// The Markov-functional model's diagnostics: per calibration point in date order, its expiry, tenor, at-the-money level
/// and annuity, the numeraire's adjustment factors, the yield curve's fit and the smile's fit on the moneyness grid, and
/// the calibration's messages.
/// </summary>
/// <remarks>
/// Immutable, computed per call of <see cref="MarkovFunctional.ModelOutputs"/>. QuantLib's model keeps its outputs and a
/// dirty flag; its at-the-money levels and annuities are appended to at every computation, so a second one after a
/// recalculation holds stale entries before the fresh ones (<c>markovfunctional.cpp:618-648</c>). Here each call
/// gives the model's own outputs once, and there is no dirty flag.
/// </remarks>
public sealed class MarkovFunctionalModelOutputs
{
    internal MarkovFunctionalModelOutputs(
        MarkovFunctionalModelSettings settings,
        ImmutableArray<Date> expiries,
        ImmutableArray<Period> tenors,
        ImmutableArray<double> atm,
        ImmutableArray<double> annuity,
        ImmutableArray<double> adjustmentFactors,
        ImmutableArray<double> digitalsAdjustmentFactors,
        ImmutableArray<string> messages,
        ImmutableArray<ImmutableArray<double>> smileStrikes,
        ImmutableArray<ImmutableArray<double>> marketRawCallPremium,
        ImmutableArray<ImmutableArray<double>> marketRawPutPremium,
        ImmutableArray<ImmutableArray<double>> marketCallPremium,
        ImmutableArray<ImmutableArray<double>> marketPutPremium,
        ImmutableArray<ImmutableArray<double>> modelCallPremium,
        ImmutableArray<ImmutableArray<double>> modelPutPremium,
        ImmutableArray<ImmutableArray<double>> marketVega,
        ImmutableArray<double> marketZerorate,
        ImmutableArray<double> modelZerorate)
    {
        Settings = settings;
        Expiries = expiries;
        Tenors = tenors;
        Atm = atm;
        Annuity = annuity;
        AdjustmentFactors = adjustmentFactors;
        DigitalsAdjustmentFactors = digitalsAdjustmentFactors;
        Messages = messages;
        SmileStrikes = smileStrikes;
        MarketRawCallPremium = marketRawCallPremium;
        MarketRawPutPremium = marketRawPutPremium;
        MarketCallPremium = marketCallPremium;
        MarketPutPremium = marketPutPremium;
        ModelCallPremium = modelCallPremium;
        ModelPutPremium = modelPutPremium;
        MarketVega = marketVega;
        MarketZerorate = marketZerorate;
        ModelZerorate = modelZerorate;
    }

    /// <summary>Gets the model's settings, as validated.</summary>
    public MarkovFunctionalModelSettings Settings { get; }

    /// <summary>Gets the calibration points' expiries.</summary>
    public ImmutableArray<Date> Expiries { get; }

    /// <summary>Gets the calibration points' tenors.</summary>
    public ImmutableArray<Period> Tenors { get; }

    /// <summary>Gets the calibration points' at-the-money levels on the model's curve.</summary>
    public ImmutableArray<double> Atm { get; }

    /// <summary>Gets the calibration points' annuities on the model's curve.</summary>
    public ImmutableArray<double> Annuity { get; }

    /// <summary>Gets the numeraire's adjustment factors to the yield curve, 1 without the adjustment.</summary>
    public ImmutableArray<double> AdjustmentFactors { get; }

    /// <summary>Gets the digitals' adjustment factors, 1 without the adjustment.</summary>
    public ImmutableArray<double> DigitalsAdjustmentFactors { get; }

    /// <summary>Gets the calibration's messages and warnings.</summary>
    public ImmutableArray<string> Messages { get; }

    /// <summary>Gets each point's strikes on the moneyness grid.</summary>
    public ImmutableArray<ImmutableArray<double>> SmileStrikes { get; }

    /// <summary>Gets each point's market call premia, from its smile before pretreatment; 0 where that smile prices
    /// none.</summary>
    public ImmutableArray<ImmutableArray<double>> MarketRawCallPremium { get; }

    /// <summary>Gets each point's market put premia, from its smile before pretreatment; 0 where that smile prices
    /// none.</summary>
    public ImmutableArray<ImmutableArray<double>> MarketRawPutPremium { get; }

    /// <summary>Gets each point's market call premia, from its pretreated smile.</summary>
    public ImmutableArray<ImmutableArray<double>> MarketCallPremium { get; }

    /// <summary>Gets each point's market put premia, from its pretreated smile.</summary>
    public ImmutableArray<ImmutableArray<double>> MarketPutPremium { get; }

    /// <summary>Gets each point's model call premia.</summary>
    public ImmutableArray<ImmutableArray<double>> ModelCallPremium { get; }

    /// <summary>Gets each point's model put premia.</summary>
    public ImmutableArray<ImmutableArray<double>> ModelPutPremium { get; }

    /// <summary>Gets each point's market vegas, from its pretreated smile.</summary>
    public ImmutableArray<ImmutableArray<double>> MarketVega { get; }

    /// <summary>Gets the yield curve's continuous zero rates at the points' expiries.</summary>
    public ImmutableArray<double> MarketZerorate { get; }

    /// <summary>Gets the model's continuous zero rates at the points' expiries.</summary>
    public ImmutableArray<double> ModelZerorate { get; }

    /// <summary>The outputs as QuantLib's <c>operator&lt;&lt;</c> writes them: the settings, the messages, the yield
    /// curve's fit and the smile's fit, separated by semicolons.</summary>
    /// <returns>The text.</returns>
    public override string ToString()
    {
        var o = new StringBuilder();
        o.Append("Markov functional model trace output \n");
        o.Append("Model settings\n");
        o.Append("Grid points y        : ").Append(Settings.YGridPoints.ToString(CultureInfo.InvariantCulture)).Append('\n');
        o.Append("Std devs y           : ").Append(G(Settings.YStdDevs)).Append('\n');
        o.Append("Lower rate bound     : ").Append(G(Settings.LowerRateBound)).Append('\n');
        o.Append("Upper rate bound     : ").Append(G(Settings.UpperRateBound)).Append('\n');
        o.Append("Gauss Hermite points : ").Append(Settings.GaussHermitePoints.ToString(CultureInfo.InvariantCulture)).Append('\n');
        o.Append("Digital gap          : ").Append(G(Settings.DigitalGap)).Append('\n');
        MarkovFunctionalModelSettingsAdjustments a = Settings.Adjustments;
        o.Append("Adjustments          : ")
            .Append((a & AdjustDigitals) != 0 ? "Digitals " : string.Empty)
            .Append((a & AdjustYts) != 0 ? "Yts " : string.Empty)
            .Append((a & ExtrapolatePayoffFlat) != 0 ? "FlatPayoffExt " : string.Empty)
            .Append((a & NoPayoffExtrapolation) != 0 ? "NoPayoffExt " : string.Empty)
            .Append((a & KahaleSmile) != 0 ? "Kahale " : string.Empty)
            .Append((a & SmileExponentialExtrapolation) != 0 ? "SmileExp " : string.Empty)
            .Append((a & KahaleInterpolation) != 0 ? "KahaleInt " : string.Empty)
            .Append((a & SmileDeleteArbitragePoints) != 0 ? "SmileDelArb " : string.Empty)
            .Append((a & SabrSmile) != 0 ? "Sabr" : string.Empty)
            .Append('\n');
        o.Append("Smile moneyness checkpoints: ");
        ImmutableArray<double> checkpoints = Settings.SmileMoneynessCheckpoints;
        for (int i = 0; i < checkpoints.Length; i++)
        {
            o.Append(G(checkpoints[i])).Append(i < checkpoints.Length - 1 ? ";" : string.Empty);
        }

        o.Append('\n');

        if (Expiries.IsEmpty)
        {
            return o.ToString(); // no trace information was collected so no output
        }

        o.Append('\n');
        o.Append("Messages:\n");
        foreach (string message in Messages)
        {
            o.Append(message).Append('\n');
        }

        o.Append('\n');
        o.Append("Yield termstructure fit:\n");
        o.Append("expiry;tenor;atm;annuity;digitalAdj;ytsAdj;marketzerorate;modelzerorate;diff(bp)\n");
        for (int i = 0; i < Expiries.Length; i++)
        {
            o.Append(Expiries[i].ToString()).Append(';').Append(Tenors[i].ToString()).Append(';').Append(G16(Atm[i])).Append(';')
                .Append(G16(Annuity[i])).Append(';').Append(G16(DigitalsAdjustmentFactors[i])).Append(';')
                .Append(G16(AdjustmentFactors[i])).Append(';').Append(G16(MarketZerorate[i])).Append(';')
                .Append(G16(ModelZerorate[i])).Append(';').Append(G16((MarketZerorate[i] - ModelZerorate[i]) * 10000.0))
                .Append('\n');
        }

        o.Append('\n');
        o.Append("Volatility smile fit:\n");
        for (int i = 0; i < Expiries.Length; i++)
        {
            string p = Expiries[i].ToString() + "/" + Tenors[i].ToString();
            o.Append("strike(" + p + ");marketCallRaw(" + p + ";marketCall(" + p + ");modelCall(" + p + ");marketPutRaw(" + p)
                .Append(");marketPut(" + p + ");modelPut(" + p + ");marketVega(" + p + ")")
                .Append(i < Expiries.Length - 1 ? ";" : string.Empty);
        }

        o.Append('\n');
        for (int j = 0; j < SmileStrikes[0].Length; j++)
        {
            for (int i = 0; i < Expiries.Length; i++)
            {
                o.Append(G16(SmileStrikes[i][j])).Append(';').Append(G16(MarketRawCallPremium[i][j])).Append(';')
                    .Append(G16(MarketCallPremium[i][j])).Append(';').Append(G16(ModelCallPremium[i][j])).Append(';')
                    .Append(G16(MarketRawPutPremium[i][j])).Append(';').Append(G16(MarketPutPremium[i][j])).Append(';')
                    .Append(G16(ModelPutPremium[i][j])).Append(';').Append(G16(MarketVega[i][j]))
                    .Append(i < Expiries.Length - 1 ? ";" : string.Empty);
            }

            o.Append('\n');
        }

        return o.ToString();
    }

    // A C++ stream's default output, six significant digits, and after setprecision(16), sixteen.
    private static string G(double x) => DataFormattersFunctions.General(x);

    private static string G16(double x) => DataFormattersFunctions.General(x, 16);
}
