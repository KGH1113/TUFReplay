#!/usr/bin/env python3
"""Check scoped S3 credentials without printing them or retaining the probe object."""
import datetime
import hashlib
import hmac
import os
import pathlib
import shlex
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid


def check(path):
    config = {}
    for line in pathlib.Path(path).read_text().splitlines():
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        name, value = line.split("=", 1)
        parts = shlex.split(value, comments=True)
        config[name.strip()] = parts[0] if parts else ""
    endpoint = urllib.parse.urlsplit(config["R2_ENDPOINT"])
    if (endpoint.scheme != "https" or endpoint.username or endpoint.password
            or not endpoint.hostname.endswith(".r2.cloudflarestorage.com")
            or endpoint.path not in ("", "/") or endpoint.query or endpoint.fragment):
        raise ValueError("invalid R2 endpoint")
    key = "connectivity-check/" + str(uuid.uuid4())
    uri = "/" + urllib.parse.quote(config["R2_BUCKET"], safe="") + "/" + key
    payload = os.urandom(32768)

    def request(method, body=b""):
        date = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
        day = date[:8]
        digest = hashlib.sha256(body).hexdigest()
        headers = {"host": endpoint.netloc, "x-amz-content-sha256": digest, "x-amz-date": date}
        signed = ";".join(sorted(headers))
        canonical = "\n".join([method, uri, "", "".join(n + ":" + headers[n] + "\n" for n in sorted(headers)), signed, digest])
        scope = day + "/auto/s3/aws4_request"
        string = "AWS4-HMAC-SHA256\n" + date + "\n" + scope + "\n" + hashlib.sha256(canonical.encode()).hexdigest()
        signing = ("AWS4" + config["R2_SECRET_ACCESS_KEY"]).encode()
        for part in (day, "auto", "s3", "aws4_request"):
            signing = hmac.new(signing, part.encode(), hashlib.sha256).digest()
        signature = hmac.new(signing, string.encode(), hashlib.sha256).hexdigest()
        headers["Authorization"] = ("AWS4-HMAC-SHA256 Credential=" + config["R2_ACCESS_KEY_ID"] + "/" + scope
                                    + ", SignedHeaders=" + signed + ", Signature=" + signature)
        req = urllib.request.Request(endpoint.scheme + "://" + endpoint.netloc + uri,
                                     data=body if method == "PUT" else None, headers=headers, method=method)
        with urllib.request.urlopen(req, timeout=30) as response:
            return response.read(65537)

    uploaded = False
    try:
        request("PUT", payload)
        uploaded = True
        received = request("GET")
        if hashlib.sha256(received).digest() != hashlib.sha256(payload).digest():
            raise ValueError("R2 probe integrity mismatch")
    finally:
        if uploaded:
            request("DELETE")
    print("R2 PUT / GET / SHA-256 / DELETE: PASS")


if __name__ == "__main__":
    try:
        check(sys.argv[1])
    except urllib.error.HTTPError as error:
        print("R2 probe failed: HTTP", error.code, file=sys.stderr)
        sys.exit(1)
    except Exception as error:
        # Avoid dumping request headers, credentials, or provider response bodies.
        print("R2 probe failed:", type(error).__name__, file=sys.stderr)
        sys.exit(1)
