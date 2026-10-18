// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/stubiborcoupon.hpp: StubIborCoupon::WeightedIndex, which the wheel doesn't bind, built inside each call
// from its components, [[index, weight], ...], each index an Ibor index spec of indexes/indexspec.hpp; QuantLib reads
// a null component before its own check, so the harness's specs hold none. The constructor's result is the index's
// inspectors, {"name", "familyName", "tenor" ([length, units]), "fixingDays", "dayCounter", "businessDayConvention",
// "endOfMonth", "allowsNativeFixings", "components" ([[name, weight], ...])}; pastFixing's is null for
// Null<Real>().

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/cashflows/stubiborcoupon.hpp>
#include <ql/utilities/null.hpp>

#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using Components = std::vector<std::pair<indexes::IndexSpec, Real>>;

        Components components(const json::value& v) {
            const std::string expected = "expected components as [[index, weight], ...], got ";
            if (!v.is_array())
                throw InvalidParams(expected + json::serialize(v));
            Components out;
            for (const json::value& c : v.get_array()) {
                if (!c.is_array() || c.get_array().size() != 2)
                    throw InvalidParams(expected + json::serialize(v));
                out.emplace_back(indexes::iborIndex(c.get_array()[0]), real(c.get_array()[1]));
            }
            return out;
        }

        ext::shared_ptr<StubIborCoupon::WeightedIndex> make(const Components& c) {
            std::vector<StubIborCoupon::WeightedIndex::Component> built;
            for (const auto& [spec, weight] : c)
                built.emplace_back(indexes::makeIborIndex(spec), weight);
            return ext::make_shared<StubIborCoupon::WeightedIndex>(built);
        }

        template <class F>
        Call atDate(const Args& a, F f) {
            arity(a, 2);
            Components c = components(a[0]);
            DateSpec d = date(a[1]);
            return [=] { return f(*make(c), makeDate(d)); };
        }

        const Register unit({
            {"StubIborCoupon::WeightedIndex",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Components c = components(a[0]);
                 return [=] {
                     auto index = make(c);
                     json::array parts;
                     for (const auto& [component, weight] : index->components())
                         parts.emplace_back(json::array{json::value(component->name()), json::value(weight)});
                     return json::value(json::object{
                         {"name", index->name()},
                         {"familyName", index->familyName()},
                         {"tenor", json::array{index->tenor().length(), static_cast<int>(index->tenor().units())}},
                         {"fixingDays", index->fixingDays()},
                         {"dayCounter", index->dayCounter().name()},
                         {"businessDayConvention", static_cast<int>(index->businessDayConvention())},
                         {"endOfMonth", index->endOfMonth()},
                         {"allowsNativeFixings", index->allowsNativeFixings()},
                         {"components", std::move(parts)}});
                 };
             }},
            {"StubIborCoupon::WeightedIndex::maturityDate",
             [](const Args& a) {
                 return atDate(a, [](const StubIborCoupon::WeightedIndex& i, const Date& d) {
                     return result(i.maturityDate(d));
                 });
             }},
            {"StubIborCoupon::WeightedIndex::forecastFixing",
             [](const Args& a) {
                 return atDate(a, [](const StubIborCoupon::WeightedIndex& i, const Date& d) {
                     return json::value(i.forecastFixing(d));
                 });
             }},
            {"StubIborCoupon::WeightedIndex::pastFixing",
             [](const Args& a) {
                 return atDate(a, [](const StubIborCoupon::WeightedIndex& i, const Date& d) {
                     Real x = i.pastFixing(d);
                     return x == Null<Real>() ? json::value(nullptr) : json::value(x);
                 });
             }},
            {"StubIborCoupon::WeightedIndex::fixing",
             [](const Args& a) {
                 return atDate(a, [](const StubIborCoupon::WeightedIndex& i, const Date& d) {
                     return json::value(i.fixing(d));
                 });
             }},
            {"StubIborCoupon::WeightedIndex::clone",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Components c = components(a[0]);
                 return [=] { return json::value(make(c)->clone(Handle<YieldTermStructure>())->name()); };
             }},
        });

    }

}
