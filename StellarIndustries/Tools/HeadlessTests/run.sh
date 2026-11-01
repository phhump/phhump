#!/usr/bin/env bash
# Builds the simulation core as netstandard2.1 / C# 9 (same constraints as Unity) and runs all tests.
set -euo pipefail
cd "$(dirname "$0")"
dotnet build Core/Core.csproj -nologo -v q -warnaserror
dotnet run --project Runner/HeadlessTests.csproj -nologo -v q
