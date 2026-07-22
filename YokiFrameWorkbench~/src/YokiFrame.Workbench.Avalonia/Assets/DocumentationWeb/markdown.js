// ═══════════════════════════════════════════════════════════════════
// YokiMarkdown — 零依赖离线 Markdown 渲染器 + 轻量代码着色器
// 设计：编辑器工具内置文档，禁外部 CDN。覆盖 YokiFrame 文档实际所需子集：
//   标题 / 段落 / 列表 / 引用 / 表格 / 围栏代码(带语言着色+复制) /
//   行内代码 / 粗体斜体 / 链接 / 分隔线 / 锚点。
// 暴露：window.YokiMarkdown.render(mdText) -> htmlString
//      window.YokiMarkdown.renderWithHeadings(mdText) -> { html, headings:[{level,text,id}] }  // 返回带标题列表的结果
//      window.YokiMarkdown.bindCopyButtons(rootEl)  // 渲染后绑定复制按钮
// ═══════════════════════════════════════════════════════════════════
(function () {
    'use strict';

    // ── HTML 转义（所有用户文本进 DOM 前必经） ──
    function esc(s) {
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    // ═══════════════════════════════════════════════════════════════
    // 代码着色：基于已转义文本做正则分层包裹。
    // 顺序很关键：先注释/字符串占位，避免关键字命中字符串内部。
    // ═══════════════════════════════════════════════════════════════
    const KEYWORDS = {
        csharp: ['public','private','protected','internal','static','readonly','const','class','struct','interface','enum','void','string','int','float','double','bool','var','new','return','if','else','for','foreach','while','switch','case','break','continue','default','using','namespace','this','base','null','true','false','async','await','override','virtual','abstract','sealed','partial','get','set','in','out','ref','is','as','typeof','nameof','throw','try','catch','finally','yield','params','where','Func','Action','Task','Dictionary','List'],
        rust: ['fn','let','mut','pub','use','mod','struct','enum','impl','trait','for','while','loop','if','else','match','return','self','Self','crate','super','async','await','move','ref','as','const','static','type','where','dyn','unsafe','true','false','Some','None','Ok','Err','Option','Result','Vec','String','str','u16','u32','i32','usize','isize','bool'],
        json: [],
        bash: ['cd','echo','export','if','then','fi','for','do','done','while','function','return','source','local','sudo','cargo','dotnet','git','npm','rg','grep'],
        js: ['const','let','var','function','return','if','else','for','while','switch','case','break','continue','new','class','extends','async','await','this','null','undefined','true','false','typeof','instanceof','try','catch','finally','throw','import','export','from','default','of','in','=>'],
    };

    const LANG_ALIAS = { cs: 'csharp', 'c#': 'csharp', rs: 'rust', shell: 'bash', sh: 'bash', javascript: 'js', ts: 'js', typescript: 'js' };
    const LANG_LABELS = { csharp: 'C#', rust: 'Rust', json: 'JSON', bash: 'Shell', js: 'JavaScript' };

    function formatLanguageLabel(lang) {
        const normalized = LANG_ALIAS[lang] || lang;
        return LANG_LABELS[normalized] || lang || '';
    }

    function highlight(code, lang) {
        const escaped = esc(code);
        lang = LANG_ALIAS[lang] || lang;
        const kw = KEYWORDS[lang];
        if (!kw) return escaped; // 未知语言：仅转义，不着色

        // 用占位符保护字符串与注释，避免内部内容被二次着色
        const slots = [];
        function slotMarker(index) { return String.fromCharCode(0xE000 + index); }
        function stash(html) {
            const marker = slotMarker(slots.length);
            slots.push(html);
            return marker;
        }

        function stashToken(className, value) {
            return stash(`<span class="${className}">${value}</span>`);
        }

        let out = escaped;

        // 1) 块注释 /* */（csharp/rust/js）
        if (lang !== 'bash' && lang !== 'json') {
            out = out.replace(/\/\*[\s\S]*?\*\//g, m => stash(`<span class="tok-comment">${m}</span>`));
            // 2) 行注释 //
            out = out.replace(/\/\/[^\n]*/g, m => stash(`<span class="tok-comment">${m}</span>`));
        }
        if (lang === 'bash') {
            out = out.replace(/#[^\n]*/g, m => stash(`<span class="tok-comment">${m}</span>`));
        }

        // 3) 字符串 "..." 与 '...'（已转义引号是 &quot; / '）
        out = out.replace(/&quot;(?:[^&]|&(?!quot;))*?&quot;/g, m => stash(`<span class="tok-string">${m}</span>`));
        out = out.replace(/'(?:[^'\\]|\\.)*?'/g, m => stash(`<span class="tok-string">${m}</span>`));

        // 4) JSON 的 key（"key": ）已被字符串占位覆盖，跳过

        // 5) C# 类型名与方法名
        if (lang === 'csharp') {
            out = out.replace(/\b([A-Za-z_][A-Za-z0-9_]*)(?=\s*\()/g, m => {
                if (kw && kw.indexOf(m) >= 0) return m;
                return stashToken('tok-method', m);
            });
            out = out.replace(/\b([A-Z][A-Za-z0-9_]*(?:&lt;[^;\n{}()]*?&gt;)?)\b/g, m =>
                stashToken('tok-type', m));
        }

        // 6) 关键字
        if (kw.length) {
            const re = new RegExp('\\b(' + kw.map(k => k.replace(/[+]/g, '\\$&')).join('|') + ')\\b', 'g');
            out = out.replace(re, m => stashToken('tok-keyword', m));
        }

        // 7) 数字
        out = out.replace(/\b(0x[0-9a-fA-F]+|\d+\.?\d*(?:f|u16|u32|i32|usize)?)\b/g, m =>
            stashToken('tok-number', m));

        // 还原占位。占位符用私有区字符，避免被数字/关键字规则二次命中。
        for (let slotIndex = 0; slotIndex < slots.length; slotIndex++) {
            out = out.split(slotMarker(slotIndex)).join(slots[slotIndex]);
        }
        return out;
    }

    // ═══════════════════════════════════════════════════════════════
    // 行内格式：粗体 / 斜体 / 行内代码 / 链接。在已转义文本上操作。
    // ═══════════════════════════════════════════════════════════════
    function inline(text) {
        // 行内代码先行，避免内部 * _ 被误解析
        const codes = [];
        let t = String(text).replace(/`([^`]+)`/g, (_, c) => {
            codes.push(esc(c));
            return `\uE100${codes.length - 1}\uE101`;
        });
        t = esc(t);
        // 链接 [text](url)
        t = t.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (_, label, url) =>
            `<a class="md-link" href="${url}" data-href="${url}">${label}</a>`);
        // 粗体 **x**
        t = t.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
        // 斜体 *x*（避免吞掉 **）
        t = t.replace(/(^|[^*])\*([^*\n]+)\*(?!\*)/g, '$1<em>$2</em>');
        // 还原行内代码
        t = t.replace(/\uE100(\d+)\uE101/g, (_, i) => `<code class="md-code-inline">${codes[+i]}</code>`);
        return t;
    }

    function slugify(s) {
        return s.toLowerCase().trim()
            .replace(/[^\w一-龥]+/g, '-')
            .replace(/^-+|-+$/g, '');
    }

    // ═══════════════════════════════════════════════════════════════
    // 块级解析：逐行状态机。
    // ═══════════════════════════════════════════════════════════════
    // 渲染并回传标题列表（供文档 TOC 使用）。
    // 返回 { html, headings:[{level,text,id}] }。render() 是其向后兼容包装。
    function renderWithHeadings(md) {
        const lines = String(md).replace(/\r\n/g, '\n').split('\n');
        const html = [];
        const headings = [];
        let i = 0;
        let cbId = 0;

        while (i < lines.length) {
            let line = lines[i];

            // 围栏代码 ```lang
            const fence = line.match(/^```([\w#+-]*)\s*$/);
            if (fence) {
                const lang = (fence[1] || '').toLowerCase();
                const buf = [];
                i++;
                while (i < lines.length && !/^```\s*$/.test(lines[i])) {
                    buf.push(lines[i]);
                    i++;
                }
                i++; // 跳过收尾 ```
                const raw = buf.join('\n');
                const id = `cb-${cbId++}`;
                const langLabel = lang ? `<span class="md-code-lang">${esc(formatLanguageLabel(lang))}</span>` : '';
                html.push(
                    `<div class="md-codeblock">` +
                    `<div class="md-code-header">${langLabel}` +
                    `<button class="md-copy-btn" data-copy-target="${id}">Copy</button></div>` +
                    `<pre class="md-pre"><code id="${id}" class="md-code">${highlight(raw, lang)}</code></pre>` +
                    `</div>`);
                continue;
            }

            // 标题
            const h = line.match(/^(#{1,4})\s+(.*)$/);
            if (h) {
                const level = h[1].length;
                const txt = h[2].trim();
                const id = slugify(txt);
                headings.push({ level, text: txt, id });
                html.push(`<h${level} class="md-h md-h${level}" id="${id}">${inline(txt)}</h${level}>`);
                i++;
                continue;
            }

            // 分隔线
            if (/^(---|\*\*\*|___)\s*$/.test(line)) {
                html.push('<hr class="md-hr">');
                i++;
                continue;
            }

            // 引用
            if (/^>\s?/.test(line)) {
                const buf = [];
                while (i < lines.length && /^>\s?/.test(lines[i])) {
                    buf.push(lines[i].replace(/^>\s?/, ''));
                    i++;
                }
                html.push(`<blockquote class="md-quote">${inline(buf.join(' '))}</blockquote>`);
                continue;
            }

            // 表格（| a | b | 后跟 |---|---|）
            if (/^\|.*\|\s*$/.test(line) && i + 1 < lines.length && /^\|[\s:|-]+\|\s*$/.test(lines[i + 1])) {
                const head = splitRow(line);
                i += 2; // 跳过表头与分隔行
                const bodyRows = [];
                while (i < lines.length && /^\|.*\|\s*$/.test(lines[i])) {
                    bodyRows.push(splitRow(lines[i]));
                    i++;
                }
                const thead = '<tr>' + head.map(c => `<th>${inline(c)}</th>`).join('') + '</tr>';
                const tbody = bodyRows.map(r =>
                    '<tr>' + r.map(c => `<td>${inline(c)}</td>`).join('') + '</tr>').join('');
                html.push(`<table class="md-table"><thead>${thead}</thead><tbody>${tbody}</tbody></table>`);
                continue;
            }

            // 无序列表
            if (/^\s*[-*+]\s+/.test(line)) {
                const buf = [];
                while (i < lines.length && /^\s*[-*+]\s+/.test(lines[i])) {
                    buf.push(lines[i].replace(/^\s*[-*+]\s+/, ''));
                    i++;
                }
                html.push('<ul class="md-list">' + buf.map(b => `<li>${inline(b)}</li>`).join('') + '</ul>');
                continue;
            }

            // 有序列表
            if (/^\s*\d+\.\s+/.test(line)) {
                const buf = [];
                while (i < lines.length && /^\s*\d+\.\s+/.test(lines[i])) {
                    buf.push(lines[i].replace(/^\s*\d+\.\s+/, ''));
                    i++;
                }
                html.push('<ol class="md-list md-list--ol">' + buf.map(b => `<li>${inline(b)}</li>`).join('') + '</ol>');
                continue;
            }

            // 空行
            if (/^\s*$/.test(line)) {
                i++;
                continue;
            }

            // 段落（连续非空行合并）
            const buf = [line];
            i++;
            while (i < lines.length && !/^\s*$/.test(lines[i]) &&
                   !/^(#{1,4})\s/.test(lines[i]) && !/^```/.test(lines[i]) &&
                   !/^>\s?/.test(lines[i]) && !/^\s*[-*+]\s+/.test(lines[i]) &&
                   !/^\s*\d+\.\s+/.test(lines[i]) && !/^\|.*\|\s*$/.test(lines[i])) {
                buf.push(lines[i]);
                i++;
            }
            html.push(`<p class="md-p">${inline(buf.join(' '))}</p>`);
        }

        return { html: html.join('\n'), headings };
    }

    // 向后兼容包装：旧调用点只要 html 字符串。
    function render(md) {
        return renderWithHeadings(md).html;
    }

    function splitRow(line) {
        return line.replace(/^\||\|\s*$/g, '').split('|').map(c => c.trim());
    }

    // ═══════════════════════════════════════════════════════════════
    // 复制按钮绑定：渲染后调用，挂到 root 内所有 .md-copy-btn。
    // 优先 Clipboard API，回退 execCommand（Tauri webview 兼容）。
    // ═══════════════════════════════════════════════════════════════
    function bindCopyButtons(root) {
        if (!root) return;
        root.querySelectorAll('.md-copy-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                const target = root.querySelector('#' + btn.dataset.copyTarget);
                if (!target) return;
                const text = target.textContent;
                copyText(text).then(ok => {
                    const prev = btn.textContent;
                    btn.textContent = ok ? 'Copied' : 'Failed';
                    btn.classList.toggle('md-copy-btn--ok', ok);
                    setTimeout(() => {
                        btn.textContent = prev;
                        btn.classList.remove('md-copy-btn--ok');
                    }, 1200);
                });
            });
        });
    }

    function copyText(text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            return navigator.clipboard.writeText(text).then(() => true).catch(() => fallbackCopy(text));
        }
        return Promise.resolve(fallbackCopy(text));
    }

    function fallbackCopy(text) {
        try {
            const ta = document.createElement('textarea');
            ta.value = text;
            ta.style.position = 'fixed';
            ta.style.opacity = '0';
            document.body.appendChild(ta);
            ta.select();
            const ok = document.execCommand('copy');
            document.body.removeChild(ta);
            return ok;
        } catch (_) {
            return false;
        }
    }

    window.YokiMarkdown = { render, renderWithHeadings, bindCopyButtons, highlight };
})();
