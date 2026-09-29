"""Default agent file locations, matching the client's Agent/Files/AgentPaths.cs.

Unix keeps the historical /tmp files. Windows maps /tmp to a shared C:\\tmp, so the client writes
under %TEMP%\\Navrey instead; a script using the Unix spelling there would read nothing.
"""

from __future__ import annotations

import os
import tempfile

RUNTIME_DIR = os.path.join(tempfile.gettempdir(), "Navrey") if os.name == "nt" else "/tmp"

DEFAULT_CMD = os.path.join(RUNTIME_DIR, "cuocmd")
DEFAULT_LOG = os.path.join(RUNTIME_DIR, "cuolog")
DEFAULT_STATE = os.path.join(RUNTIME_DIR, "cuostate.json")
DEFAULT_WORLD = os.path.join(RUNTIME_DIR, "cuoworld.json")
