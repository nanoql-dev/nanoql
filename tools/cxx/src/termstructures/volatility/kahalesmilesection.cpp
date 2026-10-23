// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/kahalesmilesection.hpp, .cpp: KahaleSmileSection's members the wheel doesn't bind, its
// core's indices and strikes, and its prices and volatilities on the harness's own sources, over a source section spec
// (smilesectionspec.hpp), both built inside the call so that the fit's refusals are QuantLib's. A spec is {"source":
// <section spec>, "atm": <real or null>, "interpolate", "exponentialExtrapolation", "deleteArbitragePoints",
// "moneynessGrid": [reals], "gap", "forcedLeftIndex", "forcedRightIndex"}, the level null for Null<Real>() and the grid
// empty for the default.
//
//     KahaleSmileSection::coreIndices                      (spec): [left index, right index]
//     KahaleSmileSection::leftCoreStrike, rightCoreStrike  (spec)
//     KahaleSmileSection::atmLevel                         (spec)
//     KahaleSmileSection::volatility                       (spec, strike)
//     KahaleSmileSection::optionPrice                      (spec, strike, option type, discount)

#include "convert.hpp"
#include "termstructures/volatility/smilesectionspec.hpp"

#include <ql/option.hpp>
#include <ql/termstructures/volatility/kahalesmilesection.hpp>

#include <cstdint>
#include <functional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using termstructures::makeSmileSection;
        using termstructures::SmileSectionSpec;

        struct KahaleSpec {
            SmileSectionSpec source;
            Real atm = Null<Real>();
            bool interpolate = false, exponentialExtrapolation = false, deleteArbitragePoints = false;
            std::vector<Real> moneynessGrid;
            Real gap = 1.0E-5;
            int forcedLeftIndex = -1, forcedRightIndex = QL_MAX_INTEGER;
        };

        KahaleSpec kahaleSpec(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a Kahale smile section spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            const char* keys[] = {"source", "atm", "interpolate", "exponentialExtrapolation", "deleteArbitragePoints",
                                  "moneynessGrid", "gap", "forcedLeftIndex", "forcedRightIndex"};
            for (const char* k : keys)
                if (!o.contains(k))
                    throw InvalidParams(std::string("a Kahale smile section spec needs ") + k);
            if (o.size() != std::size(keys))
                throw InvalidParams("a Kahale smile section spec takes only its nine keys: " + json::serialize(v));
            KahaleSpec s;
            s.source = termstructures::smileSection(o.at("source"));
            s.atm = real(o.at("atm"));
            s.interpolate = boolean(o.at("interpolate"));
            s.exponentialExtrapolation = boolean(o.at("exponentialExtrapolation"));
            s.deleteArbitragePoints = boolean(o.at("deleteArbitragePoints"));
            s.moneynessGrid = reals(o.at("moneynessGrid"));
            s.gap = real(o.at("gap"));
            s.forcedLeftIndex = integer(o.at("forcedLeftIndex"));
            s.forcedRightIndex = integer(o.at("forcedRightIndex"));
            return s;
        }

        ext::shared_ptr<KahaleSmileSection> make(const KahaleSpec& s) {
            return ext::make_shared<KahaleSmileSection>(makeSmileSection(s.source), s.atm, s.interpolate,
                                                        s.exponentialExtrapolation, s.deleteArbitragePoints,
                                                        s.moneynessGrid, s.gap, s.forcedLeftIndex,
                                                        s.forcedRightIndex);
        }

        Method whole(std::function<json::value(const KahaleSmileSection&)> f) {
            return [f](const Args& a) -> Call {
                arity(a, 1);
                KahaleSpec s = kahaleSpec(a[0]);
                return [=] { return f(*make(s)); };
            };
        }

        const Register unit({
            {"KahaleSmileSection::coreIndices",
             whole([](const KahaleSmileSection& k) {
                 std::pair<Size, Size> r = k.coreIndices();
                 return json::value(json::array{static_cast<std::int64_t>(r.first), static_cast<std::int64_t>(r.second)});
             })},
            {"KahaleSmileSection::leftCoreStrike",
             whole([](const KahaleSmileSection& k) { return json::value(k.leftCoreStrike()); })},
            {"KahaleSmileSection::rightCoreStrike",
             whole([](const KahaleSmileSection& k) { return json::value(k.rightCoreStrike()); })},
            {"KahaleSmileSection::atmLevel",
             whole([](const KahaleSmileSection& k) { return json::value(k.atmLevel()); })},
            {"KahaleSmileSection::volatility",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 KahaleSpec s = kahaleSpec(a[0]);
                 Real strike = real(a[1]);
                 return [=] { return json::value(make(s)->volatility(strike)); };
             }},
            // (spec, strike, option type, discount)
            {"KahaleSmileSection::optionPrice",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 KahaleSpec s = kahaleSpec(a[0]);
                 Real strike = real(a[1]);
                 auto type = enumeration<Option::Type>(a[2]);
                 Real discount = real(a[3]);
                 return [=] { return json::value(make(s)->optionPrice(strike, type, discount)); };
             }},
        });

    }

}
