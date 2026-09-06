import json
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET

from benchmark_report import build_report, describe, markdown


def metric(name="CPU.Build", samples=None, unit=2):
    return {"Name": name, "Samples": samples if samples is not None else [.000123456789], "Unit": unit}


class BenchmarkReportTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.next_file = 0

    def xml(self, cases, platform="EditMode"):
        root = ET.Element("test-run", result="Passed")
        ET.SubElement(root, "property", name="platform", value=platform)
        for name, groups, result in cases:
            case = ET.SubElement(root, "test-case", name=name, fullname="Fixture." + name,
                                 classname="Fixture", result=result)
            props = ET.SubElement(case, "properties")
            ET.SubElement(props, "property", name="Category", value="Performance")
            if groups is not None:
                # Structured payload's Name need not distinguish parameters.
                ET.SubElement(case, "output").text = "##performancetestresult2:" + json.dumps({
                    "Name": "Fixture.Test", "SampleGroups": groups})
        self.next_file += 1
        path = Path(self.directory.name) / f"{self.next_file}.xml"
        ET.ElementTree(root).write(path, encoding="utf-8")
        return path

    def test_precision_parameters_and_all_metrics(self):
        path = self.xml([("Test(1)", [metric(), metric("Cache.Entries", [12], 8)], "Passed"),
                         ("Test(16)", [metric()], "Passed")])
        report = build_report([path])
        self.assertEqual(len(report["overview"]), 2)
        self.assertEqual(report["issues"], [])
        bench = report["runs"][0]["benchmarks"][0]
        self.assertEqual(bench["metrics"][0]["samples"], [.000123456789])
        self.assertEqual(bench["metrics"][1]["name"], "Cache.Entries")
        self.assertIn("Test(16)", markdown(report))

    def test_units_and_nearest_rank_tails(self):
        path = self.xml([("Test", [metric(samples=[1000000], unit=0), metric("CPU.Seconds", [.002], 3)], "Passed")])
        metrics = build_report([path])["runs"][0]["benchmarks"][0]["metrics"]
        self.assertEqual([m["samples"] for m in metrics], [[1], [2]])
        stats = describe(list(range(1, 65)))
        self.assertEqual((stats["median"], stats["p95"], stats["p99"]), (32.5, 61, 64))

    def test_repeated_runs_do_not_pool_unequal_samples(self):
        first = self.xml([("Test", [metric(samples=[1] * 100)], "Passed")])
        second = self.xml([("Test", [metric(samples=[9, 11])], "Passed")])
        report = build_report([first, second])
        summary = report["overview"][0]["metrics"][0]
        self.assertEqual(summary["median_of_run_medians"], 5.5)
        self.assertEqual(summary["worst_run_p95"], 11)
        self.assertEqual(summary["samples_per_run"], [100, 2])
        self.assertEqual(report["issues"], [])

    def test_failures_missing_and_skips_are_not_zero_timings(self):
        path = self.xml([("Good", [metric()], "Passed"), ("Failed", [metric(samples=[0])], "Failed"),
                         ("Skipped", None, "Skipped"), ("Empty", [], "Passed"), ("Missing", None, "Passed")])
        report = build_report([path])
        self.assertEqual(len(report["overview"]), 1)
        self.assertEqual({i["kind"] for i in report["issues"]}, {"Failed", "Skipped", "InvalidData", "MissingData"})

    def test_missing_gpu_samples_and_cases_are_visible(self):
        first = self.xml([("Test", [metric(), metric("GPU.FinalDraw", [2])], "Passed"),
                          ("Other", [metric()], "Passed")])
        second = self.xml([("Test", [metric()], "Passed")])
        report = build_report([first, second])
        self.assertEqual({i["kind"] for i in report["issues"]}, {"MissingCase", "MissingOptionalMetric"})
        gpu = next(m for b in report["overview"] for m in b["metrics"] if m["name"] == "GPU.FinalDraw")
        self.assertEqual((gpu["runs_present"], gpu["runs_expected"], gpu["median_of_run_medians"]), (1, 2, 2))

    def test_platforms_are_separate_and_duplicate_inputs_fail(self):
        first = self.xml([("Test", [metric()], "Passed")])
        second = self.xml([("Test", [metric()], "Passed")], "PlayMode")
        report = build_report([first, second])
        self.assertEqual(len(report["overview"]), 2)
        self.assertEqual(report["issues"], [])
        with self.assertRaises(ValueError):
            build_report([first, first])

    def test_nonfinite_samples_and_empty_run_fail(self):
        path = self.xml([("Test", [metric(samples=[float("nan")])], "Passed")])
        report = build_report([path])
        self.assertEqual(len(report["overview"]), 0)
        self.assertEqual({i["kind"] for i in report["issues"]}, {"InvalidData", "MissingData"})

    def test_suite_failure_is_visible_even_with_passed_cases(self):
        path = self.xml([("Test", [metric()], "Passed")])
        tree = ET.parse(path)
        tree.getroot().set("result", "Failed")
        tree.write(path)
        self.assertIn("RunStatus", {i["kind"] for i in build_report([path])["issues"]})

    def test_boolean_is_not_a_sample(self):
        path = self.xml([("Test", [metric(samples=[True])], "Passed")])
        self.assertEqual(build_report([path])["overview"], [])

    def test_version_drift_does_not_combine_workloads(self):
        first = self.xml([("Test", [metric()], "Passed")])
        second = self.xml([("Test", [metric()], "Passed")])
        tree = ET.parse(second)
        output = tree.find(".//output")
        payload = json.loads(output.text.split(":", 1)[1])
        payload["Version"] = "2"
        output.text = "##performancetestresult2:" + json.dumps(payload)
        tree.write(second)
        report = build_report([first, second])
        self.assertEqual(report["overview"], [])
        self.assertEqual(report["issues"][0]["kind"], "VersionMismatch")

    def test_unprobed_byte_zeros_are_labeled(self):
        first = self.xml([("Old", [metric("GC.Alloc", [0], 4)], "Passed"),
                          ("Verified", [metric("GC.Alloc", [0], 4), metric("GC.Bytes.Available", [1], 8)], "Passed")])
        report = build_report([first])
        self.assertIn("GC.Alloc (unverified bytes)", markdown(report))
        self.assertTrue(report["overview"][0]["metrics"][0]["unverified_allocation_bytes"])
        self.assertFalse(report["overview"][1]["metrics"][0]["unverified_allocation_bytes"])


if __name__ == "__main__":
    unittest.main()
