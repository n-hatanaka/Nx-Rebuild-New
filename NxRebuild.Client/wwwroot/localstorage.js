window.localStore = {
    get: (key) => localStorage.getItem(key),
    set: (key, value) => localStorage.setItem(key, value)
};
