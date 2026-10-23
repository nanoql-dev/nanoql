// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/swaption/gaussian1dswaptionvolatility.hpp, .cpp: Gaussian1dSwaptionVolatility, which the
// wheel doesn't bind, built inside each call under the case's settings from a spec {"calendar"
// (indexes/indexspec.hpp's), "convention" (QuantLib's integer), "swapIndex", "model" (a GSR spec of
// models/shortrate/onefactormodels/gsrspec.hpp), "dayCounter" (convert.hpp's), "engine"}, the swap index and the engine
// specs of ../gaussian1dspec.hpp. A tenor is [length, units], a date a serial.
//
//     Gaussian1dSwaptionVolatility::inspectors                (spec)    {"referenceDate", "maxDate", "minStrike",
//                                                                       "maxStrike", "maxSwapTenor"}
//     Gaussian1dSwaptionVolatility::volatility(Period,Period) (spec, optionTenor, swapTenor, strike)    a real
//     Gaussian1dSwaptionVolatility::volatility(Date,Period)   (spec, optionDate, swapTenor, strike)
//     Gaussian1dSwaptionVolatility::volatility(Time,Time)     (spec, optionTime, swapLength, strike)
//     Gaussian1dSwaptionVolatility::smileSection(Date,Period) (spec, optionDate, swapTenor)    {"exerciseDate",
//                                                                                             "exerciseTime", "atmLevel"}
//     Gaussian1dSwaptionVolatility::smileSection(Time,Time)   (spec, optionTime, swapLength)
//
// The members are SwaptionVolatilityStructure's, extrapolate false: smileSection(Time,Time) finds the option date
// by the structure's Newton solve.

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "models/shortrate/onefactormodels/gsrspec.hpp"
#include "termstructures/volatility/gaussian1dspec.hpp"
#include "termstructures/volatility/optionlet/optionletvolspec.hpp"

#include <ql/termstructures/volatility/smilesection.hpp>
#include <ql/termstructures/volatility/swaption/gaussian1dswaptionvolatility.hpp>

#include <optional>
#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        using gaussian1d::key;

        struct VolSpec {
            indexes::CalendarSpec calendar;
            BusinessDayConvention convention = Following;
            gaussian1d::SwapIndexSpec swapIndex;
            gaussian1d::GsrSpec model;
            DayCounterSpec dayCounter;
            std::optional<gaussian1d::EngineSpec> engine;
        };

        VolSpec volatility(const json::value& v) {
            const std::string what = "a Gaussian1d swaption volatility spec";
            if (!v.is_object())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            const json::object& o = v.get_object();
            gaussian1d::onlyKeys(o, {"calendar", "convention", "swapIndex", "model", "dayCounter", "engine"}, what);
            return {indexes::calendar(key(o, "calendar", what)),
                    enumeration<BusinessDayConvention>(key(o, "convention", what)),
                    gaussian1d::swapIndex(key(o, "swapIndex", what)), gaussian1d::gsr(key(o, "model", what)),
                    dayCounter(key(o, "dayCounter", what)), gaussian1d::engine(key(o, "engine", what))};
        }

        ext::shared_ptr<Gaussian1dSwaptionVolatility> makeVolatility(const VolSpec& s) {
            auto model = gaussian1d::makeGsr(s.model);
            return ext::make_shared<Gaussian1dSwaptionVolatility>(
                indexes::makeCalendar(s.calendar), s.convention, gaussian1d::makeSwapIndex(s.swapIndex), model,
                makeDayCounter(s.dayCounter), gaussian1d::makeEngine(s.engine, model));
        }

        json::value summary(const ext::shared_ptr<SmileSection>& section) {
            return json::value(json::object{{"exerciseDate", result(section->exerciseDate())},
                                            {"exerciseTime", section->exerciseTime()},
                                            {"atmLevel", section->atmLevel()}});
        }

        const Register unit({
            {"Gaussian1dSwaptionVolatility::inspectors",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 VolSpec s = volatility(a[0]);
                 return [=] {
                     auto vol = makeVolatility(s);
                     const Period& max = vol->maxSwapTenor();
                     return json::value(json::object{
                         {"referenceDate", result(vol->referenceDate())},
                         {"maxDate", result(vol->maxDate())},
                         {"minStrike", vol->minStrike()},
                         {"maxStrike", vol->maxStrike()},
                         {"maxSwapTenor", json::array{max.length(), static_cast<int>(max.units())}}});
                 };
             }},
            {"Gaussian1dSwaptionVolatility::volatility(Period,Period)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 VolSpec s = volatility(a[0]);
                 auto option = termstructures::tenor(a[1]);
                 auto swap = termstructures::tenor(a[2]);
                 Real strike = real(a[3]);
                 return [=] {
                     return json::value(makeVolatility(s)->volatility(termstructures::makePeriod(option),
                                                                      termstructures::makePeriod(swap), strike));
                 };
             }},
            {"Gaussian1dSwaptionVolatility::volatility(Date,Period)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 VolSpec s = volatility(a[0]);
                 DateSpec option = date(a[1]);
                 auto swap = termstructures::tenor(a[2]);
                 Real strike = real(a[3]);
                 return [=] {
                     return json::value(
                         makeVolatility(s)->volatility(makeDate(option), termstructures::makePeriod(swap), strike));
                 };
             }},
            {"Gaussian1dSwaptionVolatility::volatility(Time,Time)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 VolSpec s = volatility(a[0]);
                 Real option = real(a[1]);
                 Real swap = real(a[2]);
                 Real strike = real(a[3]);
                 return [=] { return json::value(makeVolatility(s)->volatility(option, swap, strike)); };
             }},
            {"Gaussian1dSwaptionVolatility::smileSection(Date,Period)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 VolSpec s = volatility(a[0]);
                 DateSpec option = date(a[1]);
                 auto swap = termstructures::tenor(a[2]);
                 return [=] {
                     return summary(makeVolatility(s)->smileSection(makeDate(option), termstructures::makePeriod(swap)));
                 };
             }},
            {"Gaussian1dSwaptionVolatility::smileSection(Time,Time)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 VolSpec s = volatility(a[0]);
                 Real option = real(a[1]);
                 Real swap = real(a[2]);
                 return [=] { return summary(makeVolatility(s)->smileSection(option, swap)); };
             }},
        });

    }

}
