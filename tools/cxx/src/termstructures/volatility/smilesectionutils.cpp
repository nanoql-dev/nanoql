// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/smilesectionutils.hpp, .cpp: SmileSectionUtils, which the wheel doesn't expose, over a
// section spec (smilesectionspec.hpp), both built inside the call so that the constructor's refusal is QuantLib's. A
// spec is {"section": <section spec>, "moneynessGrid": [reals], "atm": <real or null>, "deleteArbitragePoints"}, the
// grid empty for QuantLib's default and the level null for Null<Real>().
//
//     SmileSectionUtils::moneyGrid, strikeGrid, callPrices   (spec): arrays
//     SmileSectionUtils::atmLevel                            (spec)
//     SmileSectionUtils::arbitragefreeRegion                 (spec): [left strike, right strike]
//     SmileSectionUtils::arbitragefreeIndices                (spec): [left index, right index]

#include "convert.hpp"
#include "termstructures/volatility/smilesectionspec.hpp"

#include <ql/termstructures/volatility/smilesectionutils.hpp>

#include <cstdint>
#include <functional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using termstructures::makeSmileSection;
        using termstructures::SmileSectionSpec;

        struct UtilsSpec {
            SmileSectionSpec section;
            std::vector<Real> moneynessGrid;
            Real atm = Null<Real>();
            bool deleteArbitragePoints = false;
        };

        UtilsSpec utilsSpec(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a smile section utilities spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const char* k : {"section", "moneynessGrid", "atm", "deleteArbitragePoints"})
                if (!o.contains(k))
                    throw InvalidParams(std::string("a smile section utilities spec needs ") + k);
            if (o.size() != 4)
                throw InvalidParams("a smile section utilities spec is {section, moneynessGrid, atm, "
                                    "deleteArbitragePoints}: " + json::serialize(v));
            return {termstructures::smileSection(o.at("section")), reals(o.at("moneynessGrid")), real(o.at("atm")),
                    boolean(o.at("deleteArbitragePoints"))};
        }

        json::value realArray(const std::vector<Real>& v) {
            json::array out;
            for (Real x : v)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

        Method member(std::function<json::value(const SmileSectionUtils&)> f) {
            return [f](const Args& a) -> Call {
                arity(a, 1);
                UtilsSpec s = utilsSpec(a[0]);
                return [=] {
                    ext::shared_ptr<SmileSection> section = makeSmileSection(s.section);
                    SmileSectionUtils u(*section, s.moneynessGrid, s.atm, s.deleteArbitragePoints);
                    return f(u);
                };
            };
        }

        const Register unit({
            {"SmileSectionUtils::moneyGrid", member([](const SmileSectionUtils& u) { return realArray(u.moneyGrid()); })},
            {"SmileSectionUtils::strikeGrid", member([](const SmileSectionUtils& u) { return realArray(u.strikeGrid()); })},
            {"SmileSectionUtils::callPrices", member([](const SmileSectionUtils& u) { return realArray(u.callPrices()); })},
            {"SmileSectionUtils::atmLevel", member([](const SmileSectionUtils& u) { return json::value(u.atmLevel()); })},
            {"SmileSectionUtils::arbitragefreeRegion",
             member([](const SmileSectionUtils& u) {
                 std::pair<Real, Real> r = u.arbitragefreeRegion();
                 return json::value(json::array{r.first, r.second});
             })},
            {"SmileSectionUtils::arbitragefreeIndices",
             member([](const SmileSectionUtils& u) {
                 std::pair<Size, Size> r = u.arbitragefreeIndices();
                 return json::value(json::array{static_cast<std::int64_t>(r.first), static_cast<std::int64_t>(r.second)});
             })},
        });

    }

}
