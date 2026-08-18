window.mdi = window.mdi || {};

// --- Drag ---
window.mdi.initDrag = (element, dotNetHelper) => {
    let isDragging = false;
    let startX, startY, initialX, initialY;

    const onMouseDown = (e) => {
        isDragging = true;
        startX = e.clientX;
        startY = e.clientY;
        initialX = element.offsetLeft;
        initialY = element.offsetTop;

        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup', onMouseUp);
    };

    const onMouseMove = (e) => {
        if (!isDragging) return;
        const dx = e.clientX - startX;
        const dy = e.clientY - startY;
        element.style.left = (initialX + dx) + 'px';
        element.style.top = (initialY + dy) + 'px';
    };

    const onMouseUp = () => {
        if (!isDragging) return;
        isDragging = false;

        document.removeEventListener('mousemove', onMouseMove);
        document.removeEventListener('mouseup', onMouseUp);

        dotNetHelper.invokeMethodAsync('UpdatePosition', element.offsetLeft, element.offsetTop);
    };

    const titleBar = element.querySelector('.mdi-titlebar');
    titleBar.addEventListener('mousedown', onMouseDown);
};

// --- Simple Resize (unused now) ---
window.mdi.initResize = (element, dotNetHelper) => {
    const resizer = document.createElement("div");
    resizer.classList.add("mdi-resizer");
    element.appendChild(resizer);

    let startX, startY, startW, startH;

    resizer.addEventListener("mousedown", (e) => {
        startX = e.clientX;
        startY = e.clientY;
        startW = element.offsetWidth;
        startH = element.offsetHeight;

        document.addEventListener("mousemove", onResize);
        document.addEventListener("mouseup", stopResize);
    });

    function onResize(e) {
        const newW = startW + (e.clientX - startX);
        const newH = startH + (e.clientY - startY);

        element.style.width = newW + "px";
        element.style.height = newH + "px";
    }

    function stopResize() {
        document.removeEventListener("mousemove", onResize);
        document.removeEventListener("mouseup", stopResize);

        dotNetHelper.invokeMethodAsync("UpdateSize",
            element.offsetWidth,
            element.offsetHeight);
    }
};

// --- Multi Resize (8方向) ---
window.mdi.initResizeMulti = (element, dotNetHelper) => {

    // 最大化中はリサイズ禁止
    if (element.classList.contains("mdi-maximized")) return;

    const handles = element.querySelectorAll(".resize-handle");

    handles.forEach(handle => {

        handle.addEventListener("mousedown", (e) => {
            e.preventDefault();

            const rect = element.getBoundingClientRect();
            const startX = e.clientX;
            const startY = e.clientY;

            const startW = rect.width;
            const startH = rect.height;
            const startLeft = rect.left;
            const startTop = rect.top;

            const dir = handle.classList;

            const onMove = (e) => {
                let newW = startW;
                let newH = startH;
                let newLeft = startLeft;
                let newTop = startTop;

                const dx = e.clientX - startX;
                const dy = e.clientY - startY;

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

                element.style.width = newW + "px";
                element.style.height = newH + "px";
                element.style.left = newLeft + "px";
                element.style.top = newTop + "px";
            };

            const onUp = () => {
                document.removeEventListener("mousemove", onMove);
                document.removeEventListener("mouseup", onUp);

                dotNetHelper.invokeMethodAsync("UpdatePosition", element.offsetLeft, element.offsetTop);
                dotNetHelper.invokeMethodAsync("UpdateSize", element.offsetWidth, element.offsetHeight);
            };

            document.addEventListener("mousemove", onMove);
            document.addEventListener("mouseup", onUp);
        });
    });
};

// --- Screen Size ---
window.mdi.getScreenSize = () => {
    return {
        width: window.innerWidth,
        height: window.innerHeight
    };
};