// pages/tablekit-render.js
// TableKit 表单渲染：环境、输出路径、额外目标、console 和整体工作台。
function renderTableKitRegistryStatus() {
    clearMetrics();
    tableKitConfig = sanitizeTableKitConfig(tableKitConfig);
    const status = getTableKitLubanStatus();
    const html = renderTableKitGeneratorStatus(status, tableKitConfig);
    const signature = makeStableSignature({
        status,
        config: tableKitConfig,
        collapsed: tableKitCollapsedSections,
        console: getTableKitConsoleSignature(),
        preview: getTableKitPreviewSignature(),
        behavior: getTableKitBehaviorSignature(),
    });
    renderWorkbenchHtmlStable(tableKitEditorState, html, signature, bindTableKitEditor);
}

function renderTableKitGeneratorStatus(status, config) {
    const stateClass = status.available ? 'kit-state-pill--ok' : 'kit-state-pill--muted';
    const stateText = status.available ? 'Luban ON' : 'Luban OFF';
    const registryText = status.hasRegistry
        ? `${status.engine} · ${status.engineId}`
        : '等待 engine registry';
    const effectiveEngine = getTableKitEffectiveEngine(status);

    return `<div class="kit-workbench kit-workbench--table tablekit-workbench">
        <section class="kit-toolbar tablekit-command-center">
            <div class="tablekit-command-center__main">
                <div class="kit-toolbar__title">${renderKitTitle('table', 'TableKit 配置表生成')}</div>
                <div class="kit-toolbar__meta">${escapeHtml(registryText)} · ${escapeHtml(status.connected ? '在线' : '未连接或离线快照')} · ${escapeHtml(effectiveEngine)}</div>
            </div>
            <div class="kit-toolbar__actions tablekit-command-center__actions">
                <div class="tablekit-command-center__states">
                    <span class="kit-state-pill ${stateClass}">${escapeHtml(stateText)}</span>
                    <span class="kit-state-pill">${escapeHtml(status.define)}</span>
                    <span class="kit-state-pill kit-state-pill--ok">${escapeHtml('Tauri 执行')}</span>
                </div>
                <div class="tablekit-command-center__buttons">
                    <button class="btn btn-secondary btn-sm" type="button" data-tablekit-action="reset">还原默认</button>
                    <button class="btn btn-secondary btn-sm" type="button" data-tablekit-action="open-config">打开配置表</button>
                    <button class="btn btn-primary btn-sm" type="button" data-tablekit-action="generate">生成配置表</button>
                </div>
            </div>
        </section>
        <div class="tablekit-sections" data-kit-scroll-key="tablekit-sections">
            ${renderTableKitBehaviorWorkbench()}
            <section class="kit-panel tablekit-config-log-shell">
                <div class="tablekit-config-log-grid">
                    ${renderTableKitEnvironmentPanel(status, config, effectiveEngine)}
                    ${renderTableKitConsole(status, config)}
                </div>
            </section>
            ${renderTableKitPreviewPanel(status, config)}
        </div>
    </div>`;
}

function renderTableKitEnvironmentPanel(status, config, effectiveEngine) {
    const isGodot = String(effectiveEngine).toLowerCase() === 'godot';
    const assemblyNote = isGodot
        ? 'Godot 使用 .csproj/Directory.Build.props 管理编译宏；没有 Unity asmdef 语义。'
        : 'Unity 启用后会在代码输出目录生成 asmdef，并引用 Luban.Runtime。';
    return `<section class="kit-panel tablekit-section tablekit-section--environment">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('status', '环境与路径配置')}</div>
                <div class="kit-panel__desc">配置 Luban 环境位置、Luban 数据目录、生成代码和生成数据落点。</div>
            </div>
            <span class="kit-state-pill ${status.available ? 'kit-state-pill--ok' : 'kit-state-pill--muted'}">${escapeHtml(formatTableKitBool(status.available))}</span>
        </div>
        <div class="tablekit-form" data-kit-scroll-key="tablekit-form">
            ${renderTableKitLubanNotice(status)}
            ${renderTableKitLubanInstallGuide(status, config)}
            ${renderTableKitCollapsibleGroup('lubanEnvironment', 'Luban 环境', '工作目录与 Luban.dll 路径', `
                ${renderTableKitTextField('lubanWorkDir', '工作目录', config.lubanWorkDir, '包含 Datas、Defines、luban.conf 的目录')}
                ${renderTableKitTextField('lubanDllPath', 'Luban.dll', config.lubanDllPath, 'Luban 代码生成工具 DLL 路径')}
            `)}
            ${renderTableKitCollapsibleGroup('outputPaths', '数据路径', '生成数据与 C# 代码落点', `
                <div class="tablekit-form-grid tablekit-form-grid--targets">
                    ${renderTableKitSelectField('target', 'Target', config.target, TABLEKIT_TARGET_OPTIONS, 'Luban target')}
                    ${renderTableKitSelectField('codeTarget', 'Code', config.codeTarget, TABLEKIT_CODE_TARGET_OPTIONS, 'Luban code target')}
                    ${renderTableKitSelectField('dataTarget', '数据格式', config.dataTarget, TABLEKIT_DATA_TARGET_OPTIONS, 'Luban data target')}
                </div>
                ${renderTableKitTextField('outputDataDir', '数据输出', config.outputDataDir, '生成的配置表数据文件存放路径')}
                ${renderTableKitTextField('outputCodeDir', '代码输出', config.outputCodeDir, '生成的 C# 配置表代码存放路径')}
            `)}
            ${renderTableKitCollapsibleGroup('extraOutputs', '额外输出目标', '多份数据格式独立导出', renderTableKitExtraOutputs(config), `
                <button class="btn btn-secondary btn-sm" type="button" data-tablekit-action="add-extra-output">添加输出目标</button>
            `)}
            <div class="tablekit-subsection tablekit-subsection--plain">
                <div class="tablekit-subsection__title">TableKit 路径</div>
                <div class="tablekit-toggle-row">
                    ${renderKitToggle('自定义编辑器数据路径', !!config.customEditorDataPath, 'data-tablekit-toggle="customEditorDataPath"', '关闭时编辑器数据路径自动跟随数据输出路径')}
                </div>
                ${renderTableKitTextField('editorDataPath', '编辑器数据', config.editorDataPath, 'TableKit.TablesEditor 编辑器访问用的数据路径', !config.customEditorDataPath)}
                ${renderTableKitTextField('runtimePathPattern', '运行时模式', config.runtimePathPattern, '{0} 为文件名占位符；例如 Art/Table/{0}')}
            </div>
            <div class="tablekit-subsection tablekit-subsection--plain">
                <div class="tablekit-subsection__title">生成选项</div>
                <div class="tablekit-toggle-row tablekit-toggle-row--wrap">
                    ${renderKitToggle('使用独立程序集', !!config.useAssemblyDefinition, 'data-tablekit-toggle="useAssemblyDefinition"', assemblyNote)}
                    ${renderKitToggle('生成 ExternalTypeUtil', !!config.generateExternalTypeUtil, 'data-tablekit-toggle="generateExternalTypeUtil"', 'Luban 在 vector 转 Unity/Godot 类型时可追加转换工具')}
                    ${renderKitToggle('异步加载模式', !!config.useAsyncLoading, 'data-tablekit-toggle="useAsyncLoading"', '生成 InitAsync 入口；需要异步加载时启用')}
                </div>
                ${renderTableKitTextField('assemblyName', '程序集名称', config.assemblyName, 'Unity asmdef 名称；Godot 仅作为项目侧组织信息', !config.useAssemblyDefinition)}
            </div>
            <div class="kit-detail-summary kit-detail-summary--tablekit-status">
                <div><span>Package</span><strong>${escapeHtml(status.packageName)}</strong></div>
                <div><span>Asmdef</span><strong>${escapeHtml(status.asmdefName)}</strong></div>
                <div><span>Type</span><strong>${escapeHtml(status.typeName)}</strong></div>
            </div>
        </div>
    </section>`;
}

function renderTableKitCollapsibleGroup(id, title, summary, bodyHtml, actionsHtml = '') {
    const collapsed = isTableKitSectionCollapsed(id);
    return `<section class="tablekit-fold-card" data-tablekit-collapsible="${escapeHtml(id)}" data-collapsed="${escapeHtml(String(collapsed))}">
        <div class="tablekit-fold-card__head">
            <button class="tablekit-fold-card__toggle" type="button" data-tablekit-collapse="${escapeHtml(id)}" aria-expanded="${escapeHtml(String(!collapsed))}">
                <span class="tablekit-fold-card__chevron" aria-hidden="true"></span>
                <span class="tablekit-fold-card__text">
                    <strong>${escapeHtml(title)}</strong>
                    <em>${escapeHtml(summary)}</em>
                </span>
            </button>
            ${actionsHtml ? `<div class="tablekit-fold-card__actions">${actionsHtml}</div>` : ''}
        </div>
        <div class="tablekit-fold-card__body">${bodyHtml}</div>
    </section>`;
}

function renderTableKitLubanNotice(status) {
    const level = status.available ? 'success' : 'warning';
    const text = status.available
        ? '宿主已报告 Luban 环境，TableKit 编辑器代码会在对应宏定义下参与编译。'
        : 'Luban 工具不应放置在 Assets 内，推荐放在与 Assets 同级目录；安装后宿主会自动维护 YOKIFRAME_LUBAN_SUPPORT。';
    return `<div class="tablekit-callout tablekit-callout--${level}">${escapeHtml(text)}</div>`;
}

function renderTableKitLubanInstallGuide(status, config) {
    if (status.available) return '';

    const recommendedDllPath = 'Luban/Tools/Luban/Luban.dll';
    const projectRoot = getTableKitProjectRoot(status) || '<GodotProjectRoot>';
    const workDir = normalizeTableKitPath(config.lubanWorkDir);
    const dllPath = normalizeTableKitPath(config.lubanDllPath);
    const resolvedWorkDir = resolveTableKitProjectPath(projectRoot, workDir);
    const resolvedDllPath = resolveTableKitProjectPath(projectRoot, dllPath);

    return `<div class="tablekit-install-guide">
        <div class="tablekit-install-guide__title">Luban 运行时环境未安装</div>
        <ol class="tablekit-install-guide__steps">
            <li>下载 Luban release，并把工具目录放到项目根目录附近，推荐相对路径为 <code>${escapeHtml(recommendedDllPath)}</code>；当前配置会解析到 <code>${escapeHtml(resolvedDllPath)}</code>，不要放进 Assets。</li>
            <li>创建或复制 Luban/MiniTemplate 到 <code>${escapeHtml(resolvedWorkDir)}</code>，其中需要包含 Datas、Defines 和 luban.conf。</li>
            <li>在上方字段确认 Luban 工作目录为 <code>${escapeHtml(workDir)}</code>，Luban.dll 为 <code>${escapeHtml(dllPath)}</code>。</li>
            <li>确认 Godot 工程能通过 Directory.Build.props 识别 Luban.Runtime，让 ${escapeHtml(status.define)} 宏保持启用。</li>
            <li>安装完成后重启 Godot 或重新启用 YokiFrame 插件，再回到 TableKit 刷新状态。</li>
        </ol>
    </div>`;
}

function renderTableKitExtraOutputs(config) {
    const rows = config.extraOutputTargets.length
        ? config.extraOutputTargets.map((target, index) => renderTableKitExtraOutputRow(target, index)).join('')
        : '<div class="tablekit-empty-line">未配置额外输出目标；需要多份数据格式时可在这里追加。</div>';
    return `<div class="tablekit-extra-output">
        <div class="tablekit-extra-output__rows">${rows}</div>
        <div class="tablekit-warning-text">提示：不同导出目标会分别执行 Luban，确保字段记得导出。</div>
    </div>`;
}

function renderTableKitExtraOutputRow(target, index) {
    return `<div class="tablekit-extra-output-row">
        ${renderTableKitExtraSelect(index, 'target', target.target, TABLEKIT_TARGET_OPTIONS, 'Target')}
        ${renderTableKitExtraSelect(index, 'dataTarget', target.dataTarget, TABLEKIT_DATA_TARGET_OPTIONS, 'Data')}
        ${renderTableKitPathControl(target.outputDataDir, {
            picker: { kind: 'folder', buttonLabel: '选择', icon: 'folder' },
            ariaLabel: '额外输出目录',
            inputAttrs: `data-tablekit-extra-index="${index}" data-tablekit-extra-field="outputDataDir"`,
            buttonAttrs: `data-tablekit-extra-pick-folder="${index}"`,
        })}
        <button class="btn btn-secondary btn-sm" type="button" data-tablekit-remove-extra="${index}">移除</button>
    </div>`;
}

function renderTableKitExtraSelect(index, field, value, options, label) {
    const items = options.map(option => {
        const selected = option === value ? ' selected' : '';
        return `<option value="${escapeHtml(option)}"${selected}>${escapeHtml(formatTableKitOption(option))}</option>`;
    }).join('');
    return `<label class="tablekit-extra-select">
        <span>${escapeHtml(label)}</span>
        <select class="cmd-select" data-tablekit-extra-index="${index}" data-tablekit-extra-field="${escapeHtml(field)}">${items}</select>
    </label>`;
}

function renderTableKitConsole(status, config) {
    const rows = tableKitConsoleEntries.length
        ? tableKitConsoleEntries.map(entry => `<div class="tablekit-console-line tablekit-console-line--${escapeHtml(entry.level)}"><span>[${escapeHtml(entry.time)}]</span><strong>${escapeHtml(entry.level.toUpperCase())}</strong><em>${escapeHtml(entry.message)}</em></div>`).join('')
        : '<div class="tablekit-console-empty">暂无 Luban 输出。生成或验证配置表后会显示日志。</div>';
    return `<section class="kit-panel tablekit-section tablekit-section--console">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('log', '控制台')}</div>
                <div class="kit-panel__desc">显示 Luban 生成配置表后的输出日志，可复制粘贴给协作者或 AI。</div>
            </div>
            <div class="tablekit-panel-actions">
                <button class="btn btn-secondary btn-sm" type="button" data-tablekit-action="copy-console">复制</button>
                <button class="btn btn-secondary btn-sm" type="button" data-tablekit-action="clear-console">清空</button>
            </div>
        </div>
        <div class="tablekit-console-status">
            <span>${escapeHtml('Luban 命令')}</span>
            <code>${escapeHtml(renderTableKitCommandPreview(config, status).split('\n')[1] || '')}</code>
        </div>
        <div class="tablekit-console" data-kit-scroll-key="tablekit-console">${rows}</div>
    </section>`;
}

function renderTableKitBehaviorWorkbench() {
    const templates = getTableKitFilteredBehaviorTemplates();
    const nodes = getTableKitFilteredBehaviorNodes();
    const selectedTemplate = getTableKitBehaviorSelectedTemplate();
    const selectedNode = getTableKitBehaviorSelectedNode();
    const edges = getTableKitBehaviorEdges();
    return `<section class="kit-panel tablekit-section tablekit-section--wide tablekit-behavior-workbench">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('fsm', 'Behavior 节点图原型')}</div>
                <div class="kit-panel__desc">先验证节点库、图布局、属性面板和 XML 结构预览，再决定是否接入 Luban 回写。</div>
            </div>
            <div class="tablekit-panel-actions">
                <button class="btn btn-secondary btn-sm" type="button" data-tablekit-behavior-docs>查看思路</button>
                <button class="btn btn-primary btn-sm" type="button" data-tablekit-behavior-focus>聚焦根节点</button>
            </div>
        </div>
        <div class="tablekit-behavior-toolbar">
            <div class="tablekit-behavior-toolbar__stats">
                <span class="kit-state-pill">${escapeHtml(templates.length)} 模板</span>
                <span class="kit-state-pill kit-state-pill--ok">${escapeHtml(nodes.length)} 节点</span>
                <span class="kit-state-pill">${escapeHtml(edges.length)} 连线</span>
            </div>
            <label class="kit-search tablekit-behavior-search" role="search">
                ${svgIcon('search', 'kit-search__icon')}
                <input class="kit-search__input" type="search" data-tablekit-behavior-search value="${escapeHtml(tableKitBehaviorState.searchTerm)}" placeholder="搜索节点、模板、参数、XML ...">
                <button class="kit-search__clear${String(tableKitBehaviorState.searchTerm || '').trim() ? '' : ' is-empty'}" type="button" data-tablekit-behavior-search-clear title="清空搜索">${svgIcon('empty', 'kit-search__clear-icon')}</button>
            </label>
        </div>
        <div class="tablekit-behavior-layout" data-kit-scroll-key="tablekit-behavior-layout">
            <aside class="tablekit-behavior-library" data-kit-scroll-key="tablekit-behavior-library">
                <div class="tablekit-behavior-library__head">
                    <strong>节点库</strong>
                    <span>Behavior 风格原型</span>
                </div>
                <div class="tablekit-behavior-library__list">
                    ${templates.length ? templates.map(template => renderTableKitBehaviorTemplateCard(template, selectedTemplate)).join('') : emptyState('empty', '没有可用模板')}
                </div>
            </aside>
            <section class="tablekit-behavior-canvas-shell">
                <div class="tablekit-behavior-canvas-head">
                    <div>
                        <strong>图结构</strong>
                        <span>支持缩放 / 平移 / 选择</span>
                    </div>
                    <code>${escapeHtml(getTableKitBehaviorXmlPreview(selectedTemplate, selectedNode))}</code>
                </div>
                <div class="tablekit-behavior-canvas" data-tablekit-behavior-canvas>
                    ${renderTableKitBehaviorGraph(selectedNode)}
                </div>
            </section>
            <aside class="tablekit-behavior-inspector" data-kit-scroll-key="tablekit-behavior-inspector">
                ${renderTableKitBehaviorInspector(selectedTemplate, selectedNode)}
            </aside>
        </div>
    </section>`;
}

function renderTableKitBehaviorTemplateCard(template, selectedTemplate) {
    const active = selectedTemplate && String(selectedTemplate.id) === String(template.id) ? ' active' : '';
    return `<button class="tablekit-behavior-card tablekit-behavior-card--${escapeHtml(template.tone)}${active}" type="button" data-tablekit-behavior-template="${escapeHtml(template.id)}">
        <span class="tablekit-behavior-card__top">
            <strong>${escapeHtml(template.title)}</strong>
            <em>${escapeHtml(template.kind)}</em>
        </span>
        <span class="tablekit-behavior-card__summary">${escapeHtml(template.summary)}</span>
        <code>${escapeHtml(template.xmlSample)}</code>
    </button>`;
}

function renderTableKitBehaviorGraph(selectedNode) {
    const nodes = getTableKitFilteredBehaviorNodes();
    const edges = getTableKitBehaviorEdges();
    const visibleNodeIds = new Set(nodes.map(node => String(node.id)));
    const visibleEdges = edges.filter(edge => visibleNodeIds.has(String(edge.from)) && visibleNodeIds.has(String(edge.to)));
    const allNodes = TABLEKIT_BEHAVIOR_GRAPH.nodes;
    const selectedId = selectedNode ? String(selectedNode.id) : '';
    const width = 1420;
    const height = 520;
    const edgeHtml = visibleEdges.map(edge => renderTableKitBehaviorEdge(edge, allNodes, selectedId)).join('');
    const nodeHtml = nodes.map(node => renderTableKitBehaviorNode(node, selectedId)).join('');
    return `<svg class="tablekit-behavior-svg" viewBox="0 0 ${width} ${height}" preserveAspectRatio="xMidYMid meet" role="img" aria-label="Behavior graph">
        <defs>
            <marker id="tablekit-behavior-arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
                <path d="M0,0 L10,5 L0,10 z" fill="currentColor"></path>
            </marker>
            <linearGradient id="tablekit-behavior-node" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stop-color="#eef4ff"></stop>
                <stop offset="100%" stop-color="#d8e5ff"></stop>
            </linearGradient>
        </defs>
        ${edgeHtml}
        ${nodeHtml}
    </svg>`;
}

function renderTableKitBehaviorEdge(edge, nodes, selectedId) {
    const from = findTableKitBehaviorNode(nodes, edge.from);
    const to = findTableKitBehaviorNode(nodes, edge.to);
    if (!from || !to) return '';
    const startX = from.x + from.w;
    const startY = from.y + from.h / 2;
    const endX = to.x;
    const endY = to.y + to.h / 2;
    const deltaX = Math.max(120, Math.abs(endX - startX) * 0.32);
    const controlX1 = startX + deltaX;
    const controlX2 = endX - deltaX;
    const highlight = selectedId && (selectedId === String(from.id) || selectedId === String(to.id)) ? ' tablekit-behavior-edge--active' : '';
    return `<g class="tablekit-behavior-edge${highlight}">
        <path d="M ${startX} ${startY} C ${controlX1} ${startY}, ${controlX2} ${endY}, ${endX} ${endY}" marker-end="url(#tablekit-behavior-arrow)"></path>
        <text x="${(startX + endX) / 2}" y="${(startY + endY) / 2 - 6}">${escapeHtml(edge.label)}</text>
    </g>`;
}

function renderTableKitBehaviorNode(node, selectedId) {
    const active = selectedId && String(selectedId) === String(node.id) ? ' active tablekit-behavior-node--active' : '';
    const template = getTableKitBehaviorTemplateById(node.templateId);
    const statusClass = String(node.status || '').toLowerCase();
    return `<g class="tablekit-behavior-node tablekit-behavior-node--${escapeHtml(statusClass)}${active}" data-tablekit-behavior-node="${escapeHtml(node.id)}" transform="translate(${escapeHtml(node.x)}, ${escapeHtml(node.y)})">
        <rect width="${escapeHtml(node.w)}" height="${escapeHtml(node.h)}" rx="14" ry="14"></rect>
        <rect class="tablekit-behavior-node__accent" x="0" y="0" width="${escapeHtml(node.w)}" height="10" rx="14" ry="14"></rect>
        <text x="16" y="28" class="tablekit-behavior-node__title">${escapeHtml(node.title)}</text>
        <text x="16" y="48" class="tablekit-behavior-node__summary">${escapeHtml(node.summary)}</text>
        <text x="16" y="64" class="tablekit-behavior-node__meta">${escapeHtml(node.params)}</text>
        <text x="${escapeHtml(node.w - 16)}" y="28" class="tablekit-behavior-node__type">${escapeHtml(template?.kind || node.type || '--')}</text>
    </g>`;
}

function renderTableKitBehaviorInspector(selectedTemplate, selectedNode) {
    const templateRows = selectedTemplate ? [
        ['Kind', selectedTemplate.kind],
        ['Runtime', selectedTemplate.runtime],
        ['Ports', selectedTemplate.ports],
        ['XML', selectedTemplate.xmlSample],
    ] : [];
    const nodeRows = selectedNode ? [
        ['ID', selectedNode.id],
        ['Type', selectedNode.type],
        ['Params', selectedNode.params],
        ['Runtime', selectedNode.runtime],
        ['Status', selectedNode.status],
    ] : [];
    return `<div class="tablekit-behavior-inspector__shell">
        <div class="tablekit-behavior-inspector__head">
            <div>
                <strong>${escapeHtml(selectedNode ? selectedNode.title : selectedTemplate.title)}</strong>
                <span>${escapeHtml(selectedNode ? selectedNode.summary : selectedTemplate.summary)}</span>
            </div>
            <span class="kit-state-pill">${escapeHtml(selectedNode ? selectedNode.status : selectedTemplate.kind)}</span>
        </div>
        <div class="tablekit-behavior-inspector__body">
            <section class="tablekit-behavior-meta">
                <div class="tablekit-behavior-meta__title">模板信息</div>
                ${templateRows.map(row => renderTableKitBehaviorMetaRow(row[0], row[1])).join('')}
            </section>
            <section class="tablekit-behavior-meta">
                <div class="tablekit-behavior-meta__title">当前节点</div>
                ${nodeRows.map(row => renderTableKitBehaviorMetaRow(row[0], row[1])).join('') || '<div class="tablekit-empty-line">图上没有可选节点。</div>'}
            </section>
        </div>
    </div>`;
}

function renderTableKitBehaviorMetaRow(label, value) {
    return `<div class="tablekit-behavior-meta__row">
        <span>${escapeHtml(label)}</span>
        <code>${escapeHtml(String(value ?? '--'))}</code>
    </div>`;
}

function getTableKitBehaviorXmlPreview(template, node) {
    const templateId = template?.id || TABLEKIT_BEHAVIOR_DEFAULT_TEMPLATE_ID;
    const nodeId = node?.id || TABLEKIT_BEHAVIOR_DEFAULT_NODE_ID;
    return `<graph template="${templateId}" node="${nodeId}" source="xml" />`;
}

function findTableKitBehaviorNode(nodes, nodeId) {
    const id = String(nodeId || '');
    if (!id) return null;
    for (let i = 0; i < nodes.length; i++) {
        if (String(nodes[i].id) === id) return nodes[i];
    }
    return null;
}

function renderTableKitSelectField(field, label, value, options, hint) {
    const items = options.map(option => {
        const selected = option === value ? ' selected' : '';
        return `<option value="${escapeHtml(option)}"${selected}>${escapeHtml(formatTableKitOption(option))}</option>`;
    }).join('');
    return `<label class="tablekit-field">
        <span>${escapeHtml(label)}${hint ? `<em>${escapeHtml(hint)}</em>` : ''}</span>
        <select class="cmd-select" data-tablekit-field="${escapeHtml(field)}">${items}</select>
    </label>`;
}

function renderTableKitTextField(field, label, value, hint, disabled = false) {
    const picker = TABLEKIT_PATH_PICKERS[field];
    if (picker) {
        return `<div class="tablekit-field tablekit-field--path${disabled ? ' tablekit-field--disabled' : ''}">
            <span>${escapeHtml(label)}${hint ? `<em>${escapeHtml(hint)}</em>` : ''}</span>
            ${renderTableKitPathControl(value, {
                picker,
                disabled,
                ariaLabel: label,
                inputAttrs: `data-tablekit-field="${escapeHtml(field)}"`,
                buttonAttrs: `data-tablekit-pick-path="${escapeHtml(field)}"`,
            })}
        </div>`;
    }

    return `<label class="tablekit-field${disabled ? ' tablekit-field--disabled' : ''}">
        <span>${escapeHtml(label)}${hint ? `<em>${escapeHtml(hint)}</em>` : ''}</span>
        <input class="cmd-input" type="text" data-tablekit-field="${escapeHtml(field)}" value="${escapeHtml(value)}"${disabled ? ' disabled' : ''}>
    </label>`;
}

function renderTableKitPathControl(value, options) {
    const picker = options?.picker || TABLEKIT_PATH_PICKERS.outputDataDir;
    const disabled = !!options?.disabled;
    const inputAttrs = options?.inputAttrs ? ` ${options.inputAttrs}` : '';
    const buttonAttrs = options?.buttonAttrs ? ` ${options.buttonAttrs}` : '';
    const ariaLabel = options?.ariaLabel ? String(options.ariaLabel) : '路径';
    const buttonTitle = picker.kind === 'file' ? `选择${ariaLabel}` : `选择${ariaLabel}`;
    const buttonIcon = svgIcon(picker.icon || 'folder', 'shell-icon');
    return `<div class="cmd-path-control tablekit-path-control">
        <input class="cmd-input cmd-path-display" type="text" value="${escapeHtml(value)}"${inputAttrs} aria-label="${escapeHtml(ariaLabel)}" readonly aria-readonly="true"${disabled ? ' disabled' : ''}>
        <button class="btn btn-secondary btn-sm cmd-path-button" type="button"${buttonAttrs}${disabled ? ' disabled' : ''} title="${escapeHtml(buttonTitle)}">${buttonIcon}<span>${escapeHtml(picker.buttonLabel || '选择')}</span></button>
    </div>`;
}

function getTableKitPathFieldLabel(field) {
    switch (field) {
        case 'lubanWorkDir':
            return 'Luban 工作目录';
        case 'lubanDllPath':
            return 'Luban.dll';
        case 'outputDataDir':
            return '数据输出';
        case 'outputCodeDir':
            return '代码输出';
        case 'editorDataPath':
            return '编辑器数据';
        default:
            return field;
    }
}

