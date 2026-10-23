// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/gaussian1dsmilesection.hpp, .cpp: Gaussian1dSmileSection on a swap index, which the
// wheel doesn't bind, built inside each call under the case's settings from a spec {"fixingDate" (a serial), "model" (a
// GSR spec of models/shortrate/onefactormodels/gsrspec.hpp), "dayCounter" (convert.hpp's), "swapIndex", "engine"}, the
// swap index and the engine specs of gaussian1dspec.hpp.
//
//     Gaussian1dSmileSection::inspectors   (spec)                           {"atmLevel", "exerciseDate", "exerciseTime",
//                                                                            "referenceDate", "minStrike", "maxStrike"}
//     Gaussian1dSmileSection::optionPrice  (spec, strike, type, discount)   a real, the type Option::Type's integer
//     Gaussian1dSmileSection::volatility   (spec, strike)                   a real

#include "convert.hpp"
#include "models/shortrate/onefactormodels/gsrspec.hpp"
#include "termstructures/volatility/gaussian1dspec.hpp"

#include <ql/termstructures/volatility/gaussian1dsmilesection.hpp>

#include <optional>
#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        using gaussian1d::key;

        struct SectionSpec {
            DateSpec fixingDate;
            gaussian1d::GsrSpec model;
            DayCounterSpec dayCounter;
            gaussian1d::SwapIndexSpec swapIndex;
            std::optional<gaussian1d::EngineSpec> engine;
        };

        SectionSpec section(const json::value& v) {
            const std::string what = "a Gaussian1d smile section spec";
            if (!v.is_object())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            const json::object& o = v.get_object();
            gaussian1d::onlyKeys(o, {"fixingDate", "model", "dayCounter", "swapIndex", "engine"}, what);
            return {date(key(o, "fixingDate", what)), gaussian1d::gsr(key(o, "model", what)),
                    dayCounter(key(o, "dayCounter", what)), gaussian1d::swapIndex(key(o, "swapIndex", what)),
                    gaussian1d::engine(key(o, "engine", what))};
        }

        ext::shared_ptr<Gaussian1dSmileSection> makeSection(const SectionSpec& s) {
            auto model = gaussian1d::makeGsr(s.model);
            return ext::make_shared<Gaussian1dSmileSection>(makeDate(s.fixingDate), gaussian1d::makeSwapIndex(s.swapIndex),
                                                            model, makeDayCounter(s.dayCounter),
                                                            gaussian1d::makeEngine(s.engine, model));
        }

        const Register unit({
            {"Gaussian1dSmileSection::inspectors",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 SectionSpec s = section(a[0]);
                 return [=] {
                     auto section = makeSection(s);
                     return json::value(json::object{{"atmLevel", section->atmLevel()},
                                                     {"exerciseDate", result(section->exerciseDate())},
                                                     {"exerciseTime", section->exerciseTime()},
                                                     {"referenceDate", result(section->referenceDate())},
                                                     {"minStrike", section->minStrike()},
                                                     {"maxStrike", section->maxStrike()}});
                 };
             }},
            {"Gaussian1dSmileSection::optionPrice",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 SectionSpec s = section(a[0]);
                 Real strike = real(a[1]);
                 auto type = enumeration<Option::Type>(a[2]);
                 Real discount = real(a[3]);
                 return [=] { return json::value(makeSection(s)->optionPrice(strike, type, discount)); };
             }},
            {"Gaussian1dSmileSection::volatility",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 SectionSpec s = section(a[0]);
                 Real strike = real(a[1]);
                 return [=] { return json::value(makeSection(s)->volatility(strike)); };
             }},
        });

    }

}
