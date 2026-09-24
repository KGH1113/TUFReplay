"""Noninteractive maintenance must not consume an SSH-delivered bash script."""
import pathlib
import re
import subprocess
import unittest


class DeploymentStdinTests(unittest.TestCase):
    def test_backup_and_migration_commands_keep_the_rest_of_the_script(self):
        script = pathlib.Path(__file__).with_name("deploy-home.sh").read_text()
        lines = [line for line in script.splitlines() if "compose exec -T" in line
                 and any(marker in line for marker in ("pg_dump", "TUF_REPLAY_STORAGE", "task migrate_"))]
        self.assertEqual(len(lines), 4)
        for line in lines:
            with self.subTest(command=line):
                match = re.search(r"(compose exec -T .*?</dev/null)", line)
                self.assertIsNotNone(match, "SSH script stdin must not be attached to docker exec")
                # Simulate docker exec consuming all attached stdin. Later deployment
                # commands must still reach bash when the script itself arrives via stdin.
                probe = "compose() { cat >/dev/null; }\n" + match.group(1) + '\nprintf "deployment-continued\\n"\n'
                result = subprocess.run(["bash", "-s"], input=probe, text=True, capture_output=True, check=True)
                self.assertEqual(result.stdout, "deployment-continued\n")


if __name__ == "__main__":
    unittest.main()
