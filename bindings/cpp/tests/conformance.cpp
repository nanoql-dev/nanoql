// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The C++ layer's conformance suite (docs/bindings.md, "Checking the bindings"): every kind both ways through nanoql.hpp,
// the status codes as exceptions, a copy, a move and a destructor's handles, null objects, overloads and defaults,
// interfaces as bases, generators, bound engines, calibrations to C#'s bits, the experimental opt-in and a user's
// classes extending nanoql's; stable.cpp includes the headers without the opt-in. It runs on the test build, whose library has the test exports and whose header nqlt.hpp
// holds the test types, a user's assembly over nanoql's:
//   nanoql-bind build --rid <rid> --out <folder> --test-exports
//   cmake -S bindings/cpp/tests -B <build> -DNANOQL_DIR=<folder>
//   cmake --build <build> --config Release
//   ctest --test-dir <build> -C Release
#include <cmath>
#include <cstdio>
#include <cstring>
#include <limits>
#include <string>
#include <thread>
#include <type_traits>
#include <vector>

#include "nqlt.hpp"

using namespace nanoql;
using nqlt::Echo;
using nqlt::EchoObject;
using nqlt::Fixtures;
using nqlt::References;

// One argument converts to no class implicitly: a constructor one argument can call is explicit, as C# converts none.
static_assert(!std::is_convertible_v<const YieldTermStructure&, HullWhite> && std::is_constructible_v<HullWhite, const YieldTermStructure&>,
              "HullWhite(curve, a = 0.1, sigma = 0.01) is explicit");
static_assert(!std::is_convertible_v<double, ConvexMonotone> && !std::is_convertible_v<int32_t, Rounding>, "one number makes no object implicitly");

static int checks, failures;

#define CHECK(cond, what)                                                         \
    do {                                                                          \
        checks++;                                                                 \
        if (!(cond)) {                                                            \
            failures++;                                                           \
            std::printf("FAILED %s:%d: %s (%s)\n", __FILE__, __LINE__, what, #cond); \
        }                                                                         \
    } while (0)

// A call refused with an exception of a class and a code.
#define REFUSED(call, type, expected)                                                                 \
    do {                                                                                              \
        checks++;                                                                                     \
        int code_ = -1;                                                                               \
        bool caught_ = false;                                                                         \
        try {                                                                                         \
            (void)(call);                                                                             \
        } catch (const type& e_) {                                                                    \
            caught_ = true;                                                                           \
            code_ = e_.code();                                                                        \
        } catch (const std::exception& e_) {                                                          \
            std::printf("FAILED %s:%d: %s threw another exception: %s\n", __FILE__, __LINE__, #call, e_.what()); \
            failures++;                                                                               \
            break;                                                                                    \
        }                                                                                             \
        if (!caught_ || code_ != (expected)) {                                                        \
            failures++;                                                                               \
            std::printf("FAILED %s:%d: %s: code %d, expected %d\n", __FILE__, __LINE__, #call, code_, (expected)); \
        }                                                                                             \
    } while (0)

static uint64_t bits(double x)
{
    uint64_t u;
    std::memcpy(&u, &x, sizeof u);
    return u;
}

#define SAME(a, b) CHECK(bits(a) == bits(b), #a " and " #b " have the same bits")

// ---- every kind both ways ----
static void test_kinds()
{
    CHECK(Echo::double_(3.25) == 3.25, "a double");
    CHECK(std::isnan(Echo::double_(std::numeric_limits<double>::quiet_NaN())), "NaN crosses as NaN");
    CHECK(bits(Echo::double_(-0.0)) == bits(-0.0), "-0.0 keeps its sign");
    CHECK(Echo::int_(std::numeric_limits<int32_t>::min()) == std::numeric_limits<int32_t>::min(), "int32 minimum");
    CHECK(Echo::long_(std::numeric_limits<int64_t>::max()) == std::numeric_limits<int64_t>::max(), "int64 maximum");
    CHECK(Echo::bool_(true) && !Echo::bool_(false), "bools");

    // Strings: UTF-8 both ways; a null string comes back empty.
    std::string text = "Z\xc3\xbcrich \xe2\x82\xac \xf0\x9f\x93\x88";
    CHECK(Echo::string(text) == text, "a non-ASCII string round-trips");
    CHECK(Echo::stringLength(text) == 11, "11 UTF-16 code units in .NET");
    CHECK(Echo::string("") == "", "an empty string stays empty");

    // Dates: QuantLib's serial; Date() is no date.
    CHECK(Echo::date(Date(1, Month::January, 1901)).serial() == 367, "the first date");
    CHECK(Echo::date(Date(31, Month::December, 2199)).serial() == 109574, "the last date");
    CHECK(Echo::optDate(Date()).serial() == 0, "no date");
    Date d(15, Month::March, 2023);
    CHECK(d.year() == 2023 && d.month() == Month::March && d.dayOfMonth() == 15, "a date's year, month and day");
    CHECK(d + Period(1, TimeUnit::Years) == Date(15, Month::March, 2024), "Date's operators are nanoql's");
    CHECK(Period(1, TimeUnit::Years) == Period(12, TimeUnit::Months), "Period's equality is nanoql's");
    REFUSED(Date(1, Month::January, 1900), argument_out_of_range_error, NQL_ERROR_ARGUMENT_RANGE);

    Period p = Echo::period(Period(6, TimeUnit::Months));
    CHECK(p.length() == 6 && p.units() == TimeUnit::Months, "a period by value");
    CHECK(Echo::optPeriod(Period(6, TimeUnit::Months))->length() == 6, "a nullable period");
    CHECK(!Echo::optPeriod(std::nullopt), "no period");
    CHECK(Echo::enum_(TimeUnit::Years) == TimeUnit::Years, "an enum");
    CHECK(Echo::optEnum(TimeUnit::Weeks) == TimeUnit::Weeks && !Echo::optEnum(std::nullopt), "a nullable enum");
    CHECK(Echo::complex({1.5, -2.25}) == std::complex<double>(1.5, -2.25), "a complex number");
    CHECK(Echo::optDouble(2.5) == 2.5 && !Echo::optDouble(std::nullopt), "a nullable double");
    CHECK(Echo::optInt(7) == 7 && Echo::optBool(true) == true && !Echo::optBool(std::nullopt), "nullable ints and bools");
    CHECK(Echo::optLong(int64_t{1} << 40) == int64_t{1} << 40 && !Echo::optLong(std::nullopt), "a nullable long");
    CHECK(Echo::optComplex(std::complex<double>(0.5, -1.0)) == std::complex<double>(0.5, -1.0) && !Echo::optComplex(std::nullopt), "a nullable complex number");

    // A nanoql struct behind a handle.
    Currency usd = ConventionSet::quantLib().currency("USD");
    Money money(usd, 12.5);
    CHECK(Echo::money(money).value() == 12.5 && Echo::money(money) == money, "a struct by a new handle to its copy");
    CHECK(Echo::optMoney(money).value() == 12.5 && !Echo::optMoney(Money(nullptr)), "a nullable struct");
    REFUSED(Echo::money(Money(nullptr)), argument_null_error, NQL_ERROR_ARGUMENT_NULL);
    REFUSED(Echo::money(object_cast<Money>(usd)), handle_error, NQL_ERROR_TYPE);
}

static void test_arrays()
{
    std::vector<double> out = Echo::doubles({1.0, -0.0, std::numeric_limits<double>::quiet_NaN(), 4.5});
    CHECK(out.size() == 4 && bits(out[1]) == bits(-0.0) && std::isnan(out[2]), "doubles in and out");
    std::vector<double> none;
    CHECK(Echo::doublesCount(nullptr) == -1 && Echo::doublesCount(none) == 0, "null and empty differ");
    CHECK((Echo::ints({3, -1, 2}) == std::vector<int32_t>{3, -1, 2}), "ints");
    CHECK((Echo::bools({true, false, true}) == std::vector<bool>{true, false, true}), "bools");
    std::vector<Date> dates{Date(15, Month::March, 2023), Date(1, Month::January, 1901)};
    CHECK(Echo::dates(dates).size() == 2 && Echo::dateSet(dates)[0].serial() == 367, "dates");
    std::vector<std::optional<double>> opt = Echo::optDoubles({1.0, std::nullopt, 3.0});
    CHECK(opt.size() == 3 && opt[0] == 1.0 && !opt[1] && opt[2] == 3.0, "nullable doubles");
    CHECK(Echo::periods({Period(1, TimeUnit::Years), Period(3, TimeUnit::Months)})[1].length() == 3, "periods");
    CHECK((Echo::enums({TimeUnit::Days, TimeUnit::Years}) == std::vector<TimeUnit>{TimeUnit::Days, TimeUnit::Years}), "enums");
    CHECK((Echo::strings({"a", "\xc3\xa9t\xc3\xa9", ""}) == std::vector<std::string>{"a", "\xc3\xa9t\xc3\xa9", ""}), "strings");
    CHECK(Echo::complexes({{1, 2}, {-3, 4}})[1] == std::complex<double>(-3, 4), "complex numbers");
    std::vector<std::vector<double>> rows = Echo::jagged({{1, 2}, {}, {3, 4, 5, 6}});
    CHECK(rows.size() == 3 && rows[1].empty() && rows[2][3] == 6.0, "a jagged array");
    auto m = Echo::map({{Date(9, Month::December, 2025), 0.02}, {Date(15, Month::March, 2023), 0.01}});
    CHECK(m.size() == 2 && m[0].first == Date(15, Month::March, 2023) && m[0].second == 0.01, "a dictionary, by key");
    auto [whole, fraction] = Echo::split(3.75);
    CHECK(whole == 3 && fraction == 0.75, "a tuple");
    Matrix t = Echo::transpose(Matrix{{1, 2, 3}, {4, 5, 6}});
    CHECK(t.rows() == 3 && t.columns() == 2 && t(0, 1) == 4 && t(2, 1) == 6, "a matrix by rows");
    CHECK((Echo::twice({1.5, -2}) == std::vector<double>{3.0, -4.0}), "a vector");
    std::vector<double> span(3, 0.0);
    Echo::fill(span, 7.5);
    CHECK(span[0] == 7.5 && span[2] == 7.5, "a span written in place");

    // A vector the member writes into (R15): a line search's step moves the caller's point, as C# moves it.
    std::vector<double> point{0.5, 0.2};
    double step = BoundaryConstraint(-1.0, 1.0).update(point, {0.1, -0.3}, 1.0);
    CHECK(step == 1.0 && point == References::constraintUpdate(), "an in-out vector moved as C# moves it");

#ifdef NANOQL_EXPERIMENTAL
    Distribution dist = LossDistBinomial(10, 10.0).evaluate(5, 1.0, 0.3);
    double mass = 0;
    for (std::size_t k = 0; k < dist.x.size(); k++) mass += dist.density[k] * dist.dx[k];
    CHECK(dist.x.size() == 10 && std::fabs(mass - 1.0) < 1e-12 && dist.excess[0] == 1.0, "a loss distribution");
#endif
}

// ---- the status codes as exceptions ----
static void test_codes()
{
    struct kind {
        int32_t thrown, code;
    } kinds[] = {{1, 1}, {2, 2}, {3, 3}, {4, 4}, {5, 5}, {6, 6}, {7, 7}, {8, 8}, {11, 11}, {12, 12}, {13, 13}, {14, 12}, {15, 13}, {0, 99}};
    for (kind k : kinds) REFUSED(nqlt::Refusals::throw_(k.thrown), error, k.code);
    REFUSED(nqlt::Refusals::throw_(2), argument_error, NQL_ERROR_ARGUMENT);
    REFUSED(nqlt::Refusals::throw_(3), argument_null_error, NQL_ERROR_ARGUMENT_NULL);
    REFUSED(nqlt::Refusals::throw_(4), argument_out_of_range_error, NQL_ERROR_ARGUMENT_RANGE);
    try {
        nqlt::Refusals::throw_(2);
        CHECK(false, "refused");
    } catch (const argument_error& e) {
        CHECK(e.param() == "x" && e.type_name() == "System.ArgumentException", "the parameter and the .NET exception");
        CHECK(std::string(e.what()).find("a bad argument") != std::string::npos, "the message");
        CHECK((std::is_base_of_v<std::runtime_error, error>), "every refusal is a std::runtime_error");
    }
    REFUSED(nqlt::Refusals::documentedElement(5), argument_out_of_range_error, NQL_ERROR_ARGUMENT_RANGE);
    REFUSED(nqlt::Refusals::undocumentedElement(5), error, NQL_ERROR_INTERNAL);
    REFUSED(FlatForward(Date(15, Month::March, 2023), 0.03, DayCounter(nullptr)), argument_null_error, NQL_ERROR_ARGUMENT_NULL);
    REFUSED(ConventionSet::quantLib().calendar("NoSuchCalendar"), argument_error, NQL_ERROR_ARGUMENT);
    REFUSED(ConventionsXml::read("no-such-conventions.xml"), error, NQL_ERROR_IO);
}

// ---- handles: copies, moves, the destructor, null ----
static void test_handles()
{
    int64_t live0 = liveHandles();
    {
        EchoObject a(2.0, "two");
        CHECK(a.typeName() == "NanoQL.Bind.TestTypes.EchoObject", "the .NET type");
        CHECK(a.toString().find("two") != std::string::npos, "ToString");
        CHECK(liveHandles() == live0 + 1, "one handle");
        EchoObject copy = a;
        CHECK(liveHandles() == live0 + 2 && copy.handle() != a.handle() && copy == a, "a copy takes a new handle to the same object");
        EchoObject moved = std::move(copy);
        CHECK(liveHandles() == live0 + 2 && !copy && moved == a, "a move takes the handle");
        moved = a;
        CHECK(liveHandles() == live0 + 2, "a copy assignment releases the old handle");
        EchoObject b(2.0, "two");
        CHECK(a == b && a.hashCode() == b.hashCode(), "equal objects, equal hashes");
        CHECK(a.scaled(3.0).value() == 6.0 && a.value() == 2.0, "immutable");
        CHECK(a.withName("three").name() == "three", "a record's with");
        EchoObject null(nullptr);
        CHECK(!null && null == EchoObject(nullptr) && !(null == a), "null objects");
        Object base = a;
        CHECK(object_cast<EchoObject>(base).value() == 2.0, "an object as its own class");
        REFUSED(object_cast<EchoObject>(ConventionSet::quantLib().calendar("TARGET")).value(), handle_error, NQL_ERROR_TYPE);
        std::vector<EchoObject> objs = Echo::objects({a, b});
        CHECK(objs.size() == 2 && objs[1].value() == 2.0, "objects in and out");
    }
    CHECK(liveHandles() == live0, "every handle released by its destructor");

    // A move assignment onto a live object; copy and move assignments on a class whose Object two base lines reach
    // (HullWhite: Vasicek and ITermStructureConsistentModel), each keeping the source's object and parameters.
    {
        EchoObject x(1.0, "one"), y(5.0, "five");
        int64_t live1 = liveHandles();
        x = std::move(y);
        CHECK(liveHandles() == live1 - 1 && !y && x.value() == 5.0, "a move assignment releases the old handle and takes the source's");
        FlatForward flat(Date(15, Month::March, 2023), 0.03, ConventionSet::quantLib().dayCounter("Actual365Fixed"));
        HullWhite h1(flat, 0.1, 0.01), h2(flat, 0.2, 0.02);
        int64_t live2 = liveHandles();
        h1 = h2;
        CHECK(liveHandles() == live2 && h1 == h2 && h1.a() == 0.2 && h1.sigma() == 0.02, "a copy assignment across two base lines");
        HullWhite h3(flat, 0.3, 0.03);
        h1 = std::move(h3);
        CHECK(liveHandles() == live2 && !h3 && h1.a() == 0.3 && h1.sigma() == 0.03 && h2.a() == 0.2, "a move assignment across two base lines");
        const ITermStructureConsistentModel& consistent = h1;
        CHECK(consistent.termStructure().discount(2.0) == flat.discount(2.0), "the object through its other base line");
    }
    CHECK(liveHandles() == live0, "every handle released after the assignments");

    // A result of a base class, its members its own; a member of a base reached through a derived class.
    Calendar cal = ConventionSet::quantLib().calendar("TARGET");
    CHECK(cal.typeName() == "NanoQL.Time.Calendars.TARGET" || cal.typeName().find("TARGET") != std::string::npos, "the object's own class");
    CHECK(cal.isBusinessDay(Date(30, Month::October, 2026)), "a calendar's member");
    FlatForward curve(Date(15, Month::March, 2023), 0.03, ConventionSet::quantLib().dayCounter("Actual365Fixed"));
    const YieldTermStructure& as_base = curve;
    CHECK(curve.discount(5.0) == as_base.discount(5.0), "a base's member through a derived class");
}

// ---- generators: snapshots drawn in batches, by several threads at once ----
static void test_generators()
{
    MersenneTwisterUniformRng mt(42);
    auto [v1, w1, n1] = mt.draws(1000);
    auto [v2, w2, n2] = mt.draws(1000);
    CHECK(v1 == v2 && w1[0] == 1.0, "the same snapshot gives the same draws");
    auto [all, wa, na] = mt.draws(2000);
    auto [rest, wr, nr] = n1.draws(1000);
    std::vector<double> joined = v1;
    joined.insert(joined.end(), rest.begin(), rest.end());
    CHECK(all == joined, "a stream continues from the next snapshot");
    REFUSED(mt.draws(-1), argument_out_of_range_error, NQL_ERROR_ARGUMENT_RANGE);
    std::vector<std::vector<double>> seen(4);
    std::vector<std::thread> threads;
    for (auto& s : seen) threads.emplace_back([&mt, &s] { s = std::get<0>(mt.draws(1000)); });
    for (auto& t : threads) t.join();
    bool same = true;
    for (auto& s : seen) same = same && s == v1;
    CHECK(same, "threads draw from one snapshot at once");
    SobolRsg sobol(5, 1, SobolRsgDirectionIntegers::Jaeckel, true);
    Matrix points = std::get<0>(sobol.draws(3));
    CHECK(points.rows() == 3 && points.columns() == 5 && points(0, 4) == 0.5, "a sequence generator's points by rows");
    nqlt::CountingRng counting(10.0, 0.5);
    auto [cv, cw, next] = counting.draws(3);
    CHECK((cv == std::vector<double>{10.0, 10.5, 11.0}) && std::get<0>(next.draws(1))[0] == 11.5, "a user's generator");
}

// ---- overloads and defaults ----
static void test_overloads()
{
    Calendar cal = ConventionSet::quantLib().calendar("TARGET");
    Date d(30, Month::October, 2026);
    CHECK(cal.advance(d, 1, TimeUnit::Days) == Date(2, Month::November, 2026), "an overload by its arguments");
    CHECK(cal.advance(d, Period(1, TimeUnit::Weeks)) == Date(6, Month::November, 2026), "another");
    CHECK(Echo::total(2.5) == 2.5, "a number");
    // A base's form brought in beside the class's own of its name: AbcdFunction's primitive(t) is AbcdMathFunction's.
    AbcdFunction abcd;
    CHECK(abcd.primitive(1.0) == AbcdMathFunction(-0.06, 0.17, 0.54, 0.17).primitive(1.0) && std::isfinite(abcd.primitive(0.0, 1.0, 2.0)),
          "a base's form through a using-declaration");
    // A braced list of numbers fits several collections' views, as in any C++ overload set: a vector names one.
    CHECK(Echo::total(std::vector<double>{1.0, 2.0, 3.5}) == 6.5, "a collection");
    CHECK(Echo::total(array_view<std::vector<double>>{{1.0, 2.0}, {}, {4.0}}) == 7.0, "a jagged array");
    CHECK(Echo::total(array_view<std::pair<Date, double>>{{Date(2, Month::January, 2025), 1.5}, {Date(2, Month::January, 2026), 2.0}}) == 3.5, "a dictionary");
    CHECK(Echo::total(array_view<int64_t>{int64_t{1} << 40, 1}) == (int64_t{1} << 40) + 1, "longs");
    CHECK(Echo::total({EchoObject(1.0, "a"), EchoObject(2.5, "b")}) == 3.5, "objects");
    std::vector<double> span{1.0, -2.0};
    Echo::scale(span, 2.0);
    CHECK((span == std::vector<double>{2.0, -4.0}) && Echo::scale(1.5, 2.0) == 3.0, "a span or a number");

    DayCounter dc = ConventionSet::quantLib().dayCounter("Actual365Fixed");
    FlatForward a(Date(15, Month::March, 2023), 0.03, dc);
    FlatForward b(Date(15, Month::March, 2023), 0.03, dc, Compounding::Continuous, Frequency::Annual);
    CHECK(a.discount(5.0) == b.discount(5.0) && a.discount(5.0) == std::exp(-0.15), "nanoql's defaults");
}

// An optional struct left out: its default, what C# passes (R16); its implicit constructor too.
static void test_defaults()
{
    ConventionSet qs = ConventionSet::quantLib();
    std::vector<BootstrapHelper_YieldTermStructure> helpers = Fixtures::depositHelpers();
    PiecewiseFlatForward curve = PiecewiseFlatForward::create(0, qs.calendar("TARGET"), helpers, qs.dayCounter("Actual365Fixed"), Fixtures::context());
    SAME(curve.discount(2.0), References::piecewiseDiscount());
    PiecewiseFlatForward explicit_curve = PiecewiseFlatForward::create(0, qs.calendar("TARGET"), helpers, qs.dayCounter("Actual365Fixed"), Fixtures::context(),
                                                                       nullptr, nullptr, BackwardFlat(), IterativeBootstrap());
    SAME(explicit_curve.discount(2.0), References::piecewiseDiscount());
}

// ---- misc: versions, deprecation, conventions, a user's classes ----
#if defined(__clang__) || defined(__GNUC__)
#define ALLOW_DEPRECATED(x) _Pragma("GCC diagnostic push") _Pragma("GCC diagnostic ignored \"-Wdeprecated-declarations\"") x _Pragma("GCC diagnostic pop")
#else
#define ALLOW_DEPRECATED(x) __pragma(warning(suppress : 4996)) x
#endif

static void test_misc()
{
    CHECK(abiVersion() == NQL_ABI_VERSION && version() == NQL_VERSION, "the versions");
    CHECK(Date::todaysDate() > Date(1, Month::January, 2025), "today");
    double old = 0;
    ALLOW_DEPRECATED(old = nqlt::Deprecations::old(2.0);)
    CHECK(old == 2.0, "a deprecated member still runs");
}

static void test_conventions()
{
    DayCounter dc = ConventionSet::quantLib().dayCounter("Actual365Fixed");
    CHECK(dc.name() == "Actual/365 (Fixed)", "a set's day counter");
    CHECK(ConventionSet(nullptr).dayCounter("Actual365Fixed") == dc, "a null set is the current provider read as a set");

    // A user's class, in its own namespace, extending nanoql's: passed to nanoql's member.
    nqlt::UserProvider user;
    const ConventionProvider& provider = user;
    ConventionProvider::setDefault(provider);
    REFUSED(ConventionSet(nullptr).dayCounter("Actual365Fixed"), error, NQL_ERROR_INVALID_OPERATION);
    ConventionProvider::setDefault(ConventionProvider::quantLib());
    CHECK(ConventionSet(nullptr).dayCounter("Actual365Fixed") == dc, "QuantLib's again");

    // A user's class named like one of nanoql's (B2-1): each keeps its own, nanoql's named with its namespace.
    nqlt::Calendar mine("TARGET");
    nanoql::Calendar theirs = mine.nanoqls();
    CHECK(mine.name() == "TARGET" && theirs.isBusinessDay(Date(30, Month::October, 2026)), "the user's Calendar beside nanoql's");
    CHECK((!std::is_base_of_v<nanoql::Calendar, nqlt::Calendar>), "the user's Calendar isn't nanoql's");
    CHECK(theirs == ConventionSet::quantLib().calendar("TARGET"), "the user's member returns nanoql's calendar");
}

// ---- bound engines ----
static void test_engines()
{
    EvaluationContext ctx = Fixtures::context();
    YieldTermStructure rf = Fixtures::riskFree();
    BoundEngine swap_engine = DiscountingSwapEngine::bind(rf);
    VanillaSwap swap = Fixtures::swapBuilder().withPricingEngine(swap_engine).toVanillaSwap(ctx);
    SAME(swap.fixedRate(), References::swapFixedRate());
    BoundEngine heston_engine = AnalyticHestonEngine::bind(HestonModel(nullptr), std::optional<int32_t>());
    REFUSED(Fixtures::swapBuilder().withPricingEngine(heston_engine), handle_error, NQL_ERROR_TYPE);
    HestonModelHelper helper = Fixtures::hestonHelper().setPricingEngine(heston_engine);
    SAME(helper.modelValue(Fixtures::heston(), ctx), References::hestonModelValue());
}

// ---- calibrations: a calibrated model through its interface, to C#'s parameters bit for bit ----
static void test_calibrations()
{
    EvaluationContext ctx = Fixtures::calibrationContext();
    Gsr gsr = Fixtures::gsr();
    HullWhite hw = Fixtures::calibrationHullWhite();
    BoundEngine g1d = Gaussian1dSwaptionEngine::bind();
    BoundEngine cap_engine = AnalyticCapFloorEngine::bind();
    std::vector<SwaptionHelper> priced;
    for (const SwaptionHelper& s : Fixtures::swaptions()) priced.push_back(s.setPricingEngine(g1d));
    SAME(priced[1].modelValue(gsr, ctx), References::gsrSwaptionModelValue());
    REFUSED(priced[0].modelValue(hw, ctx), error, NQL_ERROR_INVALID_CAST);
    std::vector<CapHelper> capped;
    for (const CapHelper& c : Fixtures::caps()) capped.push_back(c.setPricingEngine(cap_engine));
    SAME(capped[1].modelValue(hw, ctx), References::hullWhiteCapModelValue());

    std::vector<BlackCalibrationHelper> swaptions(priced.begin(), priced.end());
    Gsr calibrated = gsr.calibrateVolatilitiesIterative(swaptions, Fixtures::method(), Fixtures::criteria(), ctx).model();
    CHECK(calibrated.params() == References::gsrVolatilities(), "GSR's calibrated parameters are C#'s, bit for bit");
    std::vector<CalibrationHelper> helpers(capped.begin(), capped.end());
    const ICalibratedModel& model = hw;
    ICalibratedModel hw_calibrated = CalibratedModelExtensions::calibrate(model, helpers, Fixtures::method(), Fixtures::criteria(), ctx).model();
    CHECK(object_cast<CalibratedModel>(hw_calibrated).params() == References::hullWhiteCaps(), "Hull-White's calibrated parameters are C#'s, bit for bit");
}

// ---- the experimental opt-in ----
static void test_experimental()
{
#ifdef NANOQL_EXPERIMENTAL
    CallableFixedRateBond bond = Fixtures::callableBond();
    CHECK(bond && bond.typeName() == "NanoQL.Experimental.CallableFixedRateBond", "an experimental class under NANOQL_EXPERIMENTAL");

    // A user's class over nanoql's experimental interface is experimental too, a nanoql object through it.
    nqlt::Walk walk;
    const IRandomWalkDistribution& distribution = walk;
    CHECK(distribution.next(MersenneTwisterUniformRng(42)) == 0.5, "a user's class through nanoql's experimental interface");
#else
    CHECK(false, "the suite is built with NANOQL_EXPERIMENTAL");
#endif
}

int main(int argc, char** argv)
{
    struct section {
        const char* name;
        void (*run)();
    } sections[] = {
        {"kinds", test_kinds}, {"arrays", test_arrays}, {"codes", test_codes}, {"handles", test_handles}, {"generators", test_generators},
        {"overloads", test_overloads}, {"defaults", test_defaults}, {"misc", test_misc}, {"conventions", test_conventions},
        {"engines", test_engines}, {"calibrations", test_calibrations}, {"experimental", test_experimental},
    };
    std::setvbuf(stdout, nullptr, _IONBF, 0);
    int ran = 0;
    for (const section& s : sections) {
        if (argc > 1 && std::strcmp(argv[1], s.name) != 0) continue;
        int before = failures;
        try {
            s.run();
        } catch (const std::exception& e) {
            failures++;
            std::printf("FAILED %s: an exception escaped: %s\n", s.name, e.what());
        }
        // Every section ends with no live handle.
        int64_t live = liveHandles();
        CHECK(live == 0, "the section ends with no live handle");
        std::printf("%s: %s\n", s.name, failures == before ? "passed" : "FAILED");
        ran++;
    }
    std::printf("%d checks, %d failed, %d sections\n", checks, failures, ran);
    return failures == 0 && ran > 0 ? 0 : 1;
}
