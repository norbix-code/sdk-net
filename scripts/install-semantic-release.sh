#!/usr/bin/env bash
# Installs the semantic-release toolchain used by release.yml and
# release-preview.yml. Versions are pinned: an unpinned
# conventional-changelog-conventionalcommits resolved to v10, which needs
# conventional-changelog-writer@9, while @semantic-release/release-notes-generator
# 14.x still ships writer 8 — the release failed in generateNotes with
# "Missing helper". Bump these together and re-run the release preview.
set -euo pipefail

npm install -g \
  semantic-release@25.0.9 \
  @semantic-release/commit-analyzer@13.0.1 \
  @semantic-release/release-notes-generator@14.1.1 \
  @semantic-release/exec@7.1.0 \
  @semantic-release/github@12.0.9 \
  conventional-changelog-conventionalcommits@9.3.1

npx semantic-release --version
