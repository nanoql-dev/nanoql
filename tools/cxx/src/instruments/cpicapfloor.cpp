// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/cpicapfloor.hpp: the CPI cap or floor, which the wheel doesn't bind, built inside the call under the
// case's settings, with the arguments it sets up for an engine:
//
//     CPICapFloor  (type, nominal, start, base CPI, maturity, fix calendar, fix convention, pay calendar, pay
//                  convention, strike, index, observation lag, interpolation): [type, nominal, strike, fixing date,
//                  pay date, the index's name, observation lag, expired, the arguments' type, nominal, start date,
//                  fix date, pay date, base CPI, maturity, fix and pay conventions, strike, observation lag and
//                  interpolation]
//
// The index is a cashflows/inflationspec.hpp spec; a calendar an indexes/indexspec.hpp spec, null for QuantLib's empty
// one; the lag [length, units]; the enums QuantLib's integers. The arguments come from setupArguments, the engine's
// input (no engine prices the instrument here).

#include "cashflows/inflationspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/instruments/cpicapfloor.hpp>

#include <optional>

using namespace QuantLib;

namespace harness {

    namespace {

        json::array period(const Period& p) {
            json::array out;
            out.emplace_back(p.length());
            out.emplace_back(static_cast<int>(p.units()));
            return out;
        }

        const Register unit({
            {"CPICapFloor",
             [](const Args& a) -> Call {
                 arity(a, 13);
                 auto type = enumeration<Option::Type>(a[0]);
                 Real nominal = real(a[1]);
                 DateSpec start = date(a[2]), maturity = date(a[4]);
                 Real baseCPI = real(a[3]);
                 std::optional<indexes::CalendarSpec> fixCalendar, payCalendar;
                 if (!a[5].is_null())
                     fixCalendar = indexes::calendar(a[5]);
                 if (!a[7].is_null())
                     payCalendar = indexes::calendar(a[7]);
                 auto fixConvention = enumeration<BusinessDayConvention>(a[6]);
                 auto payConvention = enumeration<BusinessDayConvention>(a[8]);
                 Rate strike = real(a[9]);
                 cashflows::InflationIndexSpec index = cashflows::inflationIndex(a[10]);
                 cashflows::LagPeriod lag = cashflows::lagPeriod(a[11]);
                 auto interpolation = enumeration<CPI::InterpolationType>(a[12]);
                 return [=] {
                     CPICapFloor cap(type, nominal, makeDate(start), baseCPI, makeDate(maturity),
                                     fixCalendar ? indexes::makeCalendar(*fixCalendar) : Calendar(), fixConvention,
                                     payCalendar ? indexes::makeCalendar(*payCalendar) : Calendar(), payConvention,
                                     strike, cashflows::makeZeroIndex(index), cashflows::makePeriod(lag),
                                     interpolation);
                     CPICapFloor::arguments args;
                     cap.setupArguments(&args);
                     json::array out;
                     out.emplace_back(static_cast<int>(cap.type()));
                     out.emplace_back(cap.nominal());
                     out.emplace_back(cap.strike());
                     out.emplace_back(result(cap.fixingDate()));
                     out.emplace_back(result(cap.payDate()));
                     out.emplace_back(cap.index()->name());
                     out.emplace_back(period(cap.observationLag()));
                     out.emplace_back(cap.isExpired());
                     out.emplace_back(static_cast<int>(args.type));
                     out.emplace_back(args.nominal);
                     out.emplace_back(result(args.startDate));
                     out.emplace_back(result(args.fixDate));
                     out.emplace_back(result(args.payDate));
                     out.emplace_back(args.baseCPI);
                     out.emplace_back(result(args.maturity));
                     out.emplace_back(static_cast<int>(args.fixConvention));
                     out.emplace_back(static_cast<int>(args.payConvention));
                     out.emplace_back(args.strike);
                     out.emplace_back(period(args.observationLag));
                     out.emplace_back(static_cast<int>(args.observationInterpolation));
                     return json::value(out);
                 };
             }},
        });

    }

}
