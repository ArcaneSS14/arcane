# SPDX-FileCopyrightText: 2023 SlamBamActionman <83650252+SlamBamActionman@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
# SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
#
# SPDX-License-Identifier: MIT

#!/usr/bin/env python3

import argparse
import datetime
import json
import os
import re
import sys
import urllib.error
import urllib.request
from typing import Any

import yaml

MAX_ENTRIES = 500
GITHUB_API_URL = os.environ.get("GITHUB_API_URL", "https://api.github.com")
GITHUB_SERVER_URL = os.environ.get("GITHUB_SERVER_URL", "https://github.com")
GITHUB_API_VERSION = "2022-11-28"
GITHUB_API_TIMEOUT = 10

HEADER_RE = r"(?::cl:|🆑) *\r?\n(.+)$"
ENTRY_RE = r"^ *[*-]? *(\S[^\n\r]+)\r?$"
PR_NUMBER_RE = re.compile(r"\(#(?P<number>\d+)\)")

CATEGORY_MAIN = "Main"

# From https://stackoverflow.com/a/37958106/4678631
class NoDatesSafeLoader(yaml.SafeLoader):
    @classmethod
    def remove_implicit_resolver(cls, tag_to_remove):
        if "yaml_implicit_resolvers" not in cls.__dict__:
            cls.yaml_implicit_resolvers = cls.yaml_implicit_resolvers.copy()

        for first_letter, mappings in cls.yaml_implicit_resolvers.items():
            cls.yaml_implicit_resolvers[first_letter] = [
                (tag, regexp) for tag, regexp in mappings if tag != tag_to_remove
            ]


# Hrm yes let's make the fucking default of our serialization library to PARSE ISO-8601
# but then output garbage when re-serializing.
NoDatesSafeLoader.remove_implicit_resolver("tag:yaml.org,2002:timestamp")


def load_github_event() -> dict[str, Any] | None:
    """Load the current GitHub Actions event payload, if available."""
    event_path = os.environ.get("GITHUB_EVENT_PATH")
    if not event_path:
        return None

    try:
        with open(event_path, "r", encoding="utf-8") as f:
            event = json.load(f)
    except (OSError, json.JSONDecodeError) as exc:
        print(f"Warning: failed to read GITHUB_EVENT_PATH: {exc}", file=sys.stderr)
        return None

    return event if isinstance(event, dict) else None


def make_pull_url(repository: str, pr_number: int | str) -> str:
    return f"{GITHUB_SERVER_URL.rstrip('/')}/{repository}/pull/{pr_number}"


def get_pull_url_from_event(event: dict[str, Any] | None) -> str | None:
    """Get the PR URL directly from pull_request / pull_request_target events."""
    if not event:
        return None

    pull_request = event.get("pull_request")
    if not isinstance(pull_request, dict):
        return None

    html_url = pull_request.get("html_url")
    if isinstance(html_url, str) and html_url.strip():
        return html_url.strip()

    number = pull_request.get("number") or event.get("number")
    repository = os.environ.get("GITHUB_REPOSITORY")
    if repository and isinstance(number, int):
        return make_pull_url(repository, number)

    return None


def get_pull_url_from_commit_message(event: dict[str, Any] | None) -> str | None:
    """Infer PR URL from GitHub's usual squash commit title: `Title (#123)`."""
    repository = os.environ.get("GITHUB_REPOSITORY")
    if not repository or not event:
        return None

    messages: list[str] = []

    head_commit = event.get("head_commit")
    if isinstance(head_commit, dict):
        message = head_commit.get("message")
        if isinstance(message, str):
            messages.append(message)

    commits = event.get("commits")
    if isinstance(commits, list):
        for commit in reversed(commits):
            if not isinstance(commit, dict):
                continue
            message = commit.get("message")
            if isinstance(message, str):
                messages.append(message)

    for message in messages:
        matches = list(PR_NUMBER_RE.finditer(message))
        if matches:
            return make_pull_url(repository, matches[-1].group("number"))

    return None


def get_pull_url_from_github_api() -> str | None:
    """Ask GitHub which pull request is associated with GITHUB_SHA.

    This is the most useful fallback after a squash merge: the changelog part may not
    contain an URL, but GitHub can still associate the resulting commit with its PR.
    """
    repository = os.environ.get("GITHUB_REPOSITORY")
    sha = os.environ.get("GITHUB_SHA")
    if not repository or not sha:
        return None

    url = f"{GITHUB_API_URL.rstrip('/')}/repos/{repository}/commits/{sha}/pulls"
    headers = {
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": GITHUB_API_VERSION,
        "User-Agent": "ss14-update-changelog",
    }

    token = os.environ.get("GITHUB_TOKEN")
    if token:
        headers["Authorization"] = f"Bearer {token}"

    request = urllib.request.Request(url, headers=headers)

    try:
        with urllib.request.urlopen(request, timeout=GITHUB_API_TIMEOUT) as response:
            pulls = json.load(response)
    except (urllib.error.URLError, urllib.error.HTTPError, TimeoutError, json.JSONDecodeError) as exc:
        print(f"Warning: failed to resolve PR through GitHub API: {exc}", file=sys.stderr)
        return None

    if not isinstance(pulls, list) or not pulls:
        return None

    valid_pulls = [pull for pull in pulls if isinstance(pull, dict)]
    if not valid_pulls:
        return None

    for pull in valid_pulls:
        if pull.get("merge_commit_sha") == sha:
            html_url = pull.get("html_url")
            if isinstance(html_url, str) and html_url.strip():
                return html_url.strip()

    for pull in valid_pulls:
        head = pull.get("head")
        if isinstance(head, dict) and head.get("sha") == sha:
            html_url = pull.get("html_url")
            if isinstance(html_url, str) and html_url.strip():
                return html_url.strip()

    # If GitHub returned multiple associated PRs, prefer the most recently merged one.
    merged_pulls = [pull for pull in valid_pulls if pull.get("merged_at")]
    candidates = merged_pulls or valid_pulls
    candidates.sort(key=lambda pull: str(pull.get("merged_at") or pull.get("updated_at") or ""), reverse=True)

    html_url = candidates[0].get("html_url")
    if isinstance(html_url, str) and html_url.strip():
        return html_url.strip()

    return None


def resolve_automatic_pull_url(event: dict[str, Any] | None) -> str | None:
    """Resolve the PR URL without requiring any new workflow environment variables."""
    resolvers = (
        ("GitHub event", lambda: get_pull_url_from_event(event)),
        ("squash commit message", lambda: get_pull_url_from_commit_message(event)),
        ("GitHub API", get_pull_url_from_github_api),
    )

    for source, resolver in resolvers:
        url = resolver()
        if url:
            print(f"Resolved changelog PR URL from {source}: {url}")
            return url

    print("Warning: could not automatically determine a PR URL for changelog entries", file=sys.stderr)
    return None


def normalize_optional_url(value: Any) -> str | None:
    if not isinstance(value, str):
        return None

    value = value.strip()
    return value or None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("changelog_file")
    parser.add_argument("parts_dir")
    parser.add_argument("--category", default=CATEGORY_MAIN)
    parser.add_argument(
        "--url",
        help="Fallback PR URL for changelog parts that do not specify their own URL.",
    )

    args = parser.parse_args()
    category = args.category

    with open(args.changelog_file, "r", encoding="utf-8-sig") as f:
        current_data = yaml.load(f, Loader=NoDatesSafeLoader)

    if current_data is None:
        current_data = {}
    elif not isinstance(current_data, dict):
        raise ValueError(f"{args.changelog_file} must contain a YAML mapping")

    entries_list: list[Any] = current_data.get("Entries", [])
    if not isinstance(entries_list, list):
        raise ValueError(f"{args.changelog_file}: Entries must be a list")

    max_id = max((entry["id"] for entry in entries_list), default=0)

    event = load_github_event()
    fallback_url = (
        normalize_optional_url(args.url)
        or normalize_optional_url(os.environ.get("CHANGELOG_URL"))
        or resolve_automatic_pull_url(event)
    )

    for partname in sorted(os.listdir(args.parts_dir)):
        if not partname.endswith(".yml"):
            continue

        partpath = os.path.join(args.parts_dir, partname)
        print(partpath)

        with open(partpath, "r", encoding="utf-8-sig") as f:
            partyaml = yaml.load(f, Loader=NoDatesSafeLoader)

        if not isinstance(partyaml, dict):
            raise ValueError(f"{partpath} must contain a YAML mapping")

        part_category = partyaml.get("category", CATEGORY_MAIN)
        if part_category != category:
            print(f"Skipping: wrong category ({part_category} vs {category})")
            continue

        author = partyaml["author"]
        time = partyaml.get(
            "time", datetime.datetime.now(datetime.timezone.utc).isoformat()
        )
        changes = partyaml["changes"]
        url = normalize_optional_url(partyaml.get("url")) or fallback_url

        if not isinstance(changes, list):
            changes = [changes]

        if changes:
            # Don't add empty changelog entries...
            max_id += 1
            entry: dict[str, Any] = {
                "author": author,
                "time": time,
                "changes": changes,
                "id": max_id,
            }

            if url:
                entry["url"] = url
                print(f"Using PR URL for changelog entry {max_id}: {url}")

            entries_list.append(entry)

        os.remove(partpath)

    print(f"Have {len(entries_list)} changelog entries")

    entries_list.sort(key=lambda entry: entry["id"])

    overflow = len(entries_list) - MAX_ENTRIES
    if overflow > 0:
        print(f"Removing {overflow} old entries.")
        entries_list = entries_list[overflow:]

    new_data = {"Entries": entries_list}
    for key, value in current_data.items():
        if key != "Entries":
            new_data[key] = value

    with open(args.changelog_file, "w", encoding="utf-8-sig") as f:
        yaml.safe_dump(
            new_data,
            f,
            allow_unicode=True,
            sort_keys=False,
            width=120,
        )


if __name__ == "__main__":
    main()
