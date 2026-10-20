"""Agent judge: runs the agent.review rules of the rulebook through headless Claude Code.

    python Tools/Rulebook/review.py                 # every enforced agent rule
    python Tools/Rulebook/review.py --rules REV-01  # one or more rule ids, comma separated

Each rule gets a subject (files, a diff, the rulebook, linked tests or acceptance pictures), its
own prompt, and a fixed output schema. The agent may only read: Read, Grep and Glob are allowed,
every other tool is denied. Results are written to Logs/gate/rules/<id>.json in the same shape as
the Unity rule results, plus a "judge" block with model, cost, turns and the agent's summary.
"""

import argparse
import concurrent.futures
import fnmatch
import json
import re
import subprocess
import sys
import time
from pathlib import Path

BOOK_DIR = Path(__file__).resolve().parent
ROOT = BOOK_DIR.parent.parent
OUT = ROOT / "Logs" / "gate"

VERDICT_SCHEMA = {
    "type": "object",
    "required": ["verdict", "summary", "findings"],
    "properties": {
        "verdict": {"enum": ["pass", "fail"]},
        "summary": {"type": "string", "description": "Two sentences at most: what was reviewed and the result."},
        "findings": {
            "type": "array",
            "items": {
                "type": "object",
                "required": ["target", "detail"],
                "properties": {
                    "target": {"type": "string", "description": "What is wrong: a type::member, a file, a rule id or a test name."},
                    "detail": {"type": "string", "description": "One sentence: what is wrong and why it breaks the rule."},
                    "location": {"type": "string", "description": "path:line relative to the repository root, if known."},
                },
            },
        },
    },
}

FRAME = """You are a code reviewer acting as a judge for one rule of a project rulebook.
Judge only this rule. Do not report anything else. Do not suggest unrelated improvements.
You may read files with Read, Grep and Glob. You may not change anything.
Return verdict "fail" only when you found at least one real violation, and list each one as a finding.
Use paths relative to the repository root.

Rule {id}: {title}
Statement: {rule}
Why: {why}

Task:
{prompt}

Subject:
{subject}
"""


def read_json(path, fallback=None):
    try:
        return json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return fallback


def glob_files(patterns, limit):
    found = []
    for pattern in patterns:
        found += [p for p in ROOT.glob(pattern) if p.is_file()]
    unique = sorted({p.relative_to(ROOT).as_posix() for p in found})
    return unique[:limit], len(unique)


def subject_for(rule, book, catalog):
    p = rule["check"].get("params", {})
    kind = p.get("subject")
    limit = int(p.get("maxFiles", 40))

    if kind == "files":
        files, total = glob_files(p.get("files", []), limit)
        note = f"\n(showing {len(files)} of {total} files)" if total > len(files) else ""
        return "Review these files:\n" + "\n".join(f"- {f}" for f in files) + note

    if kind == "diff":
        base = p.get("base", "origin/main")
        names = subprocess.run(["git", "diff", "--name-only", f"{base}...HEAD"], cwd=ROOT,
                               capture_output=True, text=True).stdout.split()
        names = [n for n in names if (ROOT / n).is_file()][:limit]
        if not names:
            return None
        return f"Files changed since {base}:\n" + "\n".join(f"- {n}" for n in names)

    if kind == "rules":
        rules = [{k: r.get(k) for k in ("id", "title", "rule", "why", "fix", "status", "severity", "check")}
                 for r in book["rules"]]
        return "The rulebook rules (JSON):\n" + json.dumps(rules, indent=2)

    if kind == "linkedTests":
        tests = (catalog or {}).get("tests", [])
        by_rule = {}
        for t in tests:
            if t.get("assembly") == "Tools.Rulebook.Tests":
                continue
            for rid in t.get("rules") or []:
                by_rule.setdefault(rid, []).append(t["fullname"])
        if not by_rule:
            return None
        lines = []
        for r in book["rules"]:
            if r["id"] in by_rule:
                lines.append(f"Rule {r['id']}: {r['rule']}")
                lines += [f"  - {name}" for name in sorted(by_rule[r["id"]])]
        return "Rules and the tests linked to them:\n" + "\n".join(lines)

    if kind == "pictures":
        pics = sorted(f.name for f in (ROOT / p["pictures"]).glob("*.md") if not f.name.startswith("_"))
        binds = sorted(f.name for f in (ROOT / p["bindings"]).glob("*.cs"))
        return (f"Pictures in {p['pictures']}:\n" + "\n".join(f"- {n}" for n in pics) +
                f"\n\nBindings in {p['bindings']} (each carries [Picture(\"<file>\")]):\n" + "\n".join(f"- {n}" for n in binds))

    raise ValueError(f"unknown subject '{kind}'")


def glob_match(pattern, value):
    return re.fullmatch(re.escape(pattern).replace(r"\*", ".*"), value or "") is not None


def judge(rule, book, catalog):
    p = rule["check"].get("params", {})
    result = {"rule": rule["id"], "title": rule["title"], "passed": False, "error": None, "violations": [],
              "exempted": [], "staleExceptions": [], "fix": rule["fix"],
              "judge": {"kind": "agent", "model": p.get("model", "sonnet")}}
    started = time.time()
    try:
        subject = subject_for(rule, book, catalog)
        if subject is None:
            result["passed"] = True
            result["judge"]["summary"] = "Nothing to review: the subject is empty."
            return result
        prompt = FRAME.format(subject=subject, prompt=p.get("prompt", ""), **rule)
        command = [
            "claude", "-p",
            "--output-format", "json",
            "--json-schema", json.dumps(VERDICT_SCHEMA),
            "--model", p.get("model", "sonnet"),
            "--max-turns", str(p.get("maxTurns", 30)),
            "--allowedTools", "Read", "Grep", "Glob",
            "--disallowedTools", "Bash", "Edit", "Write", "NotebookEdit", "WebFetch", "WebSearch",
            "--no-session-persistence",
        ]
        run = subprocess.run(command, input=prompt, cwd=ROOT, capture_output=True, text=True,
                             encoding="utf-8", errors="replace", timeout=900)
        envelope = json.loads(run.stdout) if run.stdout.strip().startswith("{") else {}
        if run.returncode != 0 or envelope.get("is_error"):
            raise RuntimeError((envelope.get("result") or run.stderr or run.stdout or "claude failed").strip()[:500])
        verdict = envelope.get("structured_output")
        if verdict is None:
            text = envelope.get("result", "")
            match = re.search(r"\{.*\}", text, re.S)
            verdict = json.loads(match.group(0)) if match else None
        if not isinstance(verdict, dict):
            raise RuntimeError("the agent returned no verdict")

        result["judge"].update(summary=verdict.get("summary", ""), costUsd=envelope.get("total_cost_usd"),
                               turns=envelope.get("num_turns"))
        used = set()
        for f in verdict.get("findings", []):
            v = {"target": f.get("target", "?"), "detail": f.get("detail", ""), "location": f.get("location")}
            ex = next((i for i, e in enumerate(rule.get("exceptions", [])) if glob_match(e["target"], v["target"])), None)
            if ex is None:
                result["violations"].append(v)
            else:
                used.add(ex)
                result["exempted"].append(v)
        result["staleExceptions"] = [e for i, e in enumerate(rule.get("exceptions", [])) if i not in used]
        result["passed"] = not result["violations"]
    except Exception as e:  # noqa: BLE001 - every failure becomes a rule error in the report
        result["error"] = f"{type(e).__name__}: {e}"
    finally:
        result["judge"]["durationSec"] = round(time.time() - started, 1)
    return result


def main():
    parser = argparse.ArgumentParser(description="Run agent.review rules")
    parser.add_argument("--rules", default="", help="Comma-separated rule ids. Default: every enforced agent rule.")
    parser.add_argument("--parallel", type=int, default=3)
    args = parser.parse_args()

    book = read_json(BOOK_DIR / "rulebook.json")
    catalog = read_json(OUT / "catalog.json")
    wanted = {r for r in args.rules.split(",") if r}
    rules = [r for r in book["rules"] if r["check"]["kind"] == "agent.review" and r["status"] == "enforced"
             and (not wanted or r["id"] in wanted)]

    (OUT / "rules").mkdir(parents=True, exist_ok=True)
    summary = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, args.parallel)) as pool:
        for result in pool.map(lambda r: judge(r, book, catalog), rules):
            (OUT / "rules" / f"{result['rule']}.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
            summary.append({"rule": result["rule"], "passed": result["passed"], "error": result["error"],
                            "violations": len(result["violations"])})
            print(f"{result['rule']}: {'PASS' if result['passed'] else 'FAIL'}"
                  f"{' (' + result['error'] + ')' if result['error'] else ''}", file=sys.stderr)
    print(json.dumps(summary))


if __name__ == "__main__":
    main()
