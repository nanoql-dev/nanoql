// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/barrieroption/discretizeddoublebarrieroption.hpp ql/experimental/barrieroption/discretizeddoublebarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;

namespace NanoQL.Experimental;

/// <summary>
/// A discretized double-barrier asset a binomial double-barrier engine builds: the constructor QuantLib's engine
/// template calls, <c>D(arguments, process, grid)</c> (<c>binomialdoublebarrierengine.hpp:119</c>), as a static factory
/// (<c>IDiscretizedBarrierAsset</c>'s form).
/// </summary>
/// <typeparam name="TSelf">The asset's own type.</typeparam>
[Experimental("NQL0001")]
public interface IDiscretizedDoubleBarrierAsset<TSelf>
    where TSelf : DiscretizedAsset, IDiscretizedDoubleBarrierAsset<TSelf>
{
    /// <summary>Builds the asset of a double barrier option.</summary>
    /// <param name="option">The double barrier option.</param>
    /// <param name="process">The process, which converts the exercise dates to times.</param>
    /// <param name="grid">The time grid the exercise times are moved to the closest of, or <see langword="null"/>
    /// for none, QuantLib's empty grid.</param>
    /// <returns>The asset.</returns>
    static abstract TSelf Create(DoubleBarrierOption option, StochasticProcess process, TimeGrid? grid);
}
