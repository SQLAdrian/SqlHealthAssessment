/* In the name of God, the Merciful, the Compassionate */

/**
 * Environment View — Interactive visualization of SQL Server connection topology.
 * Renders server nodes with connected host nodes, SVG edge lines, zoom/pan viewport,
 * cross-server links, and a click-to-inspect detail pane.
 */
window.environmentView = (function () {
    'use strict';

    // ── Layout constants ─────────────────────────────────────────────────────
    const SERVER_W   = 220;
    const SERVER_H   = 160;
    const HOST_W     = 180;
    const HOST_H     = 56;
    const H_GAP      = 40;      // horizontal gap between host columns
    const V_GAP      = 24;      // vertical gap between host rows
    const SERVER_GAP = 120;     // gap between server clusters (multi-server)
    const TOP_PAD    = 40;      // top padding above servers
    const HOST_TOP   = 240;     // top of host row (below server node)
    const HOSTS_PER_ROW = 6;    // max hosts per row before wrapping
    const PAD        = 30;      // canvas padding

    // ── Helpers ──────────────────────────────────────────────────────────────
    function esc(s) {
        var d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    // ── Detail pane ──────────────────────────────────────────────────────────
    function createDetailPane(container) {
        var pane = document.createElement('div');
        pane.className = 'env-detail-pane';
        pane.style.display = 'none';
        container.appendChild(pane);
        return pane;
    }

    function showHostDetail(pane, host, serverName) {
        var html = '<div class="env-detail-header">' +
            '<i class="fa-solid fa-desktop"></i> ' + esc(host.hostname) +
            '<button class="env-detail-close" onclick="this.parentElement.parentElement.style.display=\'none\'">✕</button>' +
            '</div>';

        html += '<div class="env-detail-badges">' +
            '<span class="env-badge"><i class="fa-solid fa-plug"></i> ' + host.connectionCount + ' conn</span>' +
            '<span class="env-badge"><i class="fa-solid fa-cube"></i> ' + host.uniqueApps + ' apps</span>' +
            '<span class="env-badge"><i class="fa-solid fa-user"></i> ' + host.uniqueUsers + ' users</span>' +
            '<span class="env-badge"><i class="fa-solid fa-database"></i> ' + host.uniqueDbs + ' DBs</span>' +
            '</div>';

        html += '<div class="env-detail-server">Connected to: <strong>' + esc(serverName) + '</strong></div>';

        html += '<table class="env-detail-table"><thead><tr>' +
            '<th>Application</th><th>User</th><th>Database</th><th>IP</th><th>Driver</th><th>Protocol</th><th>Auth</th><th>Encrypt</th>' +
            '</tr></thead><tbody>';

        (host.connections || []).forEach(function (c) {
            html += '<tr>' +
                '<td>' + esc(c.app) + '</td>' +
                '<td>' + esc(c.user) + '</td>' +
                '<td>' + esc(c.database) + '</td>' +
                '<td>' + esc(c.ip) + '</td>' +
                '<td>' + esc(c.driverVersion) + '</td>' +
                '<td>' + esc(c.protocolVersion) + '</td>' +
                '<td>' + esc(c.authScheme) + '</td>' +
                '<td>' + esc(c.encryptOption) + '</td>' +
                '</tr>';
        });

        html += '</tbody></table>';
        pane.innerHTML = html;
        pane.style.display = 'block';
    }

    function showServerDetail(pane, server) {
        var html = '<div class="env-detail-header">' +
            '<i class="fa-solid fa-server"></i> ' + esc(server.name) +
            '<button class="env-detail-close" onclick="this.parentElement.parentElement.style.display=\'none\'">✕</button>' +
            '</div>';

        if (server.error) {
            html += '<div style="color:#f87171;padding:8px;">Error: ' + esc(server.error) + '</div>';
        } else {
            // Server info section - extensible for future CPU/memory/edition data
            if (server.serverInfo) {
                html += '<div class="env-server-info-grid">';
                var info = server.serverInfo;
                if (info.edition)      html += '<div class="env-info-item"><span class="env-info-label">Edition</span><span class="env-info-value">' + esc(info.edition) + '</span></div>';
                if (info.version)      html += '<div class="env-info-item"><span class="env-info-label">Version</span><span class="env-info-value">' + esc(info.version) + '</span></div>';
                if (info.cpuCount)     html += '<div class="env-info-item"><span class="env-info-label">CPUs</span><span class="env-info-value">' + info.cpuCount + '</span></div>';
                if (info.memoryMB)     html += '<div class="env-info-item"><span class="env-info-label">Memory</span><span class="env-info-value">' + info.memoryMB + ' MB</span></div>';
                html += '</div>';
            }

            html += '<div class="env-detail-badges">' +
                '<span class="env-badge env-badge-green"><i class="fa-solid fa-desktop"></i> ' + server.counts.hosts + ' Hosts</span>' +
                '<span class="env-badge env-badge-green"><i class="fa-solid fa-cube"></i> ' + server.counts.apps + ' Apps</span>' +
                '<span class="env-badge env-badge-green"><i class="fa-solid fa-user"></i> ' + server.counts.users + ' Users</span>' +
                '<span class="env-badge env-badge-green"><i class="fa-solid fa-database"></i> ' + server.counts.dbs + ' DBs</span>' +
                '<span class="env-badge"><i class="fa-solid fa-network-wired"></i> ' + server.counts.ips + ' IPs</span>' +
                '<span class="env-badge"><i class="fa-solid fa-shield-halved"></i> ' + server.counts.auth + ' Auth</span>' +
                '<span class="env-badge"><i class="fa-solid fa-lock"></i> ' + server.counts.encr + ' Encr</span>' +
                '</div>';

            // Host summary list
            html += '<div class="env-detail-hosts-list"><strong>Connected Hosts:</strong><ul>';
            (server.hosts || []).forEach(function (h) {
                html += '<li><strong>' + esc(h.hostname) + '</strong> — ' +
                    h.connectionCount + ' conn, ' + h.uniqueApps + ' apps, ' + h.uniqueUsers + ' users</li>';
            });
            html += '</ul></div>';
        }

        pane.innerHTML = html;
        pane.style.display = 'block';
    }

    // ── Main render ──────────────────────────────────────────────────────────
    function render(containerId, jsonData) {
        var container = document.getElementById(containerId);
        if (!container) return;
        container.innerHTML = '';

        var data;
        try {
            data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
        } catch (e) {
            container.innerHTML = '<p style="color:#f44336;padding:16px;">Failed to parse environment data: ' + e.message + '</p>';
            return;
        }

        var servers = data.servers || [];
        if (servers.length === 0) {
            container.innerHTML = '<p style="color:var(--text-secondary);padding:16px;">No server data to display.</p>';
            return;
        }

        // ── Compute layout positions ─────────────────────────────────────────
        var serverPositions = [];   // {x, y, server, hostPositions: [{x, y, host}]}
        var currentX = PAD;

        servers.forEach(function (server) {
            var hosts = server.hosts || [];
            var hostCount = hosts.length;
            var cols = Math.min(hostCount, HOSTS_PER_ROW);
            var rows = Math.ceil(hostCount / HOSTS_PER_ROW) || 1;

            var clusterW = Math.max(SERVER_W, cols * (HOST_W + H_GAP) - H_GAP);
            var serverX = currentX + (clusterW - SERVER_W) / 2;
            var serverY = TOP_PAD;

            var sp = { x: serverX, y: serverY, server: server, hostPositions: [], clusterW: clusterW, clusterX: currentX };

            // Position hosts below server
            var hostStartX = currentX;
            hosts.forEach(function (host, i) {
                var col = i % HOSTS_PER_ROW;
                var row = Math.floor(i / HOSTS_PER_ROW);
                sp.hostPositions.push({
                    x: hostStartX + col * (HOST_W + H_GAP),
                    y: HOST_TOP + row * (HOST_H + V_GAP),
                    host: host
                });
            });

            serverPositions.push(sp);
            currentX += clusterW + SERVER_GAP;
        });

        var totalHostRows = Math.max(1, ...servers.map(function (s) { return Math.ceil((s.hosts || []).length / HOSTS_PER_ROW); }));
        var canvasW = currentX - SERVER_GAP + PAD * 2;
        var canvasH = HOST_TOP + totalHostRows * (HOST_H + V_GAP) + PAD + 80;

        // ── Zoom/pan viewport ────────────────────────────────────────────────
        var viewport = document.createElement('div');
        viewport.className = 'env-viewport';
        container.appendChild(viewport);

        // Toolbar
        var toolbar = document.createElement('div');
        toolbar.className = 'qp-v2-toolbar';
        toolbar.innerHTML =
            '<button class="qp-v2-tb-btn" data-action="zoomin"  title="Zoom in">+</button>' +
            '<button class="qp-v2-tb-btn" data-action="zoomout" title="Zoom out">\u2212</button>' +
            '<button class="qp-v2-tb-btn" data-action="fit"     title="Fit / reset">\u2922</button>';
        viewport.appendChild(toolbar);

        // Canvas wrapper
        var wrap = document.createElement('div');
        wrap.className = 'env-canvas';
        wrap.style.width = canvasW + 'px';
        wrap.style.height = canvasH + 'px';
        viewport.appendChild(wrap);

        // ── SVG edge layer ───────────────────────────────────────────────────
        var SVG_NS = 'http://www.w3.org/2000/svg';
        var svg = document.createElementNS(SVG_NS, 'svg');
        svg.setAttribute('width', canvasW);
        svg.setAttribute('height', canvasH);
        svg.style.cssText = 'position:absolute;top:0;left:0;pointer-events:none;overflow:visible;';
        wrap.appendChild(svg);

        // Arrow marker def
        var defs = document.createElementNS(SVG_NS, 'defs');
        var marker = document.createElementNS(SVG_NS, 'marker');
        marker.setAttribute('id', 'env-arrow');
        marker.setAttribute('viewBox', '0 0 10 10');
        marker.setAttribute('refX', '9');
        marker.setAttribute('refY', '5');
        marker.setAttribute('markerWidth', '6');
        marker.setAttribute('markerHeight', '6');
        marker.setAttribute('orient', 'auto-start-reverse');
        var arrowPath = document.createElementNS(SVG_NS, 'path');
        arrowPath.setAttribute('d', 'M 0 0 L 10 5 L 0 10 z');
        arrowPath.setAttribute('fill', '#5a5a5a');
        marker.appendChild(arrowPath);
        defs.appendChild(marker);
        svg.appendChild(defs);

        // Detail pane
        var pane = createDetailPane(container);

        // ── Draw server → host edges ─────────────────────────────────────────
        serverPositions.forEach(function (sp) {
            var sx = sp.x + SERVER_W / 2;
            var sy = sp.y + SERVER_H;

            sp.hostPositions.forEach(function (hp) {
                var hx = hp.x + HOST_W / 2;
                var hy = hp.y;

                var my = (sy + hy) / 2;

                var path = document.createElementNS(SVG_NS, 'path');
                path.setAttribute('d', 'M ' + sx + ' ' + sy + ' C ' + sx + ' ' + my + ' ' + hx + ' ' + my + ' ' + hx + ' ' + hy);
                path.setAttribute('fill', 'none');
                path.setAttribute('stroke', '#4a5568');
                path.setAttribute('stroke-width', Math.max(1.5, Math.min(6, 1 + Math.log2(hp.host.connectionCount + 1))));
                path.setAttribute('stroke-linecap', 'round');
                path.setAttribute('opacity', '0.6');
                svg.appendChild(path);

                // Connection count label on edge
                if (hp.host.connectionCount > 1) {
                    var txt = document.createElementNS(SVG_NS, 'text');
                    txt.setAttribute('x', (sx + hx) / 2);
                    txt.setAttribute('y', my - 4);
                    txt.setAttribute('text-anchor', 'middle');
                    txt.setAttribute('font-size', '9');
                    txt.setAttribute('fill', '#718096');
                    txt.textContent = hp.host.connectionCount;
                    svg.appendChild(txt);
                }
            });
        });

        // ── Draw cross-server links ──────────────────────────────────────────
        (data.crossLinks || []).forEach(function (link) {
            var fromSp = serverPositions.find(function (sp) { return sp.server.name === link.fromServer; });
            var toSp   = serverPositions.find(function (sp) { return sp.server.name === link.toServer; });
            if (!fromSp || !toSp) return;

            var x1 = fromSp.x + SERVER_W / 2;
            var y1 = fromSp.y + SERVER_H / 2;
            var x2 = toSp.x + SERVER_W / 2;
            var y2 = toSp.y + SERVER_H / 2;

            // Dashed line between servers
            var line = document.createElementNS(SVG_NS, 'line');
            line.setAttribute('x1', x1);
            line.setAttribute('y1', y1);
            line.setAttribute('x2', x2);
            line.setAttribute('y2', y2);
            line.setAttribute('stroke', '#f59e0b');
            line.setAttribute('stroke-width', '2');
            line.setAttribute('stroke-dasharray', '8,4');
            line.setAttribute('opacity', '0.7');
            line.setAttribute('marker-end', 'url(#env-arrow)');
            svg.appendChild(line);

            // Label
            var lbl = document.createElementNS(SVG_NS, 'text');
            lbl.setAttribute('x', (x1 + x2) / 2);
            lbl.setAttribute('y', (y1 + y2) / 2 - 8);
            lbl.setAttribute('text-anchor', 'middle');
            lbl.setAttribute('font-size', '10');
            lbl.setAttribute('fill', '#f59e0b');
            lbl.textContent = link.viaHost + ' (' + link.connectionCount + ')';
            svg.appendChild(lbl);
        });

        // ── Draw shared host indicators ──────────────────────────────────────
        (data.sharedHosts || []).forEach(function (sh) {
            // Find all host positions for this hostname across servers
            var positions = [];
            serverPositions.forEach(function (sp) {
                sp.hostPositions.forEach(function (hp) {
                    if (hp.host.hostname.toLowerCase() === sh.hostname.toLowerCase()) {
                        positions.push(hp);
                    }
                });
            });

            // Draw dashed connector between same hosts on different servers
            for (var i = 0; i < positions.length - 1; i++) {
                var p1 = positions[i];
                var p2 = positions[i + 1];
                var line = document.createElementNS(SVG_NS, 'line');
                line.setAttribute('x1', p1.x + HOST_W / 2);
                line.setAttribute('y1', p1.y + HOST_H / 2);
                line.setAttribute('x2', p2.x + HOST_W / 2);
                line.setAttribute('y2', p2.y + HOST_H / 2);
                line.setAttribute('stroke', '#38bdf8');
                line.setAttribute('stroke-width', '1.5');
                line.setAttribute('stroke-dasharray', '5,3');
                line.setAttribute('opacity', '0.5');
                svg.appendChild(line);
            }
        });

        // ── Render server nodes ──────────────────────────────────────────────
        serverPositions.forEach(function (sp) {
            var server = sp.server;

            var node = document.createElement('div');
            node.className = 'env-server-node' + (server.error ? ' env-server-error' : '');
            node.style.left = sp.x + 'px';
            node.style.top  = sp.y + 'px';

            var icon = '<div class="env-server-icon"><i class="fa-solid fa-server"></i></div>';
            var title = '<div class="env-server-name">' + esc(server.name) + '</div>';

            var badges = '';
            if (!server.error) {
                badges = '<div class="env-server-badges">' +
                    '<span class="env-sbadge" title="Hosts"><i class="fa-solid fa-desktop"></i> ' + server.counts.hosts + '</span>' +
                    '<span class="env-sbadge" title="Apps"><i class="fa-solid fa-cube"></i> ' + server.counts.apps + '</span>' +
                    '<span class="env-sbadge" title="Users"><i class="fa-solid fa-user"></i> ' + server.counts.users + '</span>' +
                    '<span class="env-sbadge" title="DBs"><i class="fa-solid fa-database"></i> ' + server.counts.dbs + '</span>' +
                    '<span class="env-sbadge" title="IPs"><i class="fa-solid fa-network-wired"></i> ' + server.counts.ips + '</span>' +
                    '</div>';
            } else {
                badges = '<div class="env-server-error-msg"><i class="fa-solid fa-circle-exclamation"></i> ' + esc(server.error) + '</div>';
            }

            node.innerHTML = icon + title + badges;
            wrap.appendChild(node);

            node.addEventListener('click', function (e) {
                e.stopPropagation();
                showServerDetail(pane, server);
            });
        });

        // ── Render host nodes ────────────────────────────────────────────────
        serverPositions.forEach(function (sp) {
            sp.hostPositions.forEach(function (hp) {
                var host = hp.host;
                var isShared = (data.sharedHosts || []).some(function (sh) {
                    return sh.hostname.toLowerCase() === host.hostname.toLowerCase();
                });

                var node = document.createElement('div');
                node.className = 'env-host-node' + (isShared ? ' env-host-shared' : '');
                node.style.left = hp.x + 'px';
                node.style.top  = hp.y + 'px';

                var hostName = host.hostname || '(unknown)';
                if (hostName.length > 20) hostName = hostName.substring(0, 18) + '\u2026';

                node.innerHTML =
                    '<div class="env-host-icon"><i class="fa-solid fa-desktop"></i></div>' +
                    '<div class="env-host-info">' +
                        '<div class="env-host-name" title="' + esc(host.hostname) + '">' + esc(hostName) + '</div>' +
                        '<div class="env-host-meta">' + host.connectionCount + ' conn \u00b7 ' + host.uniqueApps + ' apps</div>' +
                    '</div>';

                wrap.appendChild(node);

                node.addEventListener('click', function (e) {
                    e.stopPropagation();
                    showHostDetail(pane, host, sp.server.name);
                });
            });
        });

        // ── Pan / zoom ───────────────────────────────────────────────────────
        var _tx = 0, _ty = 0, _scale = 1;
        var _dragging = false, _lastX = 0, _lastY = 0;

        function applyXform() {
            wrap.style.transform = 'translate(' + _tx + 'px,' + _ty + 'px) scale(' + _scale + ')';
        }

        function clampScale(s) { return Math.max(0.12, Math.min(5, s)); }

        function zoomAt(vx, vy, factor) {
            var ns = clampScale(_scale * factor);
            _tx = vx - (vx - _tx) * (ns / _scale);
            _ty = vy - (vy - _ty) * (ns / _scale);
            _scale = ns;
            applyXform();
        }

        function resetFit() {
            // Auto-fit to viewport
            var vw = viewport.clientWidth || viewport.offsetWidth || 800;
            var vh = viewport.clientHeight || viewport.offsetHeight || 600;
            var sw = canvasW || 800;
            var sh = canvasH || 600;
            var fitScale = Math.min(vw / sw, vh / sh, 1);
            _scale = clampScale(fitScale * 0.95);
            _tx = (vw - sw * _scale) / 2;
            _ty = 10;
            applyXform();
        }

        // Wheel zoom
        viewport.addEventListener('wheel', function (e) {
            e.preventDefault();
            var r = viewport.getBoundingClientRect();
            var f = e.deltaY < 0 ? 1.12 : 1 / 1.12;
            zoomAt(e.clientX - r.left, e.clientY - r.top, f);
        }, { passive: false });

        // Drag pan
        viewport.addEventListener('mousedown', function (e) {
            if (e.button !== 0) return;
            _dragging = true;
            _lastX = e.clientX;
            _lastY = e.clientY;
            viewport.style.cursor = 'grabbing';
            e.preventDefault();
        });

        var onMove = function (e) {
            if (!_dragging) return;
            _tx += e.clientX - _lastX;
            _ty += e.clientY - _lastY;
            _lastX = e.clientX;
            _lastY = e.clientY;
            applyXform();
        };
        var onUp = function () {
            if (_dragging) { _dragging = false; viewport.style.cursor = ''; }
        };
        window.addEventListener('mousemove', onMove);
        window.addEventListener('mouseup', onUp);

        // Double-click to reset fit
        viewport.addEventListener('dblclick', resetFit);

        // Toolbar buttons
        toolbar.addEventListener('click', function (e) {
            var btn = e.target.closest('[data-action]');
            if (!btn) return;
            var r = viewport.getBoundingClientRect();
            var cx = r.width / 2, cy = r.height / 2;
            if      (btn.dataset.action === 'zoomin')  zoomAt(cx, cy, 1.25);
            else if (btn.dataset.action === 'zoomout') zoomAt(cx, cy, 1 / 1.25);
            else if (btn.dataset.action === 'fit')     resetFit();
        });

        // Click on empty space to close detail pane
        wrap.addEventListener('click', function () {
            pane.style.display = 'none';
        });

        // Cleanup on container re-use
        var obs = new MutationObserver(function () {
            if (!viewport.isConnected) {
                window.removeEventListener('mousemove', onMove);
                window.removeEventListener('mouseup', onUp);
                obs.disconnect();
            }
        });
        obs.observe(container, { childList: true });

        // Initial fit
        requestAnimationFrame(resetFit);
    }

    // ── Public API ───────────────────────────────────────────────────────────
    return {
        render: render
    };
})();
