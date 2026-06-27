// pages/eventkit-monitor.js
// EventKit 监控视图：关系图、详情、时间线和局部 DOM 更新。
function renderEventKitMonitorContent(snapshot) {
    const view = buildEventKitMonitorView(snapshot);
    return renderEventKitMonitorHtml(view);
}

function buildEventKitMonitorView(snapshot, options = {}) {
    if (!connected || !getSelectedEventKitEngine()) {
        return {
            state: 'disconnected',
            html: emptyState('event', t('eventkit.need_runtime_bridge')),
        };
    }

    if (!snapshot) {
        return {
            state: 'loading',
            html: `<div class="eventkit-loading">${t('eventkit.loading_snapshot')}</div>`,
        };
    }

    const counts = normalizeEventKitCounts(snapshot.counts);
    const regs = normalizeEventKitRegistrations(snapshot.registrations);
    const recentEvents = normalizeEventKitRecentEvents(snapshot.recentEvents);
    if (options.prepareAnimation !== false) {
        prepareEventKitMonitorAnimation(recentEvents);
    }
    const scanEvents = normalizeEventKitScanEvents(eventKitScanCache?.events);
    const hasScanData = !!eventKitScanCache && !eventKitScanCache.error && Array.isArray(eventKitScanCache.events);
    const rows = hasScanData && scanEvents.length ? buildEventKitMonitorRows(regs, recentEvents, scanEvents) : [];
    const filteredRows = filterEventKitUnifiedRows(rows);
    selectedEventKitMonitorKey = ensureEventKitSelection(filteredRows, selectedEventKitMonitorKey);
    const selectedRow = filteredRows.find(row => row.id === selectedEventKitMonitorKey) || filteredRows[0] || null;
    const engine = getSelectedEventKitEngine();
    const emptyMessage = rows.length
        ? t('eventkit.no_match_filter')
        : formatEventKitTopologyEmptyMessage(hasScanData, scanEvents);
    return {
        state: 'ready',
        counts: counts,
        regs: regs,
        recentEvents: recentEvents,
        scanEvents: scanEvents,
        hasScanData: hasScanData,
        rows: rows,
        filteredRows: filteredRows,
        selectedRow: selectedRow,
        selectedKey: selectedEventKitMonitorKey,
        engine: engine,
        emptyMessage: emptyMessage,
        html: renderEventKitMonitorHtml({
            counts: counts,
            recentEvents: recentEvents,
            scanEvents: scanEvents,
            hasScanData: hasScanData,
            rows: rows,
            filteredRows: filteredRows,
            selectedRow: selectedRow,
            selectedKey: selectedEventKitMonitorKey,
            engine: engine,
            emptyMessage: emptyMessage,
        }),
    };
}

function renderEventKitMonitorHtml(view) {
    if (view.state === 'disconnected' || view.state === 'loading') {
        return view.html;
    }

    return `
        <div class="eventkit-v1-layout eventkit-v1-layout--monitor">
            <div class="eventkit-v1-main">
                <section class="eventkit-v1-map">
                    ${eventKitRelationHeader(t('eventkit.sender_code'), t('eventkit.event_unregister'), t('eventkit.receiver_code'))}
                    <div class="eventkit-v1-relations" data-eventkit-region="flow">
                        ${view.filteredRows.length ? view.filteredRows.map(row => renderEventKitMonitorRow(row, row.id === view.selectedKey)).join('') : emptyState('event', view.emptyMessage)}
                    </div>
                </section>
                <aside class="eventkit-v1-side">
                    ${renderEventKitMonitorDetail(view.selectedRow, view.counts, view.recentEvents, view.emptyMessage)}
                </aside>
            </div>
        </div>`;
}

function scheduleEventKitMonitorPartialRender() {
    if (eventKitMonitorRenderFrame) return;
    eventKitMonitorRenderFrame = requestAnimationFrame(() => {
        eventKitMonitorRenderFrame = 0;
        renderEventKitMonitorPartialUpdate();
    });
}

function renderEventKitMonitorPartialUpdate() {
    if (activePage !== 'eventkit' || eventKitActiveTab !== 'monitor') return;
    const root = $pageBody.querySelector('[data-eventkit-workbench="root"]');
    const flow = root?.querySelector('[data-eventkit-region="flow"]');
    const side = root?.querySelector('.eventkit-v1-side');
    if (!root || !flow || !side) {
        renderEventKitWorkbench();
        return;
    }

    const view = buildEventKitMonitorView(eventKitMonitorCache, { prepareAnimation: true });
    if (view.state !== 'ready') {
        renderEventKitWorkbench();
        return;
    }
    if (!view.filteredRows.length) {
        eventKitMonitorPendingRowIds.clear();
        eventKitMonitorLastAnimatedDomRowKey = eventKitAnimatedRowKey;
        return;
    }

    const dirtyRowIds = new Set(eventKitMonitorPendingRowIds);
    eventKitMonitorPendingRowIds.clear();
    if (eventKitMonitorLastAnimatedDomRowKey && eventKitMonitorLastAnimatedDomRowKey !== eventKitAnimatedRowKey) {
        dirtyRowIds.add(eventKitMonitorLastAnimatedDomRowKey);
    }
    if (eventKitAnimatedRowKey) {
        dirtyRowIds.add(eventKitAnimatedRowKey);
    }

    let updated = false;
    const rowElements = Array.from(flow.querySelectorAll('[data-eventkit-monitor-row]'));
    for (const row of view.filteredRows) {
        if (!dirtyRowIds.has(row.id)) continue;
        const rowElement = rowElements.find(element => element.dataset.eventkitMonitorRow === row.id);
        if (!rowElement) continue;
        const nextRowHtml = renderEventKitMonitorRow(row, row.id === view.selectedKey);
        if (rowElement.outerHTML === nextRowHtml) continue;
        rowElement.outerHTML = nextRowHtml;
        updated = true;
    }

    if (!updated) {
        eventKitMonitorLastAnimatedDomRowKey = eventKitAnimatedRowKey;
        return;
    }

    const nextDetailHtml = renderEventKitMonitorDetail(view.selectedRow, view.counts, view.recentEvents, view.emptyMessage);
    if (side.innerHTML !== nextDetailHtml) {
        side.innerHTML = nextDetailHtml;
    }

    bindEventKitWorkbenchActions();
    eventKitMonitorLastAnimatedDomRowKey = eventKitAnimatedRowKey;
}

function updateEventKitMonitorSelection(value) {
    selectedEventKitMonitorKey = value;
    const root = $pageBody.querySelector('[data-eventkit-workbench="root"]');
    const side = root?.querySelector('.eventkit-v1-side');
    if (!root || !side) {
        renderEventKitWorkbench();
        return;
    }

    const view = buildEventKitMonitorView(eventKitMonitorCache, { prepareAnimation: false });
    if (view.state !== 'ready') {
        renderEventKitWorkbench();
        return;
    }

    root.querySelectorAll('[data-eventkit-monitor-row]').forEach(row => {
        row.classList.toggle('active', row.dataset.eventkitMonitorRow === view.selectedKey);
    });

    const nextDetailHtml = renderEventKitMonitorDetail(view.selectedRow, view.counts, view.recentEvents, view.emptyMessage);
    if (side.innerHTML !== nextDetailHtml) {
        side.innerHTML = nextDetailHtml;
    }
    bindEventKitWorkbenchActions();
}

function formatEventKitTopologyEmptyMessage(hasScanData, scanEvents) {
    if (eventKitScanInFlight) return t('eventkit.scan_in_progress');
    if (eventKitScanCache?.error) return t('eventkit.scan_error_hint');
    if (!hasScanData) return t('eventkit.auto_scan_preparing');
    if (!scanEvents.length) return t('eventkit.no_usage_found');
    return t('eventkit.no_relations');
}

function eventKitPanelHeader(title, description, meta = '') {
    return `<div class="eventkit-v1-panel-head">
        <div>
            <div class="eventkit-section-title">${escapeHtml(title)}</div>
            <div class="eventkit-section-desc">${escapeHtml(description)}</div>
        </div>
        ${meta ? `<span class="eventkit-v1-panel-meta">${escapeHtml(meta)}</span>` : ''}
    </div>`;
}

function eventKitRelationHeader(left, center, right) {
    return `<div class="eventkit-v1-relation-head">
        <span class="eventkit-v1-relation-head__left">${escapeHtml(left)}</span>
        <span class="eventkit-v1-relation-head__center">${escapeHtml(center)}</span>
        <span class="eventkit-v1-relation-head__right">${escapeHtml(right)}</span>
    </div>`;
}

function eventKitStatChip(label, value, tone = '') {
    return `<div class="eventkit-v1-stat eventkit-v1-stat--${escapeHtml(tone)}">
        <span>${escapeHtml(label)}</span>
        <strong>${escapeHtml(value)}</strong>
    </div>`;
}

function renderEventKitHeroActions() {
    const engine = getSelectedEventKitEngine();
    const scan = eventKitScanCache;
    const projectPath = scan?.projectPath ?? engine?.projectPath ?? latestStatusSummary?.projectPath ?? '--';
    const scanState = eventKitScanInFlight
        ? t('eventkit.scanning')
        : (scan?.error ? t('eventkit.scan_failed_short') : (scan ? `${escapeHtml(scan.matchedFileCount ?? 0)} matched / ${escapeHtml(scan.scannedFileCount ?? 0)} files` : t('eventkit.not_scanned')));
    return `<div class="eventkit-hero-actions">
        <div class="eventkit-hero-status">
            <span>${escapeHtml(projectPath)}</span>
            <strong>${scanState}</strong>
            ${scan?.error ? `<div class="eventkit-v1-note eventkit-v1-note--warning">${escapeHtml(scan.error)}</div>` : ''}
        </div>
        <div class="eventkit-hero-controls">
            ${renderEventKitEngineInline(engine)}
            ${renderKitToggle(t('eventkit.exclude_editor'), eventKitScanExcludeEditor, 'data-eventkit-exclude-editor')}
            <label class="eventkit-scan-search">
                ${svgIcon('search', 'shell-icon')}
                <input data-eventkit-scan-search value="${escapeHtml(eventKitScanSearchTerm)}" placeholder="${t('eventkit.search_sender_receiver')}">
            </label>
            <button class="btn btn-sm" onclick="refreshEventKit()">${t('eventkit.refresh_runtime')}</button>
            <button class="btn btn-primary btn-sm" onclick="runEventKitCodeScan()"${eventKitScanInFlight ? ' disabled' : ''}>${eventKitScanInFlight ? t('eventkit.scanning') : t('eventkit.scan_code')}</button>
        </div>
    </div>`;
}

function prepareEventKitMonitorAnimation(recentEvents) {
    const latestSend = recentEvents
        .slice()
        .reverse()
        .find(event => String(event.kind ?? event.event ?? '').toLowerCase() === 'send');
    const signature = latestSend ? makeEventKitRecentEventSignature(latestSend) : null;

    eventKitAnimatedRowKey = null;
    if (signature && eventKitMonitorAnimationPrimed && signature !== eventKitLastObservedEventSignature) {
        eventKitAnimatedRowKey = makeEventKitRelationId(latestSend.channel, latestSend.eventKey ?? latestSend.key, latestSend.payloadType);
    }

    eventKitLastObservedEventSignature = signature;
    eventKitMonitorAnimationPrimed = true;
}

function makeEventKitRecentEventSignature(event) {
    return [
        event.id ?? '',
        event.seq ?? '',
        String(event.kind ?? event.event ?? '').toLowerCase(),
        normalizeEventKitChannel(event.channel),
        event.eventKey ?? event.key ?? '--',
        event.time ?? '',
        event.handler ?? '',
        event.payloadType ?? '',
        event.sourceFile ?? '',
        event.sourceLine ?? '',
    ].join('|');
}

function renderEventKitFlightOverlay() {
    return `<div class="eventkit-v1-flight" aria-hidden="true">
        <span class="eventkit-v1-flight__beam eventkit-v1-flight__beam--in"></span>
        <span class="eventkit-v1-flight__beam eventkit-v1-flight__beam--out"></span>
        <span class="eventkit-v1-flight__pulse eventkit-v1-flight__pulse--sender"></span>
        <span class="eventkit-v1-flight__pulse eventkit-v1-flight__pulse--receiver"></span>
    </div>`;
}

function renderEventKitMonitorRow(row, selected) {
    const channel = normalizeEventKitChannel(row.channel);
    const channelClass = eventKitChannelClass(channel);
    const healthClass = eventKitHealthClass(row.health);
    const animated = eventKitAnimatedRowKey === row.id && row.sendCount > 0;
    return `<article class="eventkit-v1-row eventkit-v1-row--${escapeHtml(channelClass)} ${escapeHtml(healthClass)}${selected ? ' active' : ''}${animated ? ' eventkit-v1-row--animated' : ''}" data-eventkit-monitor-row="${escapeHtml(row.id)}" role="button" tabindex="0">
        ${animated ? renderEventKitFlightOverlay() : ''}
        <div class="eventkit-v1-sidecell eventkit-v1-sidecell--sender">
            ${renderEventKitMonitorSender(row)}
        </div>
        <div class="eventkit-v1-event-column">
            <div class="eventkit-v1-event-node">
                <div class="eventkit-v1-node-badges">
                    <span class="eventkit-channel eventkit-channel--${escapeHtml(channelClass)}">${escapeHtml(channel)}</span>
                    <span class="eventkit-health ${escapeHtml(healthClass)}">${escapeHtml(formatEventKitHealth(row.health))}</span>
                    ${renderEventKitPayloadBadge(row)}
                </div>
                <strong>${escapeHtml(row.key)}</strong>
                <div class="eventkit-v1-node-meta">
                    <span>Runtime ${escapeHtml(row.sendCount)} send</span>
                    <span>Code S${escapeHtml(row.staticSendCount)} / R${escapeHtml(row.staticRegisterCount)} / U${escapeHtml(row.staticUnregisterCount)}</span>
                </div>
            </div>
            ${renderEventKitMonitorUnregisters(row)}
        </div>
        <div class="eventkit-v1-sidecell eventkit-v1-sidecell--receiver">
            ${renderEventKitMonitorReceivers(row)}
        </div>
    </article>`;
}

function renderEventKitMonitorSender(row) {
    if (row.senderLocations.length) {
        return `<div class="eventkit-v1-code-stack">
            ${renderEventKitLocationButtons(row.senderLocations, 'send', row.latestSourceLocation, row.latestSourceInferred, { maxGroups: 3 })}
            ${row.sendCount > 0 ? `<div class="eventkit-v1-code-stack__runtime">${t('eventkit.trigger_suffix', row.sendCount)}${row.lastTime ? ' · ' + escapeHtml(row.lastTime) : ''}</div>` : ''}
        </div>`;
    }

    return `<div class="eventkit-v1-endpoint eventkit-v1-endpoint--muted">
        <span>${t('eventkit.no_sender_scanned')}</span>
        <strong>${row.sendCount > 0 ? t('eventkit.runtime_trigger_count', row.sendCount) : t('eventkit.waiting_scan')}</strong>
        ${row.lastTime ? `<em>${escapeHtml(row.lastTime)}</em>` : ''}
    </div>`;
}

function renderEventKitMonitorReceivers(row) {
    if (row.receiverLocations.length) {
        return `<div class="eventkit-v1-code-stack">
            ${renderEventKitLocationButtons(row.receiverLocations, 'receive', null, false, { maxGroups: 3 })}
        </div>`;
    }

    return `<div class="eventkit-v1-endpoint eventkit-v1-endpoint--warning">
        <span>${t('eventkit.no_receiver_scanned')}</span>
        <strong>${t('eventkit.unregistered')}</strong>
    </div>`;
}

function renderEventKitMonitorUnregisters(row) {
    if (row.unregisterLocations.length) {
        return `<div class="eventkit-v1-unregister-stack">
            <div class="eventkit-v1-unregister-stack__head">
                <span>${t('eventkit.unregister_party')}</span>
                <strong>${escapeHtml(row.unregisterLocations.length)}</strong>
            </div>
            ${renderEventKitLocationButtons(row.unregisterLocations, 'unregister', null, false, { maxGroups: 3 })}
            ${row.unregisterActivityCount > 0 ? `<div class="eventkit-v1-code-stack__runtime">${t('eventkit.runtime_unregister_count', row.unregisterActivityCount)}</div>` : ''}
        </div>`;
    }

    return `<div class="eventkit-v1-unregister-empty">
        <span>UnRegister</span>
        <strong>${row.unregisterActivityCount > 0 ? t('eventkit.runtime_unregister_count', row.unregisterActivityCount) : t('eventkit.no_unregister_scanned')}</strong>
    </div>`;
}

function renderEventKitScanUnregisters(files) {
    if (files.length) {
        return `<div class="eventkit-v1-unregister-stack">
            <div class="eventkit-v1-unregister-stack__head">
                <span>${t('eventkit.unregister_party')}</span>
                <strong>${escapeHtml(files.length)}</strong>
            </div>
            ${renderEventKitLocationList(files, '', 'unregister', { maxGroups: 3 })}
        </div>`;
    }

    return `<div class="eventkit-v1-unregister-empty">
        <span>UnRegister</span>
        <strong>${t('eventkit.no_unregister_scanned')}</strong>
    </div>`;
}

function renderEventKitMonitorDetail(row, counts, recentEvents, emptyMessage = t('eventkit.select_event_hint')) {
    if (!row) {
        return `<section class="eventkit-v1-detail eventkit-v1-detail-card">${emptyState('event', emptyMessage)}</section>`;
    }

    const matchingEvents = row.recentEvents.length ? row.recentEvents.slice().reverse() : [];
    const channelClass = eventKitChannelClass(row.channel);
    const healthClass = eventKitHealthClass(row.health);
    return `
        <section class="eventkit-v1-detail eventkit-v1-detail-card">
            <div class="eventkit-v1-detail-top eventkit-v1-detail-top--${escapeHtml(channelClass)}">
                <div class="eventkit-v1-detail-badges">
                    <span class="eventkit-channel eventkit-channel--${escapeHtml(channelClass)}">${escapeHtml(row.channel)}</span>
                    <span class="eventkit-health ${escapeHtml(healthClass)}">${escapeHtml(formatEventKitHealth(row.health))}</span>
                    ${renderEventKitPayloadBadge(row)}
                </div>
                <h3>${escapeHtml(row.key)}</h3>
            </div>
            <div class="eventkit-v1-detail-stats">
                ${eventKitStatChip(t('eventkit.stat_cumulative'), row.sendCount, 'send')}
                ${eventKitStatChip(t('eventkit.stat_recent'), matchingEvents.length, 'history')}
                ${eventKitStatChip(t('eventkit.stat_static'), row.senderLocations.length + row.receiverLocations.length + row.unregisterLocations.length, 'handler')}
            </div>
            <div class="eventkit-v1-detail-scroll">
                ${eventKitPanelHeader(t('eventkit.timeline_title'), t('eventkit.timeline_desc'), matchingEvents.length + ' ' + t('common.items'))}
                <div class="eventkit-v1-timeline" data-eventkit-region="timeline">
                    ${matchingEvents.length ? matchingEvents.map(renderEventKitTimelineItem).join('') : emptyState('status', t('eventkit.no_event_history'))}
                </div>
            </div>
            ${row.deprecated ? `<div class="eventkit-v1-note eventkit-v1-note--warning">${t('eventkit.string_deprecated')}</div>` : ''}
        </section>`;
}

function renderEventKitTimelineItem(event) {
    const channel = normalizeEventKitChannel(event.channel);
    const kind = event.kind ?? event.event ?? 'event';
    const sourceLocation = normalizeEventKitLocation(event.sourceFile, event.sourceLine);
    return `<div class="eventkit-v1-timeline-item eventkit-v1-timeline-item--${escapeHtml(String(kind).toLowerCase())}">
        <span class="eventkit-v1-timeline-item__time">${escapeHtml(event.time ?? '--')}</span>
        <span class="eventkit-channel eventkit-channel--${escapeHtml(channel.toLowerCase())}">${escapeHtml(channel)}</span>
        <span class="eventkit-v1-timeline-item__kind">${escapeHtml(formatEventKitKind(kind))}</span>
        <strong>${escapeHtml(event.eventKey ?? event.key ?? '--')}</strong>
        ${event.handler ? `<span class="eventkit-v1-timeline-item__payload">${escapeHtml(event.handler)}</span>` : ''}
        ${event.payloadType ? `<span class="eventkit-v1-timeline-item__payload">${escapeHtml(event.payloadType)}</span>` : ''}
        ${sourceLocation ? `<button class="eventkit-v1-timeline-source" data-eventkit-open-file="${escapeHtml(sourceLocation.filePath)}" data-eventkit-open-line="${escapeHtml(sourceLocation.line ?? '')}">${escapeHtml(formatEventKitFileRef(sourceLocation))}</button>` : ''}
    </div>`;
}

function normalizeEventKitChannel(channel) {
    const raw = String(channel ?? '--');
    if (raw.toLowerCase() === 'type') return 'Type';
    if (raw.toLowerCase() === 'enum') return 'Enum';
    if (raw.toLowerCase() === 'string') return 'String';
    return raw || '--';
}

function eventKitChannelClass(channel) {
    const normalized = normalizeEventKitChannel(channel).toLowerCase();
    if (normalized === 'type' || normalized === 'enum' || normalized === 'string') return normalized;
    return 'unknown';
}

function formatEventKitKind(kind) {
    switch (String(kind).toLowerCase()) {
        case 'send': return t('eventkit.kind_send');
        case 'register': return t('eventkit.kind_register');
        case 'unregister': return t('eventkit.kind_unregister');
        default: return kind || 'event';
    }
}

