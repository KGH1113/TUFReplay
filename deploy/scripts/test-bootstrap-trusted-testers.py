import importlib.util
import pathlib
import unittest

spec = importlib.util.spec_from_file_location(
    "bootstrap", pathlib.Path(__file__).with_name("bootstrap-trusted-testers.py")
)
bootstrap = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bootstrap)


class BootstrapTests(unittest.TestCase):
    def test_invalid_and_empty_rosters_are_rejected(self):
        for value in ["", " \n", "invalid", "'); DROP TABLE trusted_testers; --"]:
            with self.assertRaises(ValueError):
                bootstrap.render(value)

    def test_roster_is_deduplicated_atomic_and_never_reactivates(self):
        user = "00000000-0000-4000-8000-000000000001"
        sql = bootstrap.render(f"{user}\n{user}\n")
        self.assertEqual(sql.count(user), 1)
        self.assertTrue(sql.startswith("BEGIN;"))
        self.assertTrue(sql.endswith("COMMIT;\n"))
        self.assertIn("ON CONFLICT (user_id) DO NOTHING", sql)
        self.assertIn("FROM inserted", sql)


if __name__ == "__main__":
    unittest.main()
