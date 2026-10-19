// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/callablebonds/callablebondconstantvol.hpp, .cpp and callablebondvolstructure.hpp, .cpp, which the
// wheel doesn't expose: CallableBondVolatilityStructure's members, on a volatility spec of callablebondspec.hpp
// (QuantLib's constant volatility, or the harness's subclass, which keeps the base's defaults) built inside the call.
// An option is a time, a date (a serial) or a tenor ([length, units]), a bond a length or a tenor.
//
//     CallableBondVolatilityStructure::volatility(Time,Time,Rate), blackVariance(Time,Time,Rate)
//                                                  (vol, optionTime, bondLength, strike, extrapolate)
//     CallableBondVolatilityStructure::volatility(Date,Period,Rate), blackVariance(Date,Period,Rate)
//                                                  (vol, optionDate, bondTenor, strike, extrapolate)
//     CallableBondVolatilityStructure::volatility(Period,Period,Rate), blackVariance(Period,Period,Rate)
//                                                  (vol, optionTenor, bondTenor, strike, extrapolate)
//     CallableBondVolatilityStructure::smileSection(Date,Period), smileSection(Period,Period)
//                                                  (vol, option, bondTenor, strike): the section's summary at the strike
//                                                  (termstructures/volatility/smilesectionspec.hpp)
//     CallableBondVolatilityStructure::convertDates
//                                                  (vol, optionDate, bondTenor): [optionTime, bondLength]
//     CallableBondVolatilityStructure::optionDateFromTenor
//                                                  (vol, optionTenor)
//     CallableBondVolatilityStructure::referenceDate, maxDate, maxTime, maxBondLength, minStrike, maxStrike,
//     businessDayConvention, settlementDays       (vol): the inspector's value
//     CallableBondVolatilityStructure::dayCounter  (vol): its name
//     CallableBondVolatilityStructure::maxBondTenor
//                                                  (vol): [length, units]

#include "convert.hpp"
#include "experimental/callablebonds/callablebondspec.hpp"
#include "termstructures/volatility/optionlet/optionletvolspec.hpp"
#include "termstructures/volatility/smilesectionspec.hpp"

#include <ql/experimental/callablebonds/callablebondconstantvol.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        using callablebonds::ConstantVolSpec;
        using termstructures::makePeriod;
        using termstructures::PeriodSpec;
        using termstructures::tenor;

        // A member of the structure: f(vol).
        template <class F>
        Call onVol(const Args& a, F f) {
            arity(a, 1);
            ConstantVolSpec s = callablebonds::constantVol(a[0]);
            return [=] { return json::value(f(*callablebonds::makeVolatility(s))); };
        }

        json::value periodResult(const Period& p) {
            json::array out;
            out.emplace_back(static_cast<std::int64_t>(p.length()));
            out.emplace_back(static_cast<std::int64_t>(p.units()));
            return json::value(std::move(out));
        }

        // volatility and blackVariance by time: (vol, optionTime, bondLength, strike, extrapolate).
        Method byTime(bool variance) {
            return [=](const Args& a) -> Call {
                arity(a, 5);
                ConstantVolSpec s = callablebonds::constantVol(a[0]);
                Real t = real(a[1]), l = real(a[2]), k = real(a[3]);
                bool extrapolate = boolean(a[4]);
                return [=] {
                    auto v = callablebonds::makeVolatility(s);
                    return json::value(variance ? v->blackVariance(t, l, k, extrapolate)
                                                : v->volatility(t, l, k, extrapolate));
                };
            };
        }

        // volatility and blackVariance by date: (vol, optionDate, bondTenor, strike, extrapolate).
        Method byDate(bool variance) {
            return [=](const Args& a) -> Call {
                arity(a, 5);
                ConstantVolSpec s = callablebonds::constantVol(a[0]);
                DateSpec d = date(a[1]);
                PeriodSpec p = tenor(a[2]);
                Real k = real(a[3]);
                bool extrapolate = boolean(a[4]);
                return [=] {
                    auto v = callablebonds::makeVolatility(s);
                    return json::value(variance ? v->blackVariance(makeDate(d), makePeriod(p), k, extrapolate)
                                                : v->volatility(makeDate(d), makePeriod(p), k, extrapolate));
                };
            };
        }

        // volatility and blackVariance by tenor: (vol, optionTenor, bondTenor, strike, extrapolate).
        Method byTenor(bool variance) {
            return [=](const Args& a) -> Call {
                arity(a, 5);
                ConstantVolSpec s = callablebonds::constantVol(a[0]);
                PeriodSpec o = tenor(a[1]);
                PeriodSpec p = tenor(a[2]);
                Real k = real(a[3]);
                bool extrapolate = boolean(a[4]);
                return [=] {
                    auto v = callablebonds::makeVolatility(s);
                    return json::value(variance ? v->blackVariance(makePeriod(o), makePeriod(p), k, extrapolate)
                                                : v->volatility(makePeriod(o), makePeriod(p), k, extrapolate));
                };
            };
        }

        const Register unit({
            {"CallableBondVolatilityStructure::volatility(Time,Time,Rate)", byTime(false)},
            {"CallableBondVolatilityStructure::blackVariance(Time,Time,Rate)", byTime(true)},
            {"CallableBondVolatilityStructure::volatility(Date,Period,Rate)", byDate(false)},
            {"CallableBondVolatilityStructure::blackVariance(Date,Period,Rate)", byDate(true)},
            {"CallableBondVolatilityStructure::volatility(Period,Period,Rate)", byTenor(false)},
            {"CallableBondVolatilityStructure::blackVariance(Period,Period,Rate)", byTenor(true)},
            {"CallableBondVolatilityStructure::smileSection(Date,Period)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 ConstantVolSpec s = callablebonds::constantVol(a[0]);
                 DateSpec d = date(a[1]);
                 PeriodSpec p = tenor(a[2]);
                 Real k = real(a[3]);
                 return [=] {
                     auto v = callablebonds::makeVolatility(s);
                     return termstructures::sectionSummary(*v->smileSection(makeDate(d), makePeriod(p)), k);
                 };
             }},
            {"CallableBondVolatilityStructure::smileSection(Period,Period)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 ConstantVolSpec s = callablebonds::constantVol(a[0]);
                 PeriodSpec o = tenor(a[1]);
                 PeriodSpec p = tenor(a[2]);
                 Real k = real(a[3]);
                 return [=] {
                     auto v = callablebonds::makeVolatility(s);
                     return termstructures::sectionSummary(*v->smileSection(makePeriod(o), makePeriod(p)), k);
                 };
             }},
            {"CallableBondVolatilityStructure::convertDates",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 ConstantVolSpec s = callablebonds::constantVol(a[0]);
                 DateSpec d = date(a[1]);
                 PeriodSpec p = tenor(a[2]);
                 return [=] {
                     std::pair<Time, Time> x = callablebonds::makeVolatility(s)->convertDates(makeDate(d),
                                                                                              makePeriod(p));
                     json::array out;
                     out.emplace_back(x.first);
                     out.emplace_back(x.second);
                     return json::value(std::move(out));
                 };
             }},
            {"CallableBondVolatilityStructure::optionDateFromTenor",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 ConstantVolSpec s = callablebonds::constantVol(a[0]);
                 PeriodSpec o = tenor(a[1]);
                 return [=] {
                     return result(callablebonds::makeVolatility(s)->optionDateFromTenor(makePeriod(o)));
                 };
             }},
            {"CallableBondVolatilityStructure::referenceDate",
             [](const Args& a) {
                 return onVol(a, [](const CallableBondVolatilityStructure& v) { return result(v.referenceDate()); });
             }},
            {"CallableBondVolatilityStructure::maxDate",
             [](const Args& a) {
                 return onVol(a, [](const CallableBondVolatilityStructure& v) { return result(v.maxDate()); });
             }},
            {"CallableBondVolatilityStructure::maxTime",
             [](const Args& a) { return onVol(a, [](const CallableBondVolatilityStructure& v) { return v.maxTime(); }); }},
            {"CallableBondVolatilityStructure::maxBondLength",
             [](const Args& a) {
                 return onVol(a, [](const CallableBondVolatilityStructure& v) { return v.maxBondLength(); });
             }},
            {"CallableBondVolatilityStructure::minStrike",
             [](const Args& a) {
                 return onVol(a, [](const CallableBondVolatilityStructure& v) { return v.minStrike(); });
             }},
            {"CallableBondVolatilityStructure::maxStrike",
             [](const Args& a) {
                 return onVol(a, [](const CallableBondVolatilityStructure& v) { return v.maxStrike(); });
             }},
            {"CallableBondVolatilityStructure::businessDayConvention",
             [](const Args& a) {
                 return onVol(a, [](const CallableBondVolatilityStructure& v) {
                     return static_cast<std::int64_t>(v.businessDayConvention());
                 });
             }},
            {"CallableBondVolatilityStructure::settlementDays",
             [](const Args& a) {
                 return onVol(a, [](const CallableBondVolatilityStructure& v) {
                     return static_cast<std::int64_t>(v.settlementDays());
                 });
             }},
            {"CallableBondVolatilityStructure::dayCounter",
             [](const Args& a) {
                 return onVol(a, [](const CallableBondVolatilityStructure& v) { return json::value(v.dayCounter().name()); });
             }},
            {"CallableBondVolatilityStructure::maxBondTenor",
             [](const Args& a) {
                 return onVol(a, [](const CallableBondVolatilityStructure& v) { return periodResult(v.maxBondTenor()); });
             }},
        });

    }

}
