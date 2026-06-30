// pages/graphkit-runtime-contract.js
// Runtime contract preview for project-side GraphKit graph execution.
(function registerGraphKitRuntimeContractApi(root) {
    const model = typeof module !== 'undefined' && module.exports
        ? require('./graphkit-model.js')
        : root;

    function formatGraphKitRuntimeContractJson(projectOrContract) {
        const source = projectOrContract && projectOrContract.graphClass && Array.isArray(projectOrContract.graphs)
            ? projectOrContract
            : model.getGraphKitRuntimeContract(projectOrContract);
        return JSON.stringify(source, null, 2);
    }

    function renderGraphKitRuntimeContractDock(contractJson, contract) {
        const graphCount = Number.isFinite(Number(contract?.graphCount)) ? Number(contract.graphCount) : 0;
        const handlerCount = Number.isFinite(Number(contract?.handlerCount)) ? Number(contract.handlerCount) : 0;
        const portalRouteCount = Array.isArray(contract?.portalRoutes) ? contract.portalRoutes.length : 0;
        return `<details class="graphkit-runtime-contract-dock tablekit-json-panel">
        <summary>GraphRuntime Contract</summary>
        <div class="graphkit-xml-dock__meta">
            <span>${escapeHtml(`${graphCount} graphs`)}</span>
            <span>${escapeHtml(`${handlerCount} handlers`)}</span>
            <span>${escapeHtml(`${portalRouteCount} portal routes`)}</span>
        </div>
        <div class="graphkit-xml-dock__actions">
            <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="copy-runtime-contract">复制 Contract</button>
        </div>
        <textarea class="cmd-input graphkit-xml-input graphkit-runtime-contract-output" data-graphkit-runtime-contract-output spellcheck="false" readonly>${escapeHtml(contractJson)}</textarea>
    </details>`;
    }

    async function copyGraphKitRuntimeContract() {
        const json = formatGraphKitRuntimeContractJson(graphKitProject);
        try {
            await navigator.clipboard?.writeText?.(json);
            setGraphKitNotice('success', 'GraphRuntime contract 已复制到剪贴板。');
        } catch (_) {
            setGraphKitNotice('warning', '剪贴板不可用，请直接复制 Contract 预览内容。');
        }
        renderGraphKitWorkbench();
    }

    const api = {
        copyGraphKitRuntimeContract,
        formatGraphKitRuntimeContractJson,
        renderGraphKitRuntimeContractDock,
    };

    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    }

    if (root) {
        Object.assign(root, api);
    }
})(typeof window !== 'undefined' ? window : globalThis);
