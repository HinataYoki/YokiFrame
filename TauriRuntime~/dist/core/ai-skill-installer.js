// core/ai-skill-installer.js
// AI Skill 安装面板。
function renderAiSkillInstallPanel() {
    const status = aiSkillInstallerStatus;
    const skills = mergeAiSkillDefinitions(status?.skills);
    const targets = mergeAiSkillTargets(status?.targets).filter(target => target.id !== 'custom');
    const customTarget = findAiSkillTarget(status?.targets, 'custom') ?? AI_SKILL_CUSTOM_TARGET;
    const selected = skills.some(skill => skill.name === aiSkillSelectedName)
        ? aiSkillSelectedName
        : skills[0]?.name ?? 'yokiframe';
    aiSkillSelectedName = selected;
    const selectedInfo = skills.find(skill => skill.name === selected);
    const packagedCount = skills.filter(skill => skill.packaged).length;
    const installedTargetCount = targets.filter(target => isAiSkillInstalled(target, selected)).length;
    const sourceRoot = status?.sourceRoot ?? 'Assets/YokiFrame/Core/Editor/Skills';

    return panel(t('ai_skill.title'),
        `<div class="ai-skill-installer" data-ai-skill-installer>
            <div class="ai-skill-installer__summary">
                ${diagnosticTile(t('ai_skill.source'), sourceRoot, t('ai_skill.source_hint'), packagedCount === skills.length ? 'success' : 'warning')}
                ${diagnosticTile(t('ai_skill.selected'), selectedInfo?.label ?? selected, selectedInfo?.packaged ? t('ai_skill.packaged') : t('ai_skill.missing'), selectedInfo?.packaged ? 'success' : 'error')}
                ${diagnosticTile(t('ai_skill.installed_count'), `${installedTargetCount}/${targets.length}`, t('ai_skill.installed_count_hint'), installedTargetCount === targets.length ? 'success' : 'info')}
            </div>
            <div class="ai-skill-tabs" role="tablist" aria-label="${t('ai_skill.skill_list')}">
                ${skills.map(skill => renderAiSkillTab(skill, selected)).join('')}
            </div>
            <div class="ai-skill-target-grid">
                ${targets.map(target => renderAiSkillTargetCard(target, selected)).join('')}
            </div>
            <div class="ai-skill-custom">
                <label class="ai-skill-custom__field">
                    <span>${t('ai_skill.custom_path')}</span>
                    <input class="cmd-input" data-ai-skill-custom-path value="${escapeHtml(customTarget.relativePath || '.custom/skills')}" placeholder=".my-ai/skills">
                </label>
                <div class="ai-skill-custom__actions">
                    <button class="btn btn-secondary btn-sm" type="button" data-ai-skill-custom-install data-ai-skill-name="${escapeHtml(selected)}">${t('ai_skill.install')}</button>
                    <button class="btn btn-ghost btn-sm" type="button" data-ai-skill-custom-uninstall data-ai-skill-name="${escapeHtml(selected)}">${t('ai_skill.uninstall')}</button>
                </div>
            </div>
            <div class="ai-skill-installer__footer">
                <button class="btn btn-secondary btn-sm" type="button" data-ai-skill-refresh>${t('common.refresh')}</button>
                <span data-ai-skill-status-text>${escapeHtml(renderAiSkillStatusText())}</span>
            </div>
            <div class="ai-skill-installer__metadata" hidden>
                <span data-ai-skill-name="yokiframe"></span>
                <span data-ai-skill-name="yokiframe-usage"></span>
                <span data-ai-skill-name="yokiframe-command-bridge"></span>
                <span data-ai-skill-name="yokiframe-editor"></span>
                <span data-ai-skill-target="codex"></span>
                <span data-ai-skill-target="claude"></span>
            </div>
        </div>`,
        'docs');
}

function renderAiSkillTab(skill, selectedName) {
    const active = skill.name === selectedName;
    const missing = !skill.packaged;
    return `<button class="ai-skill-tab${active ? ' is-active' : ''}${missing ? ' is-missing' : ''}" type="button" data-ai-skill-name="${escapeHtml(skill.name)}">
        <span>${escapeHtml(skill.label)}</span>
        <code>${escapeHtml(skill.name)}</code>
    </button>`;
}

function renderAiSkillTargetCard(target, skillName) {
    const installed = isAiSkillInstalled(target, skillName);
    return `<article class="ai-skill-target-card${installed ? ' is-installed' : ''}" data-ai-skill-target="${escapeHtml(target.id)}">
        <div class="ai-skill-target-card__head">
            <strong>${escapeHtml(target.label)}</strong>
            <span class="status-pill ${installed ? 'status-pill--success' : 'status-pill--info'}">${installed ? t('ai_skill.installed') : t('ai_skill.not_installed')}</span>
        </div>
        <code>${escapeHtml(target.relativePath)}/${escapeHtml(skillName)}</code>
        <div class="ai-skill-target-card__actions">
            <button class="btn btn-primary btn-sm" type="button" data-ai-skill-install data-ai-skill-target="${escapeHtml(target.id)}" data-ai-skill-name="${escapeHtml(skillName)}">${t('ai_skill.install')}</button>
            <button class="btn btn-ghost btn-sm" type="button" data-ai-skill-uninstall data-ai-skill-target="${escapeHtml(target.id)}" data-ai-skill-name="${escapeHtml(skillName)}">${t('ai_skill.uninstall')}</button>
        </div>
    </article>`;
}

function bindAiSkillInstallerPanel() {
    const root = document.querySelector('[data-ai-skill-installer]');
    if (!root || root.dataset.bound === '1') return;
    root.dataset.bound = '1';

    root.querySelectorAll('[data-ai-skill-name]').forEach(button => {
        if (!button.classList.contains('ai-skill-tab')) return;
        button.addEventListener('click', () => {
            aiSkillSelectedName = button.dataset.aiSkillName || 'yokiframe';
            updateAiSkillInstallerPanel();
        });
    });

    root.querySelectorAll('[data-ai-skill-install]').forEach(button => {
        button.addEventListener('click', () => {
            void installAiSkill(button.dataset.aiSkillTarget, button.dataset.aiSkillName);
        });
    });

    root.querySelectorAll('[data-ai-skill-uninstall]').forEach(button => {
        button.addEventListener('click', () => {
            void uninstallAiSkill(button.dataset.aiSkillTarget, button.dataset.aiSkillName);
        });
    });

    root.querySelector('[data-ai-skill-custom-install]')?.addEventListener('click', () => {
        const customPath = root.querySelector('[data-ai-skill-custom-path]')?.value ?? '';
        void installAiSkill('custom', aiSkillSelectedName, customPath);
    });

    root.querySelector('[data-ai-skill-custom-uninstall]')?.addEventListener('click', () => {
        const customPath = root.querySelector('[data-ai-skill-custom-path]')?.value ?? '';
        void uninstallAiSkill('custom', aiSkillSelectedName, customPath);
    });

    root.querySelector('[data-ai-skill-refresh]')?.addEventListener('click', () => {
        void refreshAiSkillInstallerStatus({ force: true });
    });
}

async function refreshAiSkillInstallerStatus({ force = false } = {}) {
    if (!invoke || aiSkillInstallerInFlight) return;
    if (!force && aiSkillInstallerStatus) return;

    aiSkillInstallerInFlight = true;
    updateAiSkillStatusText(t('common.loading'));
    try {
        aiSkillInstallerStatus = await invoke('list_ai_skills', {
            projectRoot: resolveAiSkillProjectRoot(),
        });
        updateAiSkillInstallerPanel();
        addLog(t('ai_skill.status_loaded'), 'system');
    } catch (e) {
        updateAiSkillStatusText(t('ai_skill.status_failed', e.message || e));
        addLog(t('ai_skill.status_failed', e.message || e), 'error');
    } finally {
        aiSkillInstallerInFlight = false;
    }
}

async function installAiSkill(targetId, skillName, customPath = null) {
    if (!invoke || !targetId || !skillName) return;
    try {
        const result = await invoke('install_ai_skill', {
            projectRoot: resolveAiSkillProjectRoot(),
            targetId,
            skillName,
            customPath,
        });
        addLog(result?.log || t('ai_skill.install_success', skillName), 'success');
        await refreshAiSkillInstallerStatus({ force: true });
    } catch (e) {
        addLog(t('ai_skill.install_failed', e.message || e), 'error');
        updateAiSkillStatusText(t('ai_skill.install_failed', e.message || e));
    }
}

async function uninstallAiSkill(targetId, skillName, customPath = null) {
    if (!invoke || !targetId || !skillName) return;
    try {
        const result = await invoke('uninstall_ai_skill', {
            projectRoot: resolveAiSkillProjectRoot(),
            targetId,
            skillName,
            customPath,
        });
        addLog(result?.log || t('ai_skill.uninstall_success', skillName), 'success');
        await refreshAiSkillInstallerStatus({ force: true });
    } catch (e) {
        addLog(t('ai_skill.uninstall_failed', e.message || e), 'error');
        updateAiSkillStatusText(t('ai_skill.uninstall_failed', e.message || e));
    }
}

function updateAiSkillInstallerPanel() {
    const root = document.querySelector('[data-ai-skill-installer]');
    if (!root) return;
    const wrapper = root.closest('.panel');
    if (!wrapper) return;
    const next = document.createElement('template');
    next.innerHTML = renderAiSkillInstallPanel().trim();
    const panelNode = next.content.firstElementChild;
    if (!panelNode) return;
    wrapper.replaceWith(panelNode);
    bindAiSkillInstallerPanel();
}

function updateAiSkillStatusText(text) {
    const node = document.querySelector('[data-ai-skill-status-text]');
    if (node) node.textContent = text;
}

function renderAiSkillStatusText() {
    if (aiSkillInstallerInFlight) return t('common.loading');
    if (!invoke) return t('ai_skill.ipc_unavailable');
    if (!aiSkillInstallerStatus) return t('ai_skill.status_pending');
    return t('ai_skill.status_ready', aiSkillInstallerStatus.skills?.length ?? 0);
}

function mergeAiSkillDefinitions(rawSkills) {
    const rawByName = new Map((rawSkills || []).map(skill => [skill.name, skill]));
    const merged = AI_SKILL_DEFAULT_SKILLS.map(def => {
        const raw = rawByName.get(def.name) ?? {};
        return {
            name: def.name,
            label: t(def.labelKey),
            packaged: raw.packaged === true,
            sourcePath: raw.sourcePath || '',
        };
    });

    (rawSkills || []).forEach(skill => {
        if (!skill?.name || merged.some(item => item.name === skill.name)) return;
        merged.push({
            name: skill.name,
            label: skill.name,
            packaged: skill.packaged === true,
            sourcePath: skill.sourcePath || '',
        });
    });

    return merged;
}

function mergeAiSkillTargets(rawTargets) {
    const rawById = new Map((rawTargets || []).map(target => [target.id, target]));
    const defaults = [...AI_SKILL_DEFAULT_TARGETS, AI_SKILL_CUSTOM_TARGET];
    const merged = defaults.map(def => {
        const raw = rawById.get(def.id) ?? {};
        return {
            id: def.id,
            label: raw.label || def.label,
            relativePath: raw.relativePath || def.relativePath,
            installedSkills: Array.isArray(raw.installedSkills) ? raw.installedSkills : [],
            supportsCustomPath: raw.supportsCustomPath === true || def.id === 'custom',
        };
    });

    (rawTargets || []).forEach(target => {
        if (!target?.id || merged.some(item => item.id === target.id)) return;
        merged.push({
            id: target.id,
            label: target.label || target.id,
            relativePath: target.relativePath || '',
            installedSkills: Array.isArray(target.installedSkills) ? target.installedSkills : [],
            supportsCustomPath: target.supportsCustomPath === true,
        });
    });

    return merged;
}

function findAiSkillTarget(targets, id) {
    return mergeAiSkillTargets(targets).find(target => target.id === id);
}

function isAiSkillInstalled(target, skillName) {
    return Array.isArray(target?.installedSkills) && target.installedSkills.includes(skillName);
}

function resolveAiSkillProjectRoot() {
    const status = latestStatusSummary ?? {};
    const rawEngine = latestStatusRaw?.engines?.find(engine => engine?.projectPath) ?? latestStatusRaw?.engines?.[0];
    return rawEngine?.projectPath || status.projectPath || null;
}
