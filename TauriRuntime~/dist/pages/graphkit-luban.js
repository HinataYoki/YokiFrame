// pages/graphkit-luban.js
// Luban definition/data previews for GraphKit's XML-backed graph project.
(function registerGraphKitLubanApi(root) {
    const model = typeof module !== 'undefined' && module.exports
        ? require('./graphkit-model.js')
        : root;

    const DEFAULT_LUBAN_OPTIONS = Object.freeze({
        moduleName: 'graphkit',
        tableName: 'TbGraphProject',
        input: 'graphkit/graph_project.xml',
    });
    const DEFAULT_LUBAN_EXPORT_OPTIONS = Object.freeze({
        lubanWorkDir: 'Luban/MiniTemplate',
        definitionPath: 'Defines/graphkit.xml',
        dataPath: 'Datas/graphkit/graph_project.xml',
    });
    const GRAPHKIT_TABLEKIT_TARGET_OPTIONS = Object.freeze(['client', 'server', 'all']);
    const GRAPHKIT_TABLEKIT_CODE_TARGET_OPTIONS = Object.freeze(['cs-bin', 'cs-simple-json', 'cs-newtonsoft-json']);
    const GRAPHKIT_TABLEKIT_DATA_TARGET_OPTIONS = Object.freeze(['bin', 'json', 'lua']);
    const DEFAULT_GRAPHKIT_TABLEKIT_RUN_CONFIG = Object.freeze({
        lubanWorkDir: 'Luban/MiniTemplate',
        lubanDllPath: 'Luban/Tools/Luban/Luban.dll',
        target: 'client',
        codeTarget: 'cs-bin',
        dataTarget: 'bin',
        outputDataDir: 'Assets/Resources/Art/Table/',
        outputCodeDir: 'Assets/Scripts/TableKit/',
        runtimePathPattern: 'Art/Table/{0}',
        customEditorDataPath: false,
        editorDataPath: 'Assets/Resources/Art/Table/',
        useAssemblyDefinition: false,
        assemblyName: 'YokiFrame.TableKit',
        generateExternalTypeUtil: false,
        useAsyncLoading: false,
        extraOutputTargets: [],
    });

    function generateGraphKitLubanDefinitionXml(project, options = {}) {
        model.sanitizeGraphKitProject(project);
        const config = normalizeGraphKitLubanOptions(options);
        const lines = [
            '<?xml version="1.0" encoding="utf-8"?>',
            `<module name="${escapeGraphKitLubanAttribute(config.moduleName)}">`,
        ];

        appendGraphKitLubanBean(lines, 'GraphProjectRecord', [
            ['version', 'string'],
            ['nodeTypes', 'list,GraphNodeType'],
            ['graphs', 'list,GraphDefinition'],
        ]);
        appendGraphKitLubanBean(lines, 'GraphNodeType', [
            ['id', 'string'],
            ['title', 'string'],
            ['category', 'string'],
            ['handlerId', 'string'],
            ['color', 'string'],
            ['ports', 'list,GraphPort'],
            ['fields', 'list,GraphField'],
        ]);
        appendGraphKitLubanBean(lines, 'GraphPort', [
            ['id', 'string'],
            ['title', 'string'],
            ['kind', 'string'],
            ['direction', 'string'],
            ['multiple', 'bool'],
        ]);
        appendGraphKitLubanBean(lines, 'GraphField', [
            ['name', 'string'],
            ['title', 'string'],
            ['type', 'string'],
            ['reference', 'string'],
            ['defaultValue', 'string'],
            ['required', 'bool'],
            ['options', 'list,string'],
        ]);
        appendGraphKitLubanBean(lines, 'GraphDefinition', [
            ['id', 'string'],
            ['title', 'string'],
            ['kind', 'string'],
            ['blackboard', 'list,GraphBlackboardVar'],
            ['nodes', 'list,GraphNode'],
            ['edges', 'list,GraphEdge'],
            ['placemats', 'list,GraphPlacemat'],
            ['notes', 'list,GraphNote'],
        ]);
        appendGraphKitLubanBean(lines, 'GraphBlackboardVar', [
            ['name', 'string'],
            ['type', 'string'],
            ['section', 'string'],
            ['defaultValue', 'string'],
        ]);
        appendGraphKitLubanBean(lines, 'GraphNode', [
            ['id', 'string'],
            ['type', 'string'],
            ['x', 'int'],
            ['y', 'int'],
            ['fields', 'list,GraphFieldValue'],
        ]);
        appendGraphKitLubanBean(lines, 'GraphFieldValue', [
            ['name', 'string'],
            ['value', 'string'],
        ]);
        appendGraphKitLubanBean(lines, 'GraphEdge', [
            ['id', 'string'],
            ['from', 'string'],
            ['to', 'string'],
            ['label', 'string'],
            ['condition', 'string'],
            ['priority', 'int'],
        ]);
        appendGraphKitLubanBean(lines, 'GraphPlacemat', [
            ['id', 'string'],
            ['title', 'string'],
            ['x', 'int'],
            ['y', 'int'],
            ['width', 'int'],
            ['height', 'int'],
            ['color', 'string'],
            ['order', 'int'],
            ['locked', 'bool'],
            ['collapsed', 'bool'],
            ['nodeIds', 'list,string'],
        ]);
        appendGraphKitLubanBean(lines, 'GraphNote', [
            ['id', 'string'],
            ['title', 'string'],
            ['text', 'string'],
            ['x', 'int'],
            ['y', 'int'],
            ['width', 'int'],
            ['height', 'int'],
            ['color', 'string'],
        ]);

        lines.push(`  <table name="${escapeGraphKitLubanAttribute(config.tableName)}" value="GraphProjectRecord" input="${escapeGraphKitLubanAttribute(config.input)}" mode="one" />`);
        lines.push('</module>');
        return lines.join('\n');
    }

    function generateGraphKitLubanDataXml(project) {
        const sanitized = model.sanitizeGraphKitProject(project);
        const lines = [
            '<?xml version="1.0" encoding="utf-8"?>',
            '<data>',
        ];

        appendGraphKitLubanScalar(lines, 1, 'version', sanitized.version);
        appendGraphKitLubanList(lines, 1, 'nodeTypes', sanitized.nodeTypes, (type, itemLines, itemIndent) => {
            appendGraphKitLubanScalar(itemLines, itemIndent, 'id', type.id);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'title', type.title);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'category', type.category);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'handlerId', type.handlerId);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'color', type.color);
            appendGraphKitLubanList(itemLines, itemIndent, 'ports', type.ports, (port, portLines, portIndent) => {
                appendGraphKitLubanScalar(portLines, portIndent, 'id', port.id);
                appendGraphKitLubanScalar(portLines, portIndent, 'title', port.title);
                appendGraphKitLubanScalar(portLines, portIndent, 'kind', port.kind);
                appendGraphKitLubanScalar(portLines, portIndent, 'direction', port.direction);
                appendGraphKitLubanScalar(portLines, portIndent, 'multiple', port.multiple);
            });
            appendGraphKitLubanList(itemLines, itemIndent, 'fields', type.fields, (field, fieldLines, fieldIndent) => {
                appendGraphKitLubanScalar(fieldLines, fieldIndent, 'name', field.name);
                appendGraphKitLubanScalar(fieldLines, fieldIndent, 'title', field.title);
                appendGraphKitLubanScalar(fieldLines, fieldIndent, 'type', field.type);
                appendGraphKitLubanScalar(fieldLines, fieldIndent, 'reference', field.ref);
                appendGraphKitLubanScalar(fieldLines, fieldIndent, 'defaultValue', formatGraphKitLubanValue(field.defaultValue));
                appendGraphKitLubanScalar(fieldLines, fieldIndent, 'required', field.required);
                appendGraphKitLubanStringList(fieldLines, fieldIndent, 'options', field.options);
            });
        });
        appendGraphKitLubanList(lines, 1, 'graphs', sanitized.graphs, appendGraphKitLubanGraph);
        lines.push('</data>');
        return lines.join('\n');
    }

    function appendGraphKitLubanGraph(graph, lines, indent) {
        appendGraphKitLubanScalar(lines, indent, 'id', graph.id);
        appendGraphKitLubanScalar(lines, indent, 'title', graph.title);
        appendGraphKitLubanScalar(lines, indent, 'kind', graph.kind);
        appendGraphKitLubanList(lines, indent, 'blackboard', graph.blackboard, (item, itemLines, itemIndent) => {
            appendGraphKitLubanScalar(itemLines, itemIndent, 'name', item.name);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'type', item.type);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'section', item.section);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'defaultValue', formatGraphKitLubanValue(item.defaultValue));
        });
        appendGraphKitLubanList(lines, indent, 'nodes', graph.nodes, (node, nodeLines, nodeIndent) => {
            appendGraphKitLubanScalar(nodeLines, nodeIndent, 'id', node.id);
            appendGraphKitLubanScalar(nodeLines, nodeIndent, 'type', node.type);
            appendGraphKitLubanScalar(nodeLines, nodeIndent, 'x', node.x);
            appendGraphKitLubanScalar(nodeLines, nodeIndent, 'y', node.y);
            appendGraphKitLubanList(nodeLines, nodeIndent, 'fields', Object.entries(node.fields || {}), (entry, fieldLines, fieldIndent) => {
                appendGraphKitLubanScalar(fieldLines, fieldIndent, 'name', entry[0]);
                appendGraphKitLubanScalar(fieldLines, fieldIndent, 'value', formatGraphKitLubanValue(entry[1]));
            });
        });
        appendGraphKitLubanList(lines, indent, 'edges', graph.edges, (edge, edgeLines, edgeIndent) => {
            appendGraphKitLubanScalar(edgeLines, edgeIndent, 'id', edge.id);
            appendGraphKitLubanScalar(edgeLines, edgeIndent, 'from', edge.from);
            appendGraphKitLubanScalar(edgeLines, edgeIndent, 'to', edge.to);
            appendGraphKitLubanScalar(edgeLines, edgeIndent, 'label', edge.label);
            appendGraphKitLubanScalar(edgeLines, edgeIndent, 'condition', edge.condition);
            appendGraphKitLubanScalar(edgeLines, edgeIndent, 'priority', edge.priority);
        });
        appendGraphKitLubanList(lines, indent, 'placemats', graph.placemats, (item, itemLines, itemIndent) => {
            appendGraphKitLubanScalar(itemLines, itemIndent, 'id', item.id);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'title', item.title);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'x', item.x);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'y', item.y);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'width', item.width);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'height', item.height);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'color', item.color);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'order', item.order);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'locked', item.locked);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'collapsed', item.collapsed);
            appendGraphKitLubanStringList(itemLines, itemIndent, 'nodeIds', item.nodeIds);
        });
        appendGraphKitLubanList(lines, indent, 'notes', graph.notes, (item, itemLines, itemIndent) => {
            appendGraphKitLubanScalar(itemLines, itemIndent, 'id', item.id);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'title', item.title);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'text', item.text);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'x', item.x);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'y', item.y);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'width', item.width);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'height', item.height);
            appendGraphKitLubanScalar(itemLines, itemIndent, 'color', item.color);
        });
    }

    function appendGraphKitLubanBean(lines, name, fields) {
        lines.push(`  <bean name="${escapeGraphKitLubanAttribute(name)}">`);
        fields.forEach(field => {
            lines.push(`    <var name="${escapeGraphKitLubanAttribute(field[0])}" type="${escapeGraphKitLubanAttribute(field[1])}" />`);
        });
        lines.push('  </bean>');
    }

    function appendGraphKitLubanList(lines, indent, name, items, appendItem) {
        const prefix = getGraphKitLubanIndent(indent);
        lines.push(`${prefix}<${name}>`);
        (Array.isArray(items) ? items : []).forEach(item => {
            lines.push(`${prefix}  <item>`);
            appendItem(item, lines, indent + 2);
            lines.push(`${prefix}  </item>`);
        });
        lines.push(`${prefix}</${name}>`);
    }

    function appendGraphKitLubanStringList(lines, indent, name, items) {
        const prefix = getGraphKitLubanIndent(indent);
        lines.push(`${prefix}<${name}>`);
        (Array.isArray(items) ? items : []).forEach(item => {
            lines.push(`${prefix}  <item>${escapeGraphKitLubanText(item)}</item>`);
        });
        lines.push(`${prefix}</${name}>`);
    }

    function appendGraphKitLubanScalar(lines, indent, name, value) {
        const prefix = getGraphKitLubanIndent(indent);
        lines.push(`${prefix}<${name}>${escapeGraphKitLubanText(formatGraphKitLubanValue(value))}</${name}>`);
    }

    function normalizeGraphKitLubanOptions(options) {
        const source = options && typeof options === 'object' ? options : {};
        return {
            moduleName: normalizeGraphKitLubanIdentifier(source.moduleName, DEFAULT_LUBAN_OPTIONS.moduleName),
            tableName: normalizeGraphKitLubanIdentifier(source.tableName, DEFAULT_LUBAN_OPTIONS.tableName),
            input: normalizeGraphKitLubanInput(source.input, DEFAULT_LUBAN_OPTIONS.input),
        };
    }

    function normalizeGraphKitLubanIdentifier(value, fallback) {
        const text = String(value ?? '').trim();
        const sanitized = text.replace(/[^A-Za-z0-9_.-]/g, '_').replace(/^_+|_+$/g, '');
        return sanitized || fallback;
    }

    function normalizeGraphKitLubanInput(value, fallback) {
        const text = String(value ?? '').trim().replace(/\\/g, '/');
        const sanitized = text.replace(/[^A-Za-z0-9_./-]/g, '_').replace(/\/+/g, '/').replace(/^\/+/, '');
        return sanitized || fallback;
    }

    function formatGraphKitLubanValue(value) {
        if (value === null || value === undefined) return '';
        if (typeof value === 'boolean') return value ? 'true' : 'false';
        if (typeof value === 'object') {
            try {
                return JSON.stringify(value);
            } catch (_) {
                return String(value);
            }
        }
        return String(value);
    }

    function getGraphKitLubanExportPayload(project, options = {}) {
        const source = options && typeof options === 'object' ? options : {};
        const lubanWorkDir = normalizeGraphKitLubanInput(source.lubanWorkDir, DEFAULT_LUBAN_EXPORT_OPTIONS.lubanWorkDir);
        const definitionPath = normalizeGraphKitLubanInput(source.definitionPath, DEFAULT_LUBAN_EXPORT_OPTIONS.definitionPath);
        const dataPath = normalizeGraphKitLubanInput(source.dataPath, DEFAULT_LUBAN_EXPORT_OPTIONS.dataPath);
        return {
            lubanWorkDir,
            definitionPath,
            dataPath,
            definitionXml: generateGraphKitLubanDefinitionXml(project),
            dataXml: generateGraphKitLubanDataXml(project),
        };
    }

    function getGraphKitLubanRunRequest(config, options = {}) {
        const normalizedConfig = normalizeGraphKitTableKitRunConfig(config);
        const source = options && typeof options === 'object' ? options : {};
        return {
            projectRoot: normalizeGraphKitLubanRunString(source.projectRoot, ''),
            engine: normalizeGraphKitLubanRunString(source.engine, 'auto'),
            mode: source.mode === 'validate' ? 'validate' : 'generate',
            config: normalizedConfig,
        };
    }

    function normalizeGraphKitTableKitRunConfig(config) {
        const source = config && typeof config === 'object' ? config : {};
        const outputDataDir = normalizeGraphKitLubanRunString(source.outputDataDir, DEFAULT_GRAPHKIT_TABLEKIT_RUN_CONFIG.outputDataDir);
        const customEditorDataPath = normalizeGraphKitLubanRunBool(source.customEditorDataPath);
        return {
            lubanWorkDir: normalizeGraphKitLubanRunString(source.lubanWorkDir, DEFAULT_GRAPHKIT_TABLEKIT_RUN_CONFIG.lubanWorkDir),
            lubanDllPath: normalizeGraphKitLubanRunString(source.lubanDllPath, DEFAULT_GRAPHKIT_TABLEKIT_RUN_CONFIG.lubanDllPath),
            target: normalizeGraphKitLubanRunOption(source.target, GRAPHKIT_TABLEKIT_TARGET_OPTIONS, DEFAULT_GRAPHKIT_TABLEKIT_RUN_CONFIG.target),
            codeTarget: normalizeGraphKitLubanRunOption(source.codeTarget, GRAPHKIT_TABLEKIT_CODE_TARGET_OPTIONS, DEFAULT_GRAPHKIT_TABLEKIT_RUN_CONFIG.codeTarget),
            dataTarget: normalizeGraphKitLubanRunOption(source.dataTarget, GRAPHKIT_TABLEKIT_DATA_TARGET_OPTIONS, DEFAULT_GRAPHKIT_TABLEKIT_RUN_CONFIG.dataTarget),
            outputDataDir,
            outputCodeDir: normalizeGraphKitLubanRunString(source.outputCodeDir, DEFAULT_GRAPHKIT_TABLEKIT_RUN_CONFIG.outputCodeDir),
            runtimePathPattern: normalizeGraphKitLubanRunString(source.runtimePathPattern, DEFAULT_GRAPHKIT_TABLEKIT_RUN_CONFIG.runtimePathPattern),
            customEditorDataPath,
            editorDataPath: customEditorDataPath
                ? normalizeGraphKitLubanRunString(source.editorDataPath, outputDataDir)
                : outputDataDir,
            useAssemblyDefinition: normalizeGraphKitLubanRunBool(source.useAssemblyDefinition),
            assemblyName: normalizeGraphKitLubanRunString(source.assemblyName, DEFAULT_GRAPHKIT_TABLEKIT_RUN_CONFIG.assemblyName),
            generateExternalTypeUtil: normalizeGraphKitLubanRunBool(source.generateExternalTypeUtil),
            useAsyncLoading: normalizeGraphKitLubanRunBool(source.useAsyncLoading),
            extraOutputTargets: normalizeGraphKitTableKitExtraOutputs(source.extraOutputTargets),
        };
    }

    function normalizeGraphKitTableKitExtraOutputs(rawTargets) {
        if (!Array.isArray(rawTargets)) return [];
        return rawTargets.map(target => {
            const source = target && typeof target === 'object' ? target : {};
            return {
                target: normalizeGraphKitLubanRunOption(source.target, GRAPHKIT_TABLEKIT_TARGET_OPTIONS, 'client'),
                dataTarget: normalizeGraphKitLubanRunOption(source.dataTarget, GRAPHKIT_TABLEKIT_DATA_TARGET_OPTIONS, 'json'),
                outputDataDir: normalizeGraphKitLubanRunString(source.outputDataDir, 'Temp/LubanExtra'),
            };
        }).slice(0, 8);
    }

    function normalizeGraphKitLubanRunString(value, fallback) {
        const text = String(value ?? '').trim();
        return text || fallback;
    }

    function normalizeGraphKitLubanRunOption(value, options, fallback) {
        return options.includes(value) ? value : fallback;
    }

    function normalizeGraphKitLubanRunBool(value) {
        return value === true || value === 'true' || value === 1 || value === '1';
    }

    function formatGraphKitLubanRunSummary(action, result = {}, options = {}) {
        const source = result && typeof result === 'object' ? result : {};
        const normalizedAction = action === 'export' ? 'export' : 'generate';
        const actionLabel = normalizedAction === 'export' ? '导出' : '生成';
        const success = source.success !== false && !source.error;
        const generatedFiles = Array.isArray(source.generatedFiles)
            ? source.generatedFiles.map(file => String(file ?? '').trim()).filter(Boolean)
            : [];
        const fileDetail = generatedFiles.length ? `${generatedFiles.length} 个文件` : '未生成文件';
        const logPreview = formatGraphKitLubanLogPreview(source.log || source.error || '');
        const timestamp = normalizeGraphKitLubanRunString(
            options.timestamp,
            typeof Date !== 'undefined' ? new Date().toLocaleTimeString() : ''
        );
        return {
            action: normalizedAction,
            level: success ? 'success' : 'error',
            title: `${actionLabel}${success ? '完成' : '失败'}`,
            detail: `${fileDetail}${success ? '。' : '。请查看日志。'}`,
            timestamp,
            logPreview,
            generatedFiles,
        };
    }

    function formatGraphKitLubanLogPreview(log) {
        const lines = String(log ?? '')
            .split(/\r?\n/)
            .map(line => line.trim())
            .filter(Boolean);
        return lines.slice(-4).join('\n');
    }

    function recordGraphKitLubanResult(action, result) {
        const summary = formatGraphKitLubanRunSummary(action, result);
        if (typeof graphKitState !== 'undefined') {
            graphKitState.lubanRun = summary;
        }
        return summary;
    }

    function recordGraphKitLubanError(action, error) {
        return recordGraphKitLubanResult(action, {
            success: false,
            log: formatGraphKitLubanError(error),
        });
    }

    async function exportGraphKitLubanFilesToTableKit() {
        if (!invoke) {
            recordGraphKitLubanResult('export', { success: false, log: '当前不在 Tauri 环境中。' });
            setGraphKitNotice('warning', '当前不在 Tauri 环境中，无法导出到 TableKit Luban 工作目录。');
            renderGraphKitWorkbench();
            return false;
        }

        const projectRoot = typeof getGraphKitProjectRootForFilePicker === 'function'
            ? getGraphKitProjectRootForFilePicker()
            : '';
        const payload = getGraphKitLubanExportPayload(graphKitProject);
        try {
            graphKitState.fileBusy = true;
            renderGraphKitWorkbench();
            const result = parseGraphKitFileCommandResult(await invoke('graphkit_export_luban_files', { projectRoot, payload }));
            const fileCount = Array.isArray(result.generatedFiles) ? result.generatedFiles.length : 0;
            graphKitState.fileBusy = false;
            recordGraphKitLubanResult('export', result);
            setGraphKitNotice('success', `已导出 ${fileCount || 2} 个 Luban XML 文件到 TableKit 工作目录。`);
            renderGraphKitWorkbench();
            return true;
        } catch (error) {
            graphKitState.fileBusy = false;
            recordGraphKitLubanError('export', error);
            setGraphKitNotice('error', `导出 Luban XML 失败：${formatGraphKitLubanError(error)}`);
            renderGraphKitWorkbench();
            return false;
        }
    }

    async function exportAndRunGraphKitLuban() {
        if (!invoke) {
            recordGraphKitLubanResult('generate', { success: false, log: '当前不在 Tauri 环境中。' });
            setGraphKitNotice('warning', '当前不在 Tauri 环境中，无法执行 Luban。');
            renderGraphKitWorkbench();
            return false;
        }

        const status = typeof getTableKitLubanStatus === 'function' ? getTableKitLubanStatus() : null;
        const projectRoot = typeof getTableKitProjectRoot === 'function'
            ? getTableKitProjectRoot(status)
            : (typeof getGraphKitProjectRootForFilePicker === 'function' ? getGraphKitProjectRootForFilePicker() : '');
        const engine = typeof getTableKitEffectiveEngine === 'function'
            ? getTableKitEffectiveEngine(status)
            : (status?.engine && status.engine !== '--' ? status.engine : 'auto');
        const runRequest = getGraphKitLubanRunRequest(
            typeof tableKitConfig !== 'undefined' ? tableKitConfig : DEFAULT_GRAPHKIT_TABLEKIT_RUN_CONFIG,
            { projectRoot, engine, mode: 'generate' }
        );
        const payload = getGraphKitLubanExportPayload(graphKitProject, {
            lubanWorkDir: runRequest.config.lubanWorkDir,
        });

        try {
            graphKitState.fileBusy = true;
            renderGraphKitWorkbench();
            await invoke('graphkit_export_luban_files', { projectRoot: runRequest.projectRoot, payload });
            const result = parseGraphKitFileCommandResult(await invoke('tablekit_run_luban', runRequest));
            graphKitState.fileBusy = false;
            if (result.log && typeof appendTableKitConsoleLines === 'function') {
                appendTableKitConsoleLines(result.success ? 'info' : 'error', result.log);
            }
            if (result.success) {
                const generatedCount = Array.isArray(result.generatedFiles) ? result.generatedFiles.length : 0;
                recordGraphKitLubanResult('generate', result);
                setGraphKitNotice('success', `已导出 GraphKit XML 并生成 Luban 产物${generatedCount ? `（${generatedCount} 个文件）` : '。'}`);
                renderGraphKitWorkbench();
                return true;
            }

            recordGraphKitLubanResult('generate', result);
            setGraphKitNotice('error', 'Luban 执行失败，请查看 TableKit 控制台日志。');
            renderGraphKitWorkbench();
            return false;
        } catch (error) {
            graphKitState.fileBusy = false;
            recordGraphKitLubanError('generate', error);
            setGraphKitNotice('error', `导出并生成 Luban 失败：${formatGraphKitLubanError(error)}`);
            renderGraphKitWorkbench();
            return false;
        }
    }

    function formatGraphKitLubanError(error) {
        return String(error?.message ?? error ?? '未知错误');
    }

    function getGraphKitLubanIndent(indent) {
        return '  '.repeat(Math.max(0, indent));
    }

    function escapeGraphKitLubanAttribute(value) {
        return escapeGraphKitLubanText(value).replace(/"/g, '&quot;').replace(/'/g, '&apos;');
    }

    function escapeGraphKitLubanText(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    function renderGraphKitLubanRunSummary(summary) {
        if (!summary) {
            return `<div class="graphkit-luban-run-summary is-empty" data-graphkit-luban-run-summary>
            <div class="graphkit-luban-run-summary__title">尚未执行</div>
            <div class="graphkit-luban-run-summary__detail">导出或生成后会在这里显示最近一次结果。</div>
        </div>`;
        }
        const generatedFiles = Array.isArray(summary.generatedFiles) ? summary.generatedFiles : [];
        const fileList = generatedFiles.length
            ? `<ul class="graphkit-luban-run-summary__files">${generatedFiles.slice(0, 4).map(file => `<li>${escapeHtml(file)}</li>`).join('')}</ul>`
            : '';
        const logPreview = summary.logPreview
            ? `<pre class="graphkit-luban-run-summary__log">${escapeHtml(summary.logPreview)}</pre>`
            : '';
        const level = summary.level === 'success' ? 'success' : 'error';
        return `<div class="graphkit-luban-run-summary is-${level}" data-graphkit-luban-run-summary>
            <div class="graphkit-luban-run-summary__head">
                <span class="graphkit-luban-run-summary__title">${escapeHtml(summary.title)}</span>
                <span class="graphkit-luban-run-summary__time">${escapeHtml(summary.timestamp)}</span>
            </div>
            <div class="graphkit-luban-run-summary__detail">${escapeHtml(summary.detail)}</div>
            ${fileList}
            ${logPreview}
        </div>`;
    }

    function renderGraphKitLubanDock(definitionXml, dataXml, contract) {
        const graphCount = Number.isFinite(Number(contract?.graphCount)) ? Number(contract.graphCount) : 0;
        return `<details class="graphkit-luban-dock tablekit-json-panel">
        <summary>Luban Definition / Data</summary>
        <div class="graphkit-xml-dock__meta">
            <span>${escapeHtml(`${graphCount} graphs`)}</span>
            <span>GraphProjectRecord</span>
            <span>TbGraphProject</span>
        </div>
        <div class="graphkit-xml-dock__actions">
            <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="copy-luban-definition">复制 Definition</button>
            <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="copy-luban-data">复制 Data XML</button>
            <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="export-luban-files">导出到 TableKit</button>
            <button class="btn btn-primary btn-sm" type="button" data-graphkit-action="export-run-luban">导出并生成</button>
        </div>
        ${renderGraphKitLubanRunSummary(graphKitState.lubanRun)}
        <label class="graphkit-luban-output-label">Definition XML</label>
        <textarea class="cmd-input graphkit-xml-input graphkit-luban-output" data-graphkit-luban-definition-output spellcheck="false" readonly>${escapeHtml(definitionXml)}</textarea>
        <label class="graphkit-luban-output-label">Data XML</label>
        <textarea class="cmd-input graphkit-xml-input graphkit-luban-output" data-graphkit-luban-data-output spellcheck="false" readonly>${escapeHtml(dataXml)}</textarea>
    </details>`;
    }

    async function copyGraphKitLubanDefinition() {
        const xml = generateGraphKitLubanDefinitionXml(graphKitProject);
        await copyGraphKitLubanText(xml, 'Luban Definition 已复制到剪贴板。');
    }

    async function copyGraphKitLubanData() {
        const xml = generateGraphKitLubanDataXml(graphKitProject);
        await copyGraphKitLubanText(xml, 'Luban Data XML 已复制到剪贴板。');
    }

    async function copyGraphKitLubanText(text, successMessage) {
        try {
            await navigator.clipboard?.writeText?.(text);
            setGraphKitNotice('success', successMessage);
        } catch (_) {
            setGraphKitNotice('warning', '剪贴板不可用，请直接复制 Luban 预览内容。');
        }
        renderGraphKitWorkbench();
    }

    const api = {
        copyGraphKitLubanData,
        copyGraphKitLubanDefinition,
        exportAndRunGraphKitLuban,
        exportGraphKitLubanFilesToTableKit,
        formatGraphKitLubanRunSummary,
        generateGraphKitLubanDataXml,
        generateGraphKitLubanDefinitionXml,
        getGraphKitLubanExportPayload,
        getGraphKitLubanRunRequest,
        recordGraphKitLubanResult,
        renderGraphKitLubanDock,
        renderGraphKitLubanRunSummary,
    };

    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    }

    if (root) {
        Object.assign(root, api);
    }
})(typeof window !== 'undefined' ? window : globalThis);
