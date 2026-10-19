// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015, 2016, 2017 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2017 Paul Giltinan
// SPDX-FileCopyrightText: Copyright (C) 2017 Werner Kuerzinger
// SPDX-FileCopyrightText: Copyright (C) 2020 Marcin Rybacki
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/blackswaptionengine.hpp ql/pricingengines/swaption/blackswaptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>
/// The date a cash-settled swaption's par-yield annuity is discounted from: QuantLib's
/// <c>BlackStyleSwaptionEngine::CashAnnuityModel</c>, which both swaption engines take.
/// </summary>
/// <remarks>
/// QuantLib nests it in the engines' internal template, so each engine has a type of its own; here one type serves
/// both. QuantLib has no stream operator for it, so it has no text of its own.
/// </remarks>
public enum CashAnnuityModel
{
    /// <summary>The annuity is discounted at the swap rate alone, to the swap's valuation date.</summary>
    SwapRate = 0,

    /// <summary>
    /// The annuity is discounted at the swap rate to the swap's start date, and on the discount curve from there.
    /// </summary>
    DiscountCurve = 1,
}
