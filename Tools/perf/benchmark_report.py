"""Report every structured Unity performance metric, without rounding input samples.

Each XML is one run of its platform. Repeat the same filter when supplying multiple
runs; missing cases/metrics are reported, never filled with zeros. No dependencies.
"""

import argparse
from collections import Counter, defaultdict
import json
import math
from pathlib import Path
import statistics
import sys
import xml.etree.ElementTree as ET


MARKER = "##performancetestresult2:"
RUN_MARKER = "##performancetestruninfo2:"
OPTIONAL_METRICS = {"GPU.FinalDraw"}
UNITS = {
    0: ("ms", 1e-6), 1: ("ms", 1e-3), 2: ("ms", 1), 3: ("ms", 1000),
    4: ("bytes", 1), 5: ("KiB", 1), 6: ("MiB", 1), 7: ("GiB", 1),
    8: ("value", 1),
}


def describe(samples):
    ordered = sorted(samples)
    return {
        "count": len(samples), "min": ordered[0], "max": ordered[-1],
        "median": statistics.median(samples), "mean": statistics.mean(samples),
        "p95": ordered[math.ceil(len(samples) * .95) - 1],
        "p99": ordered[math.ceil(len(samples) * .99) - 1],
    }


def read_run(path):
    root = ET.parse(path).getroot()
    platform = root.find(".//property[@name='platform']")
    platform = platform.get("value") if platform is not None else "Unknown"
    run = {
        "source": str(Path(path).resolve()), "platform": platform,
        "test_run": dict(root.attrib), "benchmarks": [], "issues": [],
        "unity_run_info": [],
    }
    for output in root.iter("output"):
        for line in (output.text or "").splitlines():
            if line.strip().startswith(RUN_MARKER):
                try:
                    info = json.loads(line.strip()[len(RUN_MARKER):])
                    if info not in run["unity_run_info"]:
                        run["unity_run_info"].append(info)
                except ValueError as error:
                    run["issues"].append({"case": "*", "kind": "InvalidMetadata", "detail": str(error)})
    # NUnit can label a mixed passed/skipped run Skipped:Ignored.
    if root.get("result", "").split(":", 1)[0] not in ("Passed", "Skipped"):
        run["issues"].append({"case": "*", "kind": "RunStatus",
                              "detail": f"Test run status is {root.get('result', 'missing')}."})
    for case in root.iter("test-case"):
        output = case.findtext("output", "")
        categories = [p.get("value") for p in case.findall("./properties/property[@name='Category']")]
        lines = [line.strip()[len(MARKER):] for line in output.splitlines()
                 if line.strip().startswith(MARKER)]
        name = case.get("fullname", case.get("name", "unknown"))
        # Failures in mixed correctness/performance XML are visible as well.
        if case.get("result") != "Passed":
            if lines or "Performance" in categories or case.get("result") == "Failed":
                run["issues"].append({"case": name, "kind": case.get("result", "Unknown"),
                                      "detail": case.findtext("failure/message") or
                                      case.findtext("reason/message", "")})
            continue
        if not lines and "Performance" not in categories:
            continue
        if len(lines) != 1:
            run["issues"].append({"case": name, "kind": "MissingData" if not lines else "DuplicateData",
                                  "detail": f"Expected one structured result, found {len(lines)}."})
            continue
        try:
            result = json.loads(lines[0])
            metrics = []
            names = set()
            for group in result.get("SampleGroups", []):
                unit = group.get("Unit", 8)
                label, factor = UNITS[unit]
                metric_name = group["Name"]
                if metric_name in names:
                    raise ValueError(f"Duplicate metric {metric_name}")
                names.add(metric_name)
                raw = group.get("Samples", [])
                if not raw or any(type(s) not in (int, float) or not math.isfinite(s) for s in raw):
                    raise ValueError(f"No finite samples for {metric_name}")
                samples = [s * factor for s in raw]
                metrics.append({"name": metric_name, "unit": label,
                                "source_unit": unit, "raw_samples": raw,
                                "samples": samples, "statistics": describe(samples),
                                "increase_is_better": group.get("IncreaseIsBetter", False)})
            if not metrics:
                raise ValueError("Structured result contains no sample groups.")
            verified_bytes = any(m["name"] == "GC.Bytes.Available" and min(m["samples"]) == 1 for m in metrics)
            for metric in metrics:
                metric["unverified_allocation_bytes"] = (metric["name"].startswith("GC.Alloc") and
                                                         metric["unit"] == "bytes" and not verified_bytes)
        except (ValueError, TypeError, KeyError) as error:
            run["issues"].append({"case": name, "kind": "InvalidData", "detail": str(error)})
            continue
        run["benchmarks"].append({"name": name, "case": case.get("name", name),
                                  "fixture": case.get("classname") or result.get("ClassName", "Unknown"),
                                  "version": result.get("Version"), "metrics": metrics})
    if not run["benchmarks"]:
        run["issues"].append({"kind": "MissingData", "case": "*", "detail": "No usable performance results."})
    return run


def build_report(paths, metadata=None):
    resolved = [str(Path(path).resolve()) for path in paths]
    if len(resolved) != len(set(resolved)):
        raise ValueError("Each XML may be supplied only once; duplicate inputs would inflate coverage.")
    runs = [read_run(path) for path in paths]
    platforms = Counter(run["platform"] for run in runs)
    records = defaultdict(list)
    issues = []
    for index, run in enumerate(runs):
        issues.extend({"run": index + 1, **issue} for issue in run["issues"])
        seen = set()
        for bench in run["benchmarks"]:
            key = (run["platform"], bench["name"])
            if key in seen:
                issues.append({"run": index + 1, "case": bench["name"], "kind": "DuplicateCase",
                               "detail": "Case repeated within one XML; no aggregate is safe."})
                continue
            seen.add(key)
            records[key].append((index + 1, bench))
    overview = []
    for (platform, name), occurrences in sorted(records.items()):
        versions = {bench["version"] for _, bench in occurrences}
        if len(versions) != 1:
            issues.append({"kind": "VersionMismatch", "case": name,
                           "detail": "Different workload versions; aggregate omitted."})
            continue
        metrics = defaultdict(list)
        for run_index, bench in occurrences:
            for metric in bench["metrics"]:
                metrics[(metric["name"], metric["unit"])].append((run_index, metric))
        expected = platforms[platform]
        entry = {"platform": platform, "name": name, "case": occurrences[0][1]["case"],
                 "fixture": occurrences[0][1]["fixture"], "runs_present": len(occurrences),
                 "version": occurrences[0][1]["version"],
                 "runs_expected": expected, "metrics": []}
        if len(occurrences) != expected:
            issues.append({"kind": "MissingCase", "case": name,
                           "detail": f"{platform}: present in {len(occurrences)}/{expected} runs."})
        for (metric_name, unit), measurements in sorted(metrics.items()):
            summaries = [metric["statistics"] for _, metric in measurements]
            aggregate = {
                "name": metric_name, "unit": unit,
                "runs_present": len(measurements), "runs_expected": expected,
                "run_indices": [index for index, _ in measurements],
                "samples_per_run": [s["count"] for s in summaries],
                "median_of_run_medians": statistics.median(s["median"] for s in summaries),
                "min_run_median": min(s["median"] for s in summaries),
                "max_run_median": max(s["median"] for s in summaries),
                "worst_run_p95": max(s["p95"] for s in summaries),
                "worst_run_p99": max(s["p99"] for s in summaries),
                "max": max(s["max"] for s in summaries),
                "unverified_allocation_bytes": any(m["unverified_allocation_bytes"] for _, m in measurements),
            }
            entry["metrics"].append(aggregate)
            if len(measurements) != len(occurrences):
                issues.append({"kind": "MissingOptionalMetric" if metric_name in OPTIONAL_METRICS else "MissingMetric", "case": name,
                               "detail": f"{metric_name}: present in {len(measurements)}/{len(occurrences)} available case runs."})
        overview.append(entry)
    return {"schema_version": 1, "metadata": metadata or {}, "runs": runs,
            "overview": overview, "issues": issues}


def cell(value):
    return str(value).replace("|", "\\|").replace("\r", " ").replace("\n", " ")


def number(value):
    return f"{value:.6g}"


def markdown(report):
    lines = ["# NowUI benchmark overview", "",
             f"{len(report['overview'])} benchmark cases from {len(report['runs'])} XML runs. "
             f"{len(report['issues'])} data/test issues.", "",
             "Times are milliseconds. Medians are the median of each run's median; tail columns are "
             "the worst per-run nearest-rank percentile, not percentiles of pooled runs. "
             "Run-median ranges expose repeatability. With fewer than 100 samples, p99 is coarse "
             "(the maximum at 64 samples). Single-sample counters have no meaningful tail distribution.", "",
             "Workload sizes and operation units differ across cases; do not rank unrelated rows or sum them. "
             "CPU build/recording, GPU.FinalDraw, and synchronous Frame.Completion have different boundaries. "
             "GPU.FinalDraw excludes prepasses submitted during build (including masks). Missing GPU metrics "
             "are unavailable, never zero. See Docs/Benchmarks.md for methods and remaining coverage gaps.", ""]
    lines += ["Allocation byte groups without a successful known-allocation probe are labeled **unverified bytes**. "
              "Their zeros do not establish allocation-free execution. GC.Alloc.Calls counts allocation events, "
              "not bytes or collections. GC.Bytes.Available and GC.Calls.Available expose instrumentation support.", ""]
    if report["metadata"]:
        lines += ["## Run context", "", "```json", json.dumps(report["metadata"], indent=2), "```", ""]
    lines += ["## Sources", ""]
    for i, run in enumerate(report["runs"], 1):
        lines.append(f"- {i}: {cell(run['platform'])}, {cell(run['test_run'].get('start-time', 'unknown time'))}: `{cell(run['source'])}`")
        for info in run["unity_run_info"]:
            player, hardware, editor = info.get("Player", {}), info.get("Hardware", {}), info.get("Editor", {})
            context = [editor.get("Version"), hardware.get("ProcessorType"), hardware.get("GraphicsDeviceName"),
                       player.get("GraphicsApi"), player.get("ScriptingBackend"), player.get("RenderThreadingMode")]
            lines.append("  Context: " + "; ".join(cell(v).strip() for v in context if v))
    if report["issues"]:
        lines += ["", "## Data and test issues", ""]
        for issue in report["issues"]:
            lines.append(f"- {cell(issue['kind'])}: {cell(issue['case'])}: {cell(issue['detail'])}")
    fixtures = defaultdict(list)
    for entry in report["overview"]:
        fixtures[(entry["platform"], entry["fixture"])].append(entry)
    for (platform, fixture), entries in sorted(fixtures.items()):
        lines += ["", f"## {platform}: {fixture}", "",
                  "| Case | Metric | Unit | Median | Run medians (min–max) | Worst p95 | Worst p99 | Max | Samples/run | Runs |",
                  "|---|---|---|---:|---:|---:|---:|---:|---|---|"]
        counters = []
        for entry in entries:
            for metric in entry["metrics"]:
                if (metric["unit"] != "ms" or metric["name"].startswith(("Workload.", "Soak."))) and not metric["name"].startswith("GC.Alloc"):
                    counters.append((entry, metric))
                    continue
                label = metric["name"] + (" (unverified bytes)" if metric["unverified_allocation_bytes"] else "")
                values = [entry["case"], label, metric["unit"],
                          number(metric["median_of_run_medians"]),
                          f"{number(metric['min_run_median'])}–{number(metric['max_run_median'])}",
                          number(metric["worst_run_p95"]), number(metric["worst_run_p99"]), number(metric["max"]),
                          "/".join(map(str, metric["samples_per_run"])),
                          f"{metric['runs_present']}/{metric['runs_expected']}"]
                lines.append("| " + " | ".join(map(cell, values)) + " |")
        if counters:
            lines += ["", "### Workload and cache counters", "",
                      "| Case | Metric | Unit | Median | Max | Samples/run | Runs |", "|---|---|---|---:|---:|---|---|"]
            for entry, metric in counters:
                values = [entry["case"], metric["name"], metric["unit"], number(metric["median_of_run_medians"]),
                          number(metric["max"]), "/".join(map(str, metric["samples_per_run"])),
                          f"{metric['runs_present']}/{metric['runs_expected']}"]
                lines.append("| " + " | ".join(map(cell, values)) + " |")
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("xml", nargs="+", type=Path)
    parser.add_argument("--output", required=True, type=Path, help="Output prefix (.json and .md are appended).")
    parser.add_argument("--metadata", type=Path, help="Optional JSON run context recorded by the harness.")
    args = parser.parse_args()
    metadata = json.loads(args.metadata.read_text(encoding="utf-8-sig")) if args.metadata else None
    report = build_report(args.xml, metadata)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    Path(str(args.output) + ".json").write_text(json.dumps(report, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    Path(str(args.output) + ".md").write_text(markdown(report), encoding="utf-8")
    print(f"Wrote {args.output}.md and .json: {len(report['overview'])} cases, {len(report['issues'])} issues.")
    return 1 if any(issue["kind"] not in ("Skipped", "Inconclusive", "MissingOptionalMetric") for issue in report["issues"]) else 0


if __name__ == "__main__":
    sys.exit(main())
