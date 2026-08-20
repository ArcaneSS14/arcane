#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2023 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
# SPDX-FileCopyrightText: 2023 SlamBamActionman <83650252+SlamBamActionman@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Aiden <aiden@djkraz.com>
# SPDX-FileCopyrightText: 2024 Myra <vasilis@pikachu.systems>
# SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""
Sends new changelog entries or a successful publish notification to a Discord webhook.

Automatically figures out the last run and changelog contents with the GitHub API.
"""

from __future__ import annotations

import os
import sys
import time
from collections import defaultdict
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence
from urllib.parse import quote, urlparse

import requests
import yaml

DEBUG = False
DEBUG_CHANGELOG_FILE_OLD = Path("Resources/Changelog/Old.yml")

GITHUB_API_URL = os.environ.get("GITHUB_API_URL", "https://api.github.com").rstrip("/")
DISCORD_WEBHOOK_URL = os.environ.get("DISCORD_WEBHOOK_URL")

CHANGELOG_FILE = (
    os.environ.get("CHANGELOG_FILE")
    or "Resources/Changelog/ArcaneChangelog.yml"
)

PUBLISH_WORKFLOWS = tuple(
    workflow.strip()
    for workflow in os.environ.get("CHANGELOG_PUBLISH_WORKFLOWS", "").split(",")
    if workflow.strip()
)

IGNORED_PUBLISH_RUNS = frozenset(
    run.strip()
    for run in os.environ.get("CHANGELOG_IGNORED_RUNS", "").split(",")
    if run.strip()
)

DISCORD_EMBED_TITLE_LIMIT = 256
DISCORD_EMBED_DESCRIPTION_LIMIT = 4096
DISCORD_EMBED_FOOTER_LIMIT = 2048
DISCORD_EMBED_TOTAL_LIMIT = 6000

HTTP_TIMEOUT_SECONDS = float(os.environ.get("CHANGELOG_HTTP_TIMEOUT", "30"))
DISCORD_MAX_RETRIES = int(os.environ.get("CHANGELOG_DISCORD_RETRIES", "5"))

DEFAULT_EMBED_COLOR = int(os.environ.get("CHANGELOG_EMBED_COLOR", "5865F2"), 16)
PUBLISH_EMBED_COLOR = int(os.environ.get("PUBLISH_EMBED_COLOR", "57F287"), 16)
PUBLISH_ROLE_ID = os.environ.get("CHANGELOG_PUBLISH_ROLE_ID", "1512901533677916280")
CHANGELOG_FOOTER = os.environ.get("CHANGELOG_FOOTER", "Arcane Station • Changelog")
WEBHOOK_USERNAME = os.environ.get("CHANGELOG_WEBHOOK_USERNAME")
WEBHOOK_AVATAR_URL = os.environ.get("CHANGELOG_WEBHOOK_AVATAR_URL")

CHANGE_TYPES: dict[str, tuple[str, str, int]] = {
    "Add": ("🆕", "Добавлено", 0x57F287),
    "Fix": ("🐛", "Исправлено", 0xED4245),
    "Tweak": ("⚒️", "Изменено", 0xFEE75C),
    "Remove": ("🗑️", "Удалено", 0x992D22),
}
UNKNOWN_CHANGE_TYPE = ("📝", "Прочее", DEFAULT_EMBED_COLOR)
CHANGE_TYPE_ORDER = ("Add", "Fix", "Tweak", "Remove")

ChangelogEntry = dict[str, Any]


def main() -> None:
    if not DISCORD_WEBHOOK_URL:
        print("No Discord webhook URL found; skipping Discord send")
        return

    if "--publish-notification" in sys.argv:
        with requests.Session() as session:
            send_publish_notification(session)
        return

    if DEBUG:
        last_changelog_stream = DEBUG_CHANGELOG_FILE_OLD.read_text(encoding="utf-8")
    else:
        # when running this normally in a GitHub actions workflow,
        # it will get the old changelog from the GitHub API
        last_changelog_stream = get_last_changelog()

    previous = load_changelog(last_changelog_stream, "previous changelog")
    current = load_changelog_file(Path(CHANGELOG_FILE))
    entries = list(diff_changelog(previous, current))

    if not entries:
        print("No new changelog entries found")
        return

    print(f"Found {len(entries)} new changelog entr{'y' if len(entries) == 1 else 'ies'}")

    with requests.Session() as session:
        for index, entry in enumerate(entries, start=1):
            embed = changelog_entry_to_embed(entry)
            print(
                f"Sending changelog {index}/{len(entries)} "
                f"(id={entry.get('id', 'unknown')}, author={entry.get('author', 'unknown')})"
            )
            send_discord_payload(session, {"embeds": [embed]})


def load_changelog(stream: str, source: str) -> dict[str, Any]:
    try:
        data = yaml.safe_load(stream)
    except yaml.YAMLError as exc:
        raise RuntimeError(f"Failed to parse {source}: {exc}") from exc

    if not isinstance(data, dict):
        raise RuntimeError(f"Invalid {source}: expected a YAML mapping")

    entries = data.get("Entries")
    if not isinstance(entries, list):
        raise RuntimeError(f"Invalid {source}: missing or invalid 'Entries' list")

    return data


def load_changelog_file(path: Path) -> dict[str, Any]:
    try:
        stream = path.read_text(encoding="utf-8")
    except OSError as exc:
        raise RuntimeError(f"Failed to read changelog file '{path}': {exc}") from exc

    return load_changelog(stream, str(path))


def get_most_recent_workflow(
    sess: requests.Session,
    github_repository: str,
    github_run: str,
    publish_workflows: Iterable[str] = PUBLISH_WORKFLOWS,
    ignored_runs: Iterable[str] = IGNORED_PUBLISH_RUNS,
) -> dict[str, Any] | None:
    """Find the newest usable successful run across configured publish workflows."""
    workflow_run = get_current_run(sess, github_repository, github_run)
    ignored_run_ids = {str(run) for run in ignored_runs}

    workflow_urls = [workflow_run["workflow_url"]]
    if publish_workflows:
        workflows_url = f"{GITHUB_API_URL}/repos/{github_repository}/actions/workflows"
        workflow_urls = [
            f"{workflows_url}/{quote(workflow, safe='')}"
            for workflow in publish_workflows
        ]

    past_runs: list[dict[str, Any]] = []
    for workflow_url in workflow_urls:
        response = get_past_runs(sess, workflow_url, workflow_run["created_at"])
        past_runs.extend(
            run
            for run in response.get("workflow_runs", [])
            if run.get("id") != workflow_run.get("id")
            and str(run.get("id")) not in ignored_run_ids
        )

    return max(past_runs, key=lambda run: run.get("created_at", ""), default=None)


def get_current_run(
    sess: requests.Session, github_repository: str, github_run: str
) -> dict[str, Any]:
    response = sess.get(
        f"{GITHUB_API_URL}/repos/{github_repository}/actions/runs/{github_run}",
        timeout=HTTP_TIMEOUT_SECONDS,
    )
    response.raise_for_status()
    return response.json()


def get_past_runs(
    sess: requests.Session, workflow_url: str, current_run_created_at: str
) -> dict[str, Any]:
    """Get successful workflow runs that happened before the current run."""
    params = {
        "status": "success",
        "created": f"<={current_run_created_at}",
        "per_page": 100,
    }
    response = sess.get(
        f"{workflow_url}/runs",
        params=params,
        timeout=HTTP_TIMEOUT_SECONDS,
    )
    response.raise_for_status()
    return response.json()


def get_last_changelog() -> str:
    github_repository = os.environ["GITHUB_REPOSITORY"]
    github_run = os.environ["GITHUB_RUN_ID"]
    github_token = os.environ["GITHUB_TOKEN"]

    with requests.Session() as session:
        session.headers.update(
            {
                "Authorization": f"Bearer {github_token}",
                "Accept": "application/vnd.github+json",
                "X-GitHub-Api-Version": "2022-11-28",
                "User-Agent": "arcane-changelog-action",
            }
        )

        most_recent = get_most_recent_workflow(
            session, github_repository, github_run
        )
        if most_recent is None:
            workflows = ", ".join(PUBLISH_WORKFLOWS) or "the current workflow"
            raise RuntimeError(
                f"No previous successful publish run found in {workflows}; "
                "cannot calculate the changelog diff"
            )

        last_sha = most_recent["head_sha"]
        print(f"Last successful publish job was {most_recent['id']}: {last_sha}")
        return get_last_changelog_by_sha(session, last_sha, github_repository)


def get_last_changelog_by_sha(
    sess: requests.Session, sha: str, github_repository: str
) -> str:
    """Fetch the changelog file as it existed at a specific Git SHA."""
    response = sess.get(
        f"{GITHUB_API_URL}/repos/{github_repository}/contents/{CHANGELOG_FILE}",
        headers={"Accept": "application/vnd.github.raw"},
        params={"ref": sha},
        timeout=HTTP_TIMEOUT_SECONDS,
    )
    response.raise_for_status()
    return response.text


def diff_changelog(
    old: Mapping[str, Any], cur: Mapping[str, Any]
) -> Iterable[ChangelogEntry]:
    """Yield entries that are present now but were absent in the previous publish."""
    old_ids = {
        entry.get("id")
        for entry in old.get("Entries", [])
        if isinstance(entry, dict) and "id" in entry
    }

    for entry in cur.get("Entries", []):
        if not isinstance(entry, dict):
            continue
        if entry.get("id") not in old_ids:
            yield entry


def normalize_url(value: Any) -> str | None:
    if not isinstance(value, str):
        return None

    value = value.strip()
    if not value:
        return None

    parsed = urlparse(value)
    if parsed.scheme not in {"http", "https"} or not parsed.netloc:
        return None

    return value


def extract_pr_number(url: str | None) -> str | None:
    if not url:
        return None

    path = urlparse(url).path.rstrip("/")
    if not path:
        return None

    last_part = path.rsplit("/", 1)[-1]
    return last_part if last_part.isdigit() else None


def sanitize_text(value: Any, fallback: str = "") -> str:
    if value is None:
        return fallback

    text = str(value).replace("\r\n", "\n").replace("\r", "\n").strip()
    return text or fallback


def truncate(text: str, limit: int, suffix: str = "…") -> str:
    if len(text) <= limit:
        return text

    if limit <= len(suffix):
        return suffix[:limit]

    return text[: limit - len(suffix)].rstrip() + suffix


def get_change_type_info(change_type: Any) -> tuple[str, str, int]:
    return CHANGE_TYPES.get(str(change_type), UNKNOWN_CHANGE_TYPE)


def build_changelog_description(changes: Sequence[Mapping[str, Any]]) -> str:
    grouped: dict[str, list[str]] = defaultdict(list)
    unknown_types: list[str] = []

    for change in changes:
        change_type = sanitize_text(change.get("type"), "Other")
        message = sanitize_text(change.get("message"), "Без описания")

        # Keep multiline entries readable inside a bullet point.
        message = message.replace("\n", "\n  ")
        grouped[change_type].append(message)
        if change_type not in CHANGE_TYPES and change_type not in unknown_types:
            unknown_types.append(change_type)

    ordered_types = [change_type for change_type in CHANGE_TYPE_ORDER if grouped[change_type]]
    ordered_types.extend(unknown_types)

    sections: list[str] = []
    for change_type in ordered_types:
        emoji, label, _ = get_change_type_info(change_type)
        messages = "\n".join(f"• {message}" for message in grouped[change_type])
        sections.append(f"**{emoji} {label}**\n{messages}")

    if not sections:
        return "*В этой записи нет описанных изменений.*"

    full_description = "\n\n".join(sections)
    if len(full_description) <= DISCORD_EMBED_DESCRIPTION_LIMIT:
        return full_description

    # Preserve as much information as possible while keeping exactly one embed
    # per changelog entry.
    suffix = "\n\n*Описание сокращено из-за лимита Discord.*"
    return truncate(
        full_description,
        DISCORD_EMBED_DESCRIPTION_LIMIT,
        suffix=suffix,
    )


def choose_embed_color(changes: Sequence[Mapping[str, Any]]) -> int:
    change_types = {sanitize_text(change.get("type")) for change in changes}
    known_types = [change_type for change_type in CHANGE_TYPE_ORDER if change_type in change_types]

    if len(known_types) == 1:
        return CHANGE_TYPES[known_types[0]][2]

    return DEFAULT_EMBED_COLOR


def changelog_entry_to_embed(entry: Mapping[str, Any]) -> dict[str, Any]:
    author = sanitize_text(entry.get("author"), "Неизвестный автор")
    url = normalize_url(entry.get("url"))
    pr_number = extract_pr_number(url)
    entry_id = sanitize_text(entry.get("id"), "unknown")

    raw_changes = entry.get("changes", [])
    changes: list[Mapping[str, Any]] = [
        change for change in raw_changes if isinstance(change, Mapping)
    ] if isinstance(raw_changes, list) else []

    if pr_number:
        title = f"Ченджлог • PR #{pr_number}"
    else:
        title = "Новый ченджлог"

    footer = f"{CHANGELOG_FOOTER} • ID: {entry_id}"

    embed: dict[str, Any] = {
        "title": truncate(title, DISCORD_EMBED_TITLE_LIMIT),
        "description": build_changelog_description(changes),
        "color": choose_embed_color(changes),
        "author": {"name": truncate(f"👤 {author}", 256)},
        "footer": {"text": truncate(footer, DISCORD_EMBED_FOOTER_LIMIT)},
    }

    if url:
        embed["url"] = url

    timestamp = sanitize_text(entry.get("time"))
    if timestamp:
        if "T" in timestamp:
            embed["timestamp"] = timestamp

    total_chars = (
        len(embed.get("title", ""))
        + len(embed.get("description", ""))
        + len(embed.get("author", {}).get("name", ""))
        + len(embed.get("footer", {}).get("text", ""))
    )
    if total_chars > DISCORD_EMBED_TOTAL_LIMIT:
        overflow = total_chars - DISCORD_EMBED_TOTAL_LIMIT
        description = embed["description"]
        target = max(1, len(description) - overflow - 1)
        embed["description"] = truncate(description, target)

    return embed


def make_webhook_payload(
    *,
    content: str | None = None,
    embeds: Sequence[Mapping[str, Any]] | None = None,
    allowed_mentions: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    body: dict[str, Any] = {
        "allowed_mentions": allowed_mentions or {"parse": []},
    }

    if content:
        body["content"] = content
    if embeds:
        body["embeds"] = list(embeds)
    if WEBHOOK_USERNAME:
        body["username"] = WEBHOOK_USERNAME
    if WEBHOOK_AVATAR_URL:
        body["avatar_url"] = WEBHOOK_AVATAR_URL

    return body


def send_discord_payload(
    session: requests.Session,
    payload: Mapping[str, Any],
) -> None:
    if not DISCORD_WEBHOOK_URL:
        raise RuntimeError("DISCORD_WEBHOOK_URL is not configured")

    body = make_webhook_payload(
        content=payload.get("content") if isinstance(payload.get("content"), str) else None,
        embeds=payload.get("embeds") if isinstance(payload.get("embeds"), list) else None,
        allowed_mentions=(
            payload.get("allowed_mentions")
            if isinstance(payload.get("allowed_mentions"), Mapping)
            else None
        ),
    )

    last_error: Exception | None = None

    for attempt in range(DISCORD_MAX_RETRIES + 1):
        try:
            response = session.post(
                DISCORD_WEBHOOK_URL,
                json=body,
                timeout=HTTP_TIMEOUT_SECONDS,
            )
        except requests.RequestException as exc:
            last_error = exc
            if attempt >= DISCORD_MAX_RETRIES:
                break
            time.sleep(min(2**attempt, 10))
            continue

        if response.status_code == 429:
            if attempt >= DISCORD_MAX_RETRIES:
                response.raise_for_status()

            retry_after = 1.0
            try:
                retry_after = float(response.json().get("retry_after", retry_after))
            except (ValueError, TypeError, requests.JSONDecodeError):
                header_value = response.headers.get("Retry-After")
                if header_value:
                    try:
                        retry_after = float(header_value)
                    except ValueError:
                        pass

            print(f"Discord rate limit hit; retrying after {retry_after:.2f}s")
            time.sleep(max(retry_after, 0.05))
            continue

        if 500 <= response.status_code < 600:
            if attempt >= DISCORD_MAX_RETRIES:
                response.raise_for_status()
            delay = min(2**attempt, 10)
            print(f"Discord returned HTTP {response.status_code}; retrying in {delay}s")
            time.sleep(delay)
            continue

        response.raise_for_status()

        if response.headers.get("X-RateLimit-Remaining") == "0":
            reset_after = response.headers.get("X-RateLimit-Reset-After")
            if reset_after:
                try:
                    time.sleep(max(float(reset_after), 0.0))
                except ValueError:
                    pass

        return

    raise RuntimeError("Failed to send Discord webhook after retries") from last_error


def send_publish_notification(session: requests.Session) -> None:
    repository = os.environ.get("GITHUB_REPOSITORY")
    sha = os.environ.get("GITHUB_SHA")

    commit_url: str | None = None
    if repository and sha:
        commit_url = f"https://github.com/{repository}/commit/{sha}"

    description = "Новая версия успешно опубликована на сервере."
    if sha:
        short_sha = sha[:7]
        if commit_url:
            description += f"\n\n**Версия:** [`{short_sha}`]({commit_url})"
        else:
            description += f"\n\n**Версия:** `{short_sha}`"

    embed: dict[str, Any] = {
        "title": "✅ Сервер обновлён",
        "description": description,
        "color": PUBLISH_EMBED_COLOR,
        "footer": {"text": "Arcane Station • Deploy"},
    }
    if commit_url:
        embed["url"] = commit_url

    content = f"<@&{PUBLISH_ROLE_ID}>" if PUBLISH_ROLE_ID else None
    allowed_mentions: dict[str, Any] = {"parse": []}
    if PUBLISH_ROLE_ID:
        allowed_mentions["roles"] = [PUBLISH_ROLE_ID]

    print("Sending publish notification to Discord")
    send_discord_payload(
        session,
        {
            "content": content,
            "embeds": [embed],
            "allowed_mentions": allowed_mentions,
        },
    )


if __name__ == "__main__":
    main()
