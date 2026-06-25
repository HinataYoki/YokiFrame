// pages/eventkit-scan.js
// EventKit C# 代码扫描、源码跳转和扫描结果导航。
function makeEventKitScanKey(engineId = getSelectedEventKitEngine()?.engineId) {
    return engineId ? `${engineId}|${eventKitScanExcludeEditor ? 'exclude-editor' : 'include-editor'}` : '';
}

function scheduleEventKitAutoScan() {
    setTimeout(() => {
        void ensureEventKitAutoScan();
    }, 0);
}

async function ensureEventKitAutoScan() {
    const engine = getSelectedEventKitEngine();
    const key = makeEventKitScanKey(engine?.engineId);
    if (!invoke || !connected || !engine?.engineId || eventKitScanInFlight || !key) return;
    if (eventKitScanCache && eventKitScanCacheKey === key) return;
    if (eventKitAutoScanAttemptedKey === key) return;

    eventKitAutoScanAttemptedKey = key;
    await runEventKitCodeScan({ auto: true });
}

async function runEventKitCodeScan(options = {}) {
    const engine = getSelectedEventKitEngine();
    if (!invoke || !connected || !engine?.engineId || eventKitScanInFlight) return;
    const scanKey = makeEventKitScanKey(engine.engineId);
    eventKitScanInFlight = true;
    renderEventKitWorkbench();
    try {
        const raw = await invoke('scan_eventkit_code', { engineId: engine.engineId, excludeEditor: eventKitScanExcludeEditor });
        eventKitScanCache = JSON.parse(raw);
        eventKitScanCacheKey = scanKey;
    } catch (e) {
        eventKitScanCache = { error: String(e), events: [], files: [], summary: {} };
        eventKitScanCacheKey = scanKey;
        if (!options.auto) {
            eventKitAutoScanAttemptedKey = '';
        }
    } finally {
        eventKitScanInFlight = false;
        renderEventKitWorkbench();
    }
}

async function openEventKitCodeLocation(filePath, line) {
    const engine = getSelectedEventKitEngine();
    if (!invoke || !engine?.engineId || !filePath) return;
    try {
        await invoke('open_eventkit_code_location', {
            engineId: engine.engineId,
            filePath,
            line: Number(line || 0) || null,
        });
        addLog(t('source.open', filePath + (line ? ':' + line : '')), 'system');
    } catch (e) {
        addLog(t('source.open_failed', e), 'error');
    }
}

function renderEventKitScanPanel(scan) {
    if (!connected || !getSelectedEventKitEngine()) {
        return emptyState('search', t('eventkit.need_engine_scan'));
    }

    if (eventKitScanInFlight) {
        return `<div class="eventkit-v1-layout eventkit-v1-layout--scan">
            ${renderEventKitScanToolbar(scan)}
            <div class="eventkit-loading">${t('eventkit.scanning_csharp')}</div>
        </div>`;
    }

    if (!scan) {
        return `<div class="eventkit-v1-layout eventkit-v1-layout--scan">
            ${renderEventKitScanToolbar(scan)}
            <div class="eventkit-scan-shell eventkit-scan-shell--empty">
                ${emptyState('search', t('eventkit.auto_scan_hint'))}
            </div>
        </div>`;
    }

    if (scan.error) {
        return `<div class="eventkit-v1-layout eventkit-v1-layout--scan">
            ${renderEventKitScanToolbar(scan)}
            ${panel(t('eventkit.scan_failed_title'), `<span style="color:var(--error)">${escapeHtml(scan.error)}</span>`, 'warning')}
        </div>`;
    }

    return renderEventKitScanResults(scan);
}

function renderEventKitScanToolbar(scan) {
    const projectPath = scan?.projectPath ?? getSelectedEventKitEngine()?.projectPath ?? latestStatusSummary?.projectPath ?? '--';
    const scannedFileCount = scan?.scannedFileCount ?? 0;
    const matchedFileCount = scan?.matchedFileCount ?? 0;
    const scanMeta = scan
        ? `${escapeHtml(scannedFileCount)} files · ${escapeHtml(matchedFileCount)} matched`
        : t('eventkit.waiting_for_scan');
    return `<div class="eventkit-v1-scan-toolbar">
        <div>
            <div class="eventkit-section-title">${t('eventkit.code_scan_title')}</div>
            <div class="eventkit-section-desc">${t('eventkit.code_scan_desc')}</div>
            <div class="eventkit-scan-toolbar__hint">${escapeHtml(projectPath)} · ${scanMeta}</div>
        </div>
        <div class="eventkit-v1-scan-controls">
            ${renderEventKitEngineInline(getSelectedEventKitEngine())}
            <div class="eventkit-v1-scan-scope">
                <span>${t('eventkit.scan_scope')}</span>
                <strong>Assets</strong>
            </div>
            ${renderKitToggle(t('eventkit.exclude_editor'), eventKitScanExcludeEditor, 'data-eventkit-exclude-editor')}
            <label class="eventkit-scan-search">
                ${svgIcon('search', 'shell-icon')}
                <input data-eventkit-scan-search value="${escapeHtml(eventKitScanSearchTerm)}" placeholder="${t('eventkit.search_event_file')}">
            </label>
            <button class="btn btn-primary btn-sm" onclick="runEventKitCodeScan()"${eventKitScanInFlight ? ' disabled' : ''}>${eventKitScanInFlight ? t('eventkit.scanning') : t('eventkit.scan_code')}</button>
        </div>
    </div>`;
}

function renderEventKitScanResults(scan) {
    const summary = scan.summary ?? {};
    const unmatchedSendCount = scan.unmatchedSendCount ?? summary.unmatchedSendCount ?? 0;
    const unmatchedRegisterCount = scan.unmatchedRegisterCount ?? summary.unmatchedRegisterCount ?? 0;
    const allEvents = Array.isArray(scan.events) ? scan.events : [];
    const events = filterEventKitScanEvents(allEvents);
    selectedEventKitScanKey = ensureEventKitSelection(events.map(normalizeEventKitScanRow), selectedEventKitScanKey);
    const selected = events.find(event => makeEventKitRelationId(event.channel, event.eventKey, event.payloadType) === selectedEventKitScanKey) || events[0] || null;
    return `
        <div class="eventkit-v1-layout eventkit-v1-layout--scan">
            ${renderEventKitScanToolbar(scan)}
            <div class="eventkit-v1-scan-summary">
                ${eventKitStatChip('Send', summary.sendCount ?? 0, 'send')}
                ${eventKitStatChip('Register', summary.registerCount ?? 0, 'receive')}
                ${eventKitStatChip(t('eventkit.unmatched_receivers'), unmatchedSendCount, 'danger')}
                ${eventKitStatChip(t('eventkit.unmatched_senders'), unmatchedRegisterCount, 'warning')}
                ${eventKitStatChip(t('eventkit.unregister_risk'), summary.leakRiskCount ?? 0, 'warning')}
                ${eventKitStatChip('String', summary.deprecatedStringEventCount ?? 0, 'string')}
            </div>
            <div class="eventkit-v1-main">
                <section class="eventkit-v1-map">
                    ${eventKitRelationHeader(t('eventkit.send_file'), t('eventkit.event_node'), t('eventkit.receive_file'))}
                    <div class="eventkit-v1-scan-map">
                        ${events.length ? events.map(event => renderEventKitScanRelation(event, selected)).join('') : emptyState('search', t('eventkit.no_match_events'))}
                    </div>
                </section>
                <aside class="eventkit-v1-side">
                    ${renderEventKitScanNavigator(events, selected, scan)}
                </aside>
            </div>
        </div>`;
}

function normalizeEventKitScanRow(event) {
    return {
        id: makeEventKitRelationId(event.channel, event.eventKey, event.payloadType),
        channel: normalizeEventKitChannel(event.channel),
        key: event.eventKey ?? '--',
    };
}

function renderEventKitScanRelation(event, selected) {
    const id = makeEventKitRelationId(event.channel, event.eventKey, event.payloadType);
    const isSelected = selected && makeEventKitRelationId(selected.channel, selected.eventKey, selected.payloadType) === id;
    const channel = normalizeEventKitChannel(event.channel);
    const channelClass = eventKitChannelClass(channel);
    const health = event.health ?? 'balanced';
    const healthClass = eventKitHealthClass(health);
    const sendFiles = eventKitEventLocations(event, 'send');
    const registerFiles = eventKitEventLocations(event, 'register');
    const unregisterFiles = eventKitEventLocations(event, 'unregister');
    return `<article class="eventkit-v1-row eventkit-v1-row--${escapeHtml(channelClass)} ${escapeHtml(healthClass)}${isSelected ? ' active' : ''}" data-eventkit-scan-row="${escapeHtml(id)}" role="button" tabindex="0">
        <div class="eventkit-v1-sidecell eventkit-v1-sidecell--sender">
            ${renderEventKitLocationList(sendFiles, t('eventkit.no_send_source'), 'send', { maxGroups: 3 })}
        </div>
        <div class="eventkit-v1-event-column">
            <div class="eventkit-v1-event-node">
                <div class="eventkit-v1-node-badges">
                    <span class="eventkit-channel eventkit-channel--${escapeHtml(channelClass)}">${escapeHtml(channel)}</span>
                    <span class="eventkit-health ${escapeHtml(healthClass)}">${escapeHtml(formatEventKitHealth(health))}</span>
                    ${renderEventKitPayloadBadge({ channel, key: event.eventKey, payloadType: event.payloadType })}
                </div>
                <strong>${escapeHtml(event.eventKey ?? '--')}</strong>
                <div class="eventkit-v1-node-meta">
                    <span>S ${escapeHtml(event.sendCount ?? 0)}</span>
                    <span>R ${escapeHtml(event.registerCount ?? 0)}</span>
                    <span>U ${escapeHtml(event.unregisterCount ?? 0)}</span>
                </div>
            </div>
            ${renderEventKitScanUnregisters(unregisterFiles)}
        </div>
        <div class="eventkit-v1-sidecell eventkit-v1-sidecell--receiver">
            ${renderEventKitLocationList(registerFiles, t('eventkit.no_receive_source'), 'receive', { maxGroups: 3 })}
        </div>
    </article>`;
}

function renderEventKitLocationList(files, emptyText, tone, options = {}) {
    const locations = normalizeEventKitLocationItems(files);
    if (!locations.length) {
        return emptyText ? `<div class="eventkit-v1-endpoint eventkit-v1-endpoint--${tone === 'receive' ? 'warning' : 'muted'}"><span>${escapeHtml(emptyText)}</span><strong>--</strong></div>` : '';
    }

    return renderEventKitLocationGroups(locations, tone, null, false, options);
}

function renderEventKitLocationButtons(locations, tone, activeLocation, inferred, options = {}) {
    const normalizedLocations = normalizeEventKitLocationItems(locations);
    if (!normalizedLocations.length) {
        return `<div class="eventkit-v1-location-list eventkit-v1-location-list--${escapeHtml(tone)}"><span>--</span></div>`;
    }

    return renderEventKitLocationGroups(normalizedLocations, tone, activeLocation, inferred, options);
}

function renderEventKitLocationGroups(locations, tone, activeLocation, inferred, options = {}) {
    const groups = groupEventKitLocationsByFile(locations);
    const maxGroups = Number.isFinite(options.maxGroups) ? Math.max(1, options.maxGroups) : groups.length;
    const visible = groups.slice(0, maxGroups);
    const more = groups.length - visible.length;
    return `<div class="eventkit-v1-location-list eventkit-v1-location-list--${escapeHtml(tone)}">
        ${visible.map(group => renderEventKitLocationGroup(group, tone, activeLocation, inferred)).join('')}
        ${more > 0 ? `<span class="eventkit-v1-location-list__more">${t('eventkit.files_count', more)}</span>` : ''}
    </div>`;
}

function renderEventKitLocationGroup(group, tone, activeLocation, inferred) {
    const lines = group.lines.length ? group.lines : [null];
    const activeInGroup = lines.some(line => isEventKitSameLocation({ filePath: group.filePath, line }, activeLocation));
    const activeClass = activeInGroup ? ' eventkit-v1-file-group--active' : '';
    const inferredClass = activeInGroup && inferred ? ' eventkit-v1-file-group--inferred' : '';
    return `<div class="eventkit-v1-file-group eventkit-v1-file-group--${escapeHtml(tone)}${activeClass}${inferredClass}">
        <div class="eventkit-v1-file-group__head">
            <div class="eventkit-v1-file-group__title">
                <span>${escapeHtml(formatEventKitFileName({ filePath: group.filePath }))}</span>
                <strong>${t('eventkit.locations_count', lines.length)}</strong>
            </div>
        </div>
        <div class="eventkit-v1-file-group__lines">
            ${lines.map(line => renderEventKitLocationLineButton(group.filePath, line, tone, isEventKitSameLocation({ filePath: group.filePath, line }, activeLocation), inferred)).join('')}
        </div>
    </div>`;
}

function renderEventKitLocationLineButton(filePath, line, tone, active, inferred) {
    const full = line ? `${filePath}:${line}` : filePath;
    const activeClass = active ? ' eventkit-v1-line-chip--active' : '';
    const inferredClass = active && inferred ? ' eventkit-v1-line-chip--inferred' : '';
    return `<button class="eventkit-v1-line-chip eventkit-v1-line-chip--${escapeHtml(tone)}${activeClass}${inferredClass}" data-eventkit-open-file="${escapeHtml(filePath)}" data-eventkit-open-line="${escapeHtml(line ?? '')}" title="${escapeHtml(full)}">
        <span>${escapeHtml(line ? `L${line}` : t('eventkit.open_file'))}</span>
    </button>`;
}

function normalizeEventKitLocationItems(items) {
    const locations = [];
    (Array.isArray(items) ? items : []).forEach(item => {
        const location = typeof item === 'object'
            ? normalizeEventKitLocation(item.file ?? item.path ?? item.filePath, item.line ?? item.lineNumber)
            : parseEventKitLocationRef(item);
        if (location) locations.push(location);
    });
    return mergeEventKitLocations([], locations);
}

function groupEventKitLocationsByFile(locations) {
    const groups = [];
    const groupMap = new Map();
    normalizeEventKitLocationItems(locations).forEach(location => {
        let group = groupMap.get(location.filePath);
        if (!group) {
            group = { filePath: location.filePath, lines: [] };
            groupMap.set(location.filePath, group);
            groups.push(group);
        }

        if (location.line && !group.lines.includes(location.line)) {
            group.lines.push(location.line);
            group.lines.sort((a, b) => a - b);
        }
    });
    return groups;
}

function formatEventKitFileName(location) {
    const path = String(location?.filePath ?? '--');
    const file = path.split('/').pop() || path;
    return location?.line ? `${file}:${location.line}` : file;
}

function formatEventKitFileRef(file) {
    if (!file || typeof file !== 'object') return file ?? '--';
    const filePath = file.file ?? file.path ?? file.filePath ?? '--';
    const line = file.line ?? file.lineNumber;
    return line ? filePath + ':' + line : filePath;
}

function eventKitEventLocations(event, kind) {
    const fieldsByKind = {
        send: ['sendFiles'],
        register: ['registerFiles'],
        unregister: ['unregisterFiles'],
        all: ['files'],
    };
    const fields = fieldsByKind[kind] || fieldsByKind.all;
    const locations = [];
    fields.forEach(field => {
        if (!Array.isArray(event[field])) return;
        event[field].forEach(file => {
            const value = formatEventKitFileRef(file);
            const location = typeof file === 'object'
                ? normalizeEventKitLocation(file.file ?? file.path ?? file.filePath, file.line ?? file.lineNumber)
                : parseEventKitLocationRef(value);
            if (location && value && value !== '--') locations.push(location);
        });
    });
    return mergeEventKitLocations([], locations);
}

function filterEventKitScanEvents(events) {
    const term = eventKitScanSearchTerm.trim().toLowerCase();
    if (!term) return events;
    return events.filter(event => {
        const haystack = [
            event.channel,
            event.eventKey,
            event.health,
            ...eventKitEventLocations(event, 'send').map(formatEventKitFileRef),
            ...eventKitEventLocations(event, 'register').map(formatEventKitFileRef),
            ...eventKitEventLocations(event, 'unregister').map(formatEventKitFileRef),
            ...eventKitEventLocations(event, 'all').map(formatEventKitFileRef),
        ].join(' ').toLowerCase();
        return haystack.includes(term);
    });
}

function formatEventKitHealth(health) {
    switch (health) {
        case 'no_receiver': return t('eventkit.health_no_receiver');
        case 'no_sender': return t('eventkit.health_no_sender');
        case 'leak_risk': return t('eventkit.health_leak_risk');
        default: return t('eventkit.health_balanced');
    }
}

function eventKitHealthClass(health) {
    return EVENTKIT_HEALTH_CLASS_BY_STATUS[health] || EVENTKIT_HEALTH_CLASS_BY_STATUS.balanced;
}

function renderEventKitScanNavigator(events, selected, scan) {
    const grouped = [
        [t('eventkit.nav_risk'), events.filter(event => event.health && event.health !== 'balanced'), 'warning'],
        ['Type', events.filter(event => normalizeEventKitChannel(event.channel) === 'Type'), 'type'],
        ['Enum', events.filter(event => normalizeEventKitChannel(event.channel) === 'Enum'), 'enum'],
        ['String', events.filter(event => normalizeEventKitChannel(event.channel) === 'String'), 'string'],
    ].filter(([, rows]) => rows.length);
    const selectedId = selected ? makeEventKitRelationId(selected.channel, selected.eventKey, selected.payloadType) : null;
    const selectedChannel = selected ? normalizeEventKitChannel(selected.channel) : '';
    const selectedHealth = selected ? selected.health ?? 'balanced' : 'balanced';
    return `
        <section class="eventkit-v1-detail">
            <div class="eventkit-v1-detail-top eventkit-v1-detail-top--scan">
                <div class="eventkit-v1-detail-badges">
                    <span class="eventkit-channel eventkit-channel--${escapeHtml(eventKitChannelClass(selectedChannel))}">${escapeHtml(selected ? selectedChannel : '--')}</span>
                    <span class="eventkit-health ${escapeHtml(eventKitHealthClass(selectedHealth))}">${escapeHtml(selected ? formatEventKitHealth(selectedHealth) : t('eventkit.select_from_graph'))}</span>
                    ${selected ? renderEventKitPayloadBadge({ channel: selectedChannel, key: selected.eventKey, payloadType: selected.payloadType }) : ''}
                </div>
                <h3>${escapeHtml(selected?.eventKey ?? t('eventkit.no_event_selected'))}</h3>
            </div>
            <div class="eventkit-v1-detail-stats">
                ${eventKitStatChip(t('eventkit.matched_events'), events.length, 'history')}
                ${eventKitStatChip(t('eventkit.matched_files'), scan.matchedFileCount ?? 0, 'handler')}
                ${eventKitStatChip(t('eventkit.scanned_files'), scan.scannedFileCount ?? 0, 'type')}
            </div>
            <div class="eventkit-v1-quick-list">
                ${grouped.length ? grouped.map(([label, rows, tone]) => renderEventKitScanNavGroup(label, rows, tone, selectedId)).join('') : emptyState('search', t('eventkit.no_navigable_events'))}
            </div>
        </section>`;
}

function renderEventKitScanNavGroup(label, rows, tone, selectedId) {
    return `<div class="eventkit-v1-nav-group eventkit-v1-nav-group--${escapeHtml(tone)}">
        <div class="eventkit-v1-nav-group__title"><span>${escapeHtml(label)}</span><strong>${escapeHtml(rows.length)}</strong></div>
        ${rows.map(event => {
            const id = makeEventKitRelationId(event.channel, event.eventKey, event.payloadType);
            const active = selectedId === id ? ' active' : '';
            return `<button class="eventkit-v1-nav-item${active}" data-eventkit-scan-row="${escapeHtml(id)}">
                <span>${escapeHtml(formatEventKitHealth(event.health ?? 'balanced'))}</span>
                <strong>${escapeHtml(event.eventKey ?? '--')}</strong>
            </button>`;
        }).join('')}
    </div>`;
}

function bindEventKitWorkbenchActions() {
    const selectEngine = engineId => {
        selectedEventKitEngineId = engineId;
        eventKitMonitorCache = null;
        eventKitScanCache = null;
        eventKitScanCacheKey = '';
        eventKitAutoScanAttemptedKey = '';
        selectedEventKitMonitorKey = null;
        selectedEventKitScanKey = null;
        resetEventKitMonitorRuntimeState();
        renderEventKitWorkbench();
        if (eventKitActiveTab === 'monitor') {
            loadEventRegistrations(currentPageLoadToken('eventkit'));
        }
        scheduleEventKitAutoScan();
    };

    const engineSelect = $pageBody.querySelector('[data-eventkit-engine-select]');
    if (engineSelect && engineSelect.dataset.bound !== '1') {
        engineSelect.dataset.bound = '1';
        engineSelect.addEventListener('change', () => {
            selectEngine(engineSelect.value);
        });
    }

    $pageBody.querySelectorAll('[data-eventkit-engine]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            selectEngine(button.dataset.eventkitEngine);
        });
    });

    const excludeEditor = $pageBody.querySelector('[data-eventkit-exclude-editor]');
    if (excludeEditor && excludeEditor.dataset.bound !== '1') {
        excludeEditor.dataset.bound = '1';
        excludeEditor.addEventListener('change', () => {
            eventKitScanExcludeEditor = !!excludeEditor.checked;
            eventKitScanCache = null;
            eventKitScanCacheKey = '';
            eventKitAutoScanAttemptedKey = '';
            selectedEventKitScanKey = null;
            renderEventKitWorkbench();
            scheduleEventKitAutoScan();
        });
    }

    const scanSearch = $pageBody.querySelector('[data-eventkit-scan-search]');
    if (scanSearch && scanSearch.dataset.bound !== '1') {
        scanSearch.dataset.bound = '1';
        scanSearch.addEventListener('input', () => {
            eventKitScanSearchTerm = scanSearch.value || '';
            renderEventKitWorkbench();
            const next = $pageBody.querySelector('[data-eventkit-scan-search]');
            if (next) {
                next.focus();
                next.setSelectionRange?.(next.value.length, next.value.length);
            }
        });
    }

    $pageBody.querySelectorAll('[data-eventkit-open-file]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', event => {
            event.preventDefault();
            event.stopPropagation();
            openEventKitCodeLocation(button.dataset.eventkitOpenFile, button.dataset.eventkitOpenLine);
        });
    });

    bindEventKitSelectableRows('[data-eventkit-monitor-row]', value => {
        updateEventKitMonitorSelection(value);
    });
    bindEventKitSelectableRows('[data-eventkit-scan-row]', value => {
        selectedEventKitScanKey = value;
        renderEventKitWorkbench();
    });
}

function bindEventKitSelectableRows(selector, select) {
    $pageBody.querySelectorAll(selector).forEach(row => {
        if (row.dataset.bound === '1') return;
        row.dataset.bound = '1';
        const activate = () => select(row.getAttribute(selector.slice(1, -1)) || row.dataset.eventkitMonitorRow || row.dataset.eventkitScanRow);
        row.addEventListener('click', activate);
        row.addEventListener('keydown', event => {
            if (event.key !== 'Enter' && event.key !== ' ') return;
            event.preventDefault();
            activate();
        });
    });
}
