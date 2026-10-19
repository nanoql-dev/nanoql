// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "termstructures/volatility/swaption/swaptionvolmatrixspec.hpp"

#include <ql/math/matrix.hpp>

#include <initializer_list>

using namespace QuantLib;

namespace harness::termstructures {

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

        const json::array& array(const json::value& v, const std::string& what) {
            if (!v.is_array())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            return v.get_array();
        }

        std::vector<std::vector<Real>> rows(const json::value& v) {
            std::vector<std::vector<Real>> out;
            for (const json::value& row : array(v, "a matrix as an array of rows"))
                out.push_back(reals(row));
            for (const auto& row : out)
                if (row.size() != out.front().size())
                    throw InvalidParams("a matrix's rows differ in length");
            return out;
        }

        Matrix matrixOf(const std::vector<std::vector<Real>>& r) {
            Matrix m(r.size(), r.empty() ? 0 : r.front().size());
            for (Size i = 0; i < r.size(); ++i)
                for (Size j = 0; j < r[i].size(); ++j)
                    m[i][j] = r[i][j];
            return m;
        }

        std::vector<Period> periods(const std::vector<PeriodSpec>& p) {
            std::vector<Period> out;
            for (const PeriodSpec& x : p)
                out.push_back(makePeriod(x));
            return out;
        }

    }

    SwaptionVolMatrixSpec swaptionVolMatrix(const json::value& v) {
        if (!v.is_object())
            throw InvalidParams("expected a swaption volatility matrix spec, got " + json::serialize(v));
        const json::object& o = v.get_object();
        SwaptionVolMatrixSpec s;
        s.form = text(key(o, "form", "a matrix"));
        const std::string what = "a " + s.form + " matrix";
        if (s.form == "tenors" || s.form == "floating") {
            if (s.form == "tenors")
                onlyKeys(o, {"form", "referenceDate", "calendar", "convention", "optionTenors", "swapTenors",
                             "volatilities", "dayCounter", "flatExtrapolation", "type", "shifts", "enableExtrapolation"},
                         what);
            else
                onlyKeys(o, {"form", "calendar", "convention", "optionTenors", "swapTenors", "volatilities",
                             "dayCounter", "flatExtrapolation", "type", "shifts", "enableExtrapolation"},
                         what);
            for (const json::value& p : array(key(o, "optionTenors", what), "option tenors"))
                s.optionTenors.push_back(tenor(p));
        } else if (s.form == "dates") {
            onlyKeys(o, {"form", "referenceDate", "calendar", "convention", "optionDates", "swapTenors",
                         "volatilities", "dayCounter", "flatExtrapolation", "type", "shifts", "enableExtrapolation"},
                     what);
            for (const json::value& d : array(key(o, "optionDates", what), "option dates"))
                s.optionDates.push_back(date(d));
        } else {
            throw InvalidParams("unknown matrix form " + s.form);
        }
        if (s.form != "floating")
            s.referenceDate = date(key(o, "referenceDate", what));
        s.calendar = indexes::calendar(key(o, "calendar", what));
        s.convention = enumeration<BusinessDayConvention>(key(o, "convention", what));
        for (const json::value& p : array(key(o, "swapTenors", what), "swap tenors"))
            s.swapTenors.push_back(tenor(p));
        s.volatilities = rows(key(o, "volatilities", what));
        s.dayCounter = dayCounter(key(o, "dayCounter", what));
        s.flatExtrapolation = boolean(key(o, "flatExtrapolation", what));
        s.type = enumeration<VolatilityType>(key(o, "type", what));
        const json::value& shifts = key(o, "shifts", what);
        s.hasShifts = !shifts.is_null();
        if (s.hasShifts)
            s.shifts = rows(shifts);
        s.enableExtrapolation = boolean(key(o, "enableExtrapolation", what));
        return s;
    }

    ext::shared_ptr<SwaptionVolatilityMatrix> makeSwaptionVolMatrix(const SwaptionVolMatrixSpec& s) {
        Calendar cal = indexes::makeCalendar(s.calendar);
        DayCounter dc = makeDayCounter(s.dayCounter);
        Matrix vols = matrixOf(s.volatilities);
        Matrix shifts = s.hasShifts ? matrixOf(s.shifts) : Matrix();
        std::vector<Period> swaps = periods(s.swapTenors);
        ext::shared_ptr<SwaptionVolatilityMatrix> out;
        if (s.form == "tenors") {
            out = ext::make_shared<SwaptionVolatilityMatrix>(makeDate(s.referenceDate), cal, s.convention,
                                                             periods(s.optionTenors), swaps, vols, dc,
                                                             s.flatExtrapolation, s.type, shifts);
        } else if (s.form == "floating") {
            out = ext::make_shared<SwaptionVolatilityMatrix>(cal, s.convention, periods(s.optionTenors), swaps, vols,
                                                             dc, s.flatExtrapolation, s.type, shifts);
        } else {
            std::vector<Date> dates;
            for (const DateSpec& d : s.optionDates)
                dates.push_back(makeDate(d));
            out = ext::make_shared<SwaptionVolatilityMatrix>(makeDate(s.referenceDate), cal, s.convention, dates,
                                                             swaps, vols, dc, s.flatExtrapolation, s.type, shifts);
        }
        if (s.enableExtrapolation)
            out->enableExtrapolation();
        return out;
    }

}
