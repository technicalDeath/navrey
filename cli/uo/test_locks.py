"""Cross-process lock behavior; no client, accounts or game files are used."""
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from uo import AlreadyRunning, Client, holder, is_running, single_instance


class LockTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="navrey-lock-test-")
        self.addCleanup(self.temp.cleanup)
        self.cmd = str(Path(self.temp.name) / "commands")

    def child(self, body):
        program = (
            "import sys, os; sys.path.insert(0, sys.argv[1]); "
            "from uo import AlreadyRunning, holder, is_running, single_instance; "
            "cmd=sys.argv[2]\n" + body
        )
        return subprocess.run(
            [sys.executable, "-c", program, str(Path(__file__).resolve().parents[1]), self.cmd],
            capture_output=True, text=True, timeout=10, check=True,
        ).stdout.strip()

    def test_client_import_and_explicit_paths(self):
        client = Client(state_file=self.cmd + ".state", world_file=self.cmd + ".world",
                        cmd_file=self.cmd, log_file=self.cmd + ".log")
        self.assertFalse(client.is_live())
        self.assertEqual(client.commands.cmd_file, self.cmd)

    def test_duplicate_and_release(self):
        lock = single_instance("trial", self.cmd)
        self.addCleanup(lock.release)
        with self.assertRaises(AlreadyRunning):
            single_instance("trial", self.cmd)
        lock.release()
        self.assertFalse(is_running("trial", self.cmd))
        with single_instance("trial", self.cmd):
            self.assertEqual(holder("trial", self.cmd), os.getpid())

    def test_other_process_sees_pid_and_cannot_acquire(self):
        with single_instance("trial", self.cmd):
            self.assertEqual(self.child("print(holder('trial', cmd))"), str(os.getpid()))
            result = self.child(
                "try:\n single_instance('trial', cmd)\n"
                "except AlreadyRunning:\n print('blocked')\n"
                "else:\n raise AssertionError('duplicate acquired')"
            )
            self.assertEqual(result, "blocked")

    def test_separate_clients_are_independent(self):
        with single_instance("trial", self.cmd), single_instance("trial", self.cmd + "-other"):
            self.assertTrue(is_running("trial", self.cmd))
            self.assertTrue(is_running("trial", self.cmd + "-other"))

    def test_process_exit_releases_lock_without_cleanup(self):
        self.child("single_instance('trial', cmd); os._exit(0)")
        self.assertFalse(is_running("trial", self.cmd))
        self.assertIsNone(holder("trial", self.cmd))
        with single_instance("trial", self.cmd):
            self.assertTrue(is_running("trial", self.cmd))

    @unittest.skipUnless(os.name == "nt", "Windows has no graceful SIGTERM takeover")
    def test_windows_takeover_does_not_terminate_holder(self):
        with single_instance("trial", self.cmd):
            result = self.child(
                "try:\n single_instance('trial', cmd, takeover=True)\n"
                "except AlreadyRunning:\n print('refused')\n"
                "else:\n raise AssertionError('unsafe takeover')"
            )
            self.assertEqual(result, "refused")
            self.assertTrue(is_running("trial", self.cmd))


if __name__ == "__main__":
    unittest.main()
