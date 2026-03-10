/* In the name of God, the Merciful, the Compassionate */

// Query plan viewer helpers for Blazor JS interop
window.queryPlanInterop = {

    // ── public ─────────────────────────────────────────────────────────────
    showPlan: function (containerId, xml) {
        var container = document.getElementById(containerId);
        if (!container) return;
        container.innerHTML = '';
        if (window.QP && xml) {
            try {
                QP.showPlan(container, xml, { jsTooltips: true });
                // Enhancement passes — all read from container['xml'] set by QP.showPlan
                this._injectMetadataBar(container);
                this._injectWarnings(container);
                this._injectMissingIndexes(container);
                this._injectCostHeaders(container);
                this._highlightExpensiveNodes(container);
            } catch (e) {
                container.innerHTML = '<p style="color:#f44336;padding:16px;">Failed to render query plan: ' + e.message + '</p>';
            }
        }
    },

    clearPlan: function (containerId) {
        var container = document.getElementById(containerId);
        if (container) container.innerHTML = '';
    },

    // ── private helpers ────────────────────────────────────────────────────

    // Returns the parsed XML stored by QP.showPlan, with namespace helper.
    _xml: function (container) { return container['xml'] || null; },
    _ns:  'http://schemas.microsoft.com/sqlserver/2004/07/showplan',

    _getElements: function (parsedXml, tag) {
        var els = parsedXml.getElementsByTagNameNS(this._ns, tag);
        if (!els || els.length === 0) els = parsedXml.getElementsByTagName(tag);
        return els || [];
    },

    // Creates a styled info/warning/error banner div.
    _banner: function (text, type, mono) {
        var colors = {
            info:    { fg: '#9cdcfe', bg: 'rgba(46,174,241,0.10)', border: '#2eaef1' },
            warning: { fg: '#ffc107', bg: 'rgba(255,193,7,0.10)',  border: '#ffc107' },
            error:   { fg: '#f44336', bg: 'rgba(244,67,54,0.10)',  border: '#f44336' },
            success: { fg: '#4caf50', bg: 'rgba(76,175,80,0.10)',  border: '#4caf50' }
        };
        var c = colors[type] || colors.info;
        var el = document.createElement('div');
        el.style.cssText = [
            'font-family: ' + (mono ? 'Consolas, monospace' : 'inherit'),
            'font-size: 12px',
            'color: ' + c.fg,
            'background: ' + c.bg,
            'border-left: 3px solid ' + c.border,
            'padding: 5px 10px',
            'margin-bottom: 4px',
            'border-radius: 2px',
            'user-select: none',
            'white-space: pre-wrap',
            'word-break: break-all'
        ].join(';');
        el.textContent = text;
        return el;
    },

    // ── 1. Compile metadata bar ────────────────────────────────────────────
    // Shows: CompileTime, CompileMemory, DOP, MemoryGrant from <QueryPlan> and <MemoryGrantInfo>
    _injectMetadataBar: function (container) {
        var parsedXml = this._xml(container);
        if (!parsedXml) return;

        var qps = this._getElements(parsedXml, 'QueryPlan');
        if (!qps || qps.length === 0) return;
        var qp = qps[0];

        var parts = [];
        var ct = qp.getAttribute('CompileTime');
        var cm = qp.getAttribute('CompileMemory');
        if (ct) parts.push('Compile time: ' + ct + ' ms');
        if (cm) parts.push('Compile memory: ' + parseInt(cm).toLocaleString() + ' KB');

        // Available DOP
        var hw = this._getElements(parsedXml, 'OptimizerHardwareDependentProperties');
        if (hw && hw.length > 0) {
            var dop = hw[0].getAttribute('EstimatedAvailableDegreeOfParallelism');
            if (dop) parts.push('Available DOP: ' + dop);
        }

        // Memory grant
        var mg = this._getElements(parsedXml, 'MemoryGrantInfo');
        if (mg && mg.length > 0) {
            var granted = mg[0].getAttribute('GrantedMemory');
            var maxUsed = mg[0].getAttribute('MaxUsedMemory');
            if (granted && parseInt(granted) > 0)
                parts.push('Memory grant: ' + parseInt(granted).toLocaleString() + ' KB' +
                    (maxUsed ? ' (max used: ' + parseInt(maxUsed).toLocaleString() + ' KB)' : ''));
        }

        // Parallelism check — any RelOp with Parallel="true"
        var relOps = this._getElements(parsedXml, 'RelOp');
        var isParallel = false;
        for (var i = 0; i < relOps.length; i++) {
            if (relOps[i].getAttribute('Parallel') === '1' ||
                relOps[i].getAttribute('Parallel') === 'true') {
                isParallel = true;
                break;
            }
        }
        if (isParallel) parts.push('Parallel plan');

        if (parts.length === 0) return;

        var bar = document.createElement('div');
        bar.style.cssText = [
            'font-family: Consolas, monospace',
            'font-size: 11px',
            'color: #888',
            'background: rgba(0,0,0,0.2)',
            'padding: 3px 10px',
            'margin-bottom: 6px',
            'border-radius: 2px',
            'user-select: none'
        ].join(';');
        bar.textContent = parts.join('  ·  ');
        container.insertBefore(bar, container.firstElementChild);
    },

    // ── 2. Warnings ────────────────────────────────────────────────────────
    // Surfaces <Warnings> attributes: NoJoinPredicate, SpillToTempDb, ColumnsWithNoStatistics, etc.
    _injectWarnings: function (container) {
        var parsedXml = this._xml(container);
        if (!parsedXml) return;

        var warnings = this._getElements(parsedXml, 'Warnings');
        var messages = [];

        for (var i = 0; i < warnings.length; i++) {
            var w = warnings[i];

            if (w.getAttribute('NoJoinPredicate') === '1' ||
                w.getAttribute('NoJoinPredicate') === 'true')
                messages.push({ text: '⚠ No join predicate — possible accidental cross join or missing WHERE clause', type: 'error' });

            // SpillToTempDb child elements
            var spills = w.getElementsByTagName('SpillToTempDb');
            if (!spills || spills.length === 0)
                spills = w.getElementsByTagNameNS(this._ns, 'SpillToTempDb');
            if (spills && spills.length > 0)
                messages.push({ text: '⚠ Operator spilled to TempDB — insufficient memory grant; consider updating statistics or increasing grant', type: 'error' });

            // ColumnsWithNoStatistics
            var noStats = w.getElementsByTagName('ColumnsWithNoStatistics');
            if (!noStats || noStats.length === 0)
                noStats = w.getElementsByTagNameNS(this._ns, 'ColumnsWithNoStatistics');
            if (noStats && noStats.length > 0) {
                var cols = [];
                var colRefs = noStats[0].getElementsByTagName('ColumnReference');
                for (var j = 0; j < colRefs.length; j++) {
                    var col = (colRefs[j].getAttribute('Table') || '') + '.' + colRefs[j].getAttribute('Column');
                    cols.push(col);
                }
                messages.push({ text: '⚠ Columns with no statistics: ' + cols.join(', '), type: 'warning' });
            }

            // SortSpillDetails, HashSpillDetails
            var sortSpill = w.getElementsByTagName('SortSpillDetails');
            if (sortSpill && sortSpill.length > 0)
                messages.push({ text: '⚠ Sort operation spilled to TempDB', type: 'error' });
        }

        // Insert before the first element child (after metadata bar if present)
        var insertBefore = container.firstElementChild;
        for (var k = messages.length - 1; k >= 0; k--) {
            var el = this._banner(messages[k].text, messages[k].type, false);
            container.insertBefore(el, insertBefore);
            insertBefore = el;
        }
    },

    // ── 3. Missing index recommendations ──────────────────────────────────
    // Parses <MissingIndexGroup> and generates CREATE INDEX T-SQL suggestions.
    _injectMissingIndexes: function (container) {
        var parsedXml = this._xml(container);
        if (!parsedXml) return;

        var groups = this._getElements(parsedXml, 'MissingIndexGroup');
        if (!groups || groups.length === 0) return;

        for (var i = 0; i < groups.length; i++) {
            var group = groups[i];
            var impact = parseFloat(group.getAttribute('Impact') || '0').toFixed(1);

            var idxEls = group.getElementsByTagName('MissingIndex');
            if (!idxEls || idxEls.length === 0)
                idxEls = group.getElementsByTagNameNS(this._ns, 'MissingIndex');
            if (!idxEls || idxEls.length === 0) continue;

            var idx = idxEls[0];
            var db     = (idx.getAttribute('Database') || '').replace(/[\[\]]/g, '');
            var schema = (idx.getAttribute('Schema')   || '').replace(/[\[\]]/g, '');
            var table  = (idx.getAttribute('Table')    || '').replace(/[\[\]]/g, '');

            var eqCols = [], neqCols = [], incCols = [];
            var colGroups = idx.getElementsByTagName('ColumnGroup');
            if (!colGroups || colGroups.length === 0)
                colGroups = idx.getElementsByTagNameNS(this._ns, 'ColumnGroup');

            for (var g = 0; g < colGroups.length; g++) {
                var usage = colGroups[g].getAttribute('Usage');
                var colEls = colGroups[g].getElementsByTagName('Column');
                var names = [];
                for (var c = 0; c < colEls.length; c++)
                    names.push('[' + colEls[c].getAttribute('Name') + ']');

                if (usage === 'EQUALITY')   eqCols  = names;
                if (usage === 'INEQUALITY') neqCols = names;
                if (usage === 'INCLUDE')    incCols = names;
            }

            var keyCols = eqCols.concat(neqCols).join(', ');
            var sql = 'CREATE INDEX [IX_' + table + '_suggested]\n' +
                      'ON [' + db + '].[' + schema + '].[' + table + '] (' + keyCols + ')' +
                      (incCols.length > 0 ? '\nINCLUDE (' + incCols.join(', ') + ')' : '') + ';';

            var wrapper = document.createElement('div');
            wrapper.style.cssText = [
                'background: rgba(255,193,7,0.08)',
                'border-left: 3px solid #ffc107',
                'border-radius: 2px',
                'padding: 6px 10px',
                'margin-bottom: 4px'
            ].join(';');

            var header = document.createElement('div');
            header.style.cssText = 'font-size:12px;color:#ffc107;margin-bottom:4px;user-select:none;';
            header.textContent = '💡 Missing index — estimated impact: ' + impact + '%';

            var code = document.createElement('pre');
            code.style.cssText = [
                'font-family: Consolas, monospace',
                'font-size: 11px',
                'color: #ce9178',
                'margin: 0',
                'white-space: pre-wrap',
                'word-break: break-all',
                'user-select: text'
            ].join(';');
            code.textContent = sql;

            wrapper.appendChild(header);
            wrapper.appendChild(code);
            container.insertBefore(wrapper, container.firstElementChild);
        }
    },

    // ── 4. Query cost headers (relative to batch %) ────────────────────────
    _injectCostHeaders: function (container) {
        var parsedXml = this._xml(container);
        if (!parsedXml) return;

        var stmts = this._getElements(parsedXml, 'StmtSimple');
        if (!stmts || stmts.length === 0) return;

        var costs = [], totalCost = 0;
        for (var i = 0; i < stmts.length; i++) {
            var c = parseFloat(stmts[i].getAttribute('StatementSubTreeCost') || '0');
            costs.push(c);
            totalCost += c;
        }

        var rendered = container.querySelectorAll('[data-statement-id]');
        var seen = new Set();

        // QP.showPlan appends a single root element (.qp-root); banners must be inserted
        // relative to statement blocks within that root, not the container itself.
        // Use querySelector to find the actual .qp-root element, not container.firstChild
        // which might be a text node (whitespace)
        var qpRoot = container.querySelector('.qp-root') || container.firstElementChild || container;

        rendered.forEach(function (el) {
            var stmtId = el.getAttribute('data-statement-id');
            if (seen.has(stmtId)) return;
            seen.add(stmtId);

            var idx = parseInt(stmtId, 10) - 1;
            var cost = (idx >= 0 && idx < costs.length) ? costs[idx] : 0;
            var pct  = totalCost > 0 ? Math.round(cost / totalCost * 100) : 100;

            // Walk up to find the direct child of qpRoot that contains this element
            var block = el;
            while (block.parentElement && block.parentElement !== qpRoot && block.parentElement !== container)
                block = block.parentElement;

            var banner = document.createElement('div');
            banner.className = 'qp-cost-banner';
            banner.style.cssText = [
                'font-family: Consolas, monospace',
                'font-size: 12px',
                'color: #9cdcfe',
                'background: rgba(0,0,0,0.3)',
                'border-left: 3px solid #2eaef1',
                'padding: 4px 10px',
                'margin-bottom: 4px',
                'border-radius: 2px',
                'user-select: none'
            ].join(';');
            banner.textContent = 'Query cost (relative to the batch): ' + pct + '%';
            block.parentElement.insertBefore(banner, block);
        });
    },

    // ── 5. Expensive node highlighting ────────────────────────────────────
    // Applies qp-hot-red (>50%) or qp-hot-orange (25–50%) CSS classes to
    // .qp-node elements whose own cost (EstimateCPU + EstimateIO) exceeds
    // those thresholds relative to the total query cost.
    // Matches rendered nodes to XML via data-node-id ↔ RelOp[NodeId].
    _highlightExpensiveNodes: function (container) {
        var parsedXml = this._xml(container);
        if (!parsedXml) return;

        // Total query cost from statement(s)
        var stmts = this._getElements(parsedXml, 'StmtSimple');
        var totalCost = 0;
        for (var s = 0; s < stmts.length; s++)
            totalCost += parseFloat(stmts[s].getAttribute('StatementSubTreeCost') || '0');
        if (totalCost === 0) return;

        // Build NodeId → own cost (EstimateCPU + EstimateIO)
        var relOps  = this._getElements(parsedXml, 'RelOp');
        var costMap = {};
        for (var i = 0; i < relOps.length; i++) {
            var nodeId = relOps[i].getAttribute('NodeId');
            if (nodeId === null) continue;
            var cpu = parseFloat(relOps[i].getAttribute('EstimateCPU') || '0');
            var io  = parseFloat(relOps[i].getAttribute('EstimateIO')  || '0');
            costMap[nodeId] = cpu + io;
        }

        var nodes = container.querySelectorAll('.qp-node[data-node-id]');
        nodes.forEach(function (el) {
            var cost = costMap[el.getAttribute('data-node-id')] || 0;
            var pct  = cost / totalCost * 100;

            var cls = pct >= 50 ? 'qp-hot-red' : pct >= 25 ? 'qp-hot-orange' : null;
            if (!cls) return;

            el.classList.add(cls);

            var badge = el.querySelector('.qp-heat-badge');
            if (!badge) {
                badge = document.createElement('span');
                badge.className = 'qp-heat-badge';
                el.appendChild(badge);
            }
            badge.textContent = Math.round(pct) + '%';
        });
    }
};
