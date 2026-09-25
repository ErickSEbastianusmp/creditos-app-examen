(function () {
    'use strict';

    var estadoInfo = {
        'Pendiente': { texto: 'Pendiente', clase: 'bg-secondary' },
        'Aprobado': { texto: 'Aprobado', clase: 'bg-success' },
        'Rechazado': { texto: 'Rechazado', clase: 'bg-danger' }
    };

    function elementoEstado() {
        return document.getElementById('signalr-estado');
    }

    function setEstadoConexion(texto, clase) {
        var el = elementoEstado();
        if (!el) return;
        el.textContent = texto;
        el.className = 'badge ' + clase;
    }

    function crearToast(titulo, mensaje) {
        var container = document.getElementById('signalr-toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'signalr-toast-container';
            container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
            document.body.appendChild(container);
        }

        var toast = document.createElement('div');
        toast.className = 'toast align-items-center text-bg-primary border-0';
        toast.setAttribute('role', 'alert');
        toast.innerHTML =
            '<div class="d-flex">' +
            '  <div class="toast-body">' +
            '    <strong>' + titulo + '</strong><br />' + mensaje +
            '  </div>' +
            '  <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Cerrar"></button>' +
            '</div>';

        container.appendChild(toast);
        var instance = new bootstrap.Toast(toast, { delay: 5000 });
        toast.addEventListener('hidden.bs.toast', function () { toast.remove(); });
        instance.show();
    }

    function aplicaEstadoSolicitud(data) {
        var info = estadoInfo[data.estado] || { texto: data.estado, clase: 'bg-secondary' };

        document.querySelectorAll('[data-solicitud-id="' + data.solicitudId + '"]').forEach(function (el) {
            el.textContent = info.texto;
            el.className = 'badge ' + info.clase;
        });

        var motivo = document.querySelector('[data-solicitud-motivo="' + data.solicitudId + '"]');
        if (motivo) {
            motivo.textContent = data.motivoRechazo || '—';
        }

        var fila = document.querySelector('[data-fila-solicitud="' + data.solicitudId + '"]');
        if (fila) {
            fila.classList.remove('table-success', 'table-danger');
            if (info.clase === 'bg-success') fila.classList.add('table-success');
            if (info.clase === 'bg-danger') fila.classList.add('table-danger');
        }

        var mensaje = 'El estado ahora es "' + info.texto + '"';
        if (data.motivoRechazo) {
            mensaje += '<br />Motivo: ' + data.motivoRechazo;
        }
        crearToast('Solicitud #' + data.solicitudId + ' actualizada', mensaje);
    }

    function conectar() {
        var connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/solicitudes', { transport: signalR.HttpTransportType.WebSockets })
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .withHubProtocol(new signalR.JsonHubProtocol())
            .build();

        connection.on('SolicitudEstadoActualizado', function (data) {
            aplicaEstadoSolicitud(data);
        });

        connection.onclose(function () { setEstadoConexion('Desconectado', 'bg-secondary'); });
        connection.onreconnecting(function () { setEstadoConexion('Reconectando', 'bg-warning text-dark'); });
        connection.onreconnected(function () {
            setEstadoConexion('Conectado', 'bg-success');
            window.location.reload();
        });

        connection.start()
            .then(function () { setEstadoConexion('Conectado', 'bg-success'); })
            .catch(function () { setEstadoConexion('Desconectado', 'bg-danger'); });
    }

    document.addEventListener('DOMContentLoaded', conectar);
})();