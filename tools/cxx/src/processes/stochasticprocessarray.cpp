// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/processes/stochasticprocessarray.hpp, .cpp: the members of StochasticProcessArray the wheel doesn't expose.
// "StochasticProcessArray" takes (processes, correlation, referenceDate, member, argument, argument2): each process a
// spec {"kind", "parameters"}, "ou" (speed, vol, x0, level), "gbm" (s0, mue, sigma) or "bsm" (s0, r, q, vol) on flat
// curves and a constant volatility from the reference date, on Actual/365 Fixed; the correlation as its list of rows.
// member "apply" takes (x0, dx) and returns the state; "time" takes a date's serial; "process" takes i and returns
// the i-th process's x0; "correlation" takes none and returns its rows.

#include "convert.hpp"

#include <ql/math/matrix.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/processes/geometricbrownianprocess.hpp>
#include <ql/processes/ornsteinuhlenbeckprocess.hpp>
#include <ql/processes/stochasticprocessarray.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct ProcessSpec {
            std::string kind;
            std::vector<Real> parameters;
        };

        std::vector<ProcessSpec> processes(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected a list of process specs, got " + json::serialize(v));
            std::vector<ProcessSpec> out;
            for (const json::value& p : v.as_array()) {
                if (!p.is_object() || !p.as_object().contains("kind") || !p.as_object().contains("parameters"))
                    throw InvalidParams("a process spec is {\"kind\", \"parameters\"}");
                ProcessSpec s{text(p.as_object().at("kind")), reals(p.as_object().at("parameters"))};
                std::size_t n = s.kind == "ou" ? 4 : s.kind == "gbm" ? 3 : s.kind == "bsm" ? 4 : 0;
                if (n == 0 || s.parameters.size() != n)
                    throw InvalidParams("unknown process " + s.kind + " or its parameters");
                out.push_back(s);
            }
            return out;
        }

        std::vector<std::vector<Real>> rows(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected a matrix's list of rows, got " + json::serialize(v));
            std::vector<std::vector<Real>> out;
            for (const json::value& row : v.as_array())
                out.push_back(reals(row));
            for (const auto& row : out) {
                if (row.size() != out[0].size())
                    throw InvalidParams("a matrix's rows have one length");
            }
            return out;
        }

        ext::shared_ptr<StochasticProcess1D> make(const ProcessSpec& s, const Date& today) {
            const std::vector<Real>& p = s.parameters;
            if (s.kind == "ou")
                return ext::make_shared<OrnsteinUhlenbeckProcess>(p[0], p[1], p[2], p[3]);
            if (s.kind == "gbm")
                return ext::make_shared<GeometricBrownianMotionProcess>(p[0], p[1], p[2]);
            DayCounter dc = Actual365Fixed();
            return ext::make_shared<BlackScholesMertonProcess>(
                Handle<Quote>(ext::make_shared<SimpleQuote>(p[0])),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, p[2], dc)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, p[1], dc)),
                Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(today, NullCalendar(), p[3], dc)));
        }

        const Register unit({
            {"StochasticProcessArray",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 std::vector<ProcessSpec> specs = processes(a[0]);
                 std::vector<std::vector<Real>> corr = rows(a[1]);
                 DateSpec reference = date(a[2]);
                 std::string m = text(a[3]);
                 if (m != "apply" && m != "time" && m != "process" && m != "correlation")
                     throw InvalidParams("unknown member " + m);
                 std::vector<Real> x0, dx;
                 DateSpec d{true, 0};
                 Integer i = 0;
                 if (m == "apply") {
                     x0 = reals(a[4]);
                     dx = reals(a[5]);
                 } else if (m == "time") {
                     d = date(a[4]);
                 } else if (m == "process") {
                     i = integer(a[4]);
                     if (i < 0 || static_cast<std::size_t>(i) >= specs.size())
                         throw InvalidParams("a process's index");
                 }
                 return [=] {
                     Date today = makeDate(reference);
                     std::vector<ext::shared_ptr<StochasticProcess1D>> ps;
                     for (const ProcessSpec& s : specs)
                         ps.push_back(make(s, today));
                     Matrix c(corr.size(), corr.empty() ? 0 : corr[0].size());
                     for (Size r = 0; r < c.rows(); ++r)
                         for (Size k = 0; k < c.columns(); ++k)
                             c[r][k] = corr[r][k];
                     StochasticProcessArray p(ps, c);
                     if (m == "apply") {
                         Array y = p.apply(Array(x0.begin(), x0.end()), Array(dx.begin(), dx.end()));
                         json::array out;
                         for (Real v : y)
                             out.emplace_back(v);
                         return json::value(std::move(out));
                     }
                     if (m == "time")
                         return json::value(p.time(makeDate(d)));
                     if (m == "process")
                         return json::value(p.process(i)->x0());
                     Matrix cc = p.correlation();
                     json::array out;
                     for (Size r = 0; r < cc.rows(); ++r) {
                         json::array row;
                         for (Size k = 0; k < cc.columns(); ++k)
                             row.emplace_back(cc[r][k]);
                         out.emplace_back(std::move(row));
                     }
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
