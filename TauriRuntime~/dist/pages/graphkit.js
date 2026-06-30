// pages/graphkit.js
// GraphKit XML Graph prototype: Graph Toolkit style editing surface backed by Luban-friendly data.
const GRAPHKIT_NODE_WIDTH = 250;
const GRAPHKIT_NODE_MIN_HEIGHT = 104;
const GRAPHKIT_NODE_COLLAPSED_HEIGHT = 78;
const GRAPHKIT_NODE_HEADER_HEIGHT = 48;
const GRAPHKIT_NODE_FIELD_HEIGHT = 23;
const GRAPHKIT_CANVAS_WIDTH = 2200;
const GRAPHKIT_CANVAS_HEIGHT = 1100;
const GRAPHKIT_NODE_ORIGIN_X = 320;
const GRAPHKIT_NODE_ORIGIN_Y = 260;
const GRAPHKIT_NODE_MODEL_SCALE_X = 1.9;
const GRAPHKIT_NODE_MODEL_SCALE_Y = 1.18;
const GRAPHKIT_VIEWPORT_MIN_SCALE = 0.45;
const GRAPHKIT_VIEWPORT_MAX_SCALE = 1.8;
const GRAPHKIT_VIEWPORT_STEP = 1.12;
const GRAPHKIT_LOD_NODE_COUNT_THRESHOLD = 80;
const GRAPHKIT_LOD_SCALE_THRESHOLD = 0.72;
const GRAPHKIT_MINIMAP_WIDTH = 190;
const GRAPHKIT_MINIMAP_HEIGHT = 95;
const GRAPHKIT_FIT_VIEW_PADDING = 120;
let graphKitViewportDrag = null;
let graphKitNodeDrag = null;
let graphKitMiniMapDrag = null;
let graphKitOrganizationDrag = null;
let graphKitWireDrag = null;
let graphKitBlackboardDrag = null;
let graphKitWireDragSuppressClick = false;
