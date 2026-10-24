let allWorkItems = [];
let currentDetailItem = null;
let lanes = [];
let hiddenLanes = new Set();
let currentBoardKey = null;
let activeTagFilter = null;

const BOARD_STORAGE_KEY = 'markban-board';

// ── Library initialisation ────────────────────────────────────────────────────

mermaid.initialize({ startOnLoad: false, theme: document.documentElement.dataset.theme === 'dark' ? 'dark' : 'default' });
let mermaidRenderQueue = Promise.resolve();

window.addEventListener('themechange', () => {
    const content = document.getElementById('detail-content');
    renderMermaidBlocks(content).catch(err => console.error('Diagram theme update failed:', err));
});

marked.use({
    gfm: true,
    renderer: buildMarkedRenderer(),
});

function buildMarkedRenderer() {
    return {
        code({ text, lang }) {
            if (lang === 'mermaid') {
                return `<div class="mermaid">${esc(text)}</div>`;
            }

            const language = lang && hljs.getLanguage(lang) ? lang : null;
            const highlighted = language
                ? hljs.highlight(text, { language }).value
                : esc(text);
            const langClass = language ? ` class="hljs language-${language}"` : ' class="hljs"';
            return `<pre><code${langClass}>${highlighted}</code></pre>`;
        },
    };
}

function renderMermaidBlocks(container) {
    const blocks = Array.from(container.querySelectorAll('div.mermaid'));
    if (blocks.length === 0) {
        return Promise.resolve();
    }

    const render = mermaidRenderQueue.then(() => renderDiagrams(blocks));
    mermaidRenderQueue = render.catch(err => console.error('Diagram rendering failed:', err));
    return render;
}

async function renderDiagrams(blocks) {
    mermaid.initialize({
        startOnLoad: false,
        theme: document.documentElement.dataset.theme === 'dark' ? 'dark' : 'default',
    });
    for (const block of blocks) {
        if (!block.isConnected) {
            continue;
        }

        const definition = block.dataset.definition ?? block.textContent ?? '';
        block.dataset.definition = definition;
        const id = 'mermaid-' + Math.random().toString(36).slice(2);
        const renderHost = document.createElement('div');
        block.replaceChildren(renderHost);
        try {
            const { svg } = await mermaid.render(id, definition, renderHost);
            block.innerHTML = svg;
        } catch (err) {
            block.innerHTML = `<div class="mermaid-error">Diagram error: ${esc(String(err?.message ?? err))}</div>`;
        }
    }
}

// ── Utilities ─────────────────────────────────────────────────────────────────

function esc(str) {
    return String(str ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
}

function buildCardBadges(item) {
    let html = '';
    if (item.blocked) {
        html += `<span class="badge badge-blocked" title="${esc(item.blocked)}">⊘ blocked</span>`;
    }

    if (item.tags && item.tags.length > 0) {
        html += item.tags.map(t => `<span class="badge badge-tag" data-tag="${esc(t)}" onclick="filterByTag('${esc(t)}')">${esc(t)}</span>`).join('');
    }

    return html ? `<div class="card-badges">${html}</div>` : '';
}

async function getJson(url, fallbackMessage) {
    const response = await fetch(url);
    const data = await response.json();
    if (!response.ok) {
        throw new Error(data.error || fallbackMessage);
    }

    return data;
}

async function initBoards() {
    try {
        const boards = await getJson('/api/boards', 'Failed to load boards.');
        if (!Array.isArray(boards) || boards.length === 0) {
            return;
        }

        const container = document.getElementById('board-switcher-container');
        const select = document.getElementById('board-select');
        select.innerHTML = '';
        boards.forEach(board => {
            const option = document.createElement('option');
            option.value = board.key;
            option.textContent = board.name;
            select.appendChild(option);
        });

        const savedBoard = localStorage.getItem(BOARD_STORAGE_KEY);
        const validKey = boards.find(board => board.key === savedBoard)?.key || boards[0].key;
        select.value = validKey;
        currentBoardKey = validKey;
        container.style.display = 'block';
    } catch (err) {
        console.error('Failed to load boards:', err);
    }
}

function onBoardChange(key) {
    currentBoardKey = key;
    localStorage.setItem(BOARD_STORAGE_KEY, key);
    hiddenLanes = new Set();
    initLanes().then(() => {
        buildBoard();
        loadItems();
    });
}

async function initLanes() {
    try {
        const url = currentBoardKey ? `/api/lanes?board=${encodeURIComponent(currentBoardKey)}` : '/api/lanes';
        lanes = await getJson(url, 'Failed to load lanes.');
    } catch (err) {
        lanes = [];
        console.error('Failed to load lanes:', err);
    }
}

function buildBoard() {
    const kanban = document.getElementById('kanban');
    kanban.innerHTML = '';
    const moveSelect = document.getElementById('move-target');
    moveSelect.innerHTML = '<option value="">Move to\u2026</option>';
    lanes.forEach(lane => {
        const colId = `col-${lane.replace(/ /g, '-')}`;
        const col = document.createElement('div');
        col.className = 'column';
        col.setAttribute('data-lane', lane);
    col.innerHTML = `<h2>${lane} <span class="col-count"></span><span class="col-blocked-count"></span></h2><div class="cards" id="${colId}"></div>`;
        kanban.appendChild(col);
        const option = document.createElement('option');
        option.value = lane;
        option.textContent = lane;
        moveSelect.appendChild(option);
    });
}

document.addEventListener('keydown', event => {
    if (event.key === 'Escape') {
        closeDetail();
    }
});

window.addEventListener('hashchange', () => {
    const slug = window.location.hash.slice(1);
    if (slug) {
        const item = allWorkItems.find(workItem => workItem.slug === slug);
        if (item) {
            showDetail(item, false);
        }
    } else {
        closeDetail(false);
    }
});

function buildToolbar() {
    const toolbar = document.getElementById('toolbar');
    toolbar.innerHTML = '';
    lanes.forEach(lane => {
        const button = document.createElement('button');
        button.className = 'filter-btn active';
        button.setAttribute('aria-pressed', 'true');
        button.setAttribute('data-lane', lane);
        const count = allWorkItems.filter(item => item.status === lane).length;
        button.innerHTML = `${lane}<span class="count">(${count})</span>`;
        button.onclick = () => toggleLane(lane, button);
        toolbar.appendChild(button);
    });

    const separator = document.createElement('div');
    separator.className = 'toolbar-separator';
    toolbar.appendChild(separator);

    const refresh = document.createElement('button');
    refresh.className = 'refresh-btn';
    refresh.textContent = '\u21bb Refresh';
    refresh.onclick = () => loadItems();
    toolbar.appendChild(refresh);
}

function toggleLane(lane, button) {
    const column = document.querySelector(`.column[data-lane="${lane}"]`);
    if (hiddenLanes.has(lane)) {
        hiddenLanes.delete(lane);
        button.classList.add('active');
        button.setAttribute('aria-pressed', 'true');
        column.style.display = '';
        return;
    }

    hiddenLanes.add(lane);
    button.classList.remove('active');
    button.setAttribute('aria-pressed', 'false');
    column.style.display = 'none';
}

function updateColumnCounts() {
    document.querySelectorAll('.column').forEach(column => {
        const lane = column.getAttribute('data-lane');
        const count = column.querySelectorAll('.card:not(.hidden)').length;
        const span = column.querySelector('.col-count');
        if (span) {
            span.textContent = `(${count})`;
        }

        const blockedCount = allWorkItems.filter(i => i.status === lane && i.blocked).length;
        const blockedSpan = column.querySelector('.col-blocked-count');
        if (blockedSpan) {
            blockedSpan.textContent = blockedCount > 0 ? ` · ${blockedCount} blocked` : '';
        }
    });
}

async function loadItems() {
    try {
        const url = currentBoardKey ? `/api/items?board=${encodeURIComponent(currentBoardKey)}` : '/api/items';
        allWorkItems = await getJson(url, 'Failed to load work items.');

        document.querySelectorAll('.cards').forEach(cards => {
            cards.innerHTML = '';
        });

        allWorkItems.forEach(item => {
            const colId = `col-${item.status.replace(' ', '-')}`;
            const column = document.getElementById(colId);
            if (!column) {
                return;
            }

            const card = document.createElement('div');
            card.className = `card ${item.status.replace(' ', '-')}`;
            card.setAttribute('data-id', item.id);
            card.setAttribute('data-slug', item.slug);
            card.setAttribute('data-content', item.content.toLowerCase());
            card.setAttribute('data-status', item.status);
            card.setAttribute('data-tags', item.tags ? item.tags.join(',') : '');
            const cardBadges = buildCardBadges(item);
            card.innerHTML = `<span class="card-id">${esc(item.id)}</span>${esc(item.slug)}${cardBadges}`;
            card.draggable = true;
            card.addEventListener('dragstart', onCardDragStart);
            card.addEventListener('dragend', onCardDragEnd);
            card.onclick = event => {
                if (wasDragging) {
                    wasDragging = false;
                    return;
                }

                event.preventDefault();
                showDetail(item);
            };
            column.appendChild(card);
        });

        updateStats();
        updateColumnCounts();
        buildToolbar();
        setupDropZones();

        hiddenLanes.forEach(lane => {
            const column = document.querySelector(`.column[data-lane="${lane}"]`);
            if (column) {
                column.style.display = 'none';
            }

            const button = document.querySelector(`.filter-btn[data-lane="${lane}"]`);
            if (button) {
                button.classList.remove('active');
                button.setAttribute('aria-pressed', 'false');
            }
        });

        const initialSlug = window.location.hash.slice(1);
        if (initialSlug) {
            const item = allWorkItems.find(workItem => workItem.slug === initialSlug);
            if (item) {
                showDetail(item, false);
            }
        }
    } catch (err) {
        console.error('Failed to load work items:', err);
    }
}

function filterCards(term) {
    const words = term.toLowerCase().split(' ').filter(word => word.length > 0);

    document.querySelectorAll('.card').forEach(card => {
        if (words.length === 0) {
            card.classList.remove('hidden');
            return;
        }

        const id = card.getAttribute('data-id').toLowerCase();
        const slug = card.getAttribute('data-slug').replace(/-/g, ' ').toLowerCase();
        const content = card.getAttribute('data-content');
        const matchesAll = words.every(word => id.includes(word) || slug.includes(word) || content.includes(word));

        if (matchesAll) {
            card.classList.remove('hidden');
            return;
        }

        card.classList.add('hidden');
    });

    updateStats();
    updateColumnCounts();
}

function filterByTag(tag) {
    if (activeTagFilter === tag) {
        activeTagFilter = null;
    } else {
        activeTagFilter = tag;
    }

    document.querySelectorAll('.card').forEach(card => {
        if (!activeTagFilter) {
            card.classList.remove('hidden');
            return;
        }

        const tags = card.getAttribute('data-tags') || '';
        const cardTags = tags.split(',').map(t => t.trim()).filter(t => t.length > 0);
        if (cardTags.includes(activeTagFilter)) {
            card.classList.remove('hidden');
        } else {
            card.classList.add('hidden');
        }
    });

    document.querySelectorAll('.badge-tag').forEach(badge => {
        if (badge.getAttribute('data-tag') === activeTagFilter) {
            badge.classList.add('active');
        } else {
            badge.classList.remove('active');
        }
    });

    updateStats();
    updateColumnCounts();
}

function updateStats() {
    const visible = document.querySelectorAll('.card:not(.hidden)').length;
    const total = allWorkItems.length;
    document.getElementById('stats').innerText = `Showing ${visible} of ${total} items`;
}

async function showDetail(item, updateHash = true) {
    currentDetailItem = item;
    const pane = document.getElementById('detail-pane');
    const content = document.getElementById('detail-content');
    const editor = document.getElementById('detail-editor');
    const statusEl = document.getElementById('detail-status');
    const moveTarget = document.getElementById('move-target');
    const moveBtn = document.getElementById('move-btn');
    const feedback = document.getElementById('move-feedback');

    statusEl.textContent = item.status;
    moveTarget.value = '';
    moveBtn.disabled = true;
    feedback.textContent = '';

    content.style.display = '';
    editor.style.display = 'none';

    Array.from(moveTarget.options).forEach(option => {
        option.disabled = option.value === item.status;
    });

    moveTarget.onchange = () => {
        moveBtn.disabled = !moveTarget.value || moveTarget.value === item.status;
        feedback.textContent = '';
    };

    try {
        if (updateHash) {
            window.location.hash = item.slug;
        }

        let html = marked.parse(item.content);
        html = html.replace(/\[([a-z0-9-]+)\]/g, (match, slug) => {
            const target = allWorkItems.find(workItem => workItem.slug === slug);
            if (target) {
                return `<a href="#${slug}" style="color: var(--accent); text-decoration: underline; font-weight: bold;">[${slug}]</a>`;
            }

            return match;
        });

        content.innerHTML = html;
        pane.classList.add('open');
        pane.scrollTop = 0;
        await renderMermaidBlocks(content);
    } catch (err) {
        console.error('Markdown parsing or processing failed:', err);
        content.innerText = item.content;
        pane.classList.add('open');
    }
}

function closeDetail(updateHash = true) {
    document.getElementById('detail-pane').classList.remove('open');
    currentDetailItem = null;
    if (updateHash) {
        window.location.hash = '';
    }
}

function startEdit() {
    if (!currentDetailItem) {
        return;
    }

    document.getElementById('detail-content').style.display = 'none';
    const editor = document.getElementById('detail-editor');
    document.getElementById('editor-textarea').value = currentDetailItem.content;
    document.getElementById('save-feedback').textContent = '';
    editor.style.display = '';
    document.getElementById('editor-textarea').focus();
}

function cancelEdit() {
    document.getElementById('detail-editor').style.display = 'none';
    document.getElementById('detail-content').style.display = '';
}

async function saveEdit() {
    if (!currentDetailItem) {
        return;
    }

    const newContent = document.getElementById('editor-textarea').value;
    const feedback = document.getElementById('save-feedback');
    const saveBtn = document.getElementById('save-btn');
    saveBtn.disabled = true;
    feedback.className = '';
    feedback.textContent = 'Saving\u2026';

    const itemUrl = currentBoardKey
        ? `/api/items/${encodeURIComponent(currentDetailItem.id)}?board=${encodeURIComponent(currentBoardKey)}`
        : `/api/items/${encodeURIComponent(currentDetailItem.id)}`;

    try {
        const response = await fetch(itemUrl, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ content: newContent }),
        });
        const data = await response.json();
        if (!response.ok) {
            feedback.className = 'move-err';
            feedback.textContent = data.error || 'Save failed.';
            saveBtn.disabled = false;
            return;
        }

        currentDetailItem = { ...currentDetailItem, content: newContent };
        cancelEdit();
        feedback.className = 'move-msg';
        feedback.textContent = data.message;
        await refreshDetailContent(newContent);
    } catch (err) {
        feedback.className = 'move-err';
        feedback.textContent = 'Network error.';
        saveBtn.disabled = false;
    } finally {
        saveBtn.disabled = false;
    }
}

async function refreshDetailContent(rawMarkdown) {
    const content = document.getElementById('detail-content');
    let html = marked.parse(rawMarkdown);
    html = html.replace(/\[([a-z0-9-]+)\]/g, (match, slug) => {
        const target = allWorkItems.find(workItem => workItem.slug === slug);
        if (target) {
            return `<a href="#${slug}" style="color: var(--accent); text-decoration: underline; font-weight: bold;">[${slug}]</a>`;
        }

        return match;
    });
    content.innerHTML = html;
    await renderMermaidBlocks(content);
}

async function moveItem() {
    if (!currentDetailItem) {
        return;
    }

    const target = document.getElementById('move-target').value;
    if (!target) {
        return;
    }

    const button = document.getElementById('move-btn');
    const feedback = document.getElementById('move-feedback');
    button.disabled = true;
    feedback.className = '';
    feedback.textContent = 'Moving\u2026';

    const identifier = currentDetailItem.id || currentDetailItem.slug;
    try {
        const moveUrl = currentBoardKey ? `/api/move?board=${encodeURIComponent(currentBoardKey)}` : '/api/move';
        const response = await fetch(moveUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ identifier, target })
        });
        const data = await response.json();
        if (!response.ok) {
            feedback.className = 'move-err';
            feedback.textContent = data.error || 'Move failed';
            button.disabled = false;
            return;
        }

        feedback.className = 'move-msg';
        feedback.textContent = data.message;
        closeDetail();
        await loadItems();
    } catch (err) {
        feedback.className = 'move-err';
        feedback.textContent = 'Network error';
        button.disabled = false;
    }
}

let draggedId = null;
let draggedStatus = null;
let wasDragging = false;

function onCardDragStart(event) {
    draggedId = event.currentTarget.getAttribute('data-id');
    draggedStatus = event.currentTarget.getAttribute('data-status');
    wasDragging = true;
    event.currentTarget.classList.add('dragging');
    event.dataTransfer.effectAllowed = 'move';
}

function onCardDragEnd(event) {
    event.currentTarget.classList.remove('dragging');
    document.querySelectorAll('.cards.drag-over').forEach(element => element.classList.remove('drag-over'));
}

function setupDropZones() {
    document.querySelectorAll('.cards').forEach(zone => {
        zone.addEventListener('dragover', event => {
            event.preventDefault();
            event.dataTransfer.dropEffect = 'move';
            zone.classList.add('drag-over');
        });
        zone.addEventListener('dragleave', event => {
            if (!zone.contains(event.relatedTarget)) {
                zone.classList.remove('drag-over');
            }
        });
        zone.addEventListener('drop', async event => {
            event.preventDefault();
            zone.classList.remove('drag-over');
            const targetLane = zone.closest('.column').getAttribute('data-lane');
            if (!draggedId || targetLane === draggedStatus) {
                return;
            }

            await moveItemById(draggedId, targetLane);
            draggedId = null;
            draggedStatus = null;
        });
    });
}

async function moveItemById(identifier, target) {
    try {
        const moveUrl = currentBoardKey ? `/api/move?board=${encodeURIComponent(currentBoardKey)}` : '/api/move';
        const response = await fetch(moveUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ identifier, target })
        });
        if (response.ok) {
            await loadItems();
            return;
        }

        const data = await response.json();
        console.error('Move failed:', data.error);
    } catch (err) {
        console.error('Network error during drag-move:', err);
    }
}

initBoards().then(() => initLanes()).then(() => {
    buildBoard();
    loadItems();
});
