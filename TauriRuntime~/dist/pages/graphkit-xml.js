// pages/graphkit-xml.js
// GraphKit XML import helpers for the Luban-friendly graph project format.
(function registerGraphKitXmlApi(root) {
    const model = typeof module !== 'undefined' && module.exports
        ? require('./graphkit-model.js')
        : root;

    function parseGraphKitXml(xmlText) {
        const documentNode = parseGraphKitXmlTree(xmlText);
        const projectNode = findFirstGraphKitXmlChild(documentNode, 'graphProject');
        if (!projectNode) throw new Error('XML 缺少 graphProject 根节点。');

        const nodeTypes = parseGraphKitXmlNodeTypes(projectNode);
        const graphs = parseGraphKitXmlGraphs(projectNode);
        return model.sanitizeGraphKitProject({
            version: getGraphKitXmlAttribute(projectNode, 'version', model.GRAPHKIT_PROJECT_VERSION),
            nodeTypes,
            graph: graphs[0],
            graphs,
            activeGraphId: graphs[0]?.id || '',
        });
    }

    function parseGraphKitXmlNodeTypes(projectNode) {
        const nodeTypesNode = findFirstGraphKitXmlChild(projectNode, 'nodeTypes');
        const nodes = nodeTypesNode ? findGraphKitXmlChildren(nodeTypesNode, 'nodeType') : [];
        return nodes.map(nodeType => {
            const portsNode = findFirstGraphKitXmlChild(nodeType, 'ports');
            const fieldsNode = findFirstGraphKitXmlChild(nodeType, 'fields');
            return {
                id: getGraphKitXmlAttribute(nodeType, 'id', ''),
                title: getGraphKitXmlAttribute(nodeType, 'title', ''),
                category: getGraphKitXmlAttribute(nodeType, 'category', 'Custom'),
                handlerId: getGraphKitXmlAttribute(nodeType, 'handlerId', getGraphKitXmlAttribute(nodeType, 'handler', '')),
                color: getGraphKitXmlAttribute(nodeType, 'color', ''),
                ports: portsNode ? findGraphKitXmlChildren(portsNode, 'port').map(port => ({
                    id: getGraphKitXmlAttribute(port, 'id', ''),
                    title: getGraphKitXmlAttribute(port, 'title', ''),
                    kind: getGraphKitXmlAttribute(port, 'kind', 'flow'),
                    direction: getGraphKitXmlAttribute(port, 'direction', 'output'),
                    multiple: getGraphKitXmlAttribute(port, 'multiple', 'false'),
                })) : [],
                fields: fieldsNode ? findGraphKitXmlChildren(fieldsNode, 'field').map(field => ({
                    name: getGraphKitXmlAttribute(field, 'name', ''),
                    title: getGraphKitXmlAttribute(field, 'title', ''),
                    type: getGraphKitXmlAttribute(field, 'type', 'string'),
                    ref: getGraphKitXmlAttribute(field, 'ref', ''),
                    defaultValue: getGraphKitXmlAttribute(field, 'defaultValue', ''),
                    required: getGraphKitXmlAttribute(field, 'required', 'false'),
                    options: getGraphKitXmlAttribute(field, 'options', ''),
                })) : [],
            };
        });
    }

    function parseGraphKitXmlGraphs(projectNode) {
        const graphsNode = findFirstGraphKitXmlChild(projectNode, 'graphs');
        const graphNodes = graphsNode ? findGraphKitXmlChildren(graphsNode, 'graph') : [];
        if (!graphNodes.length) throw new Error('XML 缺少 graph 数据。');
        return graphNodes.map(parseGraphKitXmlGraphNode);
    }

    function parseGraphKitXmlGraphNode(graphNode) {
        const blackboardNode = findFirstGraphKitXmlChild(graphNode, 'blackboard');
        const nodesNode = findFirstGraphKitXmlChild(graphNode, 'nodes');
        const edgesNode = findFirstGraphKitXmlChild(graphNode, 'edges');
        const placematsNode = findFirstGraphKitXmlChild(graphNode, 'placemats');
        const notesNode = findFirstGraphKitXmlChild(graphNode, 'notes');
        return {
            id: getGraphKitXmlAttribute(graphNode, 'id', ''),
            title: getGraphKitXmlAttribute(graphNode, 'title', ''),
            kind: getGraphKitXmlAttribute(graphNode, 'type', getGraphKitXmlAttribute(graphNode, 'kind', '')),
            blackboard: blackboardNode ? findGraphKitXmlChildren(blackboardNode, 'var').map(item => ({
                name: getGraphKitXmlAttribute(item, 'name', ''),
                type: getGraphKitXmlAttribute(item, 'type', 'string'),
                section: getGraphKitXmlAttribute(item, 'section', model.GRAPHKIT_DEFAULT_BLACKBOARD_SECTION || 'Default'),
                defaultValue: getGraphKitXmlText(item),
            })) : [],
            nodes: nodesNode ? findGraphKitXmlChildren(nodesNode, 'node').map(node => ({
                id: getGraphKitXmlAttribute(node, 'id', ''),
                type: getGraphKitXmlAttribute(node, 'type', ''),
                x: getGraphKitXmlAttribute(node, 'x', '0'),
                y: getGraphKitXmlAttribute(node, 'y', '0'),
                collapsed: getGraphKitXmlAttribute(node, 'collapsed', 'false'),
                fields: parseGraphKitXmlNodeFields(node),
            })) : [],
            edges: edgesNode ? findGraphKitXmlChildren(edgesNode, 'edge').map(edge => ({
                id: getGraphKitXmlAttribute(edge, 'id', ''),
                from: getGraphKitXmlAttribute(edge, 'from', ''),
                to: getGraphKitXmlAttribute(edge, 'to', ''),
                label: getGraphKitXmlAttribute(edge, 'label', ''),
                condition: getGraphKitXmlAttribute(edge, 'condition', ''),
                priority: getGraphKitXmlAttribute(edge, 'priority', '0'),
            })) : [],
            placemats: parseGraphKitXmlPlacemats(placematsNode),
            notes: parseGraphKitXmlNotes(notesNode),
        };
    }

    function parseGraphKitXmlPlacemats(placematsNode) {
        return placematsNode ? findGraphKitXmlChildren(placematsNode, 'placemat').map(item => ({
            id: getGraphKitXmlAttribute(item, 'id', ''),
            title: getGraphKitXmlAttribute(item, 'title', ''),
            x: getGraphKitXmlAttribute(item, 'x', '0'),
            y: getGraphKitXmlAttribute(item, 'y', '0'),
            width: getGraphKitXmlAttribute(item, 'width', '320'),
            height: getGraphKitXmlAttribute(item, 'height', '180'),
            color: getGraphKitXmlAttribute(item, 'color', ''),
            order: getGraphKitXmlAttribute(item, 'order', '0'),
            locked: getGraphKitXmlAttribute(item, 'locked', 'false'),
            collapsed: getGraphKitXmlAttribute(item, 'collapsed', 'false'),
            nodeIds: findGraphKitXmlChildren(item, 'member').map(member => getGraphKitXmlAttribute(member, 'node', '')).filter(Boolean),
        })) : [];
    }

    function parseGraphKitXmlNotes(notesNode) {
        return notesNode ? findGraphKitXmlChildren(notesNode, 'note').map(item => ({
            id: getGraphKitXmlAttribute(item, 'id', ''),
            title: getGraphKitXmlAttribute(item, 'title', ''),
            text: getGraphKitXmlText(item),
            x: getGraphKitXmlAttribute(item, 'x', '0'),
            y: getGraphKitXmlAttribute(item, 'y', '0'),
            width: getGraphKitXmlAttribute(item, 'width', '180'),
            height: getGraphKitXmlAttribute(item, 'height', '110'),
            color: getGraphKitXmlAttribute(item, 'color', ''),
        })) : [];
    }

    function parseGraphKitXmlNodeFields(node) {
        const fields = {};
        findGraphKitXmlChildren(node, 'field').forEach(field => {
            fields[getGraphKitXmlAttribute(field, 'name', '')] = getGraphKitXmlText(field);
        });
        return fields;
    }

    function parseGraphKitXmlTree(xmlText) {
        const source = String(xmlText ?? '').trim();
        if (!source) throw new Error('XML 内容为空。');
        const rootNode = { tagName: '#document', attributes: {}, children: [], text: '' };
        const stack = [rootNode];
        const tokenPattern = /<\?[\s\S]*?\?>|<!--[\s\S]*?-->|<!\[CDATA\[[\s\S]*?\]\]>|<\/?[^>]+>|[^<]+/g;
        let tokenMatch;

        while ((tokenMatch = tokenPattern.exec(source)) !== null) {
            const token = tokenMatch[0];
            if (!token || token.startsWith('<?') || token.startsWith('<!--')) continue;
            if (token.startsWith('<![CDATA[')) {
                stack[stack.length - 1].text += token.slice(9, -3);
                continue;
            }
            if (token[0] !== '<') {
                stack[stack.length - 1].text += decodeGraphKitXmlText(token);
                continue;
            }
            if (token[1] === '/') {
                const tagName = token.slice(2, -1).trim();
                const current = stack.pop();
                if (!current || current.tagName !== tagName) throw new Error(`XML 节点闭合不匹配：${tagName}。`);
                continue;
            }

            const selfClosing = /\/\s*>$/.test(token);
            const content = token.slice(1, selfClosing ? token.lastIndexOf('/') : -1).trim();
            const tagMatch = /^([^\s/>]+)/.exec(content);
            if (!tagMatch) continue;
            const node = {
                tagName: tagMatch[1],
                attributes: parseGraphKitXmlAttributes(content.slice(tagMatch[0].length)),
                children: [],
                text: '',
            };
            stack[stack.length - 1].children.push(node);
            if (!selfClosing) stack.push(node);
        }

        if (stack.length !== 1) throw new Error('XML 存在未闭合节点。');
        return rootNode;
    }

    function parseGraphKitXmlAttributes(source) {
        const attributes = {};
        const attributePattern = /([A-Za-z_:][\w:.-]*)\s*=\s*("([^"]*)"|'([^']*)')/g;
        let match;
        while ((match = attributePattern.exec(source)) !== null) {
            attributes[match[1]] = decodeGraphKitXmlText(match[3] !== undefined ? match[3] : match[4]);
        }
        return attributes;
    }

    function findFirstGraphKitXmlChild(node, tagName) {
        return findGraphKitXmlChildren(node, tagName)[0] || null;
    }

    function findGraphKitXmlChildren(node, tagName) {
        return (node?.children || []).filter(child => child.tagName === tagName);
    }

    function getGraphKitXmlAttribute(node, name, fallback) {
        const value = node?.attributes ? node.attributes[name] : undefined;
        return value === undefined || value === '' ? fallback : value;
    }

    function getGraphKitXmlText(node) {
        return String(node?.text ?? '').trim();
    }

    function decodeGraphKitXmlText(value) {
        return String(value ?? '')
            .replace(/&quot;/g, '"')
            .replace(/&apos;/g, "'")
            .replace(/&lt;/g, '<')
            .replace(/&gt;/g, '>')
            .replace(/&amp;/g, '&');
    }

    const graphKitXmlApi = {
        parseGraphKitXml,
    };

    if (typeof module !== 'undefined' && module.exports) {
        module.exports = graphKitXmlApi;
    }

    if (root) {
        Object.assign(root, graphKitXmlApi);
    }
})(typeof window !== 'undefined' ? window : globalThis);
