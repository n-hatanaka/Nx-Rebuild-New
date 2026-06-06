window.mdi = {
    initDrag: (element, dotNetHelper) => {
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

        const onMouseUp = (e) => {
            if (!isDragging) return;
            isDragging = false;

            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup', onMouseUp);

            dotNetHelper.invokeMethodAsync('UpdatePosition', element.offsetLeft, element.offsetTop);
        };

        const titleBar = element.querySelector('.mdi-titlebar');
        titleBar.addEventListener('mousedown', onMouseDown);
    },

    initResize: (element, dotNetHelper) => {
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
    },
    getScreenSize:() => {
        return {
            width: window.innerWidth,
            height: window.innerHeight
        };
    }
};
