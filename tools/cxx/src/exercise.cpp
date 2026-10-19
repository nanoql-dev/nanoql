// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/exercise.hpp: EarlyExercise::payoffAtExpiry and AmericanExercise(latestDate, payoffAtExpiry), which the wheel
// doesn't expose, with the inspectors of every exercise (type, dates, lastDate, dateAt). An exercise argument is a
// spec built inside the call, so that a constructor's refusal is QuantLib's:
//
//     Exercise           {"exerciseType"}
//     EarlyExercise      {"exerciseType", "payoffAtExpiry"}
//     AmericanExercise   {"earliestDate", "latestDate", "payoffAtExpiry"}, or without "earliestDate" for the
//                        constructor from the latest date only
//     BermudanExercise   {"dates", "payoffAtExpiry"}
//     EuropeanExercise   {"date"}
//
// each with its "type"; the exercise type as Exercise::Type's integer, dates as serials or null for Date().

#include "convert.hpp"

#include <ql/exercise.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct ExerciseSpec {
            std::string type;
            Exercise::Type exerciseType;
            bool hasEarliestDate;
            DateSpec earliestDate, latestDate, date;
            std::vector<DateSpec> dates;
            bool payoffAtExpiry;
        };

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        void onlyKeys(const json::object& o, const std::vector<const char*>& keys, const std::string& what) {
            for (const auto& kv : o) {
                bool known = kv.key() == "type";
                for (const char* k : keys)
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
        }

        ExerciseSpec exercise(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected an exercise spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            const json::value* type = o.if_contains("type");
            if (type == nullptr || !type->is_string())
                throw InvalidParams("an exercise spec needs a type: " + json::serialize(v));
            ExerciseSpec s{std::string(type->get_string()), Exercise::European, false, {}, {}, {}, {}, false};
            const std::string& what = s.type;
            if (s.type == "Exercise") {
                onlyKeys(o, {"exerciseType"}, what);
                s.exerciseType = enumeration<Exercise::Type>(key(o, "exerciseType", what));
            } else if (s.type == "EarlyExercise") {
                onlyKeys(o, {"exerciseType", "payoffAtExpiry"}, what);
                s.exerciseType = enumeration<Exercise::Type>(key(o, "exerciseType", what));
                s.payoffAtExpiry = boolean(key(o, "payoffAtExpiry", what));
            } else if (s.type == "AmericanExercise") {
                onlyKeys(o, {"earliestDate", "latestDate", "payoffAtExpiry"}, what);
                s.hasEarliestDate = o.if_contains("earliestDate") != nullptr;
                if (s.hasEarliestDate)
                    s.earliestDate = date(key(o, "earliestDate", what));
                s.latestDate = date(key(o, "latestDate", what));
                s.payoffAtExpiry = boolean(key(o, "payoffAtExpiry", what));
            } else if (s.type == "BermudanExercise") {
                onlyKeys(o, {"dates", "payoffAtExpiry"}, what);
                const json::value& ds = key(o, "dates", what);
                if (!ds.is_array())
                    throw InvalidParams("expected an array of date serials, got " + json::serialize(ds));
                for (const json::value& d : ds.get_array())
                    s.dates.push_back(date(d));
                s.payoffAtExpiry = boolean(key(o, "payoffAtExpiry", what));
            } else if (s.type == "EuropeanExercise") {
                onlyKeys(o, {"date"}, what);
                s.date = date(key(o, "date", what));
            } else {
                throw InvalidParams("unknown exercise " + s.type);
            }
            return s;
        }

        ext::shared_ptr<Exercise> makeExercise(const ExerciseSpec& s) {
            if (s.type == "Exercise")
                return ext::make_shared<Exercise>(s.exerciseType);
            if (s.type == "EarlyExercise")
                return ext::make_shared<EarlyExercise>(s.exerciseType, s.payoffAtExpiry);
            if (s.type == "AmericanExercise") {
                if (s.hasEarliestDate)
                    return ext::make_shared<AmericanExercise>(makeDate(s.earliestDate), makeDate(s.latestDate),
                                                              s.payoffAtExpiry);
                return ext::make_shared<AmericanExercise>(makeDate(s.latestDate), s.payoffAtExpiry);
            }
            if (s.type == "BermudanExercise") {
                std::vector<Date> dates;
                for (const DateSpec& d : s.dates)
                    dates.push_back(makeDate(d));
                return ext::make_shared<BermudanExercise>(dates, s.payoffAtExpiry);
            }
            return ext::make_shared<EuropeanExercise>(makeDate(s.date));
        }

        // An exercise spec of a type derived from EarlyExercise.
        ExerciseSpec earlyExercise(const json::value& v) {
            ExerciseSpec s = exercise(v);
            if (s.type == "Exercise" || s.type == "EuropeanExercise")
                throw InvalidParams(s.type + " is no EarlyExercise");
            return s;
        }

        Size index(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0)
                throw InvalidParams("expected a Size, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        const Register unit({
            {"Exercise::type",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 ExerciseSpec s = exercise(a[0]);
                 return [=] { return json::value(static_cast<int>(makeExercise(s)->type())); };
             }},
            {"Exercise::dates",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 ExerciseSpec s = exercise(a[0]);
                 return [=] {
                     // The exercise outlives the loop over the dates it holds.
                     ext::shared_ptr<Exercise> e = makeExercise(s);
                     json::array out;
                     for (const Date& d : e->dates())
                         out.push_back(result(d));
                     return json::value(out);
                 };
             }},
            {"Exercise::lastDate",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 ExerciseSpec s = exercise(a[0]);
                 return [=] { return result(makeExercise(s)->lastDate()); };
             }},
            {"Exercise::dateAt",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 ExerciseSpec s = exercise(a[0]);
                 Size i = index(a[1]);
                 return [=] { return result(makeExercise(s)->dateAt(i)); };
             }},
            {"EarlyExercise::payoffAtExpiry",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 ExerciseSpec s = earlyExercise(a[0]);
                 return [=] {
                     return json::value(ext::dynamic_pointer_cast<EarlyExercise>(makeExercise(s))->payoffAtExpiry());
                 };
             }},
        });

    }

}
