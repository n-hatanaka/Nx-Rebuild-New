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
    let startW = 0, startH = 