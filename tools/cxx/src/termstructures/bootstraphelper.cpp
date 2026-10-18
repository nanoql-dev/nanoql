// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/bootstraphelper.hpp: BootstrapHelper's five date inspectors with their fallbacks, which every rate
// helper ported so far overrides by setting all five dates, and RelativeDateBootstrapHelper's construction, which no
// helper ported so far derives from, each on a helper of the harness's own that implies no quote, as
// tests/NanoQL.Tests/Vectors/Cxx/TermStructures/BootstrapHelperCxxVectorTests.cs defines them; and
// operator<<(Pillar::Choice).
//
// The BootstrapHelper of YieldTermStructure sets the five protected dates it is given, {"earliestDate", "latestDate",
// "maturityDate", "latestRelevantDate", "pillarDate"}, each a serial or null for Date(). The RelativeDateBootstrapHelper
// of YieldTermStructure, {"updateDates", "fixedDate", "earliestOffset", "pillarOffset"}, computes its earliest and
// pillar dates in initializeDates(), which its constructor calls, as the given offsets in days from the evaluation date
// its base recorded when its dates follow it, or from the fixed date when they don't; it sets no other date.

#include "convert.hpp"

#include <ql/errors.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/bootstraphelper.hpp>
#include <ql/termstructures/yieldtermstructure.hpp>

#include <initializer_list>
#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        // The harness's helper with chosen dates.
        class ChosenDatesHelper : public BootstrapHelper<YieldTermStructure> {
          public:
            ChosenDatesHelper(const Date& earliest,
                              const Date& latest,
                              const Date& maturity,
                              const Date& latestRelevant,
                              const Date& pillar)
            : BootstrapHelper<YieldTermStructure>(0.0) {
                earliestDate_ = earliest;
                latestDate_ = latest;
                maturityDate_ = maturity;
                latestRelevantDate_ = latestRelevant;
                pillarDate_ = pillar;
            }
            Real impliedQuote() const override { QL_FAIL("the harness's helper with chosen dates implies no quote"); }
        };

        // The harness's helper whose dates are offsets from the evaluation date or from a fixed date.
        class OffsetDatesHelper : public RelativeDateBootstrapHelper<YieldTermStructure> {
          public:
            OffsetDatesHelper(bool updateDates, const Date& fixedDate, Integer earliestOffset, Integer pillarOffset)
            : RelativeDateBootstrapHelper<YieldTermStructure>(0.0, updateDates), fixedDate_(fixedDate),
              earliestOffset_(earliestOffset), pillarOffset_(pillarOffset) {
                OffsetDatesHelper::initializeDates();
            }
            Real impliedQuote() const override { QL_FAIL("the harness's helper with offset dates implies no quote"); }

          protected:
            void initializeDates() override {
                Date from = updateDates_ ? evaluationDate_ : fixedDate_;
                earliestDate_ = from + earliestOffset_;
                pillarDate_ = from + pillarOffset_;
            }

          private:
            Date fixedDate_;
            Integer earliestOffset_, pillarOffset_;
        };

        const json::object& object(const json::value& v, std::initializer_list<const char*> keys,
                                   const std::string& what) {
            if (!v.is_object())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : keys)
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams(what + " take no key " + std::string(kv.key()));
            }
            for (const char* k : keys) {
                if (o.if_contains(k) == nullptr)
                    throw InvalidParams(what + " need " + k);
            }
            return o;
        }

        struct DatesSpec {
            DateSpec earliest, latest, maturity, latestRelevant, pillar;
        };

        DatesSpec dates(const json::value& v) {
            const json::object& o = object(
                v, {"earliestDate", "latestDate", "maturityDate", "latestRelevantDate", "pillarDate"},
                "a helper's dates");
            return {date(o.at("earliestDate")), date(o.at("latestDate")), date(o.at("maturityDate")),
                    date(o.at("latestRelevantDate")), date(o.at("pillarDate"))};
        }

        ChosenDatesHelper helper(const DatesSpec& s) {
            return {makeDate(s.earliest), makeDate(s.latest), makeDate(s.maturity), makeDate(s.latestRelevant),
                    makeDate(s.pillar)};
        }

        struct OffsetsSpec {
            bool updateDates;
            DateSpec fixedDate;
            Integer earliestOffset, pillarOffset;
        };

        OffsetsSpec offsets(const json::value& v) {
            const json::object& o = object(v, {"updateDates", "fixedDate", "earliestOffset", "pillarOffset"},
                                           "a relative helper's offsets");
            return {boolean(o.at("updateDates")), date(o.at("fixedDate")), integer(o.at("earliestOffset")),
                    integer(o.at("pillarOffset"))};
        }

        OffsetDatesHelper relativeHelper(const OffsetsSpec& s) {
            return {s.updateDates, makeDate(s.fixedDate), s.earliestOffset, s.pillarOffset};
        }

        // One of the five inspectors, on the helper a spec describes.
        template <class S, class H>
        Method inspector(S (*spec)(const json::value&),
                         H (*make)(const S&),
                         Date (BootstrapHelper<YieldTermStructure>::*read)() const) {
            return [spec, make, read](const Args& a) -> Call {
                arity(a, 1);
                S s = spec(a[0]);
                return [=] {
                    H h = make(s);
                    return result((h.*read)());
                };
            };
        }

        using Base = BootstrapHelper<YieldTermStructure>;

        const Register unit({
            {"BootstrapHelper<YieldTermStructure>::earliestDate", inspector(&dates, &helper, &Base::earliestDate)},
            {"BootstrapHelper<YieldTermStructure>::maturityDate", inspector(&dates, &helper, &Base::maturityDate)},
            {"BootstrapHelper<YieldTermStructure>::latestRelevantDate",
             inspector(&dates, &helper, &Base::latestRelevantDate)},
            {"BootstrapHelper<YieldTermStructure>::pillarDate", inspector(&dates, &helper, &Base::pillarDate)},
            {"BootstrapHelper<YieldTermStructure>::latestDate", inspector(&dates, &helper, &Base::latestDate)},
            {"RelativeDateBootstrapHelper<YieldTermStructure>::earliestDate",
             inspector(&offsets, &relativeHelper, &Base::earliestDate)},
            {"RelativeDateBootstrapHelper<YieldTermStructure>::maturityDate",
             inspector(&offsets, &relativeHelper, &Base::maturityDate)},
            {"RelativeDateBootstrapHelper<YieldTermStructure>::latestRelevantDate",
             inspector(&offsets, &relativeHelper, &Base::latestRelevantDate)},
            {"RelativeDateBootstrapHelper<YieldTermStructure>::pillarDate",
             inspector(&offsets, &relativeHelper, &Base::pillarDate)},
            {"RelativeDateBootstrapHelper<YieldTermStructure>::latestDate",
             inspector(&offsets, &relativeHelper, &Base::latestDate)},
            {"operator<<(Pillar::Choice)",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 auto c = enumeration<Pillar::Choice>(a[0]);
                 return [=] { return json::value(streamed(c)); };
             }},
        });

    }

}
