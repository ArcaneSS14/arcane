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

import json
import os
import sys
import time
from collections import defaultdict
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence
from urllib.parse import parse_qsl, quote, urlencode, urlparse

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

DISCORD_EMBED_DESCRIPTION_LIMIT = 4096
DISCORD_CONTENT_LIMIT = 2000
DISCORD_MEDIA_URL_LIMIT = 2000

DISCORD_COMPONENTS_V2_FLAG = 1 << 15
DISCORD_CV2_TEXT_LIMIT = 3500
DISCORD_MEDIA_GALLERY_MAX_ITEMS = 10
DISCORD_ATTACHMENT_SIZE_LIMIT = 25 * 1024 * 1024
MEDIA_MAX_DOWNLOAD_SIZE = 25 * 1024 * 1024
MEDIA_READ_CHUNK_SIZE = 64 * 1024

DESCRIPTION_TRUNCATION_SUFFIX = "\n\n*Описание сокращено из-за лимита Discord.*"

MEDIA_FILE_EXTENSIONS = frozenset({
    ".apng",
    ".avif",
    ".bmp",
    ".gif",
    ".jpeg",
    ".jpg",
    ".m4v",
    ".mkv",
    ".mov",
    ".mp4",
    ".png",
    ".webm",
    ".webp",
})

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


class DiscordFileTooLarge(RuntimeError):
    """Raised when Discord rejects a webhook message because a file is too large."""

_used_media_names: set[str] = set()


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
            print(
                f"Sending changelog {index}/{len(entries)} "
                f"(id={entry.get('id', 'unknown')}, author={entry.get('author', 'unknown')})"
            )
            send_changelog_entry(session, entry)


def send_changelog_entry(session: requests.Session, entry: Mapping[str, Any]) -> None:
    """Download an entry's media and post it through Components V2 cards."""
    changes = entry.get("changes", [])
    description = build_changelog_description(
        [change for change in changes if isinstance(change, Mapping)]
    )

    media_files: list[tuple[str, str, bytes, str]] = []
    link_urls: list[str] = []
    for url in iter_media_urls(entry):
        downloaded = download_media(session, url)
        if downloaded is None:
            print(f"Falling back to a link for media ({url})")
            link_urls.append(url)
            continue
        filename, mime, data, _ = downloaded
        media_files.append(downloaded)
        print(f"Downloaded media ({len(data)} bytes): {url} -> {filename}")

    first_payload = True
    pending_link_urls = list(link_urls)
    payload_gen = build_entry_cv2_payloads(entry, description, media_files, link_urls)
    for payload, chunk_urls in payload_gen:
        try:
            send_discord_payload(session, payload)
        except DiscordFileTooLarge as exc:
            print(f"Chunk rejected as too large ({exc}); posting its links instead")
            all_urls = [*pending_link_urls, *chunk_urls]
            pending_link_urls.clear()
            for fallback in build_link_fallback_payloads(
                description if first_payload else "", all_urls
            ):
                send_discord_payload(session, fallback)
        except RuntimeError as exc:
            print(f"Media message rejected ({exc}); posting links instead")
            remaining_urls = [
                url for _, urls in payload_gen for url in urls
            ]
            all_urls = [*link_urls, *chunk_urls, *remaining_urls]
            for fallback in build_link_fallback_payloads(description, all_urls):
                send_discord_payload(session, fallback)
            return
        first_payload = False


def build_entry_cv2_payloads(
    entry: Mapping[str, Any],
    description: str,
    media_files: Sequence[tuple[str, str, bytes, str]],
    link_urls: Sequence[str],
) -> Iterable[tuple[dict[str, Any], Sequence[str]]]:
    """Chunk downloaded media into CV2 messages with media gallery cards.

    Yields ``(payload, urls)`` pairs; the URLs let callers fall back to plain
    links when Discord rejects a chunk. The first chunk rides inside the
    changelog card; overflow media go into their own cards. Every chunk
    respects the media gallery item cap and an attachment byte budget so each
    message stays well-formed.
    """
    if not media_files:
        container = build_entry_container(entry, description, [], link_urls)
        yield make_media_payload(container, [])
        return

    remaining = list(media_files)
    first = True
    while remaining:
        chunk: list[tuple[str, str, bytes, str]] = []
        chunk_bytes = 0
        while remaining and len(chunk) < DISCORD_MEDIA_GALLERY_MAX_ITEMS:
            _, _, data, _ = remaining[0]
            if chunk and chunk_bytes + len(data) > DISCORD_ATTACHMENT_SIZE_LIMIT:
                break
            chunk.append(remaining.pop(0))
            chunk_bytes += len(data)

        if first:
            container = build_entry_container(
                entry, description, gallery_items(chunk), link_urls
            )
            first = False
        else:
            container = build_media_overflow_container(entry, gallery_items(chunk))

        yield make_media_payload(container, chunk)


def build_media_overflow_container(
    entry: Mapping[str, Any],
    gallery_items: Sequence[Mapping[str, Any]],
) -> dict[str, Any]:
    """CV2 card carrying media gallery items beyond the first message."""
    author = sanitize_text(entry.get("author"), "Неизвестный автор")
    url = normalize_url(entry.get("url"))
    pr_number = extract_pr_number(url)
    entry_id = sanitize_text(entry.get("id"), "unknown")
    raw_time = entry.get("time")

    if pr_number and url:
        title = f"**Медиа • [PR #{pr_number}]({url})**"
    elif pr_number:
        title = f"**Медиа • PR #{pr_number}**"
    else:
        title = "**Дополнительные медиа**"

    footer_parts: list[str] = []
    if raw_time:
        try:
            from datetime import datetime
            dt = datetime.fromisoformat(str(raw_time))
            footer_parts.append(dt.strftime("%d.%m.%Y %H:%M"))
        except (ValueError, TypeError):
            footer_parts.append(str(raw_time))
    footer_parts.append(f"ID: {entry_id}")

    return {
        "type": 17,
        "accent_color": DEFAULT_EMBED_COLOR,
        "components": [
            {"type": 10, "content": f"👤 **{author}**"},
            {"type": 10, "content": title},
            {"type": 14, "divider": True, "spacing": 1},
            {"type": 12, "items": list(gallery_items)},
            {"type": 14, "divider": True, "spacing": 1},
            {"type": 10, "content": f"-# 🕐 {' • '.join(footer_parts)}"},
        ],
    }


def gallery_items(files: Sequence[tuple[str, str, bytes, str]]) -> list[dict[str, Any]]:
    """Media gallery items referencing uploaded attachments by filename."""
    return [
        {"media": {"url": f"attachment://{filename}"}}
        for filename, _, _, _ in files
    ]


def make_media_payload(
    container: Mapping[str, Any],
    files: Sequence[tuple[str, str, bytes, str]],
) -> tuple[dict[str, Any], Sequence[str]]:
    """Pack a CV2 container and its attachments into a webhook payload.

    Returns ``(payload, urls)`` so callers can fall back to links for a chunk
    Discord rejected.
    """
    attachments = [(filename, mime, data) for filename, mime, data, _ in files]
    urls = [file_url for _, _, _, file_url in files]
    return {"components": [container], "attachments": attachments}, urls


def make_link_block(link_urls: Sequence[str]) -> str:
    """A text block listing media that could only be shared as links."""
    return "\n".join(f"🔗 {url}" for url in link_urls)


def truncate_lines(text: str, limit: int) -> str:
    """Trim a text block whole lines, never splitting a line in the middle."""
    if len(text) <= limit:
        return text

    kept: list[str] = []
    used = 0
    for line in text.splitlines():
        line_len = len(line) + (1 if kept else 0)
        if kept and used + line_len > limit:
            break
        kept.append(line)
        used += line_len

    if not kept:
        return truncate(text.splitlines()[0], limit)
    return "\n".join(kept)


def build_link_fallback_payloads(
    description: str,
    link_urls: Sequence[str],
) -> list[dict[str, Any]]:
    """Plain-text fallback messages for media that could not be attached.

    Same ``content`` channel as the legacy path: one message per
    ``DISCORD_CONTENT_LIMIT``. URLs are never split; a link line is either
    kept whole in one message or skipped if it alone would exceed the limit.
    """
    if not link_urls:
        if description:
            return [{"content": truncate(description, DISCORD_CONTENT_LIMIT)}]
        return []

    lines = [f"🔗 {url}" for url in link_urls]
    if any(len(line) > DISCORD_CONTENT_LIMIT for line in lines):
        print("Skipping media URL too long for a Discord link message")
        lines = [line for line in lines if len(line) <= DISCORD_CONTENT_LIMIT]

    full = f"{description}\n\n" + "\n".join(lines) if description else "\n".join(lines)
    if len(full) <= DISCORD_CONTENT_LIMIT:
        return [{"content": full}]

    payloads: list[dict[str, Any]] = []
    if description:
        payloads.append({"content": truncate(description, DISCORD_CONTENT_LIMIT)})

    current: list[str] = []
    current_len = 0
    for line in lines:
        line_len = len(line) + (1 if current else 0)
        if current and current_len + line_len > DISCORD_CONTENT_LIMIT:
            payloads.append({"content": "\n".join(current)})
            current = [line]
            current_len = len(line)
        else:
            current.append(line)
            current_len += line_len
    if current:
        payloads.append({"content": "\n".join(current)})
    return payloads


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


def build_entry_container(
    entry: Mapping[str, Any],
    description: str,
    gallery_items: Sequence[Mapping[str, Any]],
    link_urls: Sequence[str] = (),
) -> dict[str, Any]:
    """Build a Components V2 container card for one changelog entry.

    CV2 disables classic ``embeds``, so the entry is rendered as a type-17
    container holding text displays and a media gallery. Media whose download
    failed is appended as plain link lines instead.
    """
    author = sanitize_text(entry.get("author"), "Неизвестный автор")
    url = normalize_url(entry.get("url"))
    pr_number = extract_pr_number(url)
    entry_id = sanitize_text(entry.get("id"), "unknown")
    raw_time = entry.get("time")

    raw_changes = entry.get("changes", [])
    changes: list[Mapping[str, Any]] = [
        change for change in raw_changes if isinstance(change, Mapping)
    ] if isinstance(raw_changes, list) else []

    if pr_number and url:
        title = f"**Ченджлог • [PR #{pr_number}]({url})**"
    elif pr_number:
        title = f"**Ченджлог • PR #{pr_number}**"
    else:
        title = "**Новый ченджлог**"

    components: list[dict[str, Any]] = [
        {"type": 10, "content": f"👤 **{author}**"},
        {"type": 10, "content": title},
        {"type": 14, "divider": True, "spacing": 1},
    ]

    budget = max(
        DISCORD_CV2_TEXT_LIMIT
        - sum(len(str(component.get("content", ""))) for component in components),
        1,
    )
    link_block = make_link_block(link_urls)
    if link_block:
        if len(link_block) >= budget:
            link_block = truncate_lines(link_block, max(budget // 2, 8))
        description_limit = max(budget - len(link_block), 1)
    else:
        description_limit = budget

    components.append(
        {
            "type": 10,
            "content": truncate(
                description,
                description_limit,
                suffix=DESCRIPTION_TRUNCATION_SUFFIX,
            ),
        }
    )
    if link_block:
        components.append({"type": 10, "content": link_block})

    if gallery_items:
        components.append({"type": 14, "divider": True, "spacing": 1})
        components.append({"type": 12, "items": list(gallery_items)})

    footer_parts: list[str] = []
    if raw_time:
        try:
            from datetime import datetime, timezone
            dt = datetime.fromisoformat(str(raw_time))
            footer_parts.append(dt.strftime("%d.%m.%Y %H:%M"))
        except (ValueError, TypeError):
            footer_parts.append(str(raw_time))
    footer_parts.append(f"ID: {entry_id}")

    components.append({"type": 14, "divider": True, "spacing": 1})
    components.append({"type": 10, "content": f"-# 🕐 {' • '.join(footer_parts)}"})

    return {"type": 17, "accent_color": choose_embed_color(changes), "components": components}


def iter_media_urls(entry: Mapping[str, Any]) -> Iterable[str]:
    """Yield validated, deduplicated media URLs from a changelog entry."""
    media = entry.get("media", [])
    if not isinstance(media, list):
        return

    seen: set[str] = set()
    for value in media:
        try:
            url = normalize_url(value)
            if not url:
                continue
            parsed = urlparse(url)
            if not parsed.hostname or parsed.username or parsed.password:
                continue
        except ValueError:
            continue

        if any(char.isspace() or ord(char) < 32 or char in '<>"`' for char in url):
            continue
        if len(url) > DISCORD_MEDIA_URL_LIMIT:
            print(f"Skipping media URL exceeding Discord's limit (entry {entry.get('id')})")
            continue
        if url in seen:
            continue
        seen.add(url)
        yield url


class MediaTypeError(RuntimeError):
    """Raised when a downloaded media file has unrecognized content."""


def detect_media_type(data: bytes) -> tuple[str, str]:
    """Identify (mime, extension) from magic bytes; raise MediaTypeError otherwise."""
    if data.startswith(b"\xff\xd8\xff"):
        return "image/jpeg", ".jpg"
    if data.startswith(b"\x89PNG\r\n\x1a\n"):
        return "image/png", ".png"
    if data.startswith((b"GIF87a", b"GIF89a")):
        return "image/gif", ".gif"
    if data.startswith(b"RIFF") and data[8:12] == b"WEBP":
        return "image/webp", ".webp"
    if data.startswith(b"\x1a\x45\xdf\xa3"):
        return "video/webm", ".webm"
    if len(data) >= 12 and data[4:8] == b"ftyp":
        brand = data[8:12]
        if brand in (
            b"qt  ",
            b"isom",
            b"iso2",
            b"iso3",
            b"mp41",
            b"mp42",
            b"avc1",
            b"avc3",
            b"dash",
            b"M4V ",
        ):
            if brand == b"M4V ":
                return "video/m4v", ".m4v"
            if brand == b"qt  ":
                return "video/quicktime", ".mov"
            return "video/mp4", ".mp4"
    raise MediaTypeError("unrecognized media content")


def send_get(session: requests.Session, url: str) -> requests.Response:
    """Streaming GET for media downloads, letting the session follow redirects."""
    request = requests.Request("GET", url)
    prepared = session.prepare_request(request)
    return session.send(
        prepared,
        stream=True,
        timeout=HTTP_TIMEOUT_SECONDS,
        allow_redirects=True,
    )


def download_media(
    session: requests.Session,
    url: str,
) -> tuple[str, str, bytes, str] | None:
    """Download a media file and return (filename, mime, data, url).

    Returns None when the file cannot be fetched or exceeds
    ``MEDIA_MAX_DOWNLOAD_SIZE``; callers fall back to sharing a bare link.
    """
    try:
        response = send_get(session, url)
        response.raise_for_status()

        content_length = response.headers.get("Content-Length")
        if content_length:
            try:
                if int(content_length) > MEDIA_MAX_DOWNLOAD_SIZE:
                    print(
                        f"Media too large to download ({url}): {content_length} bytes"
                    )
                    return None
            except ValueError:
                pass

        data = bytearray()
        for chunk in response.iter_content(chunk_size=MEDIA_READ_CHUNK_SIZE):
            if not chunk:
                continue
            data.extend(chunk)
            if len(data) > MEDIA_MAX_DOWNLOAD_SIZE:
                print(f"Media exceeded the download cap ({url})")
                return None
    except requests.RequestException as exc:
        print(f"Failed to download media ({url}): {exc}")
        return None

    try:
        mime, ext = detect_media_type(bytes(data))
    except MediaTypeError:
        ext = Path(urlparse(url).path).suffix
        if ext not in MEDIA_FILE_EXTENSIONS:
            print(f"Unrecognized media type ({url}); falling back to a link")
            return None
        mime = "application/octet-stream"

    filename = f"changelog-media-{abs(hash(url))}{ext}"
    candidate = filename
    counter = 1
    while candidate in _used_media_names:
        candidate = f"changelog-media-{abs(hash(url))}-{counter}{ext}"
        counter += 1
    _used_media_names.add(candidate)
    return candidate, mime, bytes(data), url


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


def discord_webhook_url(with_components: bool = False) -> str:
    """Webhook URL, optionally switched into Components V2 mode.

    Webhooks only accept ``components`` once the ``with_components=true``
    query flag has been supplied; the toggle is irreversible per webhook.
    """
    if not DISCORD_WEBHOOK_URL:
        return ""
    if not with_components:
        return DISCORD_WEBHOOK_URL

    parsed = urlparse(DISCORD_WEBHOOK_URL)
    query = dict(parse_qsl(parsed.query))
    query["with_components"] = "true"
    return parsed._replace(query=urlencode(query)).geturl()


def send_discord_payload(
    session: requests.Session,
    payload: Mapping[str, Any],
) -> None:
    if not DISCORD_WEBHOOK_URL:
        raise RuntimeError("DISCORD_WEBHOOK_URL is not configured")

    components = payload.get("components")
    use_cv2 = isinstance(components, list) and bool(components)
    target_url = discord_webhook_url(with_components=use_cv2)

    body = make_webhook_payload(
        content=payload.get("content") if isinstance(payload.get("content"), str) else None,
        embeds=payload.get("embeds") if isinstance(payload.get("embeds"), list) else None,
        allowed_mentions=(
            payload.get("allowed_mentions")
            if isinstance(payload.get("allowed_mentions"), Mapping)
            else None
        ),
    )

    files: list[tuple[str, tuple[str, bytes, str]]] = []
    if use_cv2:
        body["components"] = list(components)
        body["flags"] = payload.get("flags") or DISCORD_COMPONENTS_V2_FLAG
        attachments = payload.get("attachments")
        if isinstance(attachments, list):
            body_attachments: list[dict[str, Any]] = []
            for index, member in enumerate(attachments):
                if not isinstance(member, tuple) or len(member) != 3:
                    continue
                filename, mime, data = member
                if not isinstance(filename, str) or not isinstance(data, bytes):
                    continue
                body_attachments.append(
                    {"id": index, "filename": filename, "description": ""}
                )
                files.append(
                    (
                        f"files[{index}]",
                        (filename, data, mime or "application/octet-stream"),
                    )
                )
            if body_attachments:
                body["attachments"] = body_attachments

    post_kwargs: dict[str, Any] = {"timeout": HTTP_TIMEOUT_SECONDS}
    if files:
        post_kwargs["data"] = {"payload_json": json.dumps(body)}
        post_kwargs["files"] = files
    else:
        post_kwargs["json"] = body

    last_error: Exception | None = None

    for attempt in range(DISCORD_MAX_RETRIES + 1):
        try:
            response = session.post(target_url, **post_kwargs)
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

        if response.status_code == 413:
            raise DiscordFileTooLarge(
                "Discord rejected the webhook message: file too large"
            )

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
