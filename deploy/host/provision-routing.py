#!/usr/bin/python3
"""Apply only the two fixed TUFReplay test routes; invoked by Actions."""
import os
import fcntl
import stat
from pathlib import Path
import subprocess
import sys
import tempfile

HOSTS = (("tufreplay-dev.impl1113.dev", 4175), ("tufreplay-auto.impl1113.dev", 4176))
NGINX = Path("/etc/nginx/sites-available/tufreplay-environments")
ENABLED = Path("/etc/nginx/sites-enabled/tufreplay-environments")
TUNNEL = Path("/etc/cloudflared/config.yml")

def run(*args):
    subprocess.run(args, check=True)

def atomic(path, content, mode=0o644, owner=None):
    fd, name = tempfile.mkstemp(prefix="." + path.name + "-", dir=path.parent)
    try:
        with os.fdopen(fd, "w") as stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        os.chmod(name, mode)
        if owner is not None:
            os.chown(name, *owner)
        os.replace(name, path)
    finally:
        if os.path.exists(name):
            os.unlink(name)

def main():
    if os.geteuid() != 0 or len(sys.argv) != 1:
        raise SystemExit("This fixed routing helper requires root and accepts no arguments.")
    if any(path.is_symlink() for path in (NGINX, TUNNEL)):
        raise SystemExit("Refusing unexpected configuration symlink.")
    if (ENABLED.exists() or ENABLED.is_symlink()) and (not ENABLED.is_symlink() or ENABLED.resolve() != NGINX.resolve()):
        raise SystemExit("Existing enabled route is not the managed symlink.")
    old_tunnel = TUNNEL.read_text()
    tunnel = old_tunnel
    for hostname, _ in HOSTS:
        marker = "  - hostname: " + hostname
        expected = marker + "\n    service: http://127.0.0.1:8080"
        if marker in tunnel:
            if expected not in tunnel:
                raise SystemExit("Existing hostname has a different tunnel route: " + hostname)
        else:
            fallback = "  - service: http_status:404"
            if tunnel.count(fallback) != 1:
                raise SystemExit("Expected exactly one terminal 404 tunnel rule.")
            tunnel = tunnel.replace(fallback, expected + "\n\n" + fallback)
    config = "# Managed fixed TUFReplay test routes.\n"
    for hostname, port in HOSTS:
        config += """
server {
    listen 127.0.0.1:8080;
    server_name %s;
    location / {
        proxy_pass http://127.0.0.1:%d;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
""" % (hostname, port)
    legacy_config = config
    config = config.replace(
        "    server_name tufreplay-auto.impl1113.dev;\n",
        "    server_name tufreplay-auto.impl1113.dev;\n    client_max_body_size 128m;\n",
    )
    old_nginx = NGINX.read_text() if NGINX.exists() else None
    # Permit only the exact previous managed revision, never arbitrary edits.
    if old_nginx is not None and old_nginx not in (config, legacy_config):
        raise SystemExit("Existing Nginx file differs from the fixed managed configuration.")
    had_link = ENABLED.is_symlink()
    tunnel_stat = TUNNEL.stat()
    tunnel_mode = tunnel_stat.st_mode & 0o777
    tunnel_owner = (tunnel_stat.st_uid, tunnel_stat.st_gid)
    if old_nginx is not None:
        atomic(NGINX.with_suffix(".previous"), old_nginx, 0o600)
    atomic(TUNNEL.with_suffix(".previous"), old_tunnel, 0o600)
    try:
        atomic(NGINX, config)
        if not had_link:
            ENABLED.symlink_to(NGINX)
        atomic(TUNNEL, tunnel, tunnel_mode, tunnel_owner)
        run("/usr/sbin/nginx", "-t")
        run("/usr/local/bin/cloudflared", "tunnel", "--config", str(TUNNEL), "ingress", "validate")
        for hostname, _ in HOSTS:
            run("/usr/sbin/runuser", "-u", "kgh", "--", "/usr/local/bin/cloudflared",
                "tunnel", "route", "dns", "impl1113-home", hostname)
        run("/usr/bin/systemctl", "reload", "nginx")
        if tunnel != old_tunnel:
            run("/usr/bin/systemctl", "restart", "cloudflared")
    except Exception:
        atomic(TUNNEL, old_tunnel, tunnel_mode, tunnel_owner)
        if old_nginx is None:
            NGINX.unlink(missing_ok=True)
        else:
            atomic(NGINX, old_nginx)
        if not had_link:
            ENABLED.unlink(missing_ok=True)
        subprocess.run(("/usr/sbin/nginx", "-t"), check=False)
        subprocess.run(("/usr/bin/systemctl", "reload", "nginx"), check=False)
        if tunnel != old_tunnel:
            subprocess.run(("/usr/bin/systemctl", "restart", "cloudflared"), check=False)
        raise
    print("TUFReplay dev and auto-submission routes verified and applied.")

if __name__ == "__main__":
    if os.geteuid() != 0:
        raise SystemExit("Administrator authentication is required.")
    lock_dir = Path("/run/tuf-replay-routing")
    lock_dir.mkdir(mode=0o700, exist_ok=True)
    directory_stat = lock_dir.lstat()
    if not stat.S_ISDIR(directory_stat.st_mode) or directory_stat.st_uid != 0 or directory_stat.st_mode & 0o077:
        raise SystemExit("Routing lock directory must be root-owned and private.")
    descriptor = os.open(lock_dir / "lock", os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
    with os.fdopen(descriptor, "r+") as lock:
        lock_stat = os.fstat(lock.fileno())
        if not stat.S_ISREG(lock_stat.st_mode) or lock_stat.st_uid != 0 or lock_stat.st_nlink != 1 or lock_stat.st_mode & 0o077:
            raise SystemExit("Routing lock must be a private root-owned regular file.")
        fcntl.flock(lock, fcntl.LOCK_EX)
        main()
