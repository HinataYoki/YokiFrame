// pages/audiokit-mixer.js
// AudioKit 混音台、Voice 列表和播放历史渲染。
const AUDIO_STRIP_DETAIL_LIMIT = 12;

function renderAudioKitWorkbench(stats, buses, voices, history) {
    const normalizedBuses = normalizeAudioKitBuses(stats, buses, voices);
    const masterBus = normalizedBuses.find(item => item.isMaster) ?? { name: 'Master', volume: stats?.masterVolume ?? 1, effectiveVolume: stats?.masterVolume ?? 1, muted: false, activeVoiceCount: voices.length };
    return `<div class="kit-workbench kit-workbench--audio">
        <section class="audio-master-strip">
            <div class="audio-master-strip__main">
                <div class="kit-toolbar__title">${renderKitTitle('audio', '音频混音台')}</div>
                <div class="kit-toolbar__meta">Backend: ${escapeHtml(stats?.backendName ?? 'None')} · Bus ${escapeHtml(normalizedBuses.length)} · 活跃 ${escapeHtml(stats?.activeVoiceCount ?? voices.length ?? 0)} · 历史 ${escapeHtml(stats?.historyCount ?? history.length ?? 0)}</div>
            </div>
            <label class="audio-master-control">
                <span>Master</span>
                <input type="range" min="0" max="1" step="0.01" value="${escapeHtml(normalizeAudioVolume(masterBus.volume))}" data-audiokit-master-volume>
                <strong>${formatAudioVolume(masterBus.effectiveVolume ?? masterBus.volume)}</strong>
            </label>
            <div class="kit-toolbar__actions">
                <button class="btn btn-sm" data-audiokit-master-mute="${masterBus.muted ? 'false' : 'true'}">${masterBus.muted ? '取消静音' : '静音'}</button>
                <button class="btn btn-sm" data-audiokit-clear-history>清空历史</button>
                <button class="btn btn-sm" data-audiokit-stop-all>停止全部</button>
            </div>
        </section>
        <section class="audio-mixer-console">
            <div class="kit-panel__head">
                <div>
                    <div class="kit-panel__title">${renderKitTitle('audio', '实时混音台')}</div>
                    <div class="kit-panel__desc">通道横向滚动显示；AudioChannel 是内置快捷入口，自定义音量通道使用 string AudioBus。</div>
                </div>
                <span class="kit-panel__count">${escapeHtml(normalizedBuses.length)}</span>
            </div>
            <div class="audio-channel-rail" data-kit-scroll-key="audio-buses">${renderAudioChannelStrips(normalizedBuses, voices, history)}</div>
        </section>
    </div>`;
}

function normalizeAudioKitBuses(stats, buses, voices) {
    const normalized = [];
    const add = (bus) => {
        if (!bus?.name) return;
        if (normalized.some(item => String(item.name).toLowerCase() === String(bus.name).toLowerCase())) return;
        normalized.push({
            name: bus.name,
            volume: Number.isFinite(Number(bus.volume)) ? Number(bus.volume) : 1,
            effectiveVolume: Number.isFinite(Number(bus.effectiveVolume)) ? Number(bus.effectiveVolume) : (Number.isFinite(Number(bus.volume)) ? Number(bus.volume) : 1),
            muted: !!bus.muted,
            isMaster: !!bus.isMaster || String(bus.name).toLowerCase() === 'master',
            isDefault: !!bus.isDefault,
            activeVoiceCount: Number(bus.activeVoiceCount ?? 0) || 0
        });
    };
    [
        { name: 'Master', volume: stats?.masterVolume ?? 1, effectiveVolume: stats?.masterVolume ?? 1, isMaster: true, isDefault: true },
        { name: 'Music', volume: stats?.musicVolume ?? 1, effectiveVolume: stats?.musicVolume ?? 1, isDefault: true },
        { name: 'Sfx', volume: stats?.sfxVolume ?? 1, effectiveVolume: stats?.sfxVolume ?? 1, isDefault: true },
        { name: 'Voice', volume: stats?.voiceVolume ?? 1, effectiveVolume: stats?.voiceVolume ?? 1, isDefault: true },
        { name: 'Ambience', volume: stats?.ambienceVolume ?? 1, effectiveVolume: stats?.ambienceVolume ?? 1, isDefault: true },
        { name: 'UI', volume: stats?.uiVolume ?? 1, effectiveVolume: stats?.uiVolume ?? 1, isDefault: true },
    ].forEach(add);
    (Array.isArray(buses) ? buses : []).forEach(add);
    (Array.isArray(voices) ? voices : []).forEach(voice => add({ name: voice.bus, volume: 1, effectiveVolume: 1, activeVoiceCount: 0 }));
    normalized.forEach(bus => {
        if (bus.isMaster) {
            bus.activeVoiceCount = Array.isArray(voices) ? voices.length : bus.activeVoiceCount;
            return;
        }
        bus.activeVoiceCount = (Array.isArray(voices) ? voices : []).filter(voice => String(voice.bus).toLowerCase() === String(bus.name).toLowerCase()).length;
    });
    return normalized.sort((a, b) => audioBusSortOrder(a.name) - audioBusSortOrder(b.name) || String(a.name).localeCompare(String(b.name)));
}

function audioBusSortOrder(name) {
    const key = String(name || '').toLowerCase();
    if (key === 'master') return 0;
    if (key === 'music') return 1;
    if (key === 'sfx') return 2;
    if (key === 'voice') return 3;
    if (key === 'ambience') return 4;
    if (key === 'ui') return 5;
    return 100;
}

function renderAudioChannelStrips(buses, voices, history) {
    const allActive = audioKitState.selectedBus === 'All';
    const allStrip = `<button class="audio-channel-strip audio-channel-strip--all${allActive ? ' active' : ''}" type="button" data-audiokit-channel-strip="All" data-audiokit-voice-bus-filter="All">
        <div class="audio-strip-head">
            <strong>ALL</strong>
            <span>${escapeHtml(Array.isArray(voices) ? voices.length : 0)}</span>
        </div>
        <div class="audio-strip-actions">
            <span class="audio-strip-chip">全部</span>
            <span class="audio-strip-chip">Voices</span>
        </div>
        <div class="audio-strip-meter"><i style="height:${escapeHtml(formatAudioMeterWidth(1))}"></i></div>
        <div class="audio-strip-value">全部播放声部</div>
        <div class="audio-strip-playback">${renderAudioStripPlayback(voices)}</div>
        <div class="audio-strip-history">${renderAudioStripHistory(history)}</div>
    </button>`;
    return allStrip + (Array.isArray(buses) ? buses : []).map(bus => {
        const selected = String(bus.name) === String(audioKitState.selectedBus);
        const volume = normalizeAudioVolume(bus.volume);
        const effective = normalizeAudioVolume(bus.effectiveVolume);
        const busVoices = filterAudioKitVoicesByBus(voices, bus.name);
        const busHistory = filterAudioKitHistoryByBus(history, bus.name);
        return `<div class="audio-channel-strip${selected ? ' active' : ''}" data-audiokit-channel-strip="${escapeHtml(bus.name)}">
            <button class="audio-strip-select" type="button" data-audiokit-voice-bus-filter="${escapeHtml(bus.name)}">
                <span class="audio-strip-head">
                    <strong>${escapeHtml(formatAudioBusLabel(bus.name))}</strong>
                    <span>${escapeHtml(bus.activeVoiceCount ?? 0)}</span>
                </span>
                <em>${bus.isDefault ? 'Default Bus' : 'Custom Bus'}</em>
            </button>
            <div class="audio-strip-actions">
                <button class="audio-strip-button" type="button" data-audiokit-bus-mute="${escapeHtml(bus.name)}" data-audiokit-bus-muted="${bus.muted ? 'false' : 'true'}">${bus.muted ? '取消 M' : 'M'}</button>
                <button class="audio-strip-button audio-strip-button--danger" type="button" data-audiokit-stop-bus="${escapeHtml(bus.name)}" ${bus.isMaster ? 'disabled' : ''}>S</button>
            </div>
            <div class="audio-strip-body">
                <div class="audio-strip-meter"><i style="height:${escapeHtml(formatAudioMeterWidth(effective))}"></i></div>
                <label class="audio-strip-fader">
                    <input type="range" min="0" max="1" step="0.01" value="${escapeHtml(volume)}" data-audiokit-bus-volume="${escapeHtml(bus.name)}" ${bus.isMaster ? 'disabled' : ''}>
                </label>
            </div>
            <div class="audio-strip-value">${formatAudioVolume(effective)}</div>
            <div class="audio-strip-playback">${renderAudioStripPlayback(busVoices)}</div>
            <div class="audio-strip-history">${renderAudioStripHistory(busHistory)}</div>
        </div>`;
    }).join('');
}

function filterAudioKitVoicesByBus(voices, busName) {
    if (String(busName).toLowerCase() === 'master') return Array.isArray(voices) ? voices : [];
    return (Array.isArray(voices) ? voices : []).filter(voice => String(voice.bus).toLowerCase() === String(busName).toLowerCase());
}

function filterAudioKitHistoryByBus(history, busName) {
    if (String(busName).toLowerCase() === 'master') return Array.isArray(history) ? history : [];
    return (Array.isArray(history) ? history : []).filter(item => String(item.bus).toLowerCase() === String(busName).toLowerCase());
}

function renderAudioStripPlayback(voices) {
    if (!Array.isArray(voices) || !voices.length) {
        return '<strong>正在播放</strong><span>未运行</span>';
    }
    const rows = voices.slice(0, AUDIO_STRIP_DETAIL_LIMIT).map(voice => {
        const title = voice.clipName ?? voice.path ?? ('Voice ' + (voice.voiceId ?? '--'));
        const state = voice.isPlaying === false ? 'Paused' : 'Playing';
        const ratio = getAudioVoiceProgressRatio(voice);
        const progress = formatAudioProgress(voice);
        const progressPercent = formatAudioProgressPercent(voice);
        return `<span class="audio-strip-playback-item">
            <span class="audio-strip-playback-title">${escapeHtml(title)}</span>
            <em>${escapeHtml(state)} · ${escapeHtml(progress)} · ${escapeHtml(progressPercent)}</em>
            <i class="audio-strip-progress" aria-hidden="true"><b style="width:${escapeHtml(formatAudioMeterWidth(ratio))}"></b></i>
        </span>`;
    }).join('');
    const more = voices.length > AUDIO_STRIP_DETAIL_LIMIT ? `<span>+${escapeHtml(voices.length - AUDIO_STRIP_DETAIL_LIMIT)} 个 Voice</span>` : '';
    return `<strong>正在播放</strong>${rows}${more}`;
}

function renderAudioStripHistory(history) {
    if (!Array.isArray(history) || !history.length) {
        return '<strong>历史</strong><span>暂无历史</span>';
    }
    const rows = history.slice(0, AUDIO_STRIP_DETAIL_LIMIT).map(renderAudioStripHistoryItem).join('');
    const more = history.length > AUDIO_STRIP_DETAIL_LIMIT ? `<span>+${escapeHtml(history.length - AUDIO_STRIP_DETAIL_LIMIT)} 条记录</span>` : '';
    return `<strong>历史</strong>${rows}${more}`;
}

function renderAudioStripHistoryItem(item) {
    const label = formatAudioHistoryLabel(item);
    const title = formatAudioHistoryTitle(item);
    const meta = formatAudioHistoryMeta(item);
    const compactPath = formatAudioCompactPath(item.path);
    const fullTarget = item?.path ?? item?.clipName ?? item?.bus ?? '';
    const pathMarkup = compactPath ? `<i class="audio-strip-history-path">${escapeHtml(compactPath)}</i>` : '';
    return `<span class="audio-strip-history-item" title="${escapeHtml(fullTarget)}">
        <b class="audio-strip-history-badge">${escapeHtml(label)}</b>
        <span class="audio-strip-history-main">
            <span class="audio-strip-history-title">${escapeHtml(title)}</span>
            <em class="audio-strip-history-meta">${escapeHtml(meta)}</em>
            ${pathMarkup}
        </span>
    </span>`;
}

function formatAudioHistoryTitle(item) {
    const clipName = String(item?.clipName ?? '').trim();
    if (clipName) return clipName;
    const path = String(item?.path ?? '').replace(/\\/g, '/');
    const leaf = path.split('/').filter(Boolean).pop();
    if (leaf) return leaf.replace(/\.[^.]+$/, '');
    const bus = String(item?.bus ?? '').trim();
    if (bus) return formatAudioBusLabel(bus) + ' 通道';
    if (item?.voiceId !== undefined && item?.voiceId !== null && item?.voiceId !== '') {
        return 'Voice ' + item.voiceId;
    }
    return '--';
}

function formatAudioHistoryMeta(item) {
    const parts = [];
    const clock = formatAudioHistoryClock(item?.timestampUtc);
    if (clock && clock !== '--') parts.push(clock);
    if (item?.bus) parts.push(formatAudioBusLabel(item.bus));
    if (item?.voiceId !== undefined && item?.voiceId !== null && item?.voiceId !== '') {
        parts.push('V' + item.voiceId);
    }
    const volume = Number(item?.volume);
    if (Number.isFinite(volume)) parts.push('Vol ' + formatAudioVolume(volume));
    const fadeOutDuration = Number(item?.fadeOutDuration);
    if (Number.isFinite(fadeOutDuration) && fadeOutDuration > 0) {
        parts.push('Fade ' + formatAudioTime(fadeOutDuration));
    }
    if (item?.loop === true) parts.push('Loop');
    if (item?.is3D === true) parts.push('3D');
    return parts.length ? parts.join(' · ') : '--';
}

function formatAudioHistoryClock(value) {
    if (!value) return '--';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return String(value);
    return date.toLocaleTimeString([], {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false,
    });
}

function formatAudioCompactPath(path) {
    if (!path) return '';
    const normalized = String(path).replace(/\\/g, '/').replace(/\/+/g, '/');
    const parts = normalized.split('/').filter(Boolean);
    const compact = parts.slice(-3).join('/');
    if (compact.length <= 42) return compact;
    return '...' + compact.slice(-39);
}

function formatAudioBusLabel(name) {
    const key = String(name || '').toLowerCase();
    if (key === 'music') return 'BGM';
    if (key === 'sfx') return 'SFX';
    if (key === 'ambience') return 'AMBIENT';
    return String(name || '--').toUpperCase();
}

function filterAudioKitVoices(voices) {
    return (Array.isArray(voices) ? voices : []).filter(voice => kitSearchMatches(audioKitState.searchTerm, [
        voice.voiceId,
        voice.path,
        voice.clipName,
        voice.bus,
        voice.backendName,
        voice.is3D ? '3d' : '2d',
        voice.followTargetName,
        voice.rolloffMode,
    ]) && (audioKitState.selectedBus === 'All' || String(voice.bus) === String(audioKitState.selectedBus)));
}

function renderAudioVoiceRows(voices) {
    if (!Array.isArray(voices) || !voices.length) {
        return emptyState('audio', '暂无活跃声音。播放 AudioKit.PlaySfx 或 PlayMusic 后会显示。');
    }
    return voices.map(voice => {
        const selected = String(voice.voiceId) === String(audioKitState.selectedVoiceId);
        const mode = voice.is3D ? '3D' : '2D';
        return `<div class="audio-voice-row${selected ? ' active' : ''}" data-audiokit-voice="${escapeHtml(voice.voiceId ?? '')}">
            <span class="audio-voice-row__main">
                <strong>${escapeHtml(voice.path ?? voice.clipName ?? '--')}</strong>
                <em>${escapeHtml(voice.bus ?? '--')} · ${escapeHtml(mode)} · ${formatAudioTime(voice.elapsed)} / ${formatAudioTime(voice.duration)}</em>
            </span>
            <span class="audio-voice-row__state">${voice.isPlaying === false ? 'Paused' : 'Playing'}</span>
            <span class="audio-voice-row__progress">${formatAudioProgress(voice)}</span>
            <span class="audio-voice-row__volume">${formatAudioVolume(voice.volume)}</span>
            <button class="btn btn-sm" type="button" data-audiokit-stop-voice="${escapeHtml(voice.voiceId ?? '')}">Stop</button>
        </div>`;
    }).join('');
}

function renderAudioHistory(history) {
    if (!Array.isArray(history) || !history.length) {
        return emptyState('audio', '暂无播放历史。');
    }
    return `<div class="kit-timeline" data-kit-scroll-key="audio-history">${history.slice(0, 100).map(item => `<div class="kit-timeline-row">
        <span>${escapeHtml(formatAudioHistoryLabel(item))}</span>
        <strong>${escapeHtml(item.path ?? item.bus ?? '--')}</strong>
        <em>${escapeHtml(item.timestampUtc ?? '--')}</em>
    </div>`).join('')}</div>`;
}

function formatAudioHistoryLabel(item) {
    const eventType = item?.eventType ?? 'event';
    if (eventType === 'play_started') return '播放';
    if (eventType === 'play_stopped') return '停止';
    if (eventType === 'play_stop_requested') return '淡出';
    if (eventType === 'volume_changed') return '音量';
    return eventType;
}

function formatAudioVolume(value) {
    return percentText(Number(value ?? 0));
}

function normalizeAudioVolume(value) {
    const number = Number(value);
    if (!Number.isFinite(number)) return 0;
    return Math.max(0, Math.min(1, number));
}

function formatAudioMeterWidth(value) {
    return Math.round(normalizeAudioVolume(value) * 100) + '%';
}

function formatAudioTime(value) {
    const number = Number(value);
    if (!Number.isFinite(number) || number <= 0) return '0.0s';
    return number.toFixed(1) + 's';
}

function formatAudioProgress(voice) {
    return `${formatAudioTime(voice?.elapsed)} / ${formatAudioTime(voice?.duration)}`;
}

function getAudioVoiceProgressRatio(voice) {
    const duration = Number(voice?.duration);
    if (!Number.isFinite(duration) || duration <= 0) return 0;
    const elapsed = Number(voice?.elapsed);
    if (!Number.isFinite(elapsed) || elapsed <= 0) return 0;
    return Math.max(0, Math.min(1, elapsed / duration));
}

function formatAudioProgressPercent(voice) {
    const duration = Number(voice?.duration);
    if (!Number.isFinite(duration) || duration <= 0) return '--';
    return percentText(getAudioVoiceProgressRatio(voice));
}

function formatAudioNumber(value, decimals) {
    const number = Number(value);
    if (!Number.isFinite(number)) return '--';
    return number.toFixed(decimals);
}

function formatAudioPosition(position) {
    if (!position) return '位置 --';
    const x = formatAudioNumber(position.x ?? position.X, 1);
    const y = formatAudioNumber(position.y ?? position.Y, 1);
    const z = formatAudioNumber(position.z ?? position.Z, 1);
    return `位置 (${x}, ${y}, ${z})`;
}
