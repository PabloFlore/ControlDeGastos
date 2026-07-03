window.descargarArchivo = function (nombre, contenido, tipo) {
    const blob = new Blob([contenido], { type: tipo });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = nombre;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

window.generarExcel = function (registros, nombreMes, anio, monedaSimbolo, totalGastos, totalIngresos) {
    if (typeof XLSX === 'undefined') {
        alert('Error: La librería xlsx no está cargada. Recarga la página.');
        return;
    }

    var data = [['Fecha', 'Tipo', 'Categoría', 'Descripción', 'Monto']];

    for (var i = 0; i < registros.length; i++) {
        var r = registros[i];
        var tipo = r.monto >= 0 ? 'Gasto' : 'Ingreso';
        data.push([r.fecha, tipo, r.categoria, r.descripcion || '', r.monto]);
    }

    data.push([]);
    data.push(['', '', '', 'Total gastos', Math.abs(totalGastos)]);
    data.push(['', '', '', 'Total ingresos', Math.abs(totalIngresos)]);
    data.push(['', '', '', 'Balance', totalIngresos - totalGastos]);

    var ws = XLSX.utils.aoa_to_sheet(data);

    var anchos = [
        { wch: 14 },
        { wch: 10 },
        { wch: 16 },
        { wch: 30 },
        { wch: 14 },
    ];
    ws['!cols'] = anchos;

    for (var r = 1; r <= registros.length; r++) {
        var ref = 'E' + (r + 1);
        if (ws[ref]) {
            ws[ref].t = 'n';
            ws[ref].z = '$#,##0.00';
        }
    }

    for (var r2 = registros.length + 2; r2 <= registros.length + 4; r2++) {
        var ref2 = 'D' + (r2 + 1);
        var ref2v = 'E' + (r2 + 1);
        if (ws[ref2]) ws[ref2].s = { font: { bold: true } };
        if (ws[ref2v]) {
            ws[ref2v].t = 'n';
            ws[ref2v].z = '$#,##0.00';
            ws[ref2v].s = { font: { bold: true } };
        }
    }

    var headerRefs = ['A1', 'B1', 'C1', 'D1', 'E1'];
    for (var h = 0; h < headerRefs.length; h++) {
        if (ws[headerRefs[h]]) {
            ws[headerRefs[h]].s = {
                font: { bold: true, color: { rgb: 'FFFFFF' } },
                fill: { fgColor: { rgb: '0071C9' } },
                alignment: { horizontal: 'center' },
            };
        }
    }

    var wsName = nombreMes + ' ' + anio;
    var wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, wsName);

    var nombre = 'gastos_' + nombreMes.toLowerCase().replace(/\s/g, '_') + '_' + anio + '.xlsx';
    XLSX.writeFile(wb, nombre);
};

window.importFileInputClick = function () {
    document.getElementById('importFileJson').click();
};

window.leerArchivoJson = function () {
    return new Promise(function (resolve, reject) {
        var input = document.getElementById('importFileJson');
        if (!input || !input.files || !input.files[0]) {
            reject('No se seleccionó ningún archivo');
            return;
        }
        var reader = new FileReader();
        reader.onload = function () {
            resolve(reader.result);
        };
        reader.onerror = function () {
            reject('Error al leer el archivo');
        };
        reader.readAsText(input.files[0]);
    });
};
