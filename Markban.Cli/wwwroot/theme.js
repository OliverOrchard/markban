const THEME_STORAGE_KEY = 'markban-theme';
const systemTheme = window.matchMedia('(prefers-color-scheme: dark)');
let themePreference = readThemePreference();

function readThemePreference() {
    try {
        const saved = localStorage.getItem(THEME_STORAGE_KEY);
        if (saved === null || ['system', 'light', 'dark'].includes(saved)) {
            return saved || 'system';
        }

        console.warn('Ignoring invalid theme preference:', saved);
    } catch (err) {
        console.warn('Unable to read theme preference:', err);
    }

    return 'system';
}

function applyTheme() {
    const theme = themePreference === 'system'
        ? (systemTheme.matches ? 'dark' : 'light')
        : themePreference;
    const changed = document.documentElement.dataset.theme !== theme;
    document.documentElement.dataset.theme = theme;
    document.querySelectorAll('.theme-select').forEach(select => {
        select.value = themePreference;
    });
    if (changed) {
        window.dispatchEvent(new CustomEvent('themechange', { detail: { theme } }));
    }
}

function changeTheme(preference) {
    themePreference = preference;
    const feedback = document.getElementById('theme-feedback');
    try {
        localStorage.setItem(THEME_STORAGE_KEY, preference);
        feedback.textContent = '';
    } catch (err) {
        console.warn('Unable to save theme preference:', err);
        feedback.textContent = 'Theme changed, but this browser could not save your preference.';
    }

    applyTheme();
}

applyTheme();
systemTheme.addEventListener('change', () => {
    if (themePreference === 'system') {
        applyTheme();
    }
});
window.addEventListener('storage', event => {
    if (event.key === THEME_STORAGE_KEY || event.key === null) {
        themePreference = readThemePreference();
        applyTheme();
    }
});
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.theme-select').forEach(select => {
        select.value = themePreference;
        select.addEventListener('change', () => changeTheme(select.value));
    });
});
