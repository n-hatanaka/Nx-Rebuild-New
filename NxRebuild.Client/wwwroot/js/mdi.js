window.mdi = window.mdi || {};

/* ============================================================
   Drag (transform + rAF)
   ============================================================ */
window.mdi.initDrag = (element, dotNetHelper) => {

    let dragging = false;
    let startX = 0, startY = 0;
    let baseX = 0, baseY = 0;
    let dx = 0, dy = 0;

    const onDown = (e) => {
        dragging = true;

        startX = e.clientX;
        startY = e.clientY;

        const rect = element.getBoundingClientRect();
        baseX = rect.left;
        baseY = rect.top;

        window.addEventListener("pointermove", onMove);
        window.addEventListener("pointerup", onUp);
    };

    const onMove = (e) => {
        if (!dragging) return;
        dx = e.clientX - startX;
        dy = e.clientY - startY;
    };

    const onUp = () => {
        if (!dragging) return;
        dragging = false;

        window.removeEventListener("pointermove", onMove);
        window.removeEventListener("pointerup", onUp);

        const finalX = baseX + dx;
        const finalY = baseY + dy;

        // transform を確定位置に戻す
        element.style.transform = `translate(0px, 0px)`;
        element.style.left = `${finalX}px`;
        element.style.top = `${finalY}px`;

        dotNetHelper.invokeMethodAsync("UpdatePosition", finalX, finalY);
    };

    // rAF で DOM 更新を集約
    const renderLoop = () => {
        if (dragging) {
            element.style.transform = `translate(${dx}px, ${dy}px)`;
        }
        requestAnimationFrame(renderLoop);
    };
    renderLoop();

    const titleBar = element.querySelector(".mdi-titlebar");
    titleBar.addEventListener("pointerdown", onDown);
};

/* ============================================================
   Simple Resize (transform + rAF)
   ============================================================ */
window.mdi.initResize = (element, dotNetHelper) => {

    const resizer = document.createElement("div");
    resizer.classList.add("mdi-resizer");
    element.appendChild(resizer);

    let resizing = false;
    let startX = 0, startY = 0;
    let startW = 0, startH = 0;

    const onDown = (e) => {
        resizing = true;

        startX = e.clientX;
        startY = e.clientY;

        startW = element.offsetWidth;
        startH = element.offsetHeight;

        window.addEventListener("pointermove", onMove);
        window.addEventListener("pointerup", onUp);
    };

    let dx = 0, dy = 0;

    const onMove = (e) => {
        if (!resizing) return;
        dx = e.clientX - startX;
        dy = e.clientY - startY;
    };

    const renderLoop = () => {
        if (resizing) {
            element.style.width = `${startW + dx}px`;
            element.style.height = `${startH + dy}px`;
        }
        requestAnimationFrame(renderLoop);
    };
    renderLoop();

    const onUp = () => {
        if (!resizing) return;
        resizing = false;

        window.removeEventListener("pointermove", onMove);
        window.removeEventListener("pointerup", onUp);

        dotNetHelper.invokeMethodAsync("UpdateSize",
            element.offsetWidth,
            element.offsetHeight);
    };

    resizer.addEventListener("pointerdown", onDown);
};

/* ============================================================
   Multi Resize (8方向) transform + rAF
   ============================================================ */
window.mdi.initResizeMulti = (element, dotNetHelper) => {

    if (element.classList.contains("mdi-maximized")) return;

    const handles = element.querySelectorAll(".resize-handle");

    let resizing = false;
    let startX = 0, startY = 0;
    let startW = 0, startH = 0;
    let startLeft = 0, startTop = 0;
    let dx = 0, dy = 0;
    let dir = null;

    handles.forEach(handle => {

        handle.addEventListener("pointerdown", (e) => {
            e.preventDefault();

            resizing = true;

            const rect = element.getBoundingClientRect();
            startX = e.clientX;
            startY = e.clientY;

            startW = rect.width;
            startH = rect.height;
            startLeft = rect.left;
            startTop = rect.top;

            dx = dy = 0;
            dir = handle.classList;

            window.addEventListener("pointermove", onMove);
            window.addEventListener("pointerup", onUp);
        });
    });

    const onMove = (e) => {
        if (!resizing) return;
        dx = e.clientX - startX;
        dy = e.clientY - startY;
    };

    const renderLoop = () => {
        if (resizing) {

            let newW = startW;
            let newH = startH;
            let newLeft = startLeft;
            let newTop = startTop;

            if (dir.contains("resize-e")) newW = startW + dx;
            if (dir.contains("resize-s")) newH = startH + dy;

            if (dir.contains("resize-w")) {
                newW = startW - dx;
                newLeft = startLeft + dx;
            }

            if (dir.contains("resize-n")) {
                newH = startH - dy;
                newTop = startTop + dy;
            }

            element.style.width = `${newW}px`;
            element.style.height = `${newH}px`;
            element.style.transform = `translate(${newLeft - startLeft}px, ${newTop - startTop}px)`;
        }

        requestAnimationFrame(renderLoop);
    };
    renderLoop();

    const onUp = () => {
        if (!resizing) return;
        resizing = false;

        window.removeEventListener("pointermove", onMove);
        window.removeEventListener("pointerup", onUp);

        const rect = element.getBoundingClientRect();

        element.style.transform = `translate(0px, 0px)`;
        element.style.left = `${rect.left}px`;
        element.style.top = `${rect.top}px`;

        dotNetHelper.invokeMethodAsync("UpdatePosition", rect.left, rect.top);
        dotNetHelper.invokeMethodAsync("UpdateSize", rect.width, rect.height);
    };
};

/* ============================================================
   Screen Size
   ============================================================ */
window.mdi.getScreenSize = () => {
    return {
        width: window.innerWidth,
        height: window.innerHeight
    };
};
