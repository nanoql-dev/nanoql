// SPDX-FileCopyrightText: Copyright (C) 2004 FIMAT Group
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008 Charles Chongseok Hyun
// SPDX-FileCopyrightText: Copyright (C) 2015 Riccardo Barone
// SPDX-FileCopyrightText: Copyright (C) 2007, 2011 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008, 2011 Charles Chongseok Hyun
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: Copyright (C) 2023 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: Copyright (C) 2023 Jonghee Lee
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/southkorea.hpp ql/time/calendars/southkorea.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The South Korean calendars: the markets a <see cref="SouthKorea"/> calendar can follow.</summary>
public enum SouthKoreaMarket
{
    /// <summary>Public holidays.</summary>
    Settlement,

    /// <summary>Korea exchange.</summary>
    KRX,
}
