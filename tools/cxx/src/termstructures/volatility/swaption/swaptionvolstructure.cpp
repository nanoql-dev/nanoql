// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/swaption/swaptionvolstructure.hpp: SwaptionVolatilityStructure's members on a
// structure spec (swaptionvolspec.hpp), the structure built inside the call. Each of volatility, blackVariance, shift
// and smileSection in its six overloads, named by the option's and the swap's kinds ("(Period,Period)", "(Date,Time)",
// ...), each taking the structure, the option (a tenor as [length, units], a date as a serial, or a time), the swap (a
// tenor or a length), the strike where the member takes one, and the extrapolate argument; smileSection also takes the
// strike its section's summary reads (smilesectionspec.hpp). And maxSwapTenor, as [length, units], maxSwapLength,
// volatilityType, and swapLength on a tenor and on two dates.

#include "convert.hpp"
#include "termstructures/volatility/optionlet/optionletvolspec.hpp"
#include "termstructures/volatility/smilesectionspec.hpp"
#include "termstructures/volatility/swaption/swaptionvolspec.hpp"

#include <ql/termstructures/volatility/swaption/swaptionvolstructure.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using termstructures::makePeriod;
        using termstructures::makeSwaptionVolatility;
        using termstructures::PeriodSpec;
        using termstructures::sectionSummary;
        using termstructures::swaptionVolatility;
        using termstructures::SwaptionVolSpec;
        using termstructures::tenor;

        enum class Kind { Tenor, Date, Time };

        // An option (a tenor, a date or a time) or a swap (a tenor or a length) argument.
        struct Arg {
            Kind kind;
            PeriodSpec period{0, Days};
            DateSpec date{true, 0};
            Real time = 0.0;
        };

        Arg arg(Kind kind, const json::value& v) {
            Arg out{kind};
            if (kind == Kind::Tenor)
                out.period = tenor(v);
            else if (kind == Kind::Date)
                out.date = date(v);
            else
                out.time = real(v);
            return out;
        }

        // Calls f with the option and the swap as QuantLib's types, choosing the overload.
        template <class F>
        json::value dispatch(const Arg& o, const Arg& s, F f) {
            if (o.kind == Kind::Tenor)
                return s.kind == Kind::Tenor ? f(makePeriod(o.period), makePeriod(s.period))
                                             : f(makePeriod(o.period), s.time);
            if (o.kind == Kind::Date)
                return s.kind == Kind::Tenor ? f(makeDate(o.date), makePeriod(s.period)) : f(makeDate(o.date), s.time);
            return s.kind == Kind::Tenor ? f(o.time, makePeriod(s.period)) : f(o.time, s.time);
        }

        // volatility and blackVariance: (spec, option, swap, strike, extrapolate).
        Method withStrike(Kind option, Kind swap, bool variance) {
            return [=](const Args& a) -> Call {
                arity(a, 5);
                SwaptionVolSpec s = swaptionVolatility(a[0]);
                Arg o = arg(option, a[1]);
                Arg l = arg(swap, a[2]);
                Real strike = real(a[3]);
                bool extrapolate = boolean(a[4]);
                return [=] {
                    auto v = makeSwaptionVolatility(s);
                    return dispatch(o, l, [&](const auto& x, const auto& y) {
                        return json::value(variance ? v->blackVariance(x, y, strike, extrapolate)
                                                    : v->volatility(x, y, strike, extrapolate));
                    });
                };
            };
        }

        // shift: (spec, option, swap, extrapolate).
        Method shiftMethod(Kind option, Kind swap) {
            return [=](const Args& a) -> Call {
                arity(a, 4);
                SwaptionVolSpec s = swaptionVolatility(a[0]);
                Arg o = arg(option, a[1]);
                Arg l = arg(swap, a[2]);
                bool extrapolate = boolean(a[3]);
                return [=] {
                    auto v = makeSwaptionVolatility(s);
                    return dispatch(o, l, [&](const auto& x, const auto& y) {
                        return json::value(v->shift(x, y, extrapolate));
                    });
                };
            };
        }

        // smileSection: (spec, option, swap, extrapolate, strike), the section's summary at the strike.
        Method smileMethod(Kind option, Kind swap) {
            return [=](const Args& a) -> Call {
                arity(a, 5);
                SwaptionVolSpec s = swaptionVolatility(a[0]);
                Arg o = arg(option, a[1]);
                Arg l = arg(swap, a[2]);
                bool extrapolate = boolean(a[3]);
                Real strike = real(a[4]);
                return [=] {
                    auto v = makeSwaptionVolatility(s);
                    return dispatch(o, l, [&](const auto& x, const auto& y) {
                        return sectionSummary(*v->smileSection(x, y, extrapolate), strike);
                    });
                };
            };
        }

        constexpr Kind P = Kind::Tenor, D = Kind::Date, T = Kind::Time;

        const Register unit({
            {"SwaptionVolatilityStructure::volatility(Period,Period)", withStrike(P, P, false)},
            {"SwaptionVolatilityStructure::volatility(Date,Period)", withStrike(D, P, false)},
            {"SwaptionVolatilityStructure::volatility(Time,Period)", withStrike(T, P, false)},
            {"SwaptionVolatilityStructure::volatility(Period,Time)", withStrike(P, T, false)},
            {"SwaptionVolatilityStructure::volatility(Date,Time)", withStrike(D, T, false)},
            {"SwaptionVolatilityStructure::volatility(Time,Time)", withStrike(T, T, false)},
            {"SwaptionVolatilityStructure::blackVariance(Period,Period)", withStrike(P, P, true)},
            {"SwaptionVolatilityStructure::blackVariance(Date,Period)", withStrike(D, P, true)},
            {"SwaptionVolatilityStructure::blackVariance(Time,Period)", withStrike(T, P, true)},
            {"SwaptionVolatilityStructure::blackVariance(Period,Time)", withStrike(P, T, true)},
            {"SwaptionVolatilityStructure::blackVariance(Date,Time)", withStrike(D, T, true)},
            {"SwaptionVolatilityStructure::blackVariance(Time,Time)", withStrike(T, T, true)},
            {"SwaptionVolatilityStructure::shift(Period,Period)", shiftMethod(P, P)},
            {"SwaptionVolatilityStructure::shift(Date,Period)", shiftMethod(D, P)},
            {"SwaptionVolatilityStructure::shift(Time,Period)", shiftMethod(T, P)},
            {"SwaptionVolatilityStructure::shift(Period,Time)", shiftMethod(P, T)},
            {"SwaptionVolatilityStructure::shift(Date,Time)", shiftMethod(D, T)},
            {"SwaptionVolatilityStructure::shift(Time,Time)", shiftMethod(T, T)},
            {"SwaptionVolatilityStructure::smileSection(Period,Period)", smileMethod(P, P)},
            {"SwaptionVolatilityStructure::smileSection(Date,Period)", smileMethod(D, P)},
            {"SwaptionVolatilityStructure::smileSection(Time,Period)", smileMethod(T, P)},
            {"SwaptionVolatilityStructure::smileSection(Period,Time)", smileMethod(P, T)},
            {"SwaptionVolatilityStructure::smileSection(Date,Time)", smileMethod(D, T)},
            {"SwaptionVolatilityStructure::smileSection(Time,Time)", smileMethod(T, T)},
            {"SwaptionVolatilityStructure::maxSwapTenor",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 SwaptionVolSpec s = swaptionVolatility(a[0]);
                 return [=] {
                     Period p = makeSwaptionVolatility(s)->maxSwapTenor();
                     json::array out;
                     out.emplace_back(p.length());
                     out.emplace_back(static_cast<int>(p.units()));
                     return json::value(std::move(out));
                 };
             }},
            {"SwaptionVolatilityStructure::maxSwapLength",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 SwaptionVolSpec s = swaptionVolatility(a[0]);
                 return [=] { return json::value(makeSwaptionVolatility(s)->maxSwapLength()); };
             }},
            {"SwaptionVolatilityStructure::volatilityType",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 SwaptionVolSpec s = swaptionVolatility(a[0]);
                 return [=] { return json::value(static_cast<int>(makeSwaptionVolatility(s)->volatilityType())); };
             }},
            // (spec, tenor)
            {"SwaptionVolatilityStructure::swapLength(Period)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 SwaptionVolSpec s = swaptionVolatility(a[0]);
                 PeriodSpec p = tenor(a[1]);
                 return [=] { return json::value(makeSwaptionVolatility(s)->swapLength(makePeriod(p))); };
             }},
            // (spec, start, end)
            {"SwaptionVolatilityStructure::swapLength(Date,Date)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 SwaptionVolSpec s = swaptionVolatility(a[0]);
                 DateSpec start = date(a[1]);
                 DateSpec end = date(a[2]);
                 return [=] {
                     return json::value(makeSwaptionVolatility(s)->swapLength(makeDate(start), makeDate(end)));
                 };
             }},
        });

    }

}
