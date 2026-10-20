"""Local rulebook app: serves the page and a small JSON API over Tools/Rulebook.

Run from the repository root:
    python Tools/Rulebook/app/server.py            # http://127.0.0.1:8765
    python Tools/Rulebook/app/server.py --port 8800

Security: binds to 127.0.0.1 only, rejects other Host and Origin headers (DNS rebinding and
cross-site requests), requires a per-process token on every API call, and only ever starts
gate.ps1 with arguments checked against gate.json and rulebook.json.
"""

import argparse
import json
import re
import secrets
import subprocess
import threading
from datetime import datetime, timezone
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

APP_DIR = Path(__file__).resolve().parent
BOOK_DIR = APP_DIR.parent
ROOT = BOOK_DIR.parent.parent
RULEBOOK = BOOK_DIR / "rulebook.json"
GATE = BOOK_DIR / "gate.json"
SCHEMA = BOOK_DIR / "schema" / "rulebook.schema.json"
KINDS = BOOK_DIR / "schema" / "check-kinds.json"
OUT = ROOT / "Logs" / "gate"
TOKEN = secrets.token_urlsafe(24)


def read_json(path, fallback=None):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return fallback


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def check_kinds():
    return list(read_json(KINDS, {}).get("kinds", {}).keys())


def history():
    root = OUT / "history"
    runs = []
    if root.is_dir():
        for d in sorted((d for d in root.iterdir() if d.is_dir()), reverse=True):
            rep = read_json(d / "report.json")
            if not rep:
                continue
            runs.append({"id": d.name, "result": rep.get("result"), "startedAt": rep.get("startedAt"),
                         "durationSec": rep.get("durationSec"), "selection": rep.get("selection"),
                         "stages": rep.get("stages"), "failures": len(rep.get("failures") or []),
                         "rules": [{"rule": r.get("rule"), "passed": r.get("passed")} for r in rep.get("rules") or []]})
    return runs


# Mirrors RulebookLoader.Validate in Unity, which stays the authority the gate enforces.
def validate_rulebook(book):
    errors = []
    if book.get("version") != 1:
        errors.append("version must be 1")
    kinds = set(check_kinds())
    field_names = set()
    for f in book.get("fields", []):
        if not re.fullmatch(r"[a-z][a-zA-Z0-9]*", f.get("name", "")):
            errors.append(f"custom field name '{f.get('name')}' must be camelCase letters")
        elif f["name"] in field_names:
            errors.append(f"custom field '{f['name']}' is defined twice")
        field_names.add(f.get("name"))
        if f.get("type") not in ("text", "string", "list", "bool", "enum"):
            errors.append(f"custom field '{f.get('name')}': type must be text, string, list, bool or enum")
        if f.get("type") == "enum" and not f.get("options"):
            errors.append(f"custom field '{f.get('name')}': an enum needs options")
    book_ids = set()
    for b in book.get("books", []):
        if not b.get("id"):
            errors.append("a book has no id")
        elif b["id"] in book_ids:
            errors.append(f"book id '{b['id']}' is used twice")
        book_ids.add(b.get("id"))
    rule_ids = set()
    for r in book.get("rules", []):
        name = r.get("id") or "(no id)"
        if not re.fullmatch(r"[A-Z]+-[0-9]{2,}", r.get("id", "")):
            errors.append(f"{name}: id must look like ARCH-01")
        elif r["id"] in rule_ids:
            errors.append(f"rule id '{r['id']}' is used twice")
        rule_ids.add(r.get("id"))
        if r.get("book") not in book_ids:
            errors.append(f"{name}: book '{r.get('book')}' does not exist")
        for field in ("title", "rule", "why", "fix"):
            if not str(r.get(field, "")).strip():
                errors.append(f"{name}: '{field}' is empty")
        if r.get("status") not in ("enforced", "planned"):
            errors.append(f"{name}: status must be enforced or planned")
        if r.get("severity", "blocking") not in ("blocking", "advisory"):
            errors.append(f"{name}: severity must be blocking or advisory")
        for key in (r.get("extra") or {}):
            if key not in field_names:
                errors.append(f"{name}: extra field '{key}' is not in the fields template")
        for f in book.get("fields", []):
            if f.get("required") and f.get("name") not in (r.get("extra") or {}):
                errors.append(f"{name}: required field '{f.get('name')}' is missing")
        check = r.get("check") or {}
        if check.get("kind") not in kinds:
            errors.append(f"{name}: check kind '{check.get('kind')}' is unknown")
        if "params" in check and not isinstance(check["params"], dict):
            errors.append(f"{name}: check params must be an object")
        for e in r.get("exceptions", []):
            if not all(str(e.get(k, "")).strip() for k in ("target", "reason", "until")):
                errors.append(f"{name}: an exception needs target, reason and until")
    return errors


def validate_gate(gate):
    errors = []
    ids = set()
    for s in gate.get("stages", []):
        sid = s.get("id", "")
        if not re.fullmatch(r"[a-z][a-z0-9-]*", sid):
            errors.append(f"stage id '{sid}' is not valid")
        elif sid in ids:
            errors.append(f"stage id '{sid}' is used twice")
        ids.add(sid)
        if s.get("kind") not in ("compile", "editmode", "playmode", "review"):
            errors.append(f"{sid}: kind must be compile, editmode, playmode or review")
        if s.get("kind") in ("editmode", "playmode") and not s.get("assemblies"):
            errors.append(f"{sid}: a test stage needs at least one assembly pattern")
    return errors


class Run:
    """One gate process at a time; output is kept in memory for the page to poll."""

    def __init__(self):
        self.lock = threading.Lock()
        self.process = None
        self.lines = []
        self.args = []
        self.started = None
        self.exit_code = None

    def status(self):
        with self.lock:
            return {
                "running": self.process is not None and self.exit_code is None,
                "args": self.args,
                "startedAt": self.started,
                "exitCode": self.exit_code,
                "output": self.lines[-400:],
            }

    def start(self, args):
        with self.lock:
            if self.process is not None and self.exit_code is None:
                raise RuntimeError("a gate run is already in progress")
            command = ["powershell", "-NoProfile", "-File", str(BOOK_DIR / "gate.ps1"), *args]
            self.lines = [f"> gate.ps1 {' '.join(args)}".rstrip()]
            self.args = args
            self.started = datetime.now(timezone.utc).isoformat()
            self.exit_code = None
            self.process = subprocess.Popen(
                command, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                text=True, encoding="utf-8", errors="replace",
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            )
        threading.Thread(target=self._pump, daemon=True).start()

    def _pump(self):
        for line in self.process.stdout:
            with self.lock:
                self.lines.append(line.rstrip("\n"))
        code = self.process.wait()
        with self.lock:
            self.exit_code = code

    def stop(self):
        with self.lock:
            if self.process is None or self.exit_code is not None:
                return False
            pid = self.process.pid
        # The gate starts Unity as a child; stop the whole tree.
        subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"], capture_output=True)
        return True


RUN = Run()


def gate_args(body):
    gate = read_json(GATE, {})
    stage_ids = {s["id"] for s in gate.get("stages", [])}
    rule_ids = {r["id"] for r in read_json(RULEBOOK, {}).get("rules", [])}
    args = []
    for flag, key in (("-Only", "only"), ("-Skip", "skip"), ("-With", "with")):
        values = [v for v in body.get(key, []) if v]
        bad = [v for v in values if v not in stage_ids]
        if bad:
            raise ValueError(f"unknown stage(s): {', '.join(bad)}")
        if values:
            args += [flag, ",".join(values)]
    if body.get("all"):
        args.append("-All")
    if body.get("rule"):
        if body["rule"] not in rule_ids:
            raise ValueError(f"unknown rule {body['rule']}")
        args += ["-Rule", body["rule"]]
    return args


class Handler(BaseHTTPRequestHandler):
    server_version = "Rulebook/1"

    def log_message(self, fmt, *args):
        pass

    def _allowed(self):
        port = self.server.server_address[1]
        hosts = {f"127.0.0.1:{port}", f"localhost:{port}"}
        if self.headers.get("Host") not in hosts:
            return False
        origin = self.headers.get("Origin")
        if origin and origin not in {f"http://{h}" for h in hosts}:
            return False
        return True

    def _send(self, status, body, content_type="application/json; charset=utf-8"):
        data = body if isinstance(body, bytes) else json.dumps(body, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Content-Security-Policy", "default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'")
        self.end_headers()
        self.wfile.write(data)

    def _body(self):
        length = int(self.headers.get("Content-Length") or 0)
        return json.loads(self.rfile.read(length) or b"{}")

    def _api_ok(self):
        if not self._allowed():
            self._send(HTTPStatus.FORBIDDEN, {"error": "forbidden host or origin"})
            return False
        if self.path.startswith("/api/") and self.headers.get("X-Rulebook-Token") != TOKEN:
            self._send(HTTPStatus.UNAUTHORIZED, {"error": "missing or wrong token"})
            return False
        return True

    def do_GET(self):
        if not self._api_ok():
            return
        if self.path in ("/", "/index.html"):
            html = (APP_DIR / "index.html").read_text(encoding="utf-8").replace("__TOKEN__", TOKEN)
            return self._send(HTTPStatus.OK, html.encode("utf-8"), "text/html; charset=utf-8")
        if self.path == "/api/state":
            return self._send(HTTPStatus.OK, {
                "rulebook": read_json(RULEBOOK),
                "gate": read_json(GATE),
                "catalog": read_json(OUT / "catalog.json"),
                "report": read_json(OUT / "report.json"),
                "kinds": read_json(KINDS, {}),
                "history": history(),
                "run": RUN.status(),
            })
        m = re.fullmatch(r"/api/history/(\d{8}-\d{6})", self.path)
        if m:
            rep = read_json(OUT / "history" / m.group(1) / "report.json")
            return self._send(HTTPStatus.OK if rep else HTTPStatus.NOT_FOUND, rep or {"error": "no such run"})
        if self.path == "/api/run":
            return self._send(HTTPStatus.OK, RUN.status())
        self._send(HTTPStatus.NOT_FOUND, {"error": "not found"})

    def do_PUT(self):
        if not self._api_ok():
            return
        targets = {"/api/rulebook": (RULEBOOK, validate_rulebook), "/api/gate": (GATE, validate_gate)}
        if self.path not in targets:
            return self._send(HTTPStatus.NOT_FOUND, {"error": "not found"})
        path, validate = targets[self.path]
        try:
            value = self._body()
        except ValueError:
            return self._send(HTTPStatus.BAD_REQUEST, {"errors": ["body is not valid JSON"]})
        errors = validate(value)
        if errors:
            return self._send(HTTPStatus.UNPROCESSABLE_ENTITY, {"errors": errors})
        write_json(path, value)
        self._send(HTTPStatus.OK, {"saved": path.name})

    def do_POST(self):
        if not self._api_ok():
            return
        if self.path == "/api/run":
            try:
                RUN.start(gate_args(self._body()))
            except (ValueError, RuntimeError) as e:
                return self._send(HTTPStatus.CONFLICT, {"error": str(e)})
            return self._send(HTTPStatus.ACCEPTED, RUN.status())
        if self.path == "/api/run/stop":
            return self._send(HTTPStatus.OK, {"stopped": RUN.stop()})
        self._send(HTTPStatus.NOT_FOUND, {"error": "not found"})


def main():
    parser = argparse.ArgumentParser(description="Rulebook app")
    parser.add_argument("--port", type=int, default=8765)
    port = parser.parse_args().port
    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    print(f"Rulebook app: http://127.0.0.1:{port}/  (Ctrl+C to stop)")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
