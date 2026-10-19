// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The cap/floor term-volatility specs: see capfloorvolspec.hpp.

#include "termstructures/volatility/capfloor/capfloorvolspec.hpp"

#include <ql/math/matrix.hpp>
#include <ql/time/period.hpp>

#include <initializer_list>
#include <string>

using namespace QuantLib;

namespace harness::capfloorvols {

    namespace {

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        void onlyKeys(const json::object& o, std::initializer_list<const char*> keys, const std::string& what) {
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : keys)
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
        }

        // The keys a surface and a curve share; "strikes" and the volatilities' shape are the caller's.
        SurfaceSpec common(const json::object& o, bool isSurface, const std::string& what) {
            SurfaceSpec s;
            bool hasDate = o.if_contains("settlementDate") != nullptr;
            bool hasDays = o.if_contains("settlementDays") != nullptr;
            if (hasDate == hasDays)
                throw InvalidParams(what + " spec needs one of settlementDate and settlementDays");
            s.fixed = hasDate;
            if (isSurface)
                onlyKeys(o, {s.fixed ? "settlementDate" : "settlementDays", "calendar", "convention", "optionTenors",
                             "strikes", "volatilities", "dayCounter", "enableExtrapolation"},
                         what);
            else
                onlyKeys(o, {s.fixed ? "settlementDate" : "settlementDays", "calendar", "convention", "optionTenors",
                             "volatilities", "dayCounter", "enableExtrapolation"},
                         what);
            if (s.fixed) {
                s.settlementDate = date(key(o, "settlementDate", what));
            } else {
                Integer n = integer(key(o, "settlementDays", what));
                if (n == Null<Integer>() || n < 0)
                    throw InvalidParams("expected a Natural, got " + json::serialize(key(o, "settlementDays", what)));
                s.settlementDays = static_cast<Natural>(n);
            }
            s.calendar = indexes::calendar(key(o, "calendar", what));
            s.convention = enumeration<BusinessDayConvention>(key(o, "convention", what));
            const json::value& tenors = key(o, "optionTenors", what);
            if (!tenors.is_array())
                throw InvalidParams(what + " needs optionTenors as a list");
            for (const json::value& t : tenors.get_array())
                s.optionTenors.push_back(period(t));
            s.dayCounter = dayCounter(key(o, "dayCounter", what));
            s.enableExtrapolation = boolean(key(o, "enableExtrapolation", what));
            return s;
        }

    }

    std::pair<Integer, TimeUnit> period(const json::value& v) {
        if (!v.is_array() || v.get_array().size() != 2)
            throw InvalidParams("expected a tenor as [length, units], got " + json::serialize(v));
        return {integer(v.get_array()[0]), enumeration<TimeUnit>(v.get_array()[1])};
    }

    SurfaceSpec surface(const json::value& v) {
        const std::string what = "a cap/floor term volatility surface";
        if (!v.is_object())
            throw InvalidParams("expected " + what + " spec, got " + json::serialize(v));
        const json::object& o = v.get_object();
        SurfaceSpec s = common(o, true, what);
        s.strikes = reals(key(o, "strikes", what));
        const json::value& vols = key(o, "volatilities", what);
        if (!vols.is_array())
            throw InvalidParams(what + " needs volatilities as a list of rows");
        for (const json::value& row : vols.get_array()) {
            std::vector<Real> r = reals(row);
            if (s.rows == 0)
                s.columns = r.size();
            else if (r.size() != s.columns)
                throw InvalidParams(what + " needs the volatilities' rows of one size");
            s.volatilities.insert(s.volatilities.end(), r.begin(), r.end());
            ++s.rows;
        }
        return s;
    }

    SurfaceSpec curve(const json::value& v) {
        const std::string what = "a cap/floor term volatility curve";
        if (!v.is_object())
            throw InvalidParams("expected " + what + " spec, got " + json::serialize(v));
        const json::object& o = v.get_object();
        SurfaceSpec s = common(o, false, what);
        s.volatilities = reals(key(o, "volatilities", what));
        return s;
    }

    ext::shared_ptr<CapFloorTermVolSurface> makeSurface(const SurfaceSpec& s) {
        std::vector<Period> tenors;
        for (const auto& t : s.optionTenors)
            tenors.emplace_back(t.first, t.second);
        Matrix vols(s.rows, s.columns, s.volatilities.begin(), s.volatilities.end());
        Calendar cal = indexes::makeCalendar(s.calendar);
        DayCounter dc = makeDayCounter(s.dayCounter);
        ext::shared_ptr<CapFloorTermVolSurface> out =
            s.fixed ? ext::make_shared<CapFloorTermVolSurface>(makeDate(s.settlementDate), cal, s.convention, tenors,
                                                               s.strikes, vols, dc)
                    : ext::make_shared<CapFloorTermVolSurface>(s.settlementDays, cal, s.convention, tenors, s.strikes,
                                                               vols, dc);
        if (s.enableExtrapolation)
            out->enableExtrapolation();
        return out;
    }

    ext::shared_ptr<CapFloorTermVolCurve> makeCurve(const SurfaceSpec& s) {
        std::vector<Period> tenors;
        for (const auto& t : s.optionTenors)
            tenors.emplace_back(t.first, t.second);
        Calendar cal = indexes::makeCalendar(s.calendar);
        DayCounter dc = makeDayCounter(s.dayCounter);
        ext::shared_ptr<CapFloorTermVolCurve> out =
            s.fixed ? ext::make_shared<CapFloorTermVolCurve>(makeDate(s.settlementDate), cal, s.convention, tenors,
                                                             s.volatilities, dc)
                    : ext::make_shared<CapFloorTermVolCurve>(s.settlementDays, cal, s.convention, tenors,
                                                             s.volatilities, dc);
        if (s.enableExtrapolation)
            out->enableExtrapolation();
        return out;
    }

}
