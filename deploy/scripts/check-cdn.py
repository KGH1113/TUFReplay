#!/usr/bin/env python3
"""Verify signed edge delivery against one published replay, without logging URLs."""
import hashlib
import hmac
import json
import pathlib
import subprocess
import time
import urllib.error
import urllib.request

config = dict(line.split("=", 1) for line in pathlib.Path(
    "/home/kgh/tuf-replay-data/auto-submission/cdn.env"
).read_text().splitlines() if "=" in line)
query = "SELECT r.pid FROM run_submission_records s JOIN run_sessions r ON r.id=s.run_session_id WHERE s.state='submitted' AND s.external_pass_id IS NOT NULL AND s.manifest IS NOT NULL AND s.validation IS NOT NULL LIMIT 1;"
run = subprocess.run([
    "docker", "exec", "-i", "tufreplay-auto-postgres-production-1", "sh", "-c",
    'exec psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At',
], input=query, text=True, capture_output=True, check=True).stdout.strip()
if not run:
    raise SystemExit("No published replay available for CDN verification")

def read(url):
    request = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0 TUFReplayDeploymentCheck"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read(), response.headers

manifest = json.loads(read("https://tufreplay-auto.impl1113.dev/api/v1/replays/" + run)[0])
item = manifest["files"][0]
path = "/objects/evidence/" + run + "/" + item["sha256"]

def grant(expiry):
    signature = hmac.new(config["REPLAY_CDN_SIGNING_SECRET"].encode(),
                         (str(expiry) + "\n" + path).encode(), hashlib.sha256).hexdigest()
    return config["REPLAY_CDN_ORIGIN"] + path + "?expires=" + str(expiry) + "&signature=" + signature

url = grant(int(time.time()) + 900)
cache_statuses = []
for _ in range(3):
    data, headers = read(url)
    assert len(data) == item["bytes"] and hashlib.sha256(data).hexdigest() == item["sha256"]
    assert headers["Access-Control-Allow-Origin"] == "*"
    cache_statuses.append(headers["X-Replay-Cache"])
    print("CDN response:", headers["X-Replay-Cache"], "range:", headers.get("Content-Range", "none"))
    time.sleep(1)
assert "HIT" in cache_statuses, "Edge cache was not populated"
for invalid in [config["REPLAY_CDN_ORIGIN"] + path, grant(int(time.time()) - 1), url[:-1] + ("0" if url[-1] != "0" else "1")]:
    try:
        read(invalid)
        raise AssertionError("Invalid grant was accepted")
    except urllib.error.HTTPError as error:
        assert error.code == 403
print("CDN direct R2 download / SHA-256 / CORS / cache HIT / invalid and expired grants: PASS")
