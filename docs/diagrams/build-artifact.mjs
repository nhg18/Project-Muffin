// archify 가 만든 ui-flow.html 을 claude.ai 아티팩트용으로 변환한다.
//   node docs/diagrams/build-artifact.mjs <출력 경로>
// 아티팩트는 <html>/<head>/<body> 없이 본문만 받으므로 head · body 안쪽만 추리고,
// 한글 글꼴(Noto Sans KR)과 모바일 확대 규칙을 오버라이드로 덧붙인다. 원본 ui-flow.html 은 건드리지 않는다.
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const src = join(here, 'ui-flow.html');
const out = process.argv[2] || join(here, 'ui-flow.artifact.html');

const html = readFileSync(src, 'utf8');
const headStart = html.indexOf('<head>') + '<head>'.length;
const headEnd = html.indexOf('</head>');
const bodyTag = html.indexOf('<body');
const bodyStart = html.indexOf('>', bodyTag) + 1;
const bodyEnd = html.lastIndexOf('</body>');

let head = html.slice(headStart, headEnd);
head = head
  .replace(/<meta charset="UTF-8">\s*/, '')
  .replace(/<meta name="viewport"[^>]*>\s*/, '')
  .replace(/<title>[^<]*<\/title>\s*/, '');
const body = html.slice(bodyStart, bodyEnd);

const pre = `<title>찹츄 UI 흐름</title>
<script>document.documentElement.setAttribute("data-preset","classic");document.documentElement.setAttribute("lang","ko");</script>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Noto+Sans+KR:wght@400;500;700&display=swap">
`;

const override = `<style id="artifact-override">
  /* 아티팩트 판 전용 오버라이드.
     1) 한글 글꼴: archify 내장 JetBrains Mono 는 한글이 없어 시스템 글꼴로 떨어진다 → Noto Sans KR.
     2) 뷰어의 폭 제한(최대 1440px, 세로 기준 축소)을 풀고, 도식은 최소 1800px 로 그려 좁은 화면에서는 옆으로 스크롤한다.
     3) 좁은 화면: 기준 글자 크기를 키우고 툴바 · 안내 뷰 · 카드가 화면 안에서 줄바꿈되게 한다. */
  :root { --kr-font: 'Noto Sans KR', 'Apple SD Gothic Neo', 'Malgun Gothic', 'NanumGothic', sans-serif; }
  body, button, input, select, .container, .header, .cards, .guided-views, .toolbar { font-family: var(--kr-font) !important; }
  svg text, svg tspan { font-family: var(--kr-font) !important; font-weight: 500; }
  html { font-size: 18px; }
  .container { max-width: none !important; }
  body { padding-inline: 1rem; }
  .diagram-container { overflow-x: auto; -webkit-overflow-scrolling: touch; }
  .diagram-container > svg { width: max(100%, 1800px); min-width: 1800px; max-width: none; height: auto; }
  @media (max-width: 900px) {
    html { font-size: 20px; }
    body { padding-inline: 0.75rem; }
    .container, .header, .guided-views, .cards, .card, .diagram-container { max-width: 100%; min-width: 0; box-sizing: border-box; }
    .header { padding-right: 0 !important; }
    .header-row { flex-wrap: wrap; }
    .toolbar { position: static !important; width: 100% !important; max-width: none !important; flex-wrap: wrap; justify-content: flex-start; margin-bottom: 0.75rem; }
    .guided-views { grid-template-columns: minmax(0, 1fr) !important; }
    .guided-views ul, .guided-views ol { display: flex; flex-wrap: wrap; gap: 0.4rem; padding: 0; }
    .guided-view-chapter { flex: 1 1 100%; width: auto !important; min-width: 0; }
    .cards { grid-template-columns: 1fr; }
    .header h1, .header .title, .subtitle { white-space: normal !important; overflow: visible !important; text-overflow: clip !important; }
    .header-row > h1 { min-width: 0; flex: 1 1 auto; }
    .guided-views * { min-width: 0; }
    .guided-view-actions { flex-wrap: wrap; justify-content: flex-start; }
    .guided-view-actions button { white-space: normal !important; min-width: 0 !important; }
    .guided-views > *, .guided-views header, .guided-views nav { flex-wrap: wrap; }
    .card, .card * { overflow-wrap: anywhere; white-space: normal; }
  }
</style>
`;

writeFileSync(out, pre + head + '\n' + override + body);
console.log(`wrote ${out}`);
