window.mdi = window.mdi || {};

/* ============================================================
   Drag (titlebar)  ※ transform + rAF(動いている間だけ予約)
   ============================================================ */
window.mdi.initDrag = (element, dotNetHelper) => {
    if (element.__mdiDragDispose) element.__mdiDragDispose();

    const titleBar = element.querySelector(".mdi-titlebar");
    if (!titleBar) return;

    let dragging = false;
    let startX = 0, startY = 0, baseX = 0, baseY = 0, dx = 0, dy = 0, rafId = 0;

    const apply = () => {
        rafId = 0;
        if (dragging) element.style.transform = `translate(${dx}px, ${dy}px)`;
    };

    const onDown = (e) => {
        if (e.button !== 0) return;
        if (element.classList.contains("mdi-maximized")) return;
        // 最小化/最大化/閉じるボタンの操作を奪わない
        if (e.target.closest(".mdi-buttons")) return;

        dragging = true;
        dx = dy = 0;
        startX = e.clientX; startY = e.clientY;
        const r = element.getBoundingClientRect();
        baseX = r.left; baseY = r.top;
        titleBar.setPointerCapture(e.pointerId);
    };

    const onMove = (e) => {
        if (!dragging) return;
        dx = e.clientX - startX;
        dy = e.clientY - startY;
        if (!rafId) rafId = requestAnimationFrame(apply);
    };

    const onUp = (e) => {
        if (!dragging) return;
        dragging = false;
        if (rafId) { cancelAnimationFrame(rafId); rafId = 0; }
        if (titleBar.hasPointerCapture?.(e.pointerId)) titleBar.releasePointerCapture(e.pointerId);

        const x = baseX + dx, y = baseY + dy;
        element.style.transform = "";
        element.style.left = `${x}px`;
        element.style.top = `${y}px`;

        if (dx !== 0 || dy !== 0)
            dotNetHelper.invokeMethodAsync("UpdatePosition", x, y);
    };

    titleBar.addEventListener("pointerdown", onDown);
    titleBar.addEventListener("pointermove", onMove);
    titleBar.addEventListener("pointerup", onUp);
    titleBar.addEventListener("pointercancel", onUp);

    element.__mdiDragDispose = () => {
        titleBar.removeEventListener("pointerdown", onDown);
        titleBar.removeEventListener("pointermove", onMove);
        titleBar.removeEventListener("pointerup", onUp);
        titleBar.removeEventListener("pointercancel", onUp);
        if (rafId) cancelAnimationFrame(rafId);
        delete element.__mdiDragDispose;
    };
};

/* ============================================================
   Multi Resize (8方向: n s e w ne nw se sw)
   ============================================================ */
window.mdi.initResizeMulti = (element, dotNetHelper) => {

    // 二重初期化防止(再 init されたら前のリスナーを破棄)
    if (element.__mdiResizeDispose) element.__mdiResizeDispose();

    const MIN_W = 200, MIN_H = 120;
    const handles = element.querySelectorAll(".resize-handle");
    const cleanups = [];

    // クラス名から方向を取り出す: resize-se → "se", resize-n → "n"
    const getDir = (h) => {
        for (const c of h.classList) {
            const m = /^resize-(n|s|e|w|ne|nw|se|sw)$/.exec(c);
            if (m) return m[1];
        }
        return "";
    };

    let active = null;
    let rafId = 0;
    let dx = 0, dy = 0;

    const apply = () => {
        rafId = 0;
        if (!active) return;
        const { dir, startW, startH, startL, startT } = active;

        let w = startW, h = startH;
        if (dir.includes("e")) w = startW + dx;
        if (dir.includes("s")) h = startH + dy;
        if (dir.includes("w")) w = startW - dx;
        if (dir.includes("n")) h = startH - dy;

        w = Math.max(MIN_W, w);
        h = Math.max(MIN_H, h);

        // 左/上を動かす場合は「右端/下端を固定」するよう位置を逆算
        const l = dir.includes("w") ? startL + startW - w : startL;
        const t = dir.includes("n") ? startT + startH - h : startT;

        element.style.width = `${w}px`;
        element.style.height = `${h}px`;
        element.style.left = `${l}px`;
        element.style.top = `${t}px`;
    };

    handles.forEach(handle => {
        const dir = getDir(handle);
        if (!dir) return;

        const onDown = (e) => {
            if (e.button !== 0) return;
            if (element.classList.contains("mdi-maximized")) return;
            e.preventDefault();
            e.stopPropagation();

            const rect = element.getBoundingClientRect();
            active = {
                dir,
                startX: e.clientX, startY: e.clientY,
                startW: rect.width, startH: rect.height,
                startL: rect.left, startT: rect.top
            };
            dx = dy = 0;
            handle.setPointerCapture(e.pointerId);
        };

        const onMove = (e) => {
            if (!active) return;
            dx = e.clientX - active.startX;
            dy = e.clientY - active.startY;
            if (!rafId) rafId = requestAnimationFrame(apply);
        };

        const onUp = (e) => {
            if (!active) return;
            if (rafId) { cancelAnimationFrame(rafId); rafId = 0; }
            apply();                       // 最終位置を確定
            active = null;
            if (handle.hasPointerCapture?.(e.pointerId)) handle.releasePointerCapture(e.pointerId);

            const r = element.getBoundingClientRect();
            // ★ C# 呼び出しは1回だけ
            dotNetHelper.invokeMethodAsync("UpdateBounds", r.left, r.top, r.width, r.height);
        };

        handle.addEventListener("pointerdown", onDown);
        handle.addEventListener("pointermove", onMove);
        handle.addEventListener("pointerup", onUp);
        handle.addEventListener("pointercancel", onUp);
        cleanups.push(() => {
            handle.removeEventListener("pointerdown", onDown);
            handle.removeEventListener("pointermove", onMove);
            handle.removeEventListener("pointerup", onUp);
            handle.removeEventListener("pointercancel", onUp);
        });
    });

    element.__mdiResizeDispose = () => {
        cleanups.forEach(f => f());
        if (rafId) cancelAnimationFrame(rafId);
        delete element.__mdiResizeDispose;
    };
};

window.mdi.disposeResizeMulti = (element) => element?.__mdiResizeDispose?.();
window.mdi.disposeDrag = (element) => element?.__mdiDragDispose?.();

/* ============================================================
   Screen Size
   ============================================================ */
window.mdi.getScreenSize = () => {
    return {
        width: window.innerWidth,
        height: window.innerHeight
    };
};