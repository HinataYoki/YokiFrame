// pages/graphkit-templates.js
// Starter graph templates that prove the Luban XML model can cover multiple graph domains.
(function registerGraphKitTemplates(root) {
    const model = typeof module !== 'undefined' && module.exports
        ? require('./graphkit-model.js')
        : root;

    const GRAPHKIT_TEMPLATE_DEFINITIONS = Object.freeze([
        { id: 'dialogue.branching', title: 'Dialogue Branching', kind: 'dialogue', graphId: 'dialogue.branching' },
        { id: 'behavior.basic', title: 'Behavior Basic', kind: 'behavior', graphId: 'behavior.basic' },
        { id: 'quest.flow', title: 'Quest Flow', kind: 'quest', graphId: 'quest.flow' },
    ]);

    const GRAPHKIT_BEHAVIOR_NODE_TYPES = Object.freeze([
        {
            id: 'behavior.selector',
            title: 'Selector',
            category: 'Behavior',
            handlerId: 'behavior.selector',
            color: '#4f6f8f',
            ports: [
                { id: 'in', title: 'In', kind: 'flow', direction: 'input' },
                { id: 'out', title: 'Out', kind: 'flow', direction: 'output' },
            ],
            fields: [
                { name: 'policy', title: 'Policy', type: 'string', defaultValue: 'first-success' },
            ],
        },
        {
            id: 'behavior.condition',
            title: 'Condition',
            category: 'Behavior',
            handlerId: 'behavior.condition',
            color: '#6f7652',
            ports: [
                { id: 'in', title: 'In', kind: 'flow', direction: 'input' },
                { id: 'out', title: 'Out', kind: 'flow', direction: 'output' },
            ],
            fields: [
                { name: 'blackboardKey', title: 'Blackboard Key', type: 'string', defaultValue: 'enemyVisible' },
                { name: 'expected', title: 'Expected', type: 'bool', defaultValue: 'true' },
            ],
        },
        {
            id: 'behavior.action',
            title: 'Action',
            category: 'Behavior',
            handlerId: 'behavior.action',
            color: '#7a5d42',
            ports: [
                { id: 'in', title: 'In', kind: 'flow', direction: 'input' },
                { id: 'out', title: 'Out', kind: 'flow', direction: 'output' },
            ],
            fields: [
                { name: 'actionId', title: 'Action Id', type: 'string', defaultValue: 'combat.chase' },
                { name: 'cooldown', title: 'Cooldown', type: 'float', defaultValue: '0.2' },
            ],
        },
    ]);

    const GRAPHKIT_QUEST_NODE_TYPES = Object.freeze([
        {
            id: 'quest.condition',
            title: 'Quest Condition',
            category: 'Quest',
            handlerId: 'quest.condition',
            color: '#5d7652',
            ports: [
                { id: 'in', title: 'In', kind: 'flow', direction: 'input' },
                { id: 'out', title: 'Out', kind: 'flow', direction: 'output' },
            ],
            fields: [
                { name: 'conditionKey', title: 'Condition Key', type: 'string', defaultValue: 'has_item.herb' },
                { name: 'requiredCount', title: 'Required Count', type: 'int', defaultValue: '3' },
            ],
        },
        {
            id: 'quest.reward',
            title: 'Quest Reward',
            category: 'Quest',
            handlerId: 'quest.reward',
            color: '#8a6f26',
            ports: [
                { id: 'in', title: 'In', kind: 'flow', direction: 'input' },
                { id: 'out', title: 'Out', kind: 'flow', direction: 'output' },
            ],
            fields: [
                { name: 'rewardId', title: 'Reward Id', type: 'string', defaultValue: 'reward.herb_bundle' },
                { name: 'amount', title: 'Amount', type: 'int', defaultValue: '1' },
            ],
        },
    ]);

    function getGraphKitTemplates() {
        return GRAPHKIT_TEMPLATE_DEFINITIONS.map(template => ({ ...template }));
    }

    function createGraphKitTemplateProject(templateId) {
        const template = getGraphKitTemplateDefinition(templateId);
        if (template.id === 'behavior.basic') return createGraphKitBehaviorTemplate(template);
        if (template.id === 'quest.flow') return createGraphKitQuestTemplate(template);
        return createGraphKitDialogueTemplate(template);
    }

    function loadGraphKitTemplateProject(templateId) {
        const template = getGraphKitTemplateDefinition(templateId);
        if (typeof graphKitProject === 'undefined' || typeof graphKitState === 'undefined') return false;
        const previousProject = typeof cloneGraphKitProject === 'function'
            ? cloneGraphKitProject(graphKitProject)
            : model.sanitizeGraphKitProject(graphKitProject);
        graphKitProject = createGraphKitTemplateProject(template.id);
        const selectedNodeId = typeof getGraphKitInitialNodeId === 'function'
            ? getGraphKitInitialNodeId(graphKitProject)
            : graphKitProject.graph.nodes[0]?.id || '';

        if (typeof pushGraphKitHistory === 'function') pushGraphKitHistory(previousProject, graphKitProject, `Load template ${template.title}`);
        graphKitState.redoStack = [];
        graphKitState.searchTerm = '';
        graphKitState.selectedNodeId = selectedNodeId;
        graphKitState.selectedNodeIds = selectedNodeId ? [selectedNodeId] : [];
        graphKitState.selectedEdgeId = '';
        graphKitState.selectedBlackboardName = '';
        graphKitState.selectedNoteId = '';
        graphKitState.selectedPlacematId = '';
        graphKitState.selectedNodeTypeId = '';
        if (typeof resetGraphKitFileState === 'function') resetGraphKitFileState();
        graphKitState.pendingPortEndpoint = '';
        graphKitState.viewport = { scale: 1, offsetX: 0, offsetY: 0 };
        graphKitState.contextMenu = typeof getClosedGraphKitContextMenu === 'function'
            ? getClosedGraphKitContextMenu()
            : graphKitState.contextMenu;

        if (typeof setGraphKitNotice === 'function') setGraphKitNotice('success', `已加载样例 ${template.title}。`);
        if (typeof persistGraphKitProject === 'function') persistGraphKitProject();
        if (typeof renderGraphKitWorkbench === 'function') renderGraphKitWorkbench();
        return true;
    }

    function getGraphKitTemplateIdForProject(project) {
        const graphId = String(project?.graph?.id || '').trim();
        const match = GRAPHKIT_TEMPLATE_DEFINITIONS.find(template => template.graphId === graphId);
        return match?.id || '';
    }

    function createGraphKitDialogueTemplate(template) {
        return sanitizeGraphKitTemplateProject(template, [], {
            id: template.graphId,
            title: 'Dialogue Branching',
            kind: template.kind,
            blackboard: [
                { name: 'playerName', type: 'string', defaultValue: '旅行者' },
                { name: 'questAccepted', type: 'bool', defaultValue: 'false' },
            ],
            placemats: [
                { id: 'dialogue_main', title: 'Branching Dialogue', x: 180, y: 86, width: 690, height: 245, color: '#34566f', order: 0, locked: false, collapsed: false, nodeIds: ['line_greeting', 'choice_accept', 'line_accept'] },
            ],
            notes: [
                { id: 'dialogue_note', title: 'Design Note', text: 'Choice 节点只保存数据，接受/离开语义由项目 handler 实现。', x: 220, y: 370, width: 280, height: 116, color: '#6f5a2e' },
            ],
            nodes: [
                { id: 'start', type: 'flow.start', x: 40, y: 170, fields: {} },
                { id: 'line_greeting', type: 'dialogue.line', x: 210, y: 130, fields: { speaker: 'npc_001', text: '你好，{playerName}，可以帮我收集草药吗？' } },
                { id: 'choice_accept', type: 'dialogue.choice', x: 430, y: 120, fields: { prompt: '要接受委托吗？', acceptText: '接受', leaveText: '稍后再说' } },
                { id: 'line_accept', type: 'dialogue.line', x: 650, y: 105, fields: { speaker: 'npc_001', text: '太好了，带回 3 株草药。' } },
                { id: 'end', type: 'flow.end', x: 850, y: 178, fields: {} },
            ],
            edges: [
                { id: 'start_to_greeting', from: 'start.out', to: 'line_greeting.in' },
                { id: 'greeting_to_choice', from: 'line_greeting.out', to: 'choice_accept.in' },
                { id: 'choice_accept_to_line', from: 'choice_accept.accept', to: 'line_accept.in' },
                { id: 'line_accept_to_end', from: 'line_accept.out', to: 'end.in' },
                { id: 'choice_leave_to_end', from: 'choice_accept.leave', to: 'end.in' },
            ],
        });
    }

    function createGraphKitBehaviorTemplate(template) {
        return sanitizeGraphKitTemplateProject(template, GRAPHKIT_BEHAVIOR_NODE_TYPES, {
            id: template.graphId,
            title: 'Behavior Basic',
            kind: template.kind,
            blackboard: [
                { name: 'enemyVisible', type: 'bool', defaultValue: 'false' },
                { name: 'targetId', type: 'string', defaultValue: '' },
            ],
            placemats: [
                { id: 'behavior_loop', title: 'Decision Loop', x: 178, y: 86, width: 700, height: 230, color: '#354a60', order: 0, locked: false, collapsed: false, nodeIds: ['selector_patrol_or_chase', 'condition_enemy_visible', 'action_chase'] },
            ],
            notes: [
                { id: 'behavior_note', title: 'Runtime Note', text: 'Behavior 节点的 selector/action 只声明 handlerId，AI 或引擎侧运行时负责解释。', x: 220, y: 355, width: 300, height: 118, color: '#5d5f37' },
            ],
            nodes: [
                { id: 'start', type: 'flow.start', x: 40, y: 170, fields: {} },
                { id: 'selector_patrol_or_chase', type: 'behavior.selector', x: 230, y: 128, fields: { policy: 'first-success' } },
                { id: 'condition_enemy_visible', type: 'behavior.condition', x: 470, y: 128, fields: { blackboardKey: 'enemyVisible', expected: 'true' } },
                { id: 'action_chase', type: 'behavior.action', x: 710, y: 128, fields: { actionId: 'combat.chase', cooldown: '0.2' } },
                { id: 'end', type: 'flow.end', x: 940, y: 172, fields: {} },
            ],
            edges: [
                { id: 'start_to_selector', from: 'start.out', to: 'selector_patrol_or_chase.in' },
                { id: 'selector_to_condition', from: 'selector_patrol_or_chase.out', to: 'condition_enemy_visible.in' },
                { id: 'condition_to_action', from: 'condition_enemy_visible.out', to: 'action_chase.in' },
                { id: 'action_to_end', from: 'action_chase.out', to: 'end.in' },
            ],
        });
    }

    function createGraphKitQuestTemplate(template) {
        return sanitizeGraphKitTemplateProject(template, GRAPHKIT_QUEST_NODE_TYPES, {
            id: template.graphId,
            title: 'Quest Flow',
            kind: template.kind,
            blackboard: [
                { name: 'questId', type: 'string', defaultValue: 'quest.herb_delivery' },
                { name: 'itemCount', type: 'int', defaultValue: '0' },
            ],
            placemats: [
                { id: 'quest_resolution', title: 'Quest Resolution', x: 180, y: 86, width: 650, height: 232, color: '#455b3a', order: 0, locked: false, collapsed: false, nodeIds: ['check_items', 'grant_reward'] },
            ],
            notes: [
                { id: 'quest_note', title: 'Authoring Note', text: 'Quest Flow 可以接任务表、奖励表或对话表，字段通过 Luban ref/handler 决定实际语义。', x: 220, y: 356, width: 310, height: 118, color: '#6f5a2e' },
            ],
            nodes: [
                { id: 'start', type: 'flow.start', x: 40, y: 170, fields: {} },
                { id: 'check_items', type: 'quest.condition', x: 250, y: 128, fields: { conditionKey: 'has_item.herb', requiredCount: '3' } },
                { id: 'grant_reward', type: 'quest.reward', x: 520, y: 128, fields: { rewardId: 'reward.herb_bundle', amount: '1' } },
                { id: 'end', type: 'flow.end', x: 770, y: 174, fields: {} },
            ],
            edges: [
                { id: 'start_to_check', from: 'start.out', to: 'check_items.in' },
                { id: 'check_to_reward', from: 'check_items.out', to: 'grant_reward.in' },
                { id: 'reward_to_end', from: 'grant_reward.out', to: 'end.in' },
            ],
        });
    }

    function sanitizeGraphKitTemplateProject(template, nodeTypes, graph) {
        const projectNodeTypes = [
            ...cloneGraphKitTemplateValue(model.GRAPHKIT_DEFAULT_NODE_TYPES || []),
            ...cloneGraphKitTemplateValue(nodeTypes || []),
        ];
        return model.sanitizeGraphKitProject({
            version: model.GRAPHKIT_PROJECT_VERSION || '0.1',
            activeGraphId: graph.id,
            graph,
            graphs: [graph],
            nodeTypes: projectNodeTypes,
        });
    }

    function getGraphKitTemplateDefinition(templateId) {
        const id = String(templateId || '').trim();
        return GRAPHKIT_TEMPLATE_DEFINITIONS.find(template => template.id === id) || GRAPHKIT_TEMPLATE_DEFINITIONS[0];
    }

    function cloneGraphKitTemplateValue(value) {
        return JSON.parse(JSON.stringify(value || []));
    }

    const api = {
        createGraphKitTemplateProject,
        getGraphKitTemplateIdForProject,
        getGraphKitTemplates,
        loadGraphKitTemplateProject,
    };

    if (typeof module !== 'undefined' && module.exports) module.exports = api;
    if (root) Object.assign(root, api);
})(typeof window !== 'undefined' ? window : globalThis);
