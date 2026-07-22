#!/bin/sh
set -e

case "$(uname -m)" in
  x86_64)  export CORECLR_PROFILER_PATH=/app/newrelic/libNewRelicProfiler.so ;;
  aarch64) export CORECLR_PROFILER_PATH=/app/newrelic/linux-arm64/libNewRelicProfiler.so ;;
  *)       echo "Unsupported architecture: $(uname -m)" >&2; exit 1 ;;
esac

# The image only ships newrelic.config, not the native profiler binaries. If
# CORECLR_ENABLE_PROFILING=1 with a missing profiler .so, the CLR fails to start,
# so disable profiling automatically when the binary isn't present.
if [ ! -f "$CORECLR_PROFILER_PATH" ]; then
  echo "New Relic profiler not found at $CORECLR_PROFILER_PATH; disabling CLR profiling" >&2
  export CORECLR_ENABLE_PROFILING=0
  unset CORECLR_PROFILER_PATH
fi

exec dotnet O11yParty.dll
