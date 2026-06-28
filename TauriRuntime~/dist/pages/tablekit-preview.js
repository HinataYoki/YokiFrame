// pages/tablekit-preview.js
// TableKit 数据预览、字段矩阵和编辑器交互。
function renderTableKitPreviewPanel(status, config) {
    const filteredTables = getTableKitFilteredTables();
    const selectedTable = getTableKitSelectedTable(filteredTables);
    return `<section class="kit-panel tablekit-section tablekit-section--wide">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('table', '数据预览与配置表信息')}</div>
                <div class="kit-panel__desc">验证配置时生成临时 JSON，再用这里快速检查本次打包的数据结构。</div>
            </div>
            <div class="tablekit-panel-actions">
                <button class="btn btn-secondary btn-sm" type="button" data-tablekit-action="validate-preview">验证配置</button>
                <input class="cmd-input tablekit-preview-search" type="search" data-tablekit-search value="${escapeHtml(tableKitPreviewState.searchTerm)}" placeholder="搜索表名...">
            </div>
        </div>
        <div class="tablekit-preview-layout">
            <aside class="tablekit-table-list" data-tablekit-table-list>
                ${filteredTables.length ? filteredTables.map(table => renderTableKitTableListItem(table, selectedTable)).join('') : emptyState('table', '尚无配置表信息。验证配置后会读取 Temp/LubanValidate JSON。')}
            </aside>
            <div class="tablekit-preview-inspector" data-tablekit-preview-tree>
                ${renderTableKitPreviewInspector(selectedTable)}
            </div>
        </div>
    </section>`;
}

function getTableKitFilteredTables() {
    const query = String(tableKitPreviewState.searchTerm || '').trim().toLowerCase();
    const tables = Array.isArray(tableKitPreviewState.tables) ? tableKitPreviewState.tables : [];
    if (!query) return tables;
    return tables.filter(table => String(table?.name || table?.fullName || '').toLowerCase().includes(query));
}

function getTableKitSelectedTable(tables) {
    if (!Array.isArray(tables) || !tables.length) return null;
    const selectedName = String(tableKitPreviewState.selectedTableName || '');
    return tables.find(table => String(table?.name || table?.fullName || '') === selectedName) || tables[0];
}

function renderTableKitTableListItem(table, selectedTable) {
    const name = String(table?.name || table?.fullName || '--');
    const selectedName = String(selectedTable?.name || selectedTable?.fullName || '');
    const count = getTableKitTableRowCount(table);
    const active = name === selectedName ? ' active' : '';
    return `<button class="tablekit-table-item${active}" type="button" data-tablekit-table="${escapeHtml(name)}">
        <span>${escapeHtml(name)}</span>
        <em>${escapeHtml(count ? `${count} 行` : '结构预览')}</em>
    </button>`;
}

function renderTableKitPreviewInspector(table) {
    if (!table) {
        return `<div class="tablekit-preview-empty">${emptyState('table', '预览来自 Luban validate 生成的临时 JSON；点击验证配置后会在这里显示。')}</div>`;
    }

    const rows = flattenTableKitPreviewRows(table);
    const selectedIndex = getTableKitSelectedRowIndex(rows);
    const selectedRow = rows[selectedIndex] ?? null;
    const fields = getTableKitRowFields(selectedRow);
    const tableName = String(table?.name || table?.fullName || 'table');
    const rowText = rows.length ? `${getTableKitTableRowCount(table, rows)} 行` : '无行数据';
    const fieldText = fields.length ? `${fields.length} 字段` : '结构为空';

    return `<div class="tablekit-preview-inspector__shell">
        <div class="tablekit-preview-inspector__head">
            <div>
                <strong>${escapeHtml(tableName)}</strong>
                <span>${escapeHtml(rowText)} · ${escapeHtml(fieldText)}</span>
            </div>
            <span class="kit-state-pill">${escapeHtml(getTableKitValueType(table.preview ?? table.rows ?? table.schema ?? table))}</span>
        </div>
        <div class="tablekit-preview-inspector__body">
            ${renderTableKitRecordList(table, rows, selectedIndex)}
            <div class="tablekit-field-detail">
                ${renderTableKitFieldMatrix(selectedRow)}
                ${renderTableKitJsonBlock(selectedRow ?? table.preview ?? table.rows ?? table.schema ?? table)}
            </div>
        </div>
    </div>`;
}

function renderTableKitRecordList(table, rows, selectedIndex) {
    if (!rows.length) {
        return `<section class="tablekit-record-list tablekit-record-list--empty">${emptyState('table', '这张表没有可预览的行数据。')}</section>`;
    }

    const visibleRows = rows.slice(0, 200);
    const items = visibleRows.map((row, index) => {
        const active = index === selectedIndex ? ' active' : '';
        const title = getTableKitRecordTitle(row, index);
        const type = getTableKitValueType(row);
        const fieldCount = getTableKitRowFields(row).length;
        const summary = fieldCount ? `${fieldCount} 字段` : formatTableKitPreviewValue(row);
        return `<button class="tablekit-record-item${active}" type="button" data-tablekit-row="${index}">
            <span>
                <strong>${escapeHtml(title)}</strong>
                <em>${escapeHtml(type)}</em>
            </span>
            <code>${escapeHtml(summary)}</code>
        </button>`;
    }).join('');
    const overflowHint = rows.length > visibleRows.length
        ? `<div class="tablekit-record-list__hint">仅显示前 ${visibleRows.length} 行；完整数据可在原始 JSON 中检查。</div>`
        : '';
    return `<section class="tablekit-record-list" data-tablekit-record-list>
        <div class="tablekit-record-list__head">
            <strong>${escapeHtml(String(table?.name || table?.fullName || 'Records'))}</strong>
            <span>${escapeHtml(rows.length)} 行</span>
        </div>
        ${items}
        ${overflowHint}
    </section>`;
}

function renderTableKitFieldMatrix(row) {
    const fields = getTableKitRowFields(row);
    if (!fields.length) {
        return `<div class="tablekit-field-matrix tablekit-field-matrix--empty">${emptyState('table', '当前记录没有可展开字段。')}</div>`;
    }

    return `<div class="tablekit-field-matrix">
        ${fields.map(([key, value]) => {
            const type = getTableKitValueType(value);
            return `<article class="tablekit-field-card">
                <div class="tablekit-field-card__head">
                    <strong>${escapeHtml(key)}</strong>
                    <span class="tablekit-type-pill tablekit-type-pill--${escapeHtml(type)}">${escapeHtml(type)}</span>
                </div>
                <code class="tablekit-field-card__value">${escapeHtml(formatTableKitPreviewValue(value))}</code>
            </article>`;
        }).join('')}
    </div>`;
}

function renderTableKitJsonBlock(value) {
    return `<details class="tablekit-json-panel" open>
        <summary>原始 JSON</summary>
        <pre class="tablekit-json-block"><code>${escapeHtml(stringifyTableKitJson(value))}</code></pre>
    </details>`;
}

function flattenTableKitPreviewRows(table) {
    const payload = table?.preview ?? table?.rows ?? table?.items ?? table?.data ?? table?.schema ?? table;
    if (Array.isArray(payload)) return payload;
    if (!payload || typeof payload !== 'object') return payload == null ? [] : [payload];

    for (const key of ['rows', 'items', 'records', 'data', 'values', 'list']) {
        if (Array.isArray(payload[key])) return payload[key];
    }

    const objectArrays = Object.values(payload).filter(value => Array.isArray(value));
    const rowLikeArray = objectArrays.find(value => value.some(item => item && typeof item === 'object'));
    if (rowLikeArray) return rowLikeArray;
    if (objectArrays.length) return objectArrays[0];
    return [payload];
}

function getTableKitSelectedRowIndex(rows) {
    if (!Array.isArray(rows) || !rows.length) return 0;
    const index = Number(tableKitPreviewState.selectedRowIndex);
    if (!Number.isInteger(index) || index < 0) return 0;
    return Math.min(index, rows.length - 1);
}

function getTableKitTableRowCount(table, rows = null) {
    const explicitCount = Number(table?.count);
    if (Number.isFinite(explicitCount) && explicitCount >= 0) return explicitCount;
    const previewRows = Array.isArray(rows) ? rows : flattenTableKitPreviewRows(table);
    return previewRows.length;
}

function getTableKitRowFields(row) {
    if (Array.isArray(row)) return row.map((value, index) => [`[${index}]`, value]);
    if (row && typeof row === 'object') return Object.entries(row);
    if (row === null || row === undefined) return [];
    return [['value', row]];
}

function getTableKitRecordTitle(row, index) {
    if (row && typeof row === 'object' && !Array.isArray(row)) {
        for (const key of ['id', 'Id', 'ID', 'key', 'Key', 'name', 'Name', 'type', 'Type']) {
            const value = row[key];
            if (value !== undefined && value !== null && String(value).trim()) {
                return `${index + 1}. ${value}`;
            }
        }
    }
    return `#${index + 1}`;
}

function getTableKitValueType(value) {
    if (value === null) return 'null';
    if (Array.isArray(value)) return 'array';
    return typeof value;
}

function formatTableKitPreviewValue(value) {
    const type = getTableKitValueType(value);
    if (type === 'array') return `[${value.length} 项]`;
    if (type === 'object') return `{ ${Object.keys(value).length} 字段 }`;
    if (type === 'string') return value === '' ? '""' : value;
    if (type === 'undefined') return 'undefined';
    return String(value);
}

function stringifyTableKitJson(value) {
    if (value === undefined) return 'undefined';
    try {
        const json = JSON.stringify(value, (_, item) => typeof item === 'bigint' ? item.toString() : item, 2);
        return json === undefined ? String(value) : json;
    } catch (_) {
        return String(value);
    }
}

function bindTableKitEditor() {
    $pageBody.querySelectorAll('[data-tablekit-collapse]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            toggleTableKitCollapsedSection(button.dataset.tablekitCollapse);
        });
    });

    $pageBody.querySelectorAll('[data-tablekit-field]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        const eventName = input.tagName === 'SELECT' ? 'change' : 'input';
        input.addEventListener(eventName, () => {
            updateTableKitConfigField(input.dataset.tablekitField, input.value);
        });
    });

    $pageBody.querySelectorAll('[data-tablekit-toggle]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        input.addEventListener('change', () => {
            updateTableKitConfigField(input.dataset.tablekitToggle, input.checked);
        });
    });

    $pageBody.querySelectorAll('[data-tablekit-extra-field]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        const eventName = input.tagName === 'SELECT' ? 'change' : 'input';
        input.addEventListener(eventName, () => {
            updateTableKitExtraOutputField(input.dataset.tablekitExtraIndex, input.dataset.tablekitExtraField, input.value);
        });
    });

    $pageBody.querySelectorAll('[data-tablekit-pick-path]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            void pickTableKitPath(button.dataset.tablekitPickPath);
        });
    });

    $pageBody.querySelectorAll('[data-tablekit-extra-pick-folder]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            void pickTableKitExtraOutputFolder(button.dataset.tablekitExtraPickFolder);
        });
    });

    const search = $pageBody.querySelector('[data-tablekit-search]');
    if (search && search.dataset.bound !== '1') {
        search.dataset.bound = '1';
        search.addEventListener('input', () => {
            tableKitPreviewState.searchTerm = search.value;
            renderTableKitRegistryStatus();
        });
    }

    bindKitButtonClick('[data-tablekit-action="reset"]', () => {
        if (typeof window.confirm === 'function' && !window.confirm('还原 TableKit 生成配置为默认值？')) return;
        tableKitConfig = sanitizeTableKitConfig({});
        appendTableKitConsoleEntry('info', '已还原 TableKit 默认配置。');
        persistTableKitConfig();
        renderTableKitRegistryStatus();
    });

    bindKitButtonClick('[data-tablekit-action="docs"]', () => {
        activeDocId = 'tablekit';
        navigateTo('docs');
    });

    bindKitButtonClick('[data-tablekit-action="open-config"]', () => void handleTableKitOpenConfig());
    bindKitButtonClick('[data-tablekit-action="generate"]', () => void handleTableKitGenerate());
    bindKitButtonClick('[data-tablekit-action="validate-preview"]', () => void handleTableKitValidatePreview());
    bindKitButtonClick('[data-tablekit-action="copy-console"]', () => void copyTableKitConsoleText());
    bindKitButtonClick('[data-tablekit-action="clear-console"]', () => {
        tableKitConsoleEntries = [];
        persistTableKitConsoleEntries();
        renderTableKitRegistryStatus();
    });
    bindKitButtonClick('[data-tablekit-action="add-extra-output"]', () => addTableKitExtraOutput());

    $pageBody.querySelectorAll('[data-tablekit-remove-extra]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => removeTableKitExtraOutput(button.dataset.tablekitRemoveExtra));
    });

    $pageBody.querySelectorAll('[data-tablekit-table]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            tableKitPreviewState.selectedTableName = button.dataset.tablekitTable || '';
            tableKitPreviewState.selectedRowIndex = 0;
            renderTableKitRegistryStatus();
        });
    });

    $pageBody.querySelectorAll('[data-tablekit-row]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            const index = Number(button.dataset.tablekitRow);
            tableKitPreviewState.selectedRowIndex = Number.isInteger(index) && index >= 0 ? index : 0;
            renderTableKitRegistryStatus();
        });
    });
}

async function pickTableKitPath(field) {
    const picker = TABLEKIT_PATH_PICKERS[field];
    if (!picker) return;
    if (!invoke) {
        appendTableKitConsoleEntry('warning', '当前不在 Tauri 环境中，无法打开系统路径选择器。');
        renderTableKitRegistryStatus();
        return;
    }

    const status = getTableKitLubanStatus();
    const projectRoot = getTableKitProjectRoot(status);
    const currentValue = normalizeTableKitPath(tableKitConfig[field]);
    const resolvedCurrent = projectRoot ? resolveTableKitProjectPath(projectRoot, currentValue) : currentValue;
    const initialPath = resolvedCurrent;
    const fieldLabel = getTableKitPathFieldLabel(field);

    try {
        appendTableKitConsoleEntry('info', `${fieldLabel} 路径选择器起点：${initialPath || projectRoot || '--'}`);
        const selected = picker.kind === 'file'
            ? await invoke('pick_file', { initialPath, extension: picker.extension || '', projectRoot })
            : await invoke('pick_folder', { initialPath, projectRoot });
        if (!selected) return;

        const nextPath = normalizeTableKitPickedPath(selected, projectRoot);
        appendTableKitConsoleEntry('info', `${fieldLabel} 已选择：${nextPath}`);
        updateTableKitConfigField(field, nextPath);
    } catch (error) {
        appendTableKitConsoleEntry('error', `${fieldLabel} 选择失败：${String(error?.message ?? error ?? '未知错误')}`);
        renderTableKitRegistryStatus();
    }
}

async function pickTableKitExtraOutputFolder(index) {
    const targetIndex = Number(index);
    if (!Number.isInteger(targetIndex) || targetIndex < 0) return;
    if (!invoke) {
        appendTableKitConsoleEntry('warning', '当前不在 Tauri 环境中，无法打开系统路径选择器。');
        renderTableKitRegistryStatus();
        return;
    }

    const target = (tableKitConfig.extraOutputTargets || [])[targetIndex];
    if (!target) return;

    const status = getTableKitLubanStatus();
    const projectRoot = getTableKitProjectRoot(status);
    const initialPath = projectRoot ? resolveTableKitProjectPath(projectRoot, target.outputDataDir) : normalizeTableKitPath(target.outputDataDir);

    try {
        appendTableKitConsoleEntry('info', `额外输出目标 ${targetIndex + 1} 路径选择器起点：${initialPath || projectRoot || '--'}`);
        const selected = await invoke('pick_folder', { initialPath, projectRoot });
        if (!selected) return;

        const nextPath = normalizeTableKitPickedPath(selected, projectRoot);
        appendTableKitConsoleEntry('info', `额外输出目标 ${targetIndex + 1} 已选择：${nextPath}`);
        updateTableKitExtraOutputField(targetIndex, 'outputDataDir', nextPath);
    } catch (error) {
        appendTableKitConsoleEntry('error', `额外输出目标 ${targetIndex + 1} 选择失败：${String(error?.message ?? error ?? '未知错误')}`);
        renderTableKitRegistryStatus();
    }
}
