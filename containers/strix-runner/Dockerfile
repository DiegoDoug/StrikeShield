# Runs the Strix CLI itself (docs/ARCHITECTURE.md §3) — NOT Strix's own
# containers/Dockerfile, which is the Kali-based sandbox Strix spawns as a
# *nested* container via the Python docker SDK (docker>=7.1.0, confirmed
# in strix-agent's own pyproject.toml) once it's running. That's why the
# strix step (and only the strix step — see PlaybookExecutor) gets
# /var/run/docker.sock mounted: Strix needs to talk to the Docker daemon
# itself, not shell out to a docker CLI binary, so nothing beyond Python
# and the package is required here.
FROM python:3.12-slim

RUN pip install --no-cache-dir strix-agent

ENTRYPOINT ["strix"]
