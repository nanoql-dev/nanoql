// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using NanoQL.Instruments;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The experimental marker (playbook §4): every type ported from QuantLib's <c>ql/experimental</c> lives in
/// <c>NanoQL.Experimental</c> and carries <c>[Experimental("NQL0001")]</c>, so that a consumer that uses one gets the
/// diagnostic, and no type outside that namespace does. The check reads what a consumer sees: the assembly's exported
/// types.
/// </summary>
public class ExperimentalMarkerTests
{
    private const string Namespace = "NanoQL.Experimental";
    private const string DiagnosticId = "NQL0001";

    private static Type[] Exported { get; } = typeof(Bond).Assembly.GetExportedTypes();

    [Fact]
    public void EveryExportedTypeInTheExperimentalNamespaceCarriesTheMarker()
    {
        Type[] experimental = [.. Exported.Where(t => t.Namespace == Namespace)];
        Assert.NotEmpty(experimental);

        string[] unmarked =
        [
            .. experimental
                .Where(t => t.GetCustomAttribute<ExperimentalAttribute>(inherit: false)?.DiagnosticId != DiagnosticId)
                .Select(t => t.FullName!)
                .Order(StringComparer.Ordinal),
        ];
        Assert.Empty(unmarked);
    }

    [Fact]
    public void NoExportedTypeOutsideTheExperimentalNamespaceCarriesTheMarker()
    {
        string[] marked =
        [
            .. Exported
                .Where(t => t.Namespace != Namespace)
                .Where(t => t.GetCustomAttribute<ExperimentalAttribute>(inherit: false) is not null)
                .Select(t => t.FullName!)
                .Order(StringComparer.Ordinal),
        ];
        Assert.Empty(marked);
    }

    [Fact]
    public void TheBasisSwapRateHelpersCarryTheMarker()
    {
        foreach (Type t in new[]
        {
            typeof(NanoQL.Experimental.IborIborBasisSwapRateHelper),
            typeof(NanoQL.Experimental.OvernightIborBasisSwapRateHelper),
            typeof(NanoQL.Experimental.OvernightOvernightBasisSwapRateHelper),
        })
        {
            Assert.Equal(Namespace, t.Namespace);
            Assert.Equal(DiagnosticId, t.GetCustomAttribute<ExperimentalAttribute>(inherit: false)?.DiagnosticId);
        }
    }

    [Fact]
    public void TheFxResetsTheMtMSwapAndTheCrossCurrencyHelpersCarryTheMarker()
    {
        foreach (Type t in new[]
        {
            typeof(NanoQL.Experimental.FxReset),
            typeof(NanoQL.Experimental.FxResetConvention),
            typeof(NanoQL.Experimental.FxResetPricer),
            typeof(NanoQL.Experimental.DiscountingFxResetPricer),
            typeof(NanoQL.Experimental.FxResetCoupon),
            typeof(NanoQL.Experimental.FxResetNotionalExchange),
            typeof(NanoQL.Experimental.FxResetCashFlowsFunctions),
            typeof(NanoQL.Experimental.MtMCrossCurrencyBasisSwap),
            typeof(NanoQL.Experimental.MtMCrossCurrencyBasisSwapResults),
            typeof(NanoQL.Experimental.MtMCrossCurrencyBasisSwapType),
            typeof(NanoQL.Experimental.DiscountingMtMCrossCurrencyBasisSwapEngine),
            typeof(NanoQL.Experimental.CrossCurrencySwapRateHelperBase),
            typeof(NanoQL.Experimental.CrossCurrencyBasisSwapRateHelperBase),
            typeof(NanoQL.Experimental.ConstNotionalCrossCurrencyBasisSwapRateHelper),
            typeof(NanoQL.Experimental.MtMCrossCurrencyBasisSwapRateHelper),
            typeof(NanoQL.Experimental.ConstNotionalCrossCurrencySwapRateHelper),
        })
        {
            Assert.Equal(Namespace, t.Namespace);
            Assert.Equal(DiagnosticId, t.GetCustomAttribute<ExperimentalAttribute>(inherit: false)?.DiagnosticId);
        }
    }
}
