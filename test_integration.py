import json
import os
import subprocess
import sys
from pathlib import Path


def main():
    simulator = str(Path(sys.argv[1]).resolve())
    client = Path(__file__).resolve().parent / "Client/bin/Release/net8.0/Client.dll"
    for scenario in ("clean", "jitter", "duplicate", "missing", "corrupt", "stuck-low"):
        run = subprocess.run(
            [os.environ.get("DOTNET", "dotnet"), str(client), simulator, scenario],
            capture_output=True, text=True, timeout=20,
        )
        report = json.loads(run.stdout)
        expected_exit = 0 if scenario == "clean" else 1
        assert run.returncode == expected_exit, (scenario, run.stderr, report)
        assert report["Passed"] == (scenario == "clean"), report
        if scenario in ("missing", "corrupt"):
            assert report["MissingSequences"] == [5], report
        if scenario == "corrupt":
            assert any("CRC" in anomaly for anomaly in report["Anomalies"]), report
        if scenario in ("jitter", "duplicate", "stuck-low"):
            assert any("Timing" in anomaly for anomaly in report["Anomalies"]), report
        print(f"PASS {scenario}: accepted={len(report['Samples'])}, anomalies={len(report['Anomalies'])}")
    invalid = subprocess.run(
        [os.environ.get("DOTNET", "dotnet"), str(client), simulator, "unknown"],
        capture_output=True, text=True, timeout=20,
    )
    assert invalid.returncode == 2 and "Unknown scenario" in invalid.stderr


if __name__ == "__main__":
    main()
