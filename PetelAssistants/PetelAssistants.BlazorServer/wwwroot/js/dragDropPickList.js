window.dragDropPickList = {
    _handlers: {},

    init: function (config) {
        if (!config || !config.modalId || !config.dotNetRef) return;

        this.dispose(config.modalId);

        const modal = document.getElementById(config.modalId);
        if (!modal) return;

        const availableZone = document.getElementById(config.availableZoneId);
        const selectedZone = document.getElementById(config.selectedZoneId);
        if (!availableZone || !selectedZone) return;

        const handler = {
            modal,
            availableZone,
            selectedZone,
            dotNetRef: config.dotNetRef,
            dragItemId: null,
            dragSourcePane: null
        };

        const onDragStart = (e) => {
            const item = e.target.closest('.action-item[data-item-id]');
            if (!item) return;

            handler.dragItemId = parseInt(item.getAttribute('data-item-id'), 10);
            handler.dragSourcePane = item.closest('[data-pane]')?.getAttribute('data-pane') || 'available';
            item.classList.add('dragging');
            e.dataTransfer.effectAllowed = 'move';
            e.dataTransfer.setData('text/plain', String(handler.dragItemId));
        };

        const onDragEnd = (e) => {
            const item = e.target.closest('.action-item');
            if (item) item.classList.remove('dragging');
            availableZone.classList.remove('drop-zone-active');
            selectedZone.classList.remove('drop-zone-active');
            handler.dragItemId = null;
            handler.dragSourcePane = null;
        };

        const onDragOver = (e) => {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';
        };

        const onDragEnter = (e, zone) => {
            e.preventDefault();
            zone.classList.add('drop-zone-active');
        };

        const onDragLeave = (e, zone) => {
            if (!zone.contains(e.relatedTarget)) {
                zone.classList.remove('drop-zone-active');
            }
        };

        const onDrop = async (e, targetPane) => {
            e.preventDefault();
            availableZone.classList.remove('drop-zone-active');
            selectedZone.classList.remove('drop-zone-active');

            const itemId = handler.dragItemId ?? parseInt(e.dataTransfer.getData('text/plain'), 10);
            const sourcePane = handler.dragSourcePane;

            if (!itemId || !sourcePane || sourcePane === targetPane) return;

            try {
                await handler.dotNetRef.invokeMethodAsync('OnPickListDrop', itemId, targetPane);
            } catch (err) {
                console.error('dragDropPickList drop error:', err);
            }
        };

        const makeItemsDraggable = () => {
            modal.querySelectorAll('.action-item[data-item-id]').forEach(item => {
                item.setAttribute('draggable', 'true');
            });
        };

        handler.onDragStart = onDragStart;
        handler.onDragEnd = onDragEnd;
        handler.makeItemsDraggable = makeItemsDraggable;

        modal.addEventListener('dragstart', onDragStart);
        modal.addEventListener('dragend', onDragEnd);
        availableZone.addEventListener('dragover', onDragOver);
        selectedZone.addEventListener('dragover', onDragOver);
        availableZone.addEventListener('dragenter', (e) => onDragEnter(e, availableZone));
        selectedZone.addEventListener('dragenter', (e) => onDragEnter(e, selectedZone));
        availableZone.addEventListener('dragleave', (e) => onDragLeave(e, availableZone));
        selectedZone.addEventListener('dragleave', (e) => onDragLeave(e, selectedZone));
        availableZone.addEventListener('drop', (e) => onDrop(e, 'available'));
        selectedZone.addEventListener('drop', (e) => onDrop(e, 'selected'));

        makeItemsDraggable();
        this._handlers[config.modalId] = handler;
    },

    refresh: function (modalId) {
        const handler = this._handlers[modalId];
        if (handler && handler.makeItemsDraggable) {
            handler.makeItemsDraggable();
        }
    },

    dispose: function (modalId) {
        const handler = this._handlers[modalId];
        if (!handler) return;

        handler.modal.removeEventListener('dragstart', handler.onDragStart);
        handler.modal.removeEventListener('dragend', handler.onDragEnd);
        delete this._handlers[modalId];
    }
};
