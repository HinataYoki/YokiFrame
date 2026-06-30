// pages/graphkit-file.js
// GraphKit desktop file workflow: open/save Luban-friendly XML graph assets.
const GRAPHKIT_FILE_DEFAULT_NAME = 'Untitled Graph XML';

function getGraphKitFileName(path) {
    const value = String(path || '').trim();
    if (!value) return GRAPHKIT_FILE_DEFAULT_NAME;
    return value.split(/[\\/]/).filter(Boolean).pop() || GRAPHKIT_FILE_DEFAULT_NAME;
}

function setGraphKitCurrentFilePath(path) {
    graphKitState.filePath = String(path || '').trim();
    graphKitState.fileName = getGraphKitFileName(graphKitState.filePath);
}

function resetGraphKitFileState() {
    graphKitState.filePath = '';
    graphKitState.fileName = GRAPHKIT_FILE_DEFAULT_NAME;
    graphKitState.fileBusy = false;
}

function parseGraphKitFileCommandResult(raw) {
    if (!raw) return {};
    if (typeof raw === 'string') {
        try {
            return JSON.parse(raw);
        } catch (_) {
            return { content: raw };
        }
    }
    return raw;
}

function getGraphKitProjectRootForFilePicker() {
    const summaryRoot = typeof latestStatusSummary !== 'undefined'
        && latestStatusSummary?.projectPath
        && latestStatusSummary.projectPath !== '--'
        ? latestStatusSummary.projectPath
        : '';
    if (summaryRoot) return summaryRoot;

    const rawEngines = typeof latestStatusRaw !== 'undefined' && Array.isArray(latestStatusRaw?.engines)
        ? latestStatusRaw.engines
        : [];
    const engine = rawEngines.find(candidate => candidate?.projectPath && candidate.connected !== false)
        || rawEngines.find(candidate => candidate?.projectPath);
    return engine?.projectPath && engine.projectPath !== '--' ? engine.projectPath : '';
}

function getGraphKitDefaultXmlFileName() {
    return `${normalizeGraphKitId(graphKitProject?.graph?.id, 'graph')}.xml`;
}

function getGraphKitFilePickerInitialPath() {
    return graphKitState.filePath || getGraphKitDefaultXmlFileName();
}

function applyGraphKitLoadedXmlProject(project, filePath) {
    graphKitProject = sanitizeGraphKitProject(project);
    const selectedNodeId = getGraphKitInitialNodeId(graphKitProject);
    graphKitState.selectedNodeId = selectedNodeId;
    graphKitState.selectedNodeIds = selectedNodeId ? [selectedNodeId] : [];
    graphKitState.selectedEdgeId = '';
    graphKitState.selectedBlackboardName = '';
    graphKitState.selectedNoteId = '';
    graphKitState.selectedPlacematId = '';
    graphKitState.selectedNodeTypeId = '';
    graphKitState.pendingPortEndpoint = '';
    graphKitState.searchTerm = '';
    graphKitState.contextMenu = typeof getClosedGraphKitContextMenu === 'function'
        ? getClosedGraphKitContextMenu()
        : graphKitState.contextMenu;
    graphKitState.undoStack = [];
    graphKitState.redoStack = [];
    graphKitState.dirty = false;
    setGraphKitCurrentFilePath(filePath);
    persistGraphKitProject();
}

async function openGraphKitXmlFile() {
    if (!invoke) {
        setGraphKitNotice('warning', '当前不在 Tauri 环境中，无法打开系统文件选择器。');
        renderGraphKitWorkbench();
        return false;
    }

    const projectRoot = getGraphKitProjectRootForFilePicker();
    const initialPath = getGraphKitFilePickerInitialPath();
    try {
        graphKitState.fileBusy = true;
        renderGraphKitWorkbench();
        const selectedPath = await invoke('pick_file', { initialPath, extension: 'xml', projectRoot });
        if (!selectedPath) {
            graphKitState.fileBusy = false;
            renderGraphKitWorkbench();
            return false;
        }
        const result = parseGraphKitFileCommandResult(await invoke('graphkit_read_xml_file', { path: selectedPath }));
        const importedProject = parseGraphKitXml(result.content || '');
        const report = validateGraphKitProject(importedProject);
        if (report.errors.length) {
            throw new Error(report.errors.join(' / '));
        }

        applyGraphKitLoadedXmlProject(importedProject, result.path || selectedPath);
        setGraphKitNotice('success', `已打开 ${getGraphKitFileName(result.path || selectedPath)}。`);
        graphKitState.fileBusy = false;
        renderGraphKitWorkbench();
        return true;
    } catch (error) {
        graphKitState.fileBusy = false;
        setGraphKitNotice('error', `打开 XML 失败：${formatGraphKitFileError(error)}`);
        renderGraphKitWorkbench();
        return false;
    }
}

async function saveGraphKitXmlFile() {
    if (!graphKitState.filePath) return saveGraphKitXmlFileAs();
    return writeGraphKitXmlFileToPath(graphKitState.filePath);
}

async function saveGraphKitXmlFileAs() {
    if (!invoke) {
        setGraphKitNotice('warning', '当前不在 Tauri 环境中，无法打开系统保存选择器。');
        renderGraphKitWorkbench();
        return false;
    }

    const projectRoot = getGraphKitProjectRootForFilePicker();
    const initialPath = getGraphKitFilePickerInitialPath();
    try {
        graphKitState.fileBusy = true;
        renderGraphKitWorkbench();
        const selectedPath = await invoke('pick_save_file', { initialPath, extension: 'xml', projectRoot });
        if (!selectedPath) {
            graphKitState.fileBusy = false;
            renderGraphKitWorkbench();
            return false;
        }
        return writeGraphKitXmlFileToPath(selectedPath);
    } catch (error) {
        graphKitState.fileBusy = false;
        setGraphKitNotice('error', `另存 XML 失败：${formatGraphKitFileError(error)}`);
        renderGraphKitWorkbench();
        return false;
    }
}

async function writeGraphKitXmlFileToPath(path) {
    if (!invoke) {
        setGraphKitNotice('warning', '当前不在 Tauri 环境中，无法保存到本地文件。');
        renderGraphKitWorkbench();
        return false;
    }

    try {
        graphKitState.fileBusy = true;
        renderGraphKitWorkbench();
        const xml = serializeGraphKitXml(graphKitProject);
        const result = parseGraphKitFileCommandResult(await invoke('graphkit_write_xml_file', { path, content: xml }));
        setGraphKitCurrentFilePath(result.path || path);
        graphKitState.dirty = false;
        graphKitState.fileBusy = false;
        setGraphKitNotice('success', `已保存 ${graphKitState.fileName}。`);
        renderGraphKitWorkbench();
        return true;
    } catch (error) {
        graphKitState.fileBusy = false;
        setGraphKitNotice('error', `保存 XML 失败：${formatGraphKitFileError(error)}`);
        renderGraphKitWorkbench();
        return false;
    }
}

function formatGraphKitFileError(error) {
    return String(error?.message ?? error ?? '未知错误');
}
