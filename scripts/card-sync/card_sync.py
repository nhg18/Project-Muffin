"""카드 정리 봇 — 노션 카드 DB 를 어제 상태와 비교해 바뀐 카드만 정리한다.

사람은 카드 DB 에서 추가 · 수정 · 삭제(상태 「삭제」)만 한다. 이 스크립트가 하는 일은 README.md.
표준 라이브러리만 쓴다 (GitHub Actions 에서 설치 없이 돈다).

    python card_sync.py --snapshot snap.json --repo <저장소 루트> [--dry-run]

환경 변수 NOTION_TOKEN 이 필요하다. 아무것도 지우지 않는다.
"""
import argparse
import datetime
import difflib
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request

# ── 설정 ─────────────────────────────────────────────────────────────

CARD_DS = "3d26dad6-6abe-800c-8b9f-000be2f2c4fa"     # 카드 목록 (기획 원본)
PATCH_DS = "4196dad6-6abe-81c3-8859-000b52a6bab4"    # 📝 패치 노트
OLD_DS = "4196dad6-6abe-8179-9515-000bb16b186e"      # 🗄️ 카드 옛 버전
LOG_DS = "4196dad6-6abe-8196-beae-000b44e282e3"      # 🧾 카드 변경 기록 (🃏 카드 칸)
I18N_DS = "4196dad6-6abe-813a-963e-000bf3a619fe"     # 🌐 다국어 문구
BOT_PAGE = "4196dad6-6abe-81d2-8970-c9e1863fe31b"    # 🤖 카드 정리 봇 (스위치 · 마지막 실행)

CARDS_JSON = "MuffinProject/Assets/Cards/Main/cards.json"
CARD_LIST_MD = "docs/systems/11-card-list.md"

# 카드 규칙이 적힌 본문 절 — 이 절에서 숫자 말고 글이 바뀌면 「능력 변경」 (이펙트 · 그림 절은 안 본다)
RULE_HEADINGS = ("효과", "추가 사용 조건", "발동 조건", "예외")
REQUIRED = ("Name", "Type", "대상", "장수", "카드 문구", "등급")
GRADE_ENUM = {"일반": "Normal", "고급": "Fine", "희귀": "Rare", "개판": "Gaepan"}
TYPE_PREFIX = {"Action": 1000, "Counter": 2000, "Trap": 3000}

NOTION_VERSION = "2025-09-03"
API = "https://api.notion.com/v1"


# ── 노션 API ─────────────────────────────────────────────────────────

class Notion:
    def __init__(self, token, dry_run):
        self.token = token
        self.dry_run = dry_run
        self.writes = []   # dry-run 때 하려던 쓰기

    def call(self, method, path, body=None):
        if self.dry_run and method != "GET" and not path.endswith("/query"):
            self.writes.append((method, path, body))
            return {"id": "dry-run"}
        data = json.dumps(body).encode() if body is not None else None
        for attempt in range(5):
            req = urllib.request.Request(API + path, data=data, method=method, headers={
                "Authorization": "Bearer " + self.token,
                "Notion-Version": NOTION_VERSION,
                "Content-Type": "application/json",
            })
            try:
                with urllib.request.urlopen(req, timeout=60) as r:
                    time.sleep(0.35)   # 초당 3회 제한
                    return json.loads(r.read())
            except urllib.error.HTTPError as e:
                if e.code in (429, 502, 503) and attempt < 4:
                    time.sleep(float(e.headers.get("Retry-After", 2 ** attempt)))
                    continue
                raise RuntimeError(f"{method} {path} → {e.code} {e.read().decode(errors='replace')}")

    def query(self, ds, flt=None):
        out, cursor = [], None
        while True:
            body = {"page_size": 100}
            if flt:
                body["filter"] = flt
            if cursor:
                body["start_cursor"] = cursor
            r = self.call("POST", f"/data_sources/{ds}/query", body)
            out += r["results"]
            if not r.get("has_more"):
                return out
            cursor = r["next_cursor"]

    def children(self, block_id):
        out, cursor = [], None
        while True:
            q = "?page_size=100" + (f"&start_cursor={cursor}" if cursor else "")
            r = self.call("GET", f"/blocks/{block_id}/children{q}")
            for b in r["results"]:
                if b.get("has_children") and b["type"] not in ("child_page", "child_database"):
                    b["_children"] = self.children(b["id"])
                out.append(b)
            if not r.get("has_more"):
                return out
            cursor = r["next_cursor"]

    def create_page(self, ds, props, blocks=None):
        blocks = blocks or []
        page = self.call("POST", "/pages", {
            "parent": {"type": "data_source_id", "data_source_id": ds},
            "properties": props, "children": blocks[:100]})
        for i in range(100, len(blocks), 100):
            self.call("PATCH", f"/blocks/{page['id']}/children", {"children": blocks[i:i + 100]})
        return page

    def update_page(self, page_id, props):
        return self.call("PATCH", f"/pages/{page_id}", {"properties": props})

    def comment(self, page_id, text):
        return self.call("POST", "/comments", {"parent": {"page_id": page_id}, "rich_text": rt(text)})


def rt(text):
    """긴 글은 2000자씩 나눈다 (노션 한 조각 제한)."""
    text = text or ""
    return [{"type": "text", "text": {"content": text[i:i + 2000]}} for i in range(0, max(len(text), 1), 2000)]


def plain(prop):
    if not prop:
        return ""
    t = prop["type"]
    if t in ("title", "rich_text"):
        return "".join(x["plain_text"] for x in prop[t])
    if t == "select":
        return prop["select"]["name"] if prop["select"] else ""
    if t == "number":
        return prop["number"]
    if t == "checkbox":
        return prop["checkbox"]
    if t == "relation":
        return [x["id"] for x in prop["relation"]]
    return ""


# ── 카드 읽기 ────────────────────────────────────────────────────────

def read_card(page):
    p = page["properties"]
    num = plain(p.get("ID"))
    count = plain(p.get("장수"))
    return {
        "page": page["id"],
        "edited": page["last_edited_time"],
        "id": int(num) if isinstance(num, (int, float)) else None,
        "name": plain(p.get("Name")),
        "type": plain(p.get("Type")),
        "target": plain(p.get("대상")),
        "count": int(count) if isinstance(count, (int, float)) else None,
        "grade": plain(p.get("등급")),
        "state": plain(p.get("상태")),
        "text": plain(p.get("카드 문구")),
        "version": plain(p.get("바뀐 버전")),
    }


def block_text(b):
    t = b["type"]
    v = b.get(t, {})
    if t == "table_row":
        return " | ".join("".join(x["plain_text"] for x in cell) for cell in v.get("cells", []))
    return "".join(x["plain_text"] for x in v.get("rich_text", []))


def flatten(blocks, depth=0):
    for b in blocks:
        yield depth, b
        yield from flatten(b.get("_children", []), depth + 1)


def rule_text(blocks):
    """규칙 절(RULE_HEADINGS)의 글만 모은다. 그런 절이 없으면 본문 전체."""
    lines, on, found = [], False, False
    for depth, b in flatten(blocks):
        if depth == 0 and b["type"] in ("heading_1", "heading_2"):
            title = block_text(b).strip()
            on = title.startswith(RULE_HEADINGS)
            found = found or on
            continue
        if on:
            lines.append(block_text(b).strip())
    if not found:
        lines = [block_text(b).strip() for _, b in flatten(blocks)]
    return "\n".join(x for x in lines if x)


# ── 변경 판단 ─────────────────────────────────────────────────────────

NUM = re.compile(r"\d+")

# 패치 노트 종류 — 같은 카드의 예정 줄을 합칠 때 더 큰 쪽으로만 바꾼다 (상향 · 하향 = 사람이 고른 수치)
WEIGHT = {"문구": 1, "버그": 1, "수치": 2, "상향": 2, "하향": 2, "리워크": 3, "신규": 4, "삭제": 5}


def classify(old, new):
    """확정 카드 두 상태를 비교해 (종류, 바뀐 것 목록) — 아무것도 안 바뀌었으면 (None, [])."""
    fields = [k for k in ("name", "text", "count", "grade", "target", "type", "rules") if old.get(k) != new.get(k)]
    if not fields:
        return None, []
    if old["target"] != new["target"] or old["type"] != new["type"]:
        return "리워크", fields
    same_shape = (NUM.sub("#", old["text"]) == NUM.sub("#", new["text"])
                  and NUM.sub("#", old.get("rules", "")) == NUM.sub("#", new.get("rules", "")))
    if not same_shape:
        if NUM.sub("#", old.get("rules", "")) != NUM.sub("#", new.get("rules", "")):
            return "리워크", fields
        return "문구", fields
    if any(k in fields for k in ("text", "rules", "count")):
        return "수치", fields
    return "문구", fields    # 이름 · 등급만 바뀜


def number_changes(old, new):
    """「49 → 45」 처럼 바뀐 숫자만 뽑는다. 숫자 개수가 다르면 빈 목록."""
    a, b = NUM.findall(old), NUM.findall(new)
    if len(a) != len(b):
        return []
    seen, out = set(), []
    for x, y in zip(a, b):
        if x != y and (x, y) not in seen:
            seen.add((x, y))
            out.append(f"{x} → {y}")
    return out


def summary(card, kind, old):
    if kind == "수치":
        nums = number_changes(old["text"] + " " + old.get("rules", ""), card["text"] + " " + card.get("rules", ""))
        if old["count"] != card["count"]:
            nums.append(f"장수 {old['count']} → {card['count']}")
        return f"{card['name']} " + (" · ".join(nums) if nums else "수치 변경")
    label = {"리워크": "능력 변경", "문구": "문구 변경", "신규": "새 카드", "삭제": "삭제"}[kind]
    return f"{card['name']} — {label}"


# 변경 기록에 남기는 칸 — 카드 하나 · 하루 한 줄 (출시 전후 · 상태 가리지 않음)
LOG_FIELDS = (("name", "이름"), ("state", "상태"), ("type", "종류"), ("target", "대상"), ("grade", "등급"),
              ("count", "장수"), ("text", "문구"), ("rules", "본문 규칙"))
KST = datetime.timezone(datetime.timedelta(hours=9))
NL = chr(10)

# 변경 기록 diff — 글을 숫자 · 띄어쓰기 · 나머지로 쪼개 비교한다 (「2장을」 → 「1장을」 에서 숫자만 칠함)
WORD = re.compile(r"\d+|\s+|[^\d\s]+")
CUT = {"strikethrough": True, "color": "red"}
ADD = {"bold": True, "color": "green"}


def field(c, k):
    return "" if c.get(k) is None else str(c[k])


def word_diff(a, b):
    """(종류, 글) 조각 — '=' 같음 · '-' 빠짐 · '+' 들어감."""
    a, b = WORD.findall(a), WORD.findall(b)
    out = []
    for op, i1, i2, j1, j2 in difflib.SequenceMatcher(None, a, b, autojunk=False).get_opcodes():
        if op == "equal":
            out.append(("=", "".join(a[i1:i2])))
            continue
        if i1 < i2:
            out.append(("-", "".join(a[i1:i2])))
        if j1 < j2:
            out.append(("+", "".join(b[j1:j2])))
    return out


def line_changes(a, b):
    """바뀐 줄만 — (빠진 줄들, 들어간 줄들) 목록. 빈 줄은 뺀다 (값이 비었다가 생긴 칸)."""
    a, b = a.split(NL), b.split(NL)
    out = []
    for op, i1, i2, j1, j2 in difflib.SequenceMatcher(None, a, b, autojunk=False).get_opcodes():
        cut, add = [x for x in a[i1:i2] if x], [x for x in b[j1:j2] if x]
        if op != "equal" and (cut or add):
            out.append((cut, add))
    return out


def marked(old, new, diff):
    """「바뀐 부분」 칸 — 바뀐 줄만. 빠진 글 빨강 취소선, 들어간 글 초록 굵게.
    한 줄이 한 줄로 조금 바뀌면(60% 이상 같음) 「~」 줄에 단어만 칠하고, 새로 쓴 줄은 「−」 · 「+」 줄째 칠한다."""
    out = []

    def put(t, ann=None):
        item = {"type": "text", "text": {"content": t[:2000]}}
        if ann:
            item["annotations"] = ann
        out.append(item)

    for k, label in diff:
        put((NL if out else "") + f"[{label}]")
        for cut, add in line_changes(field(old, k), field(new, k)):
            if len(cut) == len(add) == 1 and difflib.SequenceMatcher(None, cut[0], add[0], autojunk=False).ratio() >= 0.6:
                put(NL + "~ ")
                for op, t in word_diff(cut[0], add[0]):
                    put(t, None if op == "=" else CUT if op == "-" else ADD)
            else:
                for line in cut:
                    put(NL + "− " + line, CUT)
                for line in add:
                    put(NL + "+ " + line, ADD)
    return out[:100]   # 노션 한 칸 조각 제한


def git_diff(old, new, diff):
    """행 본문 코드 블록 — git diff 꼴 (바뀐 줄 앞뒤 1줄, 떨어진 곳은 …)."""
    lines = []
    for k, label in diff:
        body = list(difflib.unified_diff(field(old, k).split(NL), field(new, k).split(NL), n=1, lineterm=""))[3:]
        lines += [f"@@ {label} @@"] + [" …" if l.startswith("@@") else l for l in body]
    return {"type": "code", "code": {"language": "diff", "rich_text": rt(NL.join(lines))}}


def describe(c):
    s = c["text"]
    if c.get("count") is not None:
        s += f"  (장수 {c['count']} · {c['grade'] or '등급 없음'} · {c['target'] or '대상 없음'})"
    return s


# ── 옛 버전 복사용 블록 변환 ──────────────────────────────────────────

TEXT_BLOCKS = ("paragraph", "heading_1", "heading_2", "heading_3", "bulleted_list_item",
               "numbered_list_item", "to_do", "toggle", "quote", "callout", "code")


def clean_rt(items):
    out = []
    for x in items:
        link = x.get("href")
        out.append({"type": "text", "annotations": x.get("annotations", {}),
                    "text": {"content": x["plain_text"], "link": {"url": link} if link and link.startswith("http") else None}})
    return out


def writable(b, depth=0):
    """읽은 블록을 새 페이지에 쓸 수 있는 꼴로. 못 옮기는 블록은 안내 한 줄로 바꾼다."""
    t, v = b["type"], b.get(b["type"], {})
    kids = b.get("_children", [])
    if t in TEXT_BLOCKS:
        out = {"rich_text": clean_rt(v.get("rich_text", []))}
        for k in ("color", "checked", "language", "is_toggleable"):
            if k in v:
                out[k] = v[k]
        if t == "callout" and v.get("icon", {}).get("type") == "emoji":
            out["icon"] = v["icon"]
    elif t == "divider":
        out = {}
    elif t == "table":
        out = {"table_width": v["table_width"], "has_column_header": v.get("has_column_header", False),
               "has_row_header": v.get("has_row_header", False),
               "children": [{"type": "table_row", "table_row": {"cells": [clean_rt(c) for c in r["table_row"]["cells"]]}}
                            for r in kids]}
        return {"type": "table", "table": out}
    else:
        note = {"image": "(그림 — 원래 카드 페이지 참고)", "child_database": "(표 데이터베이스 — 원래 카드 페이지 참고)"}
        return {"type": "paragraph", "paragraph": {"rich_text": rt(note.get(t, f"(옮기지 못한 블록: {t})"))}}
    if kids and depth < 1:   # 노션 API 는 한 번에 2단계까지만 받는다
        out["children"] = [writable(k, depth + 1) for k in kids]
    return {"type": t, t: out}


# ── 저장소 (cards.json · 11-card-list 1-1) ────────────────────────────

def sync_repo(repo, cards, reworked=(), write=True):
    """확정 카드의 이름 · 문구 · 장수 · 등급 · 종류를 cards.json 과 11-card-list 에 맞춘다.
    cards.json 에 이미 있는 카드만 고친다 — 새 카드는 효과를 만들어야 하므로 사람이 넣는다.
    돌려주는 값: 바뀐 카드 줄 목록 (PR 본문용)."""
    path = os.path.join(repo, CARDS_JSON)
    raw = open(path, encoding="utf-8").read()
    data = json.loads(raw)
    by_id = {c["id"]: c for c in cards.values() if c["state"] == "확정" and c["id"] is not None}
    changed = []
    for e in data["cards"]:
        c = by_id.get(e["id"])
        if not c:
            continue
        want = {"name": c["name"], "text": c["text"], "count": c["count"],
                "grade": GRADE_ENUM.get(c["grade"], e["grade"]), "type": c["type"] or e["type"]}
        diff = {k: (e[k], v) for k, v in want.items() if v is not None and e.get(k) != v}
        if not diff:
            continue
        # 줄 모양(한 카드 = 몇 줄)을 지키려고 문자열로 고친다
        start = raw.index(f'"id": {e["id"]},')
        end = raw.index("}", raw.index('"effects"', start))
        block = raw[start:end]
        for k, (_, v) in diff.items():
            val = json.dumps(v, ensure_ascii=False)
            block = re.sub(rf'"{k}": (?:"(?:[^"\\]|\\.)*"|\d+)', lambda m: f'"{k}": {val}', block, count=1)
        raw = raw[:start] + block + raw[end:]
        warn = " ⚠ 능력 변경 — 효과(에셋)도 고쳐야 함" if e["id"] in reworked else ""
        changed.append(f"- {e['id']} {c['name']}{warn}: " + " · ".join(f"{k} {a!r} → {b!r}" for k, (a, b) in diff.items()))
        if write:
            update_card_list(repo, e["id"], c)
    if changed and write:
        json.loads(raw)   # 깨지지 않았는지
        open(path, "w", encoding="utf-8", newline="").write(raw)
    return changed


def update_card_list(repo, card_id, c):
    """11-card-list 1-1 요약 표의 그 카드 줄(이름 · 등급 · 장수 · 문구)과 1-2 제목."""
    path = os.path.join(repo, CARD_LIST_MD)
    text = open(path, encoding="utf-8").read()
    lines = text.split("\n")
    for i, line in enumerate(lines):
        if line.startswith(f"| {card_id} |"):
            cells = line.split(" | ")
            # | ID | 이름 | 종류 | 등급 | 장수 | 대상 | 카드 문구 | 그림 | IP 점검 |
            if len(cells) >= 9:
                cells[1], cells[3], cells[4], cells[6] = c["name"], c["grade"], str(c["count"]), c["text"]
                lines[i] = " | ".join(cells)
        elif re.match(rf"#### {card_id} ", line):
            lines[i] = f"#### {card_id} {c['name']}" + ("\r" if line.endswith("\r") else "")
    open(path, "w", encoding="utf-8", newline="").write("\n".join(lines))


# ── 본체 ─────────────────────────────────────────────────────────────

class Bot:
    def __init__(self, notion, repo):
        self.n = notion
        self.repo = repo
        self.log = []          # 마지막 실행 칸 · 콘솔에 남길 줄
        self.bot_user = None
        self.reworked = set()  # 능력이 바뀌었는데 저장소에 아직 안 들어간 카드 ID (PR 경고용, 상태 파일에 남는다)

    def say(self, line):
        print(line)
        self.log.append(line)

    # 출시됨 스위치 · 마지막 실행 칸
    def released(self):
        for b in self.n.children(BOT_PAGE):
            if b["type"] == "to_do" and block_text(b).strip().startswith("출시됨"):
                return b["to_do"]["checked"]
        raise RuntimeError("봇 페이지에 「출시됨」 체크 칸이 없다")

    def write_status(self, released):
        stamp = datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=9))).strftime("%Y-%m-%d %H:%M")
        body = f"마지막 실행: {stamp} (한국 시간) · {'출시 후' if released else '출시 전'}\n"
        body += "\n".join(self.log[-30:]) if self.log else "바뀐 카드 없음."
        for b in self.n.children(BOT_PAGE):
            if b["type"] == "callout":
                self.n.call("PATCH", f"/blocks/{b['id']}", {"callout": {"rich_text": rt(body)}})
                return

    def run(self, snap):
        released = self.released()
        prev = snap.get("cards", {})
        pages = self.n.query(CARD_DS)
        cards = {}
        for p in pages:
            c = read_card(p)
            old = prev.get(c["page"])
            if old and old["edited"] == c["edited"]:
                c["blocks"], c["rules"] = old["blocks"], old["rules"]
            else:
                c["blocks"] = self.n.children(c["page"])
                c["rules"] = rule_text(c["blocks"])
            cards[c["page"]] = c

        if not prev:
            self.say("첫 실행 — 오늘 카드 DB 를 기준으로만 저장했어요.")
            return {"cards": cards}, released, []

        self.bot_user = self.n.call("GET", "/users/me")["id"]
        self.reworked = set(snap.get("reworked", []))
        in_json = {e["id"] for e in json.load(open(os.path.join(self.repo, CARDS_JSON), encoding="utf-8"))["cards"]}
        self.fill_ids(cards)

        for c in cards.values():
            self.record(prev.get(c["page"]), c)
        for page_id, old in prev.items():
            if page_id not in cards:
                self.record(old, None)

        for c in cards.values():
            old = prev.get(c["page"])
            if not old:
                self.on_new(c, released, in_json)
                continue
            was, now = old["state"] == "확정", c["state"] == "확정"
            if not was and now:
                self.on_confirmed(c, released, in_json)
            elif was and not now:
                self.on_removed(c, old, released, in_json)
            elif was and now:
                kind, fields = classify(old, c)
                if kind:
                    self.on_changed(c, old, kind, fields, released, in_json)

        changed = sync_repo(self.repo, cards, self.reworked, write=not self.n.dry_run)
        for line in changed:
            self.say("저장소 " + line[2:])
        # 저장소와 같아진 카드는 경고에서 뺀다 (PR 이 머지됨)
        pending = {int(l.split()[1]) for l in changed}
        return {"cards": cards, "reworked": sorted(self.reworked & pending)}, released, changed

    # 🧾 카드 변경 기록 — 바뀐 칸만 이전 · 이후(바뀐 줄) · 바뀐 부분(줄 diff, 조금 바뀐 줄은 단어) · 본문(git diff)
    def record(self, old, new):
        extra, blocks = {}, []
        if old is None:
            kind, labels = "추가", []
            before, after = "", NL.join(f"{label}: {new[k]}" for k, label in LOG_FIELDS if new.get(k) not in (None, ""))
        elif new is None:
            kind, labels = "삭제", []
            before, after = f"이름: {old['name']}{NL}문구: {old['text']}", "(노션에서 페이지가 사라짐 — 휴지통 확인)"
        else:
            diff = [(k, label) for k, label in LOG_FIELDS if old.get(k) != new.get(k)]
            if not diff:
                return
            labels = [label for _, label in diff]
            kind = "삭제" if new["state"] == "삭제" else ("상태" if labels == ["상태"] else "수정")
            # 바뀐 줄만 — 본문 규칙 전체를 붙이면 칸이 길어져 무엇이 바뀌었는지 안 보인다
            ch = {k: line_changes(field(old, k), field(new, k)) for k, _ in diff}
            before = NL.join(f"{label}: " + (NL.join(x for c, _ in ch[k] for x in c) or "(없음)") for k, label in diff)
            after = NL.join(f"{label}: " + (NL.join(x for _, a in ch[k] for x in a) or "(없음)") for k, label in diff)
            extra = {"바뀐 부분": {"rich_text": marked(old, new, diff)}}
            blocks = [git_diff(old, new, diff)]
        card = new or old
        title = f"{card['name'] or '(이름 없음)'} — {kind}" + (f" ({' · '.join(labels)})" if kind == "수정" else "")
        props = {"변경": {"title": rt(title)}, "종류": {"select": {"name": kind}},
                 "날짜": {"date": {"start": datetime.datetime.now(KST).date().isoformat()}},
                 "바뀐 칸": {"multi_select": [{"name": x} for x in labels]},
                 "이전": {"rich_text": rt(before)[:100]}, "이후": {"rich_text": rt(after)[:100]}}
        if new is not None:
            props["카드"] = {"relation": [{"id": new["page"]}]}
        props.update(extra)
        self.n.create_page(LOG_DS, props, blocks)
        self.say(f"기록: {title}")

    def fill_ids(self, cards):
        for c in cards.values():
            if c["id"] is not None or c["state"] == "삭제" or c["type"] not in TYPE_PREFIX:
                continue
            base = TYPE_PREFIX[c["type"]]
            used = [x["id"] for x in cards.values() if x["id"] is not None and base <= x["id"] < base + 1000]
            c["id"] = max(used, default=base) + 1
            self.n.update_page(c["page"], {"ID": {"number": c["id"]}})
            self.say(f"ID 채움: {c['name']} → {c['id']}")

    def missing(self, c):
        keys = {"Name": "name", "Type": "type", "대상": "target", "장수": "count", "카드 문구": "text", "등급": "grade"}
        return [k for k in REQUIRED if c[keys[k]] in (None, "")]

    def on_new(self, c, released, in_json):
        miss = self.missing(c)
        if miss and c["state"] != "삭제":
            self.n.comment(c["page"], "빈 칸이 있어요: " + " · ".join(miss) + ". 확정 전에 채워 주세요. — 카드 정리 봇")
            self.say(f"새 카드 {c['name'] or '(이름 없음)'}: 빈 칸 {', '.join(miss)}")
        else:
            self.say(f"새 카드 {c['name']} ({c['state']})")
        if c["state"] == "확정":
            self.on_confirmed(c, released, in_json)

    def on_confirmed(self, c, released, in_json):
        self.say(f"확정: {c['name']}")
        self.sync_i18n(c)
        if c["id"] not in in_json:
            self.n.comment(c["page"], "새로 확정됐어요. 개발 확인 필요: 효과를 만들고 cards.json 에 넣어야 게임에 들어가요. — 카드 정리 봇")
        if released:
            self.patch_note(c, "신규", None, "", describe(c), check=False)

    def on_removed(self, c, old, released, in_json):
        self.say(f"확정에서 빠짐: {c['name']} → {c['state']}")
        if c["id"] in in_json:
            self.n.comment(c["page"], f"「{c['state']}」 로 바뀌었어요. 개발 확인 필요: 게임 덱 · cards.json 에서 빼야 해요. — 카드 정리 봇")
        if c["state"] == "삭제":
            self.mark_i18n_deleted(c)
        if released:
            self.patch_note(c, "삭제", old, describe(old), "(게임에서 빠짐)", check=c["state"] != "삭제")

    def on_changed(self, c, old, kind, fields, released, in_json):
        self.say(f"바뀜: {c['name']} — {kind} ({', '.join(fields)})")
        if "name" in fields or "text" in fields:
            self.sync_i18n(c)
        if kind == "리워크":
            self.reworked.add(c["id"])
        if kind == "리워크" and c["id"] in in_json:
            self.n.comment(c["page"], "능력이 바뀌었어요. 개발 확인 필요: 효과 코드 · 카드별 봇 규칙 · 이펙트를 다시 봐야 해요. — 카드 정리 봇")
        if released:
            old_page = self.save_old_version(old) if kind == "리워크" else None
            # 이름이 바뀌면 「다른 카드가 된 것」 일 수 있다 → 사람이 본다
            self.patch_note(c, kind, old, describe(old), describe(c), check="name" in fields, old_page=old_page)

    # 패치 노트 — 같은 카드에 봇이 쓴 「예정」 줄이 있으면 새 줄 대신 그 줄을 고친다
    # (빌드 전에 여러 번 고쳐도 한 줄: 이전 = 처음 값, 이후 = 지금 값)
    def patch_note(self, c, kind, old, before, after, check, old_page=None):
        rows = self.n.query(PATCH_DS, {"and": [
            {"property": "카드", "relation": {"contains": c["page"]}},
            {"property": "상태", "select": {"equals": "예정"}}]})
        mine = [r for r in rows if r["created_by"]["id"] == self.bot_user]
        props = {"카드": {"relation": [{"id": c["page"]}]}, "상태": {"select": {"name": "예정"}},
                 "이후": {"rich_text": rt(after)}}
        if old_page:
            props["옛 버전"] = {"relation": [{"id": old_page}]}
        if mine:
            row = mine[0]
            p = row["properties"]
            prev_kind = plain(p.get("종류")) or "수치"
            # 더 큰 변경일 때만 종류를 바꾼다 — 사람이 고른 상향 · 하향은 수치 변경이 또 와도 둔다
            if WEIGHT[kind] > WEIGHT.get(prev_kind, 0):
                props.update(self.kind_props(kind, check or plain(p.get("확인 필요"))))
                label = {"수치": "수치 변경"}.get(kind) or summary(c, kind, old).split(" — ")[-1]
                props["변경"] = {"title": rt(f"{c['name']} — {label}")}
            self.n.update_page(row["id"], props)
            self.say(f"  패치 노트 예정 줄 고침: {c['name']}")
            return
        props.update(self.kind_props(kind, check))
        props["변경"] = {"title": rt(summary(c, kind, old or c))}
        props["이전"] = {"rich_text": rt(before)}
        self.n.create_page(PATCH_DS, props)
        self.say(f"  패치 노트 예정 줄 추가: {c['name']} ({kind})")

    @staticmethod
    def kind_props(kind, check):
        # 「수치」 는 상향인지 하향인지 봇이 모른다 → 종류를 비우고 확인 필요
        if kind == "수치":
            return {"종류": {"select": None}, "확인 필요": {"checkbox": True}}
        return {"종류": {"select": {"name": kind}}, "확인 필요": {"checkbox": bool(check)}}

    def save_old_version(self, old):
        page = self.n.create_page(OLD_DS, {
            "Name": {"title": rt(old["name"])},
            "카드 문구": {"rich_text": rt(old["text"])},
            "쓰인 버전": {"rich_text": rt(f"{old['version'] or '?'} ~")},
            "원래 카드": {"relation": [{"id": old["page"]}]},
        }, [writable(b) for b in old["blocks"]])
        self.say(f"  옛 버전 보관: {old['name']}")
        return page["id"]

    # 다국어 — card.<ID>.name / card.<ID>.text
    def sync_i18n(self, c):
        if c["id"] is None:
            return
        for suffix, kind, value in (("name", "카드 이름", c["name"]), ("text", "카드 문구", c["text"])):
            key = f"card.{c['id']}.{suffix}"
            rows = self.n.query(I18N_DS, {"property": "키", "title": {"equals": key}})
            if not rows:
                self.n.create_page(I18N_DS, {
                    "키": {"title": rt(key)}, "한국어": {"rich_text": rt(value)},
                    "상태": {"select": {"name": "원본만"}}, "화면": {"select": {"name": "카드"}},
                    "종류": {"select": {"name": kind}}, "카드": {"relation": [{"id": c["page"]}]}})
                self.say(f"  다국어 줄 추가: {key}")
                continue
            row = rows[0]
            if plain(row["properties"]["한국어"]) == value:
                continue
            props = {"한국어": {"rich_text": rt(value)}}
            if plain(row["properties"]["상태"]) in ("초벌 번역", "검수 완료"):
                props["상태"] = {"select": {"name": "원본 바뀜"}}
            self.n.update_page(row["id"], props)
            self.say(f"  다국어 원본 바뀜: {key}")

    def mark_i18n_deleted(self, c):
        if c["id"] is None:
            return
        for suffix in ("name", "text"):
            for row in self.n.query(I18N_DS, {"property": "키", "title": {"equals": f"card.{c['id']}.{suffix}"}}):
                memo = plain(row["properties"]["의도 · 메모"])
                if not memo.startswith("[삭제된 카드]"):
                    self.n.update_page(row["id"], {"의도 · 메모": {"rich_text": rt(("[삭제된 카드] " + memo).strip())}})


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--snapshot", required=True, help="어제 상태 파일 (없으면 첫 실행)")
    ap.add_argument("--repo", required=True, help="저장소 루트 (develop 체크아웃)")
    ap.add_argument("--pr-body", help="저장소가 바뀌면 PR 본문을 여기에 쓴다")
    ap.add_argument("--dry-run", action="store_true", help="노션에 쓰지 않고 할 일만 보여 준다")
    args = ap.parse_args()

    token = os.environ.get("NOTION_TOKEN")
    if not token:
        sys.exit("NOTION_TOKEN 이 없다")
    snap = json.load(open(args.snapshot, encoding="utf-8")) if os.path.exists(args.snapshot) else {}

    notion = Notion(token, args.dry_run)
    bot = Bot(notion, args.repo)
    new_snap, released, changed = bot.run(snap)

    if args.dry_run:
        print(f"\n[dry-run] 노션에 쓰려던 것 {len(notion.writes)}건:")
        for method, path, body in notion.writes:
            print(" ", method, path, json.dumps(body, ensure_ascii=False)[:300])
        return
    bot.write_status(released)
    json.dump(new_snap, open(args.snapshot, "w", encoding="utf-8"), ensure_ascii=False)
    if changed and args.pr_body:
        open(args.pr_body, "w", encoding="utf-8").write(
            "노션 카드 DB 의 확정 카드에 맞춰 `cards.json` · `11-card-list` 1-1 을 고쳤어요 (카드 정리 봇).\n\n"
            + "\n".join(changed)
            + "\n\n머지한 뒤 Unity 에서 `Chapchu/카드 가져오기` 를 한 번 돌려 카드 에셋에 반영해 주세요. "
              "1-2 카드별 규칙 글은 봇이 고치지 않아요 — 문구 · 수치가 본문에도 적혀 있으면 함께 봐 주세요.\n")


if __name__ == "__main__":
    main()
