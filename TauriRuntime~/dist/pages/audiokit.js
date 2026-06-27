// pages/audiokit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：AudioKit
// ═══════════════════════════════════════════════════════════════════
const audioKitState = {
    stats: {},
    buses: [],
    voices: [],
    history: [],
    searchTerm: '',
    selectedBus: 'All',
    selectedVoiceId: null,
    renderSignature: '',
};

const audioKitPageState = { renderSignature: '' };

function renderAudioKitPage() {
    $pageBody.classList.add('content-body--audiokit');
    $pageBody.classList.remove('content-body--poolkit');
    $pageBody.classList.remove('content-body--reskit');
    $pageBody.classList.remove('content-body--singletonkit');
    setHero(
        t('audiokit.title'),
        t('audiokit.subtitle'),
        t('audiokit.tab'),
        'audio',
        `<button class="btn btn-sm" onclick="clearAudioKitHistory()">${t('poolkit.clear_history')}</button><button class="btn btn-sm" onclick="stopAllAudioKit()">${t('common.all')}</button><button class="btn btn-primary btn-sm" onclick="refreshAudioKit()">${t('common.refresh')}</button>`
    );
    clearTabs();
    audioKitState.renderSignature = '';
    audioKitPageState.renderSignature = '';
    loadAudioKitWorkbench();
}

async function refreshAudioKit() { loadAudioKitWorkbench(); }

async function refreshAudioKitReactive(event) {
    await loadAudioKitWorkbench();
}

function normalizeAudioKitStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const busesSource = source.buses ?? {};
    const voicesSource = source.voices ?? {};
    const historySource = source.history ?? {};
    return {
        stats: source.stats ?? {},
        buses: Array.isArray(source.buses) ? source.buses : (Array.isArray(busesSource.buses) ? busesSource.buses : []),
        voices: Array.isArray(source.voices) ? source.voices : (Array.isArray(voicesSource.voices) ? voicesSource.voices : []),
        history: Array.isArray(source.history) ? source.history : (Array.isArray(historySource.history) ? historySource.history : []),
    };
}

async function fetchAudioKitWorkbenchState() {
    return await fetchKitWorkbenchState('AudioKit', normalizeAudioKitStatePayload);
}

async function loadAudioKitWorkbench() {
    if (!invoke || !connected) {
        renderAudioKitPageContent(emptyState('audio', '请连接引擎后查看音频播放状态。'));
        return;
    }
    try {
        const state = await fetchAudioKitWorkbenchState();
        audioKitState.stats = state.stats;
        audioKitState.buses = state.buses;
        audioKitState.voices = state.voices;
        audioKitState.history = state.history;
        reconcileAudioKitBusSelection(audioKitState.buses, audioKitState.voices);
        reconcileAudioKitSelection(audioKitState.voices);
        clearMetrics();

        renderAudioKitPageContent(renderAudioKitWorkbench(audioKitState.stats, audioKitState.buses, audioKitState.voices, audioKitState.history));
    } catch (e) {
        if (!canSendRuntimeKitCommand('AudioKit')) {
            renderAudioKitPageContent(emptyState('audio', '当前宿主未开放 AudioKit 运行时命令。'));
            return;
        }
        renderAudioKitPageContent(panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!'));
    }
}

function renderAudioKitPageContent(runtimeHtml) {
    clearMetrics();
    audioKitIndexConfig = sanitizeAudioKitIndexConfig(audioKitIndexConfig);
    const html = `<div class="audiokit-page-workbench">
        <div class="audiokit-page-runtime">${runtimeHtml}</div>
        ${renderAudioKitIndexGenerator()}
    </div>`;
    const signature = makeStableSignature({
        stats: audioKitState.stats,
        buses: audioKitState.buses,
        voices: audioKitState.voices,
        history: audioKitState.history,
        selectedBus: audioKitState.selectedBus,
        selected: audioKitState.selectedVoiceId,
        indexConfig: audioKitIndexConfig,
        indexEntries: audioKitIndexState.entries,
        indexLog: audioKitIndexState.log,
        indexBusy: audioKitIndexState.busy,
        generatedFile: audioKitIndexState.generatedFile,
        runtimeHtml,
    });
    renderWorkbenchHtmlStable(audioKitPageState, html, signature, bindAudioKitPageActions);
}

function renderAudioKitCachedPage() {
    renderAudioKitPageContent(renderAudioKitWorkbench(audioKitState.stats, audioKitState.buses, audioKitState.voices, audioKitState.history));
}

function bindAudioKitPageActions() {
    bindAudioKitIndexGeneratorActions();
    bindAudioKitWorkbenchActions();
}

function reconcileAudioKitSelection(voices) {
    if (!Array.isArray(voices) || !voices.length) {
        audioKitState.selectedVoiceId = null;
        return null;
    }
    let selected = voices.find(item => String(item.voiceId) === String(audioKitState.selectedVoiceId));
    if (!selected) {
        selected = voices[0];
        audioKitState.selectedVoiceId = selected.voiceId;
    }
    return selected;
}

function reconcileAudioKitBusSelection(buses, voices) {
    if (audioKitState.selectedBus === 'All') return;
    const hasBus = (Array.isArray(buses) ? buses : []).some(item => String(item.name) === String(audioKitState.selectedBus))
        || (Array.isArray(voices) ? voices : []).some(item => String(item.bus) === String(audioKitState.selectedBus));
    if (!hasBus) {
        audioKitState.selectedBus = 'All';
    }
}

function bindAudioKitWorkbenchActions() {
    $pageBody.querySelectorAll('[data-audiokit-voice]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            selectAudioKitVoice(button.dataset.audiokitVoice);
        });
    });
    $pageBody.querySelectorAll('[data-audiokit-stop-voice]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => stopAudioKitVoice(button.dataset.audiokitStopVoice));
    });
    bindKitButtonClick('[data-audiokit-clear-history]', () => clearAudioKitHistory());
    bindKitButtonClick('[data-audiokit-stop-all]', () => stopAllAudioKit());
    bindKitButtonClick('[data-audiokit-master-mute]', button => muteAudioKitMaster(button.dataset.audiokitMasterMute === 'true'));
    const masterVolume = $pageBody.querySelector('[data-audiokit-master-volume]');
    if (masterVolume && masterVolume.dataset.bound !== '1') {
        masterVolume.dataset.bound = '1';
        masterVolume.addEventListener('change', () => setAudioKitMasterVolume(masterVolume.value));
    }
    $pageBody.querySelectorAll('[data-audiokit-voice-bus-filter]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => selectAudioKitBus(button.dataset.audiokitVoiceBusFilter));
    });
    $pageBody.querySelectorAll('[data-audiokit-bus-volume]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        input.addEventListener('change', () => setAudioKitBusVolume(input.dataset.audiokitBusVolume, input.value));
    });
    $pageBody.querySelectorAll('[data-audiokit-bus-mute]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => muteAudioKitBus(button.dataset.audiokitBusMute, button.dataset.audiokitBusMuted === 'true'));
    });
    $pageBody.querySelectorAll('[data-audiokit-stop-bus]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => stopAudioKitBus(button.dataset.audiokitStopBus));
    });
    bindAudioKitSearch();
}

function bindAudioKitSearch() {
    const input = $pageBody.querySelector('[data-audiokit-search]');
    if (input && input.dataset.bound !== '1') {
        input.dataset.bound = '1';
        input.addEventListener('input', () => {
            audioKitState.searchTerm = input.value || '';
            updateAudioKitListDom();
        });
    }

    const clear = $pageBody.querySelector('[data-audiokit-search-clear]');
    if (clear && clear.dataset.bound !== '1') {
        clear.dataset.bound = '1';
        clear.addEventListener('click', () => {
            audioKitState.searchTerm = '';
            updateAudioKitListDom();
            $pageBody.querySelector('[data-audiokit-search]')?.focus();
        });
    }
}

function updateAudioKitListDom() {
    const visible = filterAudioKitVoices(audioKitState.voices);
    const list = $pageBody.querySelector('.audio-voice-table');
    if (list) list.innerHTML = renderAudioVoiceRows(visible);
    const count = $pageBody.querySelector('[data-audiokit-visible-count]');
    if (count) count.textContent = `${visible.length} / ${audioKitState.voices.length}`;
    const input = $pageBody.querySelector('[data-audiokit-search]');
    if (input && input.value !== audioKitState.searchTerm) input.value = audioKitState.searchTerm;
    const clear = $pageBody.querySelector('[data-audiokit-search-clear]');
    if (clear) clear.classList.toggle('is-empty', !audioKitState.searchTerm.trim());
    bindAudioKitWorkbenchActions();
}

function selectAudioKitVoice(voiceId) {
    if (!voiceId) return;
    audioKitState.selectedVoiceId = voiceId;
    $pageBody.querySelectorAll('[data-audiokit-voice]').forEach(row => {
        row.classList.toggle('active', String(row.dataset.audiokitVoice) === String(audioKitState.selectedVoiceId));
    });
    bindAudioKitWorkbenchActions();
}

function selectAudioKitBus(busName) {
    audioKitState.selectedBus = busName || 'All';
    $pageBody.querySelectorAll('[data-audiokit-channel-strip]').forEach(row => {
        row.classList.toggle('active', String(row.dataset.audiokitChannelStrip) === String(audioKitState.selectedBus));
    });
    updateAudioKitListDom();
}

async function stopAudioKitVoice(voiceId) {
    if (!invoke || !connected || !voiceId) return;
    await sendKitCommandData('AudioKit', 'stop_voice', { voiceId: Number(voiceId) });
    await loadAudioKitWorkbench();
}

async function stopAllAudioKit() {
    if (!invoke || !connected) return;
    await sendKitCommandData('AudioKit', 'stop_all');
    await loadAudioKitWorkbench();
}

async function stopAudioKitBus(bus) {
    if (!invoke || !connected || !bus) return;
    await sendKitCommandData('AudioKit', 'stop_bus', { bus });
    await loadAudioKitWorkbench();
}

async function setAudioKitMasterVolume(volume) {
    if (!invoke || !connected) return;
    await sendKitCommandData('AudioKit', 'set_master_volume', { volume: Number(volume) });
    await loadAudioKitWorkbench();
}

async function setAudioKitBusVolume(bus, volume) {
    if (!invoke || !connected || !bus) return;
    await sendKitCommandData('AudioKit', 'set_bus_volume', { bus, volume: Number(volume) });
    await loadAudioKitWorkbench();
}

async function muteAudioKitMaster(muted) {
    if (!invoke || !connected) return;
    await sendKitCommandData('AudioKit', 'mute_master', { muted: !!muted });
    await loadAudioKitWorkbench();
}

async function muteAudioKitBus(bus, muted) {
    if (!invoke || !connected || !bus) return;
    await sendKitCommandData('AudioKit', 'mute_bus', { bus, muted: !!muted });
    await loadAudioKitWorkbench();
}

async function clearAudioKitHistory() {
    if (!invoke || !connected) return;
    await sendKitCommandData('AudioKit', 'clear_history');
    await loadAudioKitWorkbench();
}
