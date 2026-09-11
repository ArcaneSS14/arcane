function getMedia(body) {
    const urls = new Set();
    let mediaLevel = 0;
    let fence = null;

    for (const line of (body || "").replace(/<!--.*?-->/gs, "").split(/\r?\n/)) {
        const fenceMatch = /^ {0,3}(`{3,}|~{3,})(.*)$/.exec(line);
        if (fenceMatch) {
            if (!fence) {
                fence = fenceMatch[1];
            } else if (fenceMatch[1][0] === fence[0]
                && fenceMatch[1].length >= fence.length && !fenceMatch[2].trim()) {
                fence = null;
            }
            continue;
        }
        if (fence) continue;

        const heading = /^ {0,3}(#{1,6})\s+(.+?)\s*#*\s*$/.exec(line);
        if (heading) {
            const level = heading[1].length;
            if (level <= mediaLevel) mediaLevel = 0;
            if (/^(медиа|media)$/i.test(heading[2])) mediaLevel = level;
            continue;
        }
        if (/^\s*(?::cl:|🆑|\*\*Изменения\*\*)/i.test(line)) mediaLevel = 0;
        if (!mediaLevel) continue;

        for (const match of line.matchAll(/https?:\/\/[^\s<>"'`]+/gi)) {
            let value = match[0].replace(/&amp;/gi, "&").replace(/[.,;!]+$/, "");
            for (const [open, close] of [["(", ")"], ["[", "]"]]) {
                while (value.endsWith(close)
                    && value.split(close).length > value.split(open).length) {
                    value = value.slice(0, -1);
                }
            }
            try {
                const url = new URL(value);
                if (url.hostname && !url.username && !url.password) urls.add(url.href);
            } catch {
                // Нет иди отсюда
            }
        }
    }

    return [...urls];
}

module.exports = { getMedia };
