// core/brand-assets.js
// 包元数据、品牌图标和外部链接。
async function loadYokiFramePackageInfo() {
    const info = await fetchFirstJson(YOKIFRAME_PACKAGE_CANDIDATES).catch(() => DEFAULT_YOKIFRAME_PACKAGE);
    yokiFramePackageInfo = info && typeof info === 'object' ? info : DEFAULT_YOKIFRAME_PACKAGE;
    applyYokiFrameBrandAssets();
}

async function fetchFirstJson(paths) {
    for (const path of paths) {
        try {
            const response = await fetch(path, { cache: 'no-store' });
            if (response.ok) {
                return await response.json();
            }
        } catch (_) { /* 尝试下一个相对路径 */ }
    }
    return DEFAULT_YOKIFRAME_PACKAGE;
}

function applyYokiFrameBrandAssets() {
    const version = String(yokiFramePackageInfo?.version || DEFAULT_YOKIFRAME_PACKAGE.version);
    const repository = normalizeRepositoryUrl(yokiFramePackageInfo?.repository?.url || yokiFramePackageInfo?.repository || DEFAULT_YOKIFRAME_PACKAGE.repository.url);
    document.querySelectorAll('[data-package-version]').forEach(node => {
        node.textContent = `v${version}`;
    });
    document.querySelectorAll('[data-package-link]').forEach(node => {
        node.setAttribute('href', repository);
        node.setAttribute('title', repository);
    });

    const icon = document.getElementById('titlebar-brand-icon');
    if (icon) {
        const candidates = isRuntimeDistLocation()
            ? [YOKIFRAME_ICON_CANDIDATES[0], YOKIFRAME_ICON_CANDIDATES[2], YOKIFRAME_ICON_CANDIDATES[3], YOKIFRAME_ICON_CANDIDATES[1]]
            : [YOKIFRAME_ICON_CANDIDATES[0], YOKIFRAME_ICON_CANDIDATES[1], YOKIFRAME_ICON_CANDIDATES[2], YOKIFRAME_ICON_CANDIDATES[3]];
        let iconCandidateIndex = 0;
        icon.onerror = () => {
            iconCandidateIndex += 1;
            if (iconCandidateIndex < candidates.length) {
                icon.src = candidates[iconCandidateIndex];
            }
        };
        icon.src = candidates[iconCandidateIndex];
    }
}

function normalizeRepositoryUrl(url) {
    const value = String(url || '').trim();
    if (!value) return DEFAULT_YOKIFRAME_PACKAGE.repository.url;
    return value.replace(/^git\+/, '').replace(/\.git$/, '');
}

function bindPackageExternalLinks() {
    document.querySelectorAll('[data-package-link]').forEach(link => {
        if (link.dataset.packageLinkBound === 'true') return;
        link.dataset.packageLinkBound = 'true';
        link.addEventListener('click', async (event) => {
            if (!invoke) return;

            event.preventDefault();
            const url = normalizeRepositoryUrl(link.getAttribute('href') || DEFAULT_YOKIFRAME_PACKAGE.repository.url);
            try {
                await invoke('open_external_url', { url });
                addLog(`打开 GitHub: ${url}`, 'system');
            } catch (e) {
                addLog(`打开 GitHub 失败: ${e}`, 'error');
            }
        });
    });
}

function isRuntimeDistLocation() {
    return /TauriRuntime~\/dist/i.test(window.location.pathname.replace(/\\/g, '/'));
}
