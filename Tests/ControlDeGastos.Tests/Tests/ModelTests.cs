namespace ControlDeGastos.Tests.Tests;

public class ModelTests
{
    [Fact]
    public void Licencia_DiasRestantes_ParaSiempre_RetornaMenosUno()
    {
        var licencia = new Licencia
        {
            LicenciaTipo = TipoLicencia.ParaSiempre,
            FechaExpiracion = null,
        };
        Assert.Equal(-1, licencia.DiasRestantes);
    }

    [Fact]
    public void Licencia_DiasRestantes_SinFechaExpiracion_RetornaCero()
    {
        var licencia = new Licencia
        {
            LicenciaTipo = TipoLicencia.Trial,
            FechaExpiracion = null,
        };
        Assert.Equal(0, licencia.DiasRestantes);
    }

    [Fact]
    public void Licencia_DiasRestantes_TrialValido_RetornaDiasCorrectos()
    {
        var licencia = new Licencia
        {
            LicenciaTipo = TipoLicencia.Trial,
            FechaExpiracion = DateTime.UtcNow.Date.AddDays(30),
        };
        Assert.Equal(30, licencia.DiasRestantes);
    }

    [Fact]
    public void Licencia_DiasRestantes_TrialExpirado_RetornaCero()
    {
        var licencia = new Licencia
        {
            LicenciaTipo = TipoLicencia.Trial,
            FechaExpiracion = DateTime.UtcNow.AddDays(-5),
        };
        Assert.Equal(0, licencia.DiasRestantes);
    }

    [Fact]
    public void Usuario_CreaGuidUnico_PorDefecto()
    {
        var u1 = new Usuario();
        var u2 = new Usuario();
        Assert.NotEqual(u1.Id, u2.Id);
    }

    [Fact]
    public void ProgresoRPG_ValoresDefault()
    {
        var p = new ProgresoRPG();
        Assert.Equal(1, p.Nivel);
        Assert.Equal(0, p.ExpActual);
        Assert.Equal(100, p.ExpRequerida);
        Assert.Equal(100, p.HpActual);
        Assert.Equal(100, p.HpMaximo);
        Assert.Empty(p.LogrosDesbloqueados);
        Assert.Empty(p.TitulosDesbloqueados);
        Assert.Empty(p.IdsCategoriasUsadas);
    }

    [Fact]
    public void Usuario_ValoresDefault()
    {
        var u = new Usuario();
        Assert.Equal(PlanType.Local, u.PlanActivo);
        Assert.False(u.ModoGamificadoActivo);
        Assert.Equal("MXN", u.Moneda);
        Assert.Null(u.Email);
        Assert.Null(u.TokenLicencia);
        Assert.Null(u.TipoLicencia);
        Assert.Null(u.HogarCodigo);
        Assert.Null(u.DispositivoFingerprint);
        Assert.Equal(30, u.PinDelaySegundos);
        Assert.Equal(default, u.FechaExpiracionLicencia);
        Assert.True((DateTime.UtcNow - u.FechaRegistro).TotalSeconds < 5);
    }

    [Fact]
    public void Financiamiento_ValoresDefault()
    {
        var f = new Financiamiento();
        Assert.Equal("Credito", f.Tipo);
        Assert.True(f.Activo);
        Assert.True((DateTime.UtcNow - f.CreadoEn).TotalSeconds < 5);
        Assert.Null(f.ActualizadoEn);
        Assert.False(f.Sincronizado);
        Assert.Null(f.HogarId);
        Assert.Null(f.TasaInteresAnual);
    }

    [Fact]
    public void Recurrencia_ValoresDefault()
    {
        var r = new Recurrencia();
        Assert.Equal(TipoRecurrencia.Mensual, r.TipoRecurrencia);
        Assert.True(r.Activa);
        Assert.Equal(1, r.Intervalo);
        Assert.False(r.Sincronizado);
        Assert.True((DateTime.UtcNow - r.CreadoEn).TotalSeconds < 5);
        Assert.True((DateTime.UtcNow - r.ActualizadoEn).TotalSeconds < 5);
        Assert.Null(r.HogarId);
    }

    [Fact]
    public void Presupuesto_ValoresDefault()
    {
        var p = new Presupuesto();
        Assert.Equal(PeriodoPresupuesto.Mensual, p.Periodo);
        Assert.True((DateTime.UtcNow - p.FechaInicio).TotalSeconds < 5);
        Assert.True((DateTime.UtcNow - p.CreadoEn).TotalSeconds < 5);
        Assert.True((DateTime.UtcNow - p.ActualizadoEn).TotalSeconds < 5);
        Assert.Null(p.FechaFin);
        Assert.Null(p.HogarId);
    }

    [Fact]
    public void Categoria_ValoresDefault()
    {
        var c = new Categoria();
        Assert.Equal("📁", c.Icono);
        Assert.Equal("#6c757d", c.Color);
        Assert.Equal(TipoGasto.Gasto, c.Tipo);
        Assert.Equal(0, c.Orden);
        Assert.False(c.EsPersonalizada);
        Assert.Null(c.PresupuestoPorDefecto);
        Assert.Null(c.HogarId);
        Assert.True((DateTime.UtcNow - c.ActualizadoEn).TotalSeconds < 5);
    }

    [Fact]
    public void Notificacion_ValoresDefault()
    {
        var n = new Notificacion();
        Assert.Equal("", n.Tipo);
        Assert.Equal("", n.Mensaje);
        Assert.Equal("", n.Icono);
        Assert.Null(n.ReferenciaId);
        Assert.True((DateTime.UtcNow - n.Fecha).TotalSeconds < 5);
    }

    [Fact]
    public void Logro_ValoresDefault()
    {
        var l = new Logro();
        Assert.Equal("🏅", l.Icono);
        Assert.Equal("", l.Nombre);
        Assert.Equal("", l.Descripcion);
    }

    [Fact]
    public void TituloCosmetico_ValoresDefault()
    {
        var t = new TituloCosmetico();
        Assert.Equal("🎖️", t.Icono);
        Assert.Equal("", t.Nombre);
        Assert.Equal("", t.Descripcion);
        Assert.Null(t.LogroRequerido);
    }

    [Fact]
    public void Hogar_ValoresDefault()
    {
        var h = new Hogar();
        Assert.Equal(TipoLicencia.Trial, h.LicenciaTipo);
        Assert.False(h.ModoGamificadoIncluido);
        Assert.Equal(PlanType.Nube, h.PlanIncluido);
        Assert.Null(h.FechaExpiracion);
    }

    [Fact]
    public void Gasto_ValoresDefault()
    {
        var g1 = new Gasto();
        var g2 = new Gasto();
        Assert.NotEqual(g1.Id, g2.Id);
        Assert.Equal(1, g1.NumeroVersion);
        Assert.False(g1.Sincronizado);
        Assert.False(g1.EsGastoCompartido);
        Assert.True((DateTime.UtcNow - g1.Fecha).TotalSeconds < 5);
        Assert.True((DateTime.UtcNow - g1.CreadoEn).TotalSeconds < 5);
        Assert.Null(g1.ActualizadoEn);
        Assert.Null(g1.HogarId);
    }

    [Fact]
    public void HogarMiembro_ValoresDefault()
    {
        var m = new HogarMiembro();
        Assert.Equal("", m.HogarId);
        Assert.Equal("", m.Email);
        Assert.True((DateTime.UtcNow - m.JoinedAt).TotalSeconds < 5);
    }

    [Fact]
    public void Licencia_ValoresDefault()
    {
        var l = new Licencia();
        Assert.Equal("", l.Token);
        Assert.Null(l.TokenHash);
        Assert.Equal(TipoLicencia.Trial, l.LicenciaTipo);
        Assert.Null(l.FechaExpiracion);
        Assert.True((DateTime.UtcNow - l.FechaActivacion).TotalSeconds < 5);
        Assert.Null(l.DispositivoId);
        Assert.Null(l.UltimaValidacion);
        Assert.False(l.Valida);
        Assert.Equal("", l.Mensaje);
        Assert.Equal(PlanType.Local, l.PlanIncluido);
        Assert.False(l.ModoGamificadoIncluido);
    }

    [Fact]
    public void Gasto_OtrosValoresDefault()
    {
        var g = new Gasto();
        Assert.Equal(default, g.UsuarioId);
        Assert.Equal(default, g.CategoriaId);
        Assert.Equal(0m, g.Monto);
        Assert.Null(g.Descripcion);
        Assert.Null(g.RecurrenciaId);
        Assert.Null(g.FinanciamientoId);
    }

    [Fact]
    public void Categoria_OtrosValoresDefault()
    {
        var c1 = new Categoria();
        var c2 = new Categoria();
        Assert.NotEqual(c1.Id, c2.Id);
        Assert.Null(c1.UsuarioId);
        Assert.Equal("", c1.Nombre);
    }

    [Fact]
    public void Presupuesto_OtrosValoresDefault()
    {
        var p1 = new Presupuesto();
        var p2 = new Presupuesto();
        Assert.NotEqual(p1.Id, p2.Id);
        Assert.Equal(default, p1.UsuarioId);
        Assert.Null(p1.CategoriaId);
        Assert.Equal(0m, p1.MontoLimite);
    }

    [Fact]
    public void Logro_OtrosValoresDefault()
    {
        var l = new Logro();
        Assert.Equal(default, l.Id);
        Assert.Equal(TipoCondicionLogro.GastosTotales, l.TipoCondicion);
        Assert.Equal(0, l.ValorCondicion);
        Assert.Equal(0, l.RecompensaExp);
        Assert.Equal(0, l.Orden);
    }

    [Fact]
    public void TituloCosmetico_OtrosValoresDefault()
    {
        var t = new TituloCosmetico();
        Assert.Equal("", t.Id);
        Assert.Equal(TipoCondicionTitulo.LogroEspecifico, t.TipoCondicion);
        Assert.Equal(0, t.ValorCondicion);
        Assert.Equal(0, t.Orden);
    }

    [Fact]
    public void ProgresoRPG_OtrosValoresDefault()
    {
        var p = new ProgresoRPG();
        Assert.NotEqual(default, p.Id);
        Assert.Equal(default, p.UsuarioId);
        Assert.Null(p.UltimoGastoFecha);
        Assert.Equal(0, p.GastosConsecutivos);
        Assert.Equal(0, p.GastosEstePeriodo);
        Assert.Equal(0, p.UltimoResetGastosMes);
        Assert.Equal(0, p.UltimoResetGastosAnio);
        Assert.Null(p.TituloActivoId);
    }

    [Fact]
    public void Hogar_OtrosValoresDefault()
    {
        var h1 = new Hogar();
        var h2 = new Hogar();
        Assert.NotEqual(h1.Id, h2.Id);
        Assert.Equal("", h1.CodigoInvitacion);
        Assert.Equal("", h1.CreadoPorEmail);
        Assert.True((DateTime.UtcNow - h1.CreatedAt).TotalSeconds < 5);
        Assert.Null(h1.TokenHash);
    }
}
