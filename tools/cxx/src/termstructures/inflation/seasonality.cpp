// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/inflation/seasonality.hpp: Seasonality's default isConsistent, which every QuantLib seasonality
// overrides, on the harness's own seasonality, which overrides only what Seasonality leaves abstract (its corrections
// add 0.001 to a zero rate and subtract 0.001 from a year-on-year rate), as tests/NanoQL.Tests/Vectors/Cxx/
// TermStructures/SeasonalityCxxVectorTests.cs defines it. "Seasonality::isConsistent" takes a curve spec, {"type":
// "ZeroInflationCurve" or "YoYInflationCurve", "referenceDate", "dates", "rates", "frequency", "dayCounter",
// "seasonality" (null), "enableExtrapolation"}, as tools/vectors/gen/termstructures/inflationtermstructure.py writes it:
// QuantLib's linear interpolated curve, built inside the call; "Seasonality::rate" takes the curve spec and a date,
// and returns the rate of the curve built with the harness's seasonality, which the curve's constructor checks with
// isConsistent and corrects with the seasonality.

#include "convert.hpp"

#include <ql/math/interpolations/linearinterpolation.hpp>
#include <ql/termstructures/inflation/interpolatedyoyinflationcurve.hpp>
#include <ql/termstructures/inflation/interpolatedzeroinflationcurve.hpp>
#include <ql/termstructures/inflation/seasonality.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        class BaseSeasonality : public Seasonality {
          public:
            Rate correctZeroRate(const Date&, Rate r, const InflationTermStructure&) const override {
                return r + 0.001;
            }
            Rate correctYoYRate(const Date&, Rate r, const InflationTermStructure&) const override {
                return r - 0.001;
            }
        };

        struct CurveSpec {
            bool zero = true;
            DateSpec referenceDate{true, 0};
            std::vector<DateSpec> dates;
            std::vector<Real> rates;
            Frequency frequency = Monthly;
            DayCounterSpec dayCounter;
            bool enableExtrapolation = false;
        };

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("an inflation curve spec needs ") + name);
            return *v;
        }

        CurveSpec curve(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected an inflation curve spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            if (o.size() != 8)
                throw InvalidParams("an inflation curve spec takes eight keys: " + json::serialize(v));
            CurveSpec s;
            std::string type = text(key(o, "type"));
            if (type != "ZeroInflationCurve" && type != "YoYInflationCurve")
                throw InvalidParams("unknown inflation curve " + type);
            s.zero = type == "ZeroInflationCurve";
            s.referenceDate = date(key(o, "referenceDate"));
            const json::value& dates = key(o, "dates");
            if (!dates.is_array())
                throw InvalidParams("expected the curve's dates as an array, got " + json::serialize(dates));
            for (const auto& d : dates.get_array())
                s.dates.push_back(date(d));
            s.rates = reals(key(o, "rates"));
            s.frequency = enumeration<Frequency>(key(o, "frequency"));
            s.dayCounter = dayCounter(key(o, "dayCounter"));
            if (!key(o, "seasonality").is_null())
                throw InvalidParams("the harness's seasonality takes a curve with none");
            s.enableExtrapolation = boolean(key(o, "enableExtrapolation"));
            return s;
        }

        ext::shared_ptr<InflationTermStructure> makeCurve(const CurveSpec& s,
                                                          const ext::shared_ptr<Seasonality>& seasonality) {
            std::vector<Date> dates;
            for (const auto& d : s.dates)
                dates.push_back(makeDate(d));
            ext::shared_ptr<InflationTermStructure> out;
            if (s.zero)
                out = ext::make_shared<InterpolatedZeroInflationCurve<Linear>>(
                    makeDate(s.referenceDate), dates, s.rates, s.frequency, makeDayCounter(s.dayCounter),
                    seasonality);
            else
                out = ext::make_shared<InterpolatedYoYInflationCurve<Linear>>(
                    makeDate(s.referenceDate), dates, s.rates, s.frequency, makeDayCounter(s.dayCounter),
                    seasonality);
            if (s.enableExtrapolation)
                out->enableExtrapolation();
            return out;
        }

        const Register unit({
            {"Seasonality::isConsistent",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 CurveSpec s = curve(a[0]);
                 return [=] { return json::value(BaseSeasonality().isConsistent(*makeCurve(s, {}))); };
             }},
            // (curve spec, date)
            {"Seasonality::rate",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 CurveSpec s = curve(a[0]);
                 DateSpec d = date(a[1]);
                 return [=] {
                     auto c = makeCurve(s, ext::make_shared<BaseSeasonality>());
                     if (s.zero)
                         return json::value(
                             ext::dynamic_pointer_cast<ZeroInflationTermStructure>(c)->zeroRate(makeDate(d)));
                     return json::value(ext::dynamic_pointer_cast<YoYInflationTermStructure>(c)->yoyRate(makeDate(d)));
                 };
             }},
        });

    }

}
