// core/app.js
// ═══════════════════════════════════════════════════════════════════
// YokiFrame Editor —— 前端应用    v2026-06-20.1
// Native Debug Console · 页面式 SPA · Tauri IPC 桥
// ═══════════════════════════════════════════════════════════════════

const { invoke } = window.__TAURI__?.core ?? {};
const { listen } = window.__TAURI__?.event ?? {};
const BridgeDiagnostics = window.YokiBridgeDiagnostics;
const t = window.YokiI18n?.t ?? ((key) => key);

function disableNativeContextMenu(event) {
    event.preventDefault();
}

document.addEventListener('contextmenu', disableNativeContextMenu);

// ═══════════════════════════════════════════════════════════════════
// DOM 引用
// ═══════════════════════════════════════════════════════════════════
const $root = document.documentElement;
const $status = document.getElementById('status');
const $metricStrip = document.getElementById('metric-strip');
const $tabBar = document.getElementById('tab-bar');
const $pageBody = document.getElementById('page-body');
const $sidebar = document.getElementById('sidebar');
const $languageSelect = document.getElementById('language-select');
const $themeToggle = document.getElementById('theme-toggle');
const $windowClose = document.getElementById('window-close');

// ═══════════════════════════════════════════════════════════════════
