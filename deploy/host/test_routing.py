"""Isolated routing tests; no host services or network calls."""
import contextlib
import importlib.util
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

class RoutingTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        base = Path(self.temp.name).resolve()
        spec = importlib.util.spec_from_file_location("routing", Path(__file__).with_name("provision-routing.py"))
        self.module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.module)
        m = self.module
        (base / "available").mkdir()
        (base / "enabled").mkdir()
        m.NGINX = base / "available/routes"
        m.ENABLED = base / "enabled/routes"
        m.TUNNEL = base / "config.yml"
        self.original = "tunnel: existing\ningress:\n  - hostname: existing.example\n    service: http://127.0.0.1:8080\n  - service: http_status:404\n"
        m.TUNNEL.write_text(self.original)
        self.stack = contextlib.ExitStack()
        self.addCleanup(self.stack.close)
        self.stack.enter_context(patch.object(m.os, "geteuid", return_value=0))
        self.stack.enter_context(patch.object(sys, "argv", ["routing"]))
        self.run = self.stack.enter_context(patch.object(m, "run"))
        self.stack.enter_context(patch.object(m.subprocess, "run"))

    def test_initial_routes_and_idempotence(self):
        m = self.module
        m.main()
        first = m.TUNNEL.read_text()
        for host, port in m.HOSTS:
            self.assertEqual(first.count(host), 1)
            self.assertIn("127.0.0.1:" + str(port), m.NGINX.read_text())
        self.assertEqual(m.ENABLED.resolve(), m.NGINX)
        self.run.reset_mock()
        m.main()
        self.assertEqual(m.TUNNEL.read_text(), first)
        self.assertNotIn(("/usr/bin/systemctl", "restart", "cloudflared"),
                         [call.args for call in self.run.call_args_list])

    def test_failed_validation_restores_existing_configuration(self):
        m = self.module
        self.run.side_effect = RuntimeError("synthetic invalid config")
        with self.assertRaises(RuntimeError):
            m.main()
        self.assertEqual(m.TUNNEL.read_text(), self.original)
        self.assertFalse(m.NGINX.exists())
        self.assertFalse(m.ENABLED.is_symlink())

    def test_dangling_foreign_link_is_rejected(self):
        m = self.module
        m.ENABLED.symlink_to(m.ENABLED.parent / "missing-foreign-config")
        with self.assertRaises(SystemExit):
            m.main()
        self.assertEqual(m.TUNNEL.read_text(), self.original)
        self.run.assert_not_called()

    def test_existing_unmanaged_nginx_file_is_preserved(self):
        m = self.module
        existing = "# unrelated existing config\n"
        m.NGINX.write_text(existing)
        with self.assertRaises(SystemExit):
            m.main()
        self.assertEqual(m.NGINX.read_text(), existing)
        self.assertEqual(m.TUNNEL.read_text(), self.original)
        self.run.assert_not_called()

    def test_conflicting_hostname_is_not_overwritten(self):
        m = self.module
        existing = self.original.replace("existing.example", m.HOSTS[0][0]).replace(":8080", ":9999")
        m.TUNNEL.write_text(existing)
        with self.assertRaises(SystemExit):
            m.main()
        self.assertEqual(m.TUNNEL.read_text(), existing)
        self.run.assert_not_called()

if __name__ == "__main__":
    unittest.main()
