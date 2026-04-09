let allWorkItems = [];
let currentDetailItem = null;
const lanes = ['Todo', 'In Progress', 'Testing', 'Done', 'Ideas', 'Rejected'];
let hiddenLanes = new Set();

document.addEventListener('keydown', e => {
    if (e.key === 'Escape') closeDetail();
});

window.addEventListener('hashchange', () => {
    const slug = window.location.hash.slice(1);
    if (slug) {
        const item = allWorkItems.find(i => i.slug === slug);
        if (item) showDetail(item, false);
    } else {
        closeDetail(false);
    }
});

function buildToolbar() {
    const tb = document.getElementById('toolbar');
    tb.innerHTML = '';
    lanes.forEach(lane => {
        const btn = document.createElement('button');
        btn.className = 'filter-btn active';
        btn.setAttribute('data-lane', lane);
        const count = allWorkItems.filter(i => i.status === lane).length;
        btn.innerHTML = `${lane}<span class="count">(${count})</span>`;
        btn.onclick = () => toggleLane(lane, btn);
        tb.appendChild(btn);
    });
    const sep = document.createElement('div');
    sep.className = 'toolbar-separator';
    tb.appendChild(sep);
    const refresh = document.createElement('button');
    refresh.className = 'refresh-btn';
    refresh.textContent = '\u21bb Refresh';
    refresh.onclick = () => loadItems();
    tb.appendChild(refresh);
}

function toggleLane(lane, btn) {
    const col = document.querySelector(`.column[data-lane="${lane}"]`);
    if (hiddenLanes.has(lane)) {
        hiddenLanes.delete(lane);
        btn.classList.add('active');
        col.style.display = '';
    } else {
        hiddenLanes.add(lane);
        btn.classList.remove('active');
        col.style.display = 'none';
    }
}

function updateColumnCounts() {
    document.querySelectorAll('.column').forEach(col => {
        const count = col.querySelectorAll('.card:not(.hidden)').length;
        const span = col.querySelector('.col-count');
        if (span) span.textContent = `(${count})`;
    });
}

async function loadItems() {
    try {
        const res = await fetch('/api/items');
        allWorkItems = await res.json();

        document.querySelectorAll('.cards').forEach(c => c.innerHTML = '');

        allWorkItems.forEach(item => {
            const colId = `col-${item.status.replace(' ', '-')}`;
            const col = document.getElementById(colId);
            if (!col) return;

            const card = document.createElement('div');
            card.className = `card ${item.status.replace(' ', '-')}`;
            card.setAttribute('data-id', item.id);
            card.setAttribute('data-slug', item.slug);
            card.setAttribute('data-content', item.content.toLowerCase());
            card.setAttribute('data-status', item.status);
            card.innerHTML = `<span class="card-id">${item.id}</span>${item.slug}`;
            card.draggable = true;
            card.addEventListener('dragstart', onCardDragStart);
            card.addEventListener('dragend', onCardDragEnd);
            card.onclick = (e) => {
                if (wasDragging) { wasDragging = false; return; }
                e.preventDefault();
                showDetail(item);
            };
            col.appendChild(card);
        });

        updateStats();
        updateColumnCounts();
        buildToolbar();
        setupDropZones();

        // Re-hide lanes that were toggled off before refresh
        hiddenLanes.forEach(lane => {
            const col = document.querySelector(`.column[data-lane="${lane}"]`);
            if (col) col.style.display = 'none';
            const btn = document.querySelector(`.filter-btn[data-lane="${lane}"]`);
            if (btn) btn.classList.remove('active');
        });

        const initialSlug = window.location.hash.slice(1);
        if (initialSlug) {
            const item = allWorkItems.find(i => i.slug === initialSlug);
            if (item) showDetail(item, false);
        }
    } catch (err) {
        console.error('Failed to load work items:', err);
    }
}

function filterCards(term) {
    term = term.toLowerCase();
    const words = term.split(' ').filter(w => w.length > 0);

    document.querySelectorAll('.card').forEach(card => {
        if (words.length === 0) {
            card.classList.remove('hidden');
            return;
        }

        const id = card.getAttribute('data-id').toLowerCase();
        const slug = card.getAttribute('data-slug').replace(/-/g, ' ').toLowerCase();
        const content = card.getAttribute('data-content');

        const matchesAll = words.every(word =>
            id.includes(word) || slug.includes(word) || content.includes(word)
        );

        if (matchesAll) {
            card.classList.remove('hidden');
        } else {
            card.classList.add('hidden');
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

function showDetail(item, updateHash = true) {
    currentDetailItem = item;
    const pane = document.getElementById('detail-pane');
    const content = document.getElementById('detail-content');
    const statusEl = document.getElementById('detail-status');
    const moveTarget = document.getElementById('move-target');
    const moveBtn = document.getElementById('move-btn');
    const feedback = document.getElementById('move-feedback');

    statusEl.textContent = item.status;
    moveTarget.value = '';
    moveBtn.disabled = true;
    feedback.textContent = '';

    Array.from(moveTarget.options).forEach(opt => {
        opt.disabled = opt.value === item.status;
    });

    moveTarget.onchange = () => {
        moveBtn.disabled = !moveTarget.value || moveTarget.value === item.status;
        feedback.textContent = '';
    };

    try {
        if (updateHash) window.location.hash = item.slug;

        let html = marked.parse(item.content);
        html = html.replace(/\[([a-z0-9-]+)\]/g, (match, slug) => {
            const target = allWorkItems.find(i => i.slug === slug);
            if (target) {
                return `<a href="#${slug}" style="color: var(--accent); text-decoration: underline; font-weight: bold;">[${slug}]</a>`;
            }
            return match;
        });

        content.innerHTML = html;
        pane.classList.add('open');
        pane.scrollTop = 0;
    } catch (err) {
        console.error('Markdown parsing or processing failed:', err);
        content.innerText = item.content;
        pane.classList.add('open');
    }
}

function closeDetail(updateHash = true) {
    document.getElementById('detail-pane').classList.remove('open');
    currentDetailItem = null;
    if (updateHash) window.location.hash = '';
}

async function moveItem() {
    if (!currentDetailItem) return;
    const target = document.getElementById('move-target').value;
    if (!target) return;

    const btn = document.getElementById('move-btn');
    const feedback = document.getElementById('move-feedback');
    btn.disabled = true;
    feedback.className = '';
    feedback.textContent = 'Moving\u2026';

    const identifier = currentDetailItem.id || currentDetailItem.slug;
    try {
        const res = await fetch('/api/move', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ identifier, target })
        });
        const data = await res.json();
        if (!res.ok) {
            feedback.className = 'move-err';
            feedback.textContent = data.error || 'Move failed';
            btn.disabled = false;
            return;
        }
        feedback.className = 'move-msg';
        feedback.textContent = data.message;
        closeDetail();
        await loadItems();
    } catch (err) {
        feedback.className = 'move-err';
        feedback.textContent = 'Network error';
        btn.disabled = false;
    }
}

// --- Drag and drop ---

let draggedId = null;
let draggedStatus = null;
let wasDragging = false;

function onCardDragStart(e) {
    draggedId = e.currentTarget.getAttribute('data-id');
    draggedStatus = e.currentTarget.getAttribute('data-status');
    wasDragging = true;
    e.currentTarget.classList.add('dragging');
    e.dataTransfer.effectAllowed = 'move';
}

function onCardDragEnd(e) {
    e.currentTarget.classList.remove('dragging');
    document.querySelectorAll('.cards.drag-over').forEach(el => el.classList.remove('drag-over'));
}

function setupDropZones() {
    document.querySelectorAll('.cards').forEach(zone => {
        zone.addEventListener('dragover', e => {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';
            zone.classList.add('drag-over');
        });
        zone.addEventListener('dragleave', e => {
            if (!zone.contains(e.relatedTarget)) {
                zone.classList.remove('drag-over');
            }
        });
        zone.addEventListener('drop', async e => {
            e.preventDefault();
            zone.classList.remove('drag-over');
            const targetLane = zone.closest('.column').getAttribute('data-lane');
            if (!draggedId || targetLane === draggedStatus) return;
            await moveItemById(draggedId, targetLane);
            draggedId = null;
            draggedStatus = null;
        });
    });
}

async function moveItemById(identifier, target) {
    try {
        const res = await fetch('/api/move', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ identifier, target })
        });
        if (res.ok) {
            await loadItems();
        } else {
            const data = await res.json();
            console.error('Move failed:', data.error);
        }
    } catch (err) {
        console.error('Network error during drag-move:', err);
    }
}

loadItems();
