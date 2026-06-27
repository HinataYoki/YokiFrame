// pages/tablekit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：TableKit
// ═══════════════════════════════════════════════════════════════════
function renderTableKitPage() {
    $pageBody.classList.add('content-body--tablekit');
    setHero(
        'TableKit 配置表编辑器',
        '在 Tauri 中配置 Luban 生成参数；运行时代码由工具生成到项目 Scripts。',
        '工具 · TABLEKIT',
        'table',
        '<button class="btn btn-primary btn-sm" onclick="refreshTableKit()">刷新</button>'
    );
    clearTabs();
    renderTableKitRegistryStatus();
    if (invoke) {
        pollStatus({ force: true });
    }
}

async function refreshTableKit() {
    if (invoke) {
        await pollStatus({ force: true });
    }
    renderTableKitRegistryStatus();
}

async function refreshTableKitReactive(event) {
    renderTableKitRegistryStatus();
}
