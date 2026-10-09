"""Local authenticated Nivalis commands. Token is kept out of logs and arguments."""
import argparse
import json
from pathlib import Path
import urllib.parse
import urllib.request
import urllib.error
import sys

def request(game, command, args=None):
    query = urllib.parse.urlencode(args or {})
    token = (Path(game) / "BepInEx/cache/nivalismodkit-bridge.token").read_text().strip()
    req = urllib.request.Request(
        "http://127.0.0.1:5710/cmd/" + urllib.parse.quote(command, safe="") + "?" + query,
        data=b"", headers={"X-Kit-Token": token}, method="POST")
    with urllib.request.urlopen(req, timeout=8) as response:
        return json.load(response)

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("command")
    parser.add_argument("args", nargs="*")
    parser.add_argument("--game", required=True)
    parser.add_argument("--out")
    opts = parser.parse_args()
    payload = dict(arg.split("=", 1) for arg in opts.args)
    try:
        result = request(opts.game, opts.command, payload)
    except urllib.error.HTTPError as error:
        print(f"Guest HTTP {error.code}: {error.read().decode('utf-8', errors='replace')}", file=sys.stderr)
        sys.exit(1)
    text = json.dumps(result, indent=2)
    if opts.out:
        Path(opts.out).write_text(text, encoding="utf-8")
        print(f"Saved {opts.out}")
    else:
        print(text)
