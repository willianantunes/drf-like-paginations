#!/usr/bin/env bash

# https://www.willianantunes.com/blog/2021/05/production-ready-shell-startup-scripts-the-set-builtin/
set -eu -o pipefail

if [ -z "$1" ]; then
  echo "Please provide the NuGet API key 👀"
  exit 0
fi

echo "### Reading variables..."
NUGET_API_KEY=$1

# In order to build a NuGet package (a .nupkg file) from the project
dotnet pack
# Now we can publish it 🤩
dotnet nuget push "**/*/DrfLikePaginations.*.nupkg" \
    --api-key $NUGET_API_KEY \
    --source https://api.nuget.org/v3/index.json \
    --skip-duplicate
