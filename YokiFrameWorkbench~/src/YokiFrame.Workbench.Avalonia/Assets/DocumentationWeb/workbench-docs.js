(function () {
    'use strict';

    let catalog = { packageVersion: '', documents: [] };
    let activeDocument = null;
    let article = null;
    let toc = null;
    let tocObserver = null;

    document.addEventListener('contextmenu', event => {
        event.preventDefault();
    });

    // 文档树 SVG 是 Kit 图标的视觉基准；Avalonia 导航与 Unity PNG 保持同一造型。
    const icons = {
        framework: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><rect x="4.5" y="5.5" width="15" height="13" rx="2.5" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M9 18.5h6" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
        docs: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><path d="M7 5.5h10.5a2 2 0 0 1 2 2v11H9a2 2 0 0 0-2 2V7.5a2 2 0 0 1 2-2Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M9.5 9h6M9.5 12h6M9.5 15h4.5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
        architecture: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><rect x="5" y="5" width="14" height="14" rx="2.4" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M8.5 9h7M8.5 12h7M8.5 15h4.5" fill="none" stroke="currentColor" stroke-width="1.55" stroke-linecap="round"/><path d="M17.5 7.2 20 5M17.5 16.8 20 19M6.5 7.2 4 5M6.5 16.8 4 19" fill="none" stroke="currentColor" stroke-width="1.45" stroke-linecap="round" opacity="0.62"/></svg>',
        codegen: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><path d="m8.5 7-4.5 5 4.5 5M15.5 7l4.5 5-4.5 5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/><path d="m12 8.2 1.2 2.6 2.6 1.2-2.6 1.2-1.2 2.6-1.2-2.6-2.6-1.2 2.6-1.2Z" fill="none" stroke="currentColor" stroke-width="1.35" stroke-linejoin="round"/></svg>',
        inspector: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><rect x="5.5" y="5.5" width="13" height="13" rx="2.2" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M8 9h8M10.5 7.5v3M8 12h8M14 10.5v3M8 15h8M11.5 13.5v3" fill="none" stroke="currentColor" stroke-width="1.55" stroke-linecap="round"/></svg>',
        log: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><path d="M5 5.5h14v13H5z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="m8 9 2.5 2.5L8 14M12.5 14H16" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        toolclass: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><path d="M5 9h14v9.5H5zM9 9V7h6v2M5 12.5h14M10 15.5h4" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        fsm: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><circle cx="7" cy="7" r="2.2" fill="currentColor"/><circle cx="17" cy="7" r="2.2" fill="currentColor"/><circle cx="12" cy="17" r="2.2" fill="currentColor"/><path d="M8.9 8.5 10.8 15M15.1 8.5 13.2 15M9.2 7h5.6" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>',
        event: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><path d="M12 4.5v5.2" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/><path d="m8.8 10.2 3.2 8.3 3.2-8.3" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M6.5 8.2h2.4M15.1 8.2h2.4" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
        pool: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><path d="M6 7.5h12M7.5 12h9M9 16.5h6" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/><rect x="5" y="5" width="14" height="14" rx="3" fill="none" stroke="currentColor" stroke-width="1.4" opacity="0.55"/></svg>',
        res: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><path d="M5.5 8.5 12 5l6.5 3.5v7L12 19l-6.5-3.5v-7Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M5.5 8.5 12 12l6.5-3.5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/></svg>',
        singleton: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><circle cx="12" cy="8" r="3.2" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M6.5 19.2c.9-3 2.9-5 5.5-5s4.6 2 5.5 5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/><path d="M18.5 6.5h1.8M3.7 6.5h1.8M12 2.8v1.4" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" opacity="0.65"/></svg>',
        action: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><path d="M6 12h12" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/><path d="m13 8 4 4-4 4" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        localization: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><circle cx="12" cy="12" r="7.5" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M4.8 12h14.4M12 4.5c2 2.1 3 4.6 3 7.5s-1 5.4-3 7.5M12 4.5c-2 2.1-3 4.6-3 7.5s1 5.4 3 7.5" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>',
        ui: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><rect x="5" y="5.5" width="14" height="12.5" rx="2.4" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M8 9h8M8 12.2h4.2M8 15.4h7.2" fill="none" stroke="currentColor" stroke-width="1.55" stroke-linecap="round"/><path d="M9.5 18.5h5" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" opacity="0.65"/></svg>',
        audio: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><path d="M5 10.2h3.2L12.5 6v12l-4.3-4.2H5z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M15.5 9.2a4 4 0 0 1 0 5.6M17.9 6.8a7.4 7.4 0 0 1 0 10.4" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
        save: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><path d="M6.5 5.5h9.2L18.5 8v10.5h-13v-13Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M8.5 5.5v5h7v-5M8.5 18.5v-4.2h7v4.2" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/></svg>',
        scene: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><path d="M5.5 6.5h13v11h-13v-11Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M8.5 15.5 11 12l2 2.4 1.6-1.9 2.9 3" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/><circle cx="9" cy="9.2" r="1.1" fill="currentColor"/></svg>',
        spatial: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><path d="M5.5 7.5h13M5.5 12h13M5.5 16.5h13M7.5 5.5v13M12 5.5v13M16.5 5.5v13" fill="none" stroke="currentColor" stroke-width="1.35" stroke-linecap="round"/><circle cx="8.5" cy="8.5" r="1.3" fill="currentColor"/><circle cx="15.5" cy="13.2" r="1.3" fill="currentColor"/><circle cx="11.5" cy="16.2" r="1.3" fill="currentColor"/></svg>',
        table: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><rect x="5" y="5.5" width="14" height="13" rx="2.2" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M5 9.5h14M5 13.5h14M9.5 5.5v13M14.5 5.5v13" fill="none" stroke="currentColor" stroke-width="1.45" stroke-linecap="round"/></svg>',
        package: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><path d="M6 8.2 12 5l6 3.2v7.6L12 19l-6-3.2V8.2Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M6 8.2 12 11.5l6-3.3M12 11.5V19" fill="none" stroke="currentColor" stroke-width="1.55" stroke-linejoin="round"/><path d="m9.2 6.5 6 3.3" fill="none" stroke="currentColor" stroke-width="1.25" stroke-linecap="round" opacity="0.58"/></svg>',
        github: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><path d="M12 4.2a7.7 7.7 0 0 0-2.4 15c.4.1.6-.2.6-.5v-1.8c-2.4.5-2.9-1-2.9-1-.4-1-.9-1.3-.9-1.3-.8-.5.1-.5.1-.5.8.1 1.3.9 1.3.9.8 1.3 2 1 2.5.8.1-.6.3-1 .5-1.2-1.9-.2-3.9-1-3.9-4.2 0-.9.3-1.7.9-2.3-.1-.2-.4-1.1.1-2.3 0 0 .7-.2 2.4.9.7-.2 1.4-.3 2.1-.3s1.4.1 2.1.3c1.6-1.1 2.4-.9 2.4-.9.5 1.2.2 2.1.1 2.3.6.6.9 1.4.9 2.3 0 3.3-2 4-3.9 4.2.3.3.6.8.6 1.6v2.4c0 .3.2.6.7.5A7.7 7.7 0 0 0 12 4.2Z" fill="currentColor"/></svg>',
        bridge: '<svg viewBox="0 0 24 24" class="doc-nav-item-svg"><path d="m7 12 4-4 4 4" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/><path d="m7 16 4-4 4 4" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" opacity="0.55"/><path d="M15.5 8H11V3.5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>'
    };

    function esc(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function post(message) {
        const payload = JSON.stringify(message);
        if (typeof window.invokeCSharpAction === 'function') {
            window.invokeCSharpAction(payload);
            return;
        }
        if (window.chrome?.webview?.postMessage) {
            window.chrome.webview.postMessage(payload);
        }
    }

    function documentGroup(document) {
        return document.group || '文档';
    }

    function isNavigationDocument(document) {
        const relativePath = String(document?.relativePath || '');
        return !relativePath.includes('/Guides/')
            && (!relativePath.includes('/Api/00-GettingStarted/')
                || relativePath.endsWith('/Api/00-GettingStarted/FrameworkOverview.md'));
    }

    function navigationDocuments() {
        return (catalog.documents || []).filter(isNavigationDocument);
    }

    function documentNavTitle(document) {
        const fileName = String(document.relativePath || '').split('/').pop()?.replace(/\.md$/i, '') || '';
        const title = document.title || fileName || '文档';
        const kitTitle = title.match(/^([a-z][a-z0-9]*kit)(?:\s|$)/i);
        if (kitTitle) return kitTitle[1];
        if (title === 'YokiFrame 框架概览') return '框架概览';
        return title;
    }

    // 将同一受控文档目录内的相对 Markdown 链接还原为稳定包内路径，避免 WebView 直接导航。
    function resolveInternalMarkdownPath(href) {
        const value = String(href || '').trim();
        const hashIndex = value.indexOf('#');
        const path = (hashIndex >= 0 ? value.slice(0, hashIndex) : value).replace(/\\/g, '/');
        if (path.length === 0
            || !path.toLowerCase().endsWith('.md')
            || /^[a-z][a-z0-9+.-]*:/i.test(path)
            || path.startsWith('/')) {
            return null;
        }

        const segments = String(activeDocument?.relativePath || '').split('/').slice(0, -1);
        for (const segment of path.split('/')) {
            if (segment.length === 0 || segment === '.') continue;
            if (segment === '..') {
                if (segments.length === 0) return null;
                segments.pop();
                continue;
            }

            segments.push(segment);
        }

        return segments.length > 0 ? segments.join('/') : null;
    }

    // 拦截正文中的包内 Markdown 链接，交给宿主继续使用受控文档服务加载目标页面。
    function bindInternalDocumentLinks() {
        if (!article) return;
        article.addEventListener('click', event => {
            const link = event.target instanceof Element ? event.target.closest('a[href]') : null;
            const targetPath = link ? resolveInternalMarkdownPath(link.getAttribute('href')) : null;
            if (!targetPath) return;

            event.preventDefault();
            post({ type: 'select-document', relativePath: targetPath });
        });
    }

    function documentVisual(document) {
        const identity = `${document.relativePath || ''} ${document.title || ''}`.toLowerCase();
        if (identity.includes('overview') || identity.includes('概览')) return { icon: 'framework', tone: 'framework' };
        if (identity.includes('quickstart') || identity.includes('快速上手')) return { icon: 'action', tone: 'actionkit' };
        if (identity.includes('architecture')) return { icon: 'architecture', tone: 'architecture' };
        if (identity.includes('codegenkit')) return { icon: 'codegen', tone: 'codegenkit' };
        if (identity.includes('inspectorkit')) return { icon: 'inspector', tone: 'inspectorkit' };
        if (identity.includes('eventkit')) return { icon: 'event', tone: 'eventkit' };
        if (identity.includes('fsmkit')) return { icon: 'fsm', tone: 'fsmkit' };
        if (identity.includes('logkit')) return { icon: 'log', tone: 'logkit' };
        if (identity.includes('poolkit')) return { icon: 'pool', tone: 'poolkit' };
        if (identity.includes('reskit')) return { icon: 'res', tone: 'reskit' };
        if (identity.includes('singletonkit')) return { icon: 'singleton', tone: 'singletonkit' };
        if (identity.includes('actionkit')) return { icon: 'action', tone: 'actionkit' };
        if (identity.includes('audiokit')) return { icon: 'audio', tone: 'audiokit' };
        if (identity.includes('localizationkit')) return { icon: 'localization', tone: 'localizationkit' };
        if (identity.includes('savekit')) return { icon: 'save', tone: 'savekit' };
        if (identity.includes('scenekit')) return { icon: 'scene', tone: 'scenekit' };
        if (identity.includes('spatialkit')) return { icon: 'spatial', tone: 'spatialkit' };
        if (identity.includes('tablekit')) return { icon: 'table', tone: 'tablekit' };
        if (identity.includes('toolclass')) return { icon: 'toolclass', tone: 'toolclass' };
        if (identity.includes('uikit')) return { icon: 'ui', tone: 'uikit' };
        if (identity.includes('thirdpartyrecommendations')) return { icon: 'github', tone: 'github' };
        if (identity.includes('thirdpartyindex')) return { icon: 'package', tone: 'package' };
        return { icon: 'docs', tone: 'docs' };
    }

    // 将文档开篇 H1 与其紧随导语交给标题卡片承载，正文只保留后续章节。
    function documentPresentation(markdown) {
        const lines = String(markdown || '').split(/\r?\n/);
        let headingIndex = 0;
        while (headingIndex < lines.length && lines[headingIndex].trim().length === 0) headingIndex++;
        if (headingIndex >= lines.length || !/^#\s+/.test(lines[headingIndex].trim())) {
            return { bodyMarkdown: String(markdown || ''), summary: '' };
        }

        let contentIndex = headingIndex + 1;
        while (contentIndex < lines.length && lines[contentIndex].trim().length === 0) contentIndex++;
        const summaryLines = [];
        while (contentIndex < lines.length) {
            const value = lines[contentIndex].trim();
            if (value.length === 0 || isMarkdownBlockStart(value)) break;
            summaryLines.push(value);
            contentIndex++;
        }

        let bodyIndex = contentIndex;
        while (bodyIndex < lines.length && lines[bodyIndex].trim().length === 0) bodyIndex++;
        return {
            bodyMarkdown: lines.slice(bodyIndex).join('\n'),
            summary: markdownInlineText(summaryLines)
        };
    }

    // 判断当前行是否会开启新的 Markdown 块，防止把章节、列表或代码误当页面导语。
    function isMarkdownBlockStart(value) {
        return /^(#{1,6}\s|```|~~~|>|[-*+]\s|\d+[.)]\s|\|)/.test(value)
            || /^(?:-{3,}|\*{3,}|_{3,})$/.test(value);
    }

    // 将导语中的行内 Markdown 收敛为标题卡片可安全显示的单行纯文本。
    function markdownInlineText(lines) {
        return lines.join(' ')
            .replace(/!\[([^\]]*)]\([^)]*\)/g, '$1')
            .replace(/\[([^\]]+)]\([^)]*\)/g, '$1')
            .replace(/<[^>]+>/g, '')
            .replace(/[*_`~]/g, '')
            .replace(/\s+/g, ' ')
            .trim()
            .slice(0, 160);
    }

    // 从当前搜索投影中读取对应文档的摘要，目录项本身仍只承载可阅读的 Markdown 文档。
    function documentSearchSnippet(document) {
        const result = (catalog.searchResults || []).find(item => item.relativePath === document.relativePath);
        return String(result?.snippet || '').replace(/\s+/g, ' ').trim();
    }

    function renderShell() {
        const groups = {};
        for (const document of navigationDocuments()) {
            (groups[documentGroup(document)] ||= []).push(document);
        }

        const navHtml = Object.entries(groups).map(([group, items]) => `
            <div class="doc-nav-group">
                <div class="doc-nav-group-title">${esc(group)}</div>
                ${items.map(document => {
                    const visual = documentVisual(document);
                    const snippet = documentSearchSnippet(document);
                    const snippetHtml = snippet.length > 0
                        ? `<div class="doc-nav-item-snippet">${esc(snippet)}</div>`
                        : '';
                    return `
                    <div class="doc-nav-item${document.relativePath === activeDocument?.relativePath ? ' active' : ''}"
                         data-doc="${esc(document.relativePath)}">
                        <span class="doc-nav-item-icon" data-doc-icon-tone="${visual.tone}">${icons[visual.icon]}</span>
                        <div class="doc-nav-item-content">
                            <div class="doc-nav-item-title">${esc(documentNavTitle(document))}</div>
                            ${snippetHtml}
                        </div>
                    </div>`;
                }).join('')}
            </div>`).join('');
        const emptyNavigation = String(catalog.searchQuery || '').trim().length > 0
            ? '未找到匹配文档'
            : '暂无可用文档';
        const navContent = navHtml.length > 0
            ? navHtml
            : `<div class="doc-nav-empty">${emptyNavigation}</div>`;

        document.body.innerHTML = `
            <div class="doc-layout">
                <aside class="doc-nav">
                    <div class="doc-nav-list">${navContent}</div>
                </aside>
                <article class="doc-article" id="doc-article"></article>
                <nav class="doc-toc" id="doc-toc" aria-label="本页导航"></nav>
            </div>`;
        article = document.getElementById('doc-article');
        toc = document.getElementById('doc-toc');
        bindInternalDocumentLinks();
        document.querySelectorAll('.doc-nav-item').forEach(item => {
            item.addEventListener('click', () => {
                document.querySelectorAll('.doc-nav-item').forEach(navItem => navItem.classList.toggle(
                    'active', navItem === item));
                post({ type: 'select-document', relativePath: item.dataset.doc });
            });
        });
    }

    // 只处理渲染后的文本节点，避免把搜索词作为 HTML 插入并保持 Markdown 生成的结构不变。
    function highlightSearchMatches(root, query) {
        const normalizedQuery = String(query || '').trim();
        if (normalizedQuery.length === 0) return null;

        const lowerQuery = normalizedQuery.toLowerCase();
        const textNodes = [];
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
            acceptNode(textNode) {
                const parent = textNode.parentElement;
                if (!textNode.nodeValue
                    || parent?.closest('script, style, button, .doc-search-match')
                    || !textNode.nodeValue.toLowerCase().includes(lowerQuery)) {
                    return NodeFilter.FILTER_REJECT;
                }

                return NodeFilter.FILTER_ACCEPT;
            }
        });
        while (walker.nextNode()) {
            textNodes.push(walker.currentNode);
        }

        let firstMatch = null;
        for (const textNode of textNodes) {
            const sourceText = textNode.nodeValue;
            const lowerText = sourceText.toLowerCase();
            let segmentStart = 0;
            let matchIndex = lowerText.indexOf(lowerQuery, segmentStart);
            const fragment = document.createDocumentFragment();
            while (matchIndex >= 0) {
                if (matchIndex > segmentStart) {
                    fragment.append(document.createTextNode(sourceText.slice(segmentStart, matchIndex)));
                }

                const match = document.createElement('mark');
                match.className = 'doc-search-match';
                match.textContent = sourceText.slice(matchIndex, matchIndex + normalizedQuery.length);
                fragment.append(match);
                firstMatch ??= match;
                segmentStart = matchIndex + normalizedQuery.length;
                matchIndex = lowerText.indexOf(lowerQuery, segmentStart);
            }

            if (segmentStart < sourceText.length) {
                fragment.append(document.createTextNode(sourceText.slice(segmentStart)));
            }

            textNode.parentNode.replaceChild(fragment, textNode);
        }

        return firstMatch;
    }

    // 在当前文档独立滚动区内定位首个命中；新页面替换后会自动忽略过期回调。
    function scrollToSearchMatch(match) {
        if (!article) return;
        article.scrollTop = 0;
        if (!match) return;

        window.requestAnimationFrame(() => {
            if (!article.contains(match)) return;
            const articleTop = article.getBoundingClientRect().top;
            const matchTop = match.getBoundingClientRect().top;
            article.scrollTop += Math.max(0, matchTop - articleTop - 64);
        });
    }

    function renderArticle() {
        if (!article) return;
        if (tocObserver) {
            tocObserver.disconnect();
            tocObserver = null;
        }
        if (!activeDocument) {
            article.innerHTML = '<div class="doc-empty-article">未找到匹配文档。</div>';
            if (toc) toc.innerHTML = '';
            article.scrollTop = 0;
            return;
        }
        const presentation = documentPresentation(activeDocument.markdown || '');
        const rendered = window.YokiMarkdown.renderWithHeadings(presentation.bodyMarkdown);
        const summary = presentation.summary.length > 0
            ? `<p>${esc(presentation.summary)}</p>`
            : '';
        const hero = `<header class="doc-hero">
            <span class="doc-chip">${esc(activeDocument.group || '文档')}</span>
            <h1>${esc(activeDocument.title || '文档')}</h1>
            ${summary}
        </header>`;
        article.innerHTML = hero + `<div class="doc-body">${rendered.html}</div>`;
        window.YokiMarkdown.bindCopyButtons(article);
        const firstMatch = highlightSearchMatches(article, catalog.searchQuery);
        buildToc(rendered.headings || []);
        scrollToSearchMatch(firstMatch);
    }

    function buildToc(headings) {
        if (!toc) return;
        const items = headings.filter(item => item.level === 2 || item.level === 3);
        toc.innerHTML = items.length === 0
            ? ''
            : `<div class="doc-toc-title">本页导航</div>` + items.map(item =>
                `<button type="button" class="doc-toc-item doc-toc-item--h${item.level}" data-target="${esc(item.id)}">${esc(item.text)}</button>`).join('');
        toc.querySelectorAll('.doc-toc-item').forEach(link => link.addEventListener('click', event => {
            event.preventDefault();
            document.getElementById(link.dataset.target)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }));
        if (!items.length || typeof IntersectionObserver !== 'function') return;
        const headingElements = items.map(item => document.getElementById(item.id)).filter(Boolean);
        tocObserver = new IntersectionObserver(entries => {
            const visible = entries.filter(entry => entry.isIntersecting)
                .sort((left, right) => left.boundingClientRect.top - right.boundingClientRect.top);
            if (!visible.length) return;
            const activeId = visible[0].target.id;
            toc.querySelectorAll('.doc-toc-item').forEach(link => link.classList.toggle(
                'active', link.dataset.target === activeId));
        }, { root: article, rootMargin: '0px 0px -70% 0px', threshold: 0 });
        headingElements.forEach(element => tocObserver.observe(element));
    }

    window.yokiDocs = {
        setTheme(value) {
            const theme = value?.theme === 'light' ? 'light' : 'dark';
            document.documentElement.dataset.theme = theme;
            if (typeof value?.hostSurface === 'string' && value.hostSurface.length > 0) {
                document.documentElement.style.setProperty('--host-surface', value.hostSurface);
            }
        },
        setCatalog(value) {
            const previousRelativePath = activeDocument?.relativePath;
            catalog = value || { packageVersion: '', documents: [] };
            const documents = navigationDocuments();
            activeDocument = documents.find(document => document.relativePath === previousRelativePath)
                || documents[0]
                || null;
            renderShell();
            renderArticle();
        },
        setDocument(value) {
            activeDocument = value;
            document.querySelectorAll('.doc-nav-item').forEach(item => item.classList.toggle(
                'active', item.dataset.doc === value?.relativePath));
            renderArticle();
        }
    };
})();
