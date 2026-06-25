// pages/docs.js
// ═══════════════════════════════════════════════════════════════════
// 页面：Docs（静态 Markdown，HTTP 同源加载，不依赖引擎连接）
// ═══════════════════════════════════════════════════════════════════
let docsIndex = null;       // { docs: [...] } 缓存
let activeDocId = null;
const docCache = {};        // id -> { html, headings } 缓存
let docScrollSpy = null;    // 当前文档的 IntersectionObserver（切换/离开时断开）
let docsVisibilitySignature = '';

async function renderDocsPage() {
    setHero(
        'YokiFrame 文档',
        '框架指南、架构说明和模块参考，适合长时间阅读。',
        '文档',
        'docs',
        '<button class="btn btn-secondary btn-sm" onclick="reloadDocs()">重新加载</button>'
    );
    clearMetrics();
    clearTabs();

    if (!docsIndex) {
        $pageBody.innerHTML = panel('文档', emptyState('≡', '正在加载文档索引…'), '≡');
        try {
            const res = await fetch('docs/index.json', { cache: 'no-cache' });
            docsIndex = await res.json();
        } catch (e) {
            $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">无法加载 docs/index.json: ${e}</span>`, '!');
            return;
        }
    }

    const docs = getVisibleDocsForCurrentEngine();
    docsVisibilitySignature = getDocsVisibilitySignature(docs);
    if (!docs.length) {
        $pageBody.innerHTML = panel('文档', emptyState('≡', '当前引擎没有可显示的文档。'), '≡');
        return;
    }
    if (!activeDocId || !docs.some(doc => doc.id === activeDocId)) activeDocId = docs[0].id;

    // 分组目录 + 内容 + 右侧 TOC 三栏布局
    const groups = {};
    for (const d of docs) {
        (groups[d.group ?? '文档'] ??= []).push(d);
    }
    const navHtml = Object.entries(groups).map(([group, items]) => `
        <div class="doc-nav-group">
            <div class="doc-nav-group-title">${escapeHtml(group)}</div>
            ${items.map(d => `
                <div class="doc-nav-item${d.id === activeDocId ? ' active' : ''}" data-doc="${escapeHtml(d.id)}">
                    <span class="doc-nav-item-icon" data-doc-icon-tone="${escapeHtml(getDocIconTone(d))}">${svgIcon(getDocIconName(d), 'doc-nav-item-svg')}</span>
                    <div class="doc-nav-item-title">${escapeHtml(getDocNavTitle(d))}</div>
                </div>`).join('')}
        </div>`).join('');

    $pageBody.innerHTML = `
        <div class="doc-layout">
            <aside class="doc-nav">${navHtml}</aside>
            <article class="doc-article" id="doc-article">
                <div class="empty-state"><div class="empty-state-icon">≡</div><div class="empty-state-text">正在加载…</div></div>
            </article>
            <nav class="doc-toc" id="doc-toc" aria-label="本页导航"></nav>
        </div>`;

    $pageBody.querySelectorAll('.doc-nav-item').forEach(item => {
        item.addEventListener('click', () => {
            activeDocId = item.dataset.doc;
            $pageBody.querySelectorAll('.doc-nav-item').forEach(i =>
                i.classList.toggle('active', i.dataset.doc === activeDocId));
            loadDocContent(activeDocId);
        });
    });

    loadDocContent(activeDocId);
}

function getDocKit(doc) {
    const id = String(doc?.id ?? '').toLowerCase();
    return KIT_PAGE_ID_TO_KIT[id] || '';
}

function getVisibleDocsForCurrentEngine() {
    const docs = docsIndex?.docs ?? [];
    const targetEngine = getSelectedEngineForNavigation();
    return docs.filter(doc => {
        const kit = getDocKit(doc);
        if (!kit) return true;
        if (!latestStatusRaw || !targetEngine) return true;
        return engineSupportsKit(targetEngine, kit);
    });
}

function getDocsVisibilitySignature(docs) {
    return (docs ?? []).map(doc => doc.id).join('|');
}

function syncDocsAvailability() {
    if (activePage !== 'docs' || !docsIndex) return;

    const docs = getVisibleDocsForCurrentEngine();
    const signature = getDocsVisibilitySignature(docs);
    if (signature === docsVisibilitySignature) return;
    renderDocsPage();
}

function getDocNavTitle(doc) {
    return doc?.navTitle ?? doc?.title ?? '文档';
}

function getDocIconName(doc) {
    const icon = String(doc?.icon ?? '').trim();
    if (icon) return icon;
    return 'docs';
}

function getDocIconTone(doc) {
    const tone = String(doc?.iconTone ?? doc?.id ?? doc?.icon ?? 'docs').trim().toLowerCase();
    return /^[a-z0-9_-]+$/.test(tone) ? tone : 'docs';
}

function renderDocArticleShell(doc, html) {
    return `
        <header class="doc-hero">
            <span class="doc-chip">${escapeHtml(doc.group ?? '文档')}</span>
            <h1>${escapeHtml(doc.title ?? '文档')}</h1>
            ${doc.summary ? `<p>${escapeHtml(doc.summary)}</p>` : ''}
        </header>
        <div class="doc-body">${html}</div>`;
}

async function loadDocContent(docId) {
    const article = document.getElementById('doc-article');
    if (!article) return;

    // 切换文档：断开旧的 scroll-spy 观察者
    if (docScrollSpy) { docScrollSpy.disconnect(); docScrollSpy = null; }

    if (docCache[docId]) {
        article.innerHTML = docCache[docId].html;
        window.YokiMarkdown.bindCopyButtons(article);
        article.scrollTop = 0;
        buildDocToc(docCache[docId].headings, article);
        return;
    }

    const doc = getVisibleDocsForCurrentEngine().find(d => d.id === docId);
    if (!doc) { article.innerHTML = emptyState('!', '未找到文档。'); return; }

    try {
        const res = await fetch(doc.file, { cache: 'no-cache' });
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const md = await res.text();
        const { html, headings } = window.YokiMarkdown.renderWithHeadings(md);
        const shellHtml = renderDocArticleShell(doc, html);
        docCache[docId] = { html: shellHtml, headings };
        article.innerHTML = shellHtml;
        window.YokiMarkdown.bindCopyButtons(article);
        article.scrollTop = 0;
        buildDocToc(headings, article);
    } catch (e) {
        article.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">无法加载 ${doc.file}: ${e}</span>`, '!');
        buildDocToc([], article);
    }
}

// 构建右侧 TOC（仅取 h2/h3），点击平滑滚动 + IntersectionObserver scroll-spy。
function buildDocToc(headings, article) {
    const toc = document.getElementById('doc-toc');
    if (!toc) return;

    const items = (headings ?? []).filter(h => h.level === 2 || h.level === 3);
    if (!items.length) {
        toc.innerHTML = '';
        toc.classList.remove('has-items');
        return;
    }
    toc.classList.add('has-items');
    toc.innerHTML = `<div class="doc-toc-title">本页导航</div>` +
        items.map(h =>
            `<a class="doc-toc-item doc-toc-item--h${h.level}" href="#${h.id}" data-toc="${h.id}">${escapeHtml(h.text)}</a>`
        ).join('');

    // 点击：平滑滚动到锚点（在 article 容器内滚动）
    toc.querySelectorAll('.doc-toc-item').forEach(a => {
        a.addEventListener('click', (e) => {
            e.preventDefault();
            const target = article.querySelector('#' + cssEscape(a.dataset.toc));
            if (target) target.scrollIntoView({ behavior: 'smooth', block: 'start' });
        });
    });

    // scroll-spy：高亮当前可见章节
    const headingEls = items
        .map(h => article.querySelector('#' + cssEscape(h.id)))
        .filter(Boolean);
    docScrollSpy = new IntersectionObserver((entries) => {
        // 选取最靠近顶部的可见标题
        const visible = entries.filter(en => en.isIntersecting);
        if (!visible.length) return;
        visible.sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);
        const id = visible[0].target.id;
        toc.querySelectorAll('.doc-toc-item').forEach(a =>
            a.classList.toggle('active', a.dataset.toc === id));
    }, { root: article, rootMargin: '0px 0px -70% 0px', threshold: 0 });
    headingEls.forEach(el => docScrollSpy.observe(el));
}

window.reloadDocs = async () => {
    docsIndex = null;
    for (const k in docCache) delete docCache[k];
    if (docScrollSpy) { docScrollSpy.disconnect(); docScrollSpy = null; }
    await renderDocsPage();
};

