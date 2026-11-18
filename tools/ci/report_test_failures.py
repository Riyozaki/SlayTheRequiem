#!/usr/bin/env python3
"""Expose bounded test diagnostics as GitHub annotations, even when raw CI-log downloads are unavailable.
Does not change test exit codes. Receives a log and optional TRX files; never includes environment/credentials.
"""
from pathlib import Path
import argparse
import xml.etree.ElementTree as ET


def annotation(kind: str, text: str) -> None:
    escaped = text[:12000].replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
    print(f"::{kind}::{escaped}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("log", type=Path)
    parser.add_argument("--trx", type=Path, nargs="*")
    args = parser.parse_args()
    reported = False
    for path in args.trx or []:
        if not path.is_file():
            continue
        root = ET.parse(path).getroot()
        for test in root.findall(".//{*}UnitTestResult"):
            if test.get("outcome") != "Failed":
                continue
            message = "\n".join(t.text or "" for t in test.findall(".//{*}ErrorInfo/*"))
            annotation("error", f"{test.get('testName')}: {message}")
            reported = True
        counters = root.find(".//{*}Counters")
        if counters is not None:
            annotation("notice", f"Core test result: {counters.attrib}")
    if not reported and args.log.is_file():
        annotation("error", "Test command failed. Log tail:\n" + args.log.read_text(encoding="utf-8", errors="replace")[-12000:])


if __name__ == "__main__":
    main()
