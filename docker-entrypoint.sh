#!/bin/sh
set -e

case "$(uname -m)" in
  x86_64)  export CORECLR_PROFILER_PATH=/app/newrelic/linux-x64/libNewRelicProfiler.so ;;
  aarch64) export CORECLR_PROFILER_PATH=/app/newrelic/linux-arm64/libNewRelicProfiler.so ;;
  *)       echo "Unsupported architecture: $(uname -m)" >&2; exit 1 ;;
esac

exec dotnet O11yParty.dll
