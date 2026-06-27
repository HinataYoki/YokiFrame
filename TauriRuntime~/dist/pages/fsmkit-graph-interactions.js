// pages/fsmkit-graph-interactions.js
// FsmKit 图视口交互：缩放、平移、视口保存和恢复。
function resolveFsmGraphViewportKey(fsmName = '') {
    const scroll = document.getElementById('fsm-graph-scroll');
    return fsmName || scroll?.dataset.fsmName || selectedFsmName || '';
}

function captureFsmGraphViewport(fsmName = '') {
    const scroll = document.getElementById('fsm-graph-scroll');
    const key = resolveFsmGraphViewportKey(fsmName);
    if (!scroll || !key) return;
    const svg = scroll.querySelector('.fsm-graph-svg');
    const pan = scroll.querySelector('.fsm-graph-pan');
    const zoom = Number(scroll.dataset.zoom || svg?.dataset.zoom || '1') || 1;
    fsmGraphViewportByMachine.set(key, {
        scrollLeft: scroll.scrollLeft,
        scrollTop: scroll.scrollTop,
        panX: Number(pan?.dataset.panX || '0') || 0,
        panY: Number(pan?.dataset.panY || '0') || 0,
        zoom,
    });
}

function restoreFsmGraphViewport(fsmName = '') {
    const scroll = document.getElementById('fsm-graph-scroll');
    const key = resolveFsmGraphViewportKey(fsmName);
    if (!scroll || !key) return;
    const viewport = fsmGraphViewportByMachine.get(key);
    if (!viewport) {
        requestAnimationFrame(() => fitFsmGraphToViewport(false));
        return;
    }
    applyFsmGraphZoom(viewport?.zoom ?? 1, false);
    applyFsmGraphPan(viewport?.panX ?? 0, viewport?.panY ?? 0, false);
    requestAnimationFrame(() => {
        scroll.scrollLeft = viewport.scrollLeft;
        scroll.scrollTop = viewport.scrollTop;
    });
}

function applyFsmGraphZoom(zoom, persist = true) {
    const scroll = document.getElementById('fsm-graph-scroll');
    const svg = scroll?.querySelector('.fsm-graph-svg');
    const pan = scroll?.querySelector('.fsm-graph-pan');
    if (!scroll || !svg || !pan) return;
    const nextZoom = Math.max(0.35, Math.min(1.8, zoom));
    const baseWidth = Number(svg.getAttribute('data-base-width') || svg.getAttribute('width')) || 360;
    const baseHeight = Number(svg.getAttribute('data-base-height') || svg.getAttribute('height')) || 200;
    svg.setAttribute('data-base-width', String(baseWidth));
    svg.setAttribute('data-base-height', String(baseHeight));
    svg.setAttribute('data-zoom', String(nextZoom));
    pan.style.width = `${Math.round(baseWidth * nextZoom)}px`;
    pan.style.height = `${Math.round(baseHeight * nextZoom)}px`;
    svg.style.width = `${Math.round(baseWidth * nextZoom)}px`;
    svg.style.height = `${Math.round(baseHeight * nextZoom)}px`;
    scroll.dataset.zoom = String(nextZoom);
    const label = document.getElementById('fsm-graph-zoom-label');
    if (label) label.textContent = `${Math.round(nextZoom * 100)}%`;
    applyFsmGraphPan(Number(pan.dataset.panX || '0') || 0, Number(pan.dataset.panY || '0') || 0, false);
    if (persist) captureFsmGraphViewport();
}

function applyFsmGraphPan(panX, panY, persist = true) {
    const scroll = document.getElementById('fsm-graph-scroll');
    const pan = scroll?.querySelector('.fsm-graph-pan');
    if (!scroll || !pan) return;
    pan.dataset.panX = String(Math.round(panX));
    pan.dataset.panY = String(Math.round(panY));
    pan.style.transform = `translate(${Math.round(panX)}px, ${Math.round(panY)}px)`;
    if (persist) captureFsmGraphViewport();
}

function zoomFsmGraphAtPoint(nextZoom, clientX, clientY) {
    const scroll = document.getElementById('fsm-graph-scroll');
    if (!scroll) return;
    const beforeZoom = Number(scroll.dataset.zoom || '1') || 1;
    const rect = scroll.getBoundingClientRect();
    const beforeLeft = scroll.scrollLeft + clientX - rect.left;
    const beforeTop = scroll.scrollTop + clientY - rect.top;
    applyFsmGraphZoom(nextZoom, false);
    const afterZoom = Number(scroll.dataset.zoom || '1') || 1;
    const ratio = afterZoom / beforeZoom;
    scroll.scrollLeft = beforeLeft * ratio - (clientX - rect.left);
    scroll.scrollTop = beforeTop * ratio - (clientY - rect.top);
    captureFsmGraphViewport();
}

function fitFsmGraphToViewport(persist = true) {
    const scroll = document.getElementById('fsm-graph-scroll');
    const svg = scroll?.querySelector('.fsm-graph-svg');
    if (!scroll || !svg) return;
    const baseWidth = Number(svg.getAttribute('data-base-width') || svg.getAttribute('width')) || 360;
    const baseHeight = Number(svg.getAttribute('data-base-height') || svg.getAttribute('height')) || 200;
    const widthZoom = (scroll.clientWidth - 32) / baseWidth;
    const heightZoom = (scroll.clientHeight - 32) / baseHeight;
    const fitZoom = Math.min(1, widthZoom, heightZoom);
    applyFsmGraphZoom(Math.max(0.35, fitZoom), false);
    applyFsmGraphPan(0, 0, false);
    scroll.scrollLeft = 0;
    scroll.scrollTop = 0;
    if (persist) captureFsmGraphViewport();
}

// 图交互：滚轮缩放、按住拖拽平移，并保留工具按钮。
function bindFsmGraphInteractions() {
    const fit = document.getElementById('fsm-graph-fit');
    const zoomIn = document.getElementById('fsm-graph-zoom-in');
    const zoomOut = document.getElementById('fsm-graph-zoom-out');
    const scroll = document.getElementById('fsm-graph-scroll');
    if (fit && fit.dataset.bound !== '1') {
        fit.dataset.bound = '1';
        fit.addEventListener('click', () => {
            fitFsmGraphToViewport(true);
        });
    }
    if (zoomIn && zoomIn.dataset.bound !== '1') {
        zoomIn.dataset.bound = '1';
        zoomIn.addEventListener('click', () => zoomFsmGraphAtPoint(Number(scroll?.dataset.zoom || '1') + 0.15, scroll?.clientWidth / 2 ?? 0, scroll?.clientHeight / 2 ?? 0));
    }
    if (zoomOut && zoomOut.dataset.bound !== '1') {
        zoomOut.dataset.bound = '1';
        zoomOut.addEventListener('click', () => zoomFsmGraphAtPoint(Number(scroll?.dataset.zoom || '1') - 0.15, scroll?.clientWidth / 2 ?? 0, scroll?.clientHeight / 2 ?? 0));
    }
    if (scroll && scroll.dataset.bound !== '1') {
        scroll.dataset.bound = '1';
        scroll.addEventListener('scroll', () => captureFsmGraphViewport(), { passive: true });
        scroll.addEventListener('wheel', event => {
            event.preventDefault();
            const delta = event.deltaY < 0 ? 0.12 : -0.12;
            zoomFsmGraphAtPoint(Number(scroll.dataset.zoom || '1') + delta, event.clientX, event.clientY);
        }, { passive: false });
        let dragState = null;
        scroll.addEventListener('pointerdown', event => {
            if (event.button !== 0) return;
            const pan = scroll.querySelector('.fsm-graph-pan');
            dragState = {
                pointerId: event.pointerId,
                startX: event.clientX,
                startY: event.clientY,
                panX: Number(pan?.dataset.panX || '0') || 0,
                panY: Number(pan?.dataset.panY || '0') || 0,
            };
            scroll.classList.add('fsm-graph-scroll--panning');
            scroll.setPointerCapture?.(event.pointerId);
        });
        scroll.addEventListener('pointermove', event => {
            if (!dragState || dragState.pointerId !== event.pointerId) return;
            applyFsmGraphPan(
                dragState.panX + event.clientX - dragState.startX,
                dragState.panY + event.clientY - dragState.startY
            );
        });
        const endDrag = event => {
            if (!dragState || dragState.pointerId !== event.pointerId) return;
            dragState = null;
            scroll.classList.remove('fsm-graph-scroll--panning');
            scroll.releasePointerCapture?.(event.pointerId);
            captureFsmGraphViewport();
        };
        scroll.addEventListener('pointerup', endDrag);
        scroll.addEventListener('pointercancel', endDrag);
    }
    restoreFsmGraphViewport();
}
