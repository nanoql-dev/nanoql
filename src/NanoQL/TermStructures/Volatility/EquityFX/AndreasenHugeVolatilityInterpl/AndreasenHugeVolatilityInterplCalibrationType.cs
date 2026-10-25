// SPDX-FileCopyrightText: Copyright (C) 2017, 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/andreasenhugevolatilityinterpl.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// Which options <see cref="AndreasenHugeVolatilityInterpl"/> calibrates to: QuantLib's
/// <c>AndreasenHugeVolatilityInterpl::CalibrationType</c>, its values QuantLib's.
/// </summary>
public enum AndreasenHugeVolatilityInterplCalibrationType
{
    /// <summary>Puts, QuantLib's <c>Option::Put</c>.</summary>
    Put = -1,

    /// <summary>Puts and calls together.</summary>
    CallPut = 0,

    /// <summary>Calls, QuantLib's <c>Option::Call</c>.</summary>
    Call = 1,
}
