namespace Wrexercise.Features.Atividades;

/// <summary>
/// Os números do Percurso pelo algoritmo v1, o mesmo do Movr (<c>RouteMetrics</c>, ADR 0085). Os dois aplicativos
/// guardam os pontos crus e calculam na leitura, ponto a ponto:
/// <list type="number">
/// <item>Precisão pior que 30 m é descartada.</item>
/// <item>Salto acima da velocidade limite do tipo é descartado; depois de 5 descartes seguidos, o percurso
/// recomeça no ponto novo, sem somar o salto.</item>
/// <item>Filtro de Kalman de velocidade constante, por eixo, em metros num plano local. O desvio da medida é a
/// precisão ÷ 1,51 (a precisão do GPS é o raio de 68% no plano).</item>
/// <item>Em movimento: velocidade suavizada acima do limiar do tipo <b>e</b> deslocamento em 15 s compatível com
/// esse limiar. Parado, nada soma e o tempo em movimento não conta.</item>
/// <item>A distância soma cordas do trajeto suavizado só quando ele se afasta do último ponto contado pelo
/// menos a corda mínima do tipo (ou a precisão, se maior), tirando metade da variância da posição.</item>
/// <item>Lacuna de mais de 30 s: até 2 min e com velocidade plausível, soma a reta (túnel); senão, não soma.
/// Outro <c>Segmento</c> nunca liga ao anterior.</item>
/// <item>Altitude: média exponencial e histerese de 3 m na subida e na descida (o barômetro, quando há, vale
/// antes do GPS).</item>
/// <item>Parciais interpoladas na marca exata (1 km, ou 5 km no Ciclismo).</item>
/// </list>
/// Mudar uma regra aqui exige a mesma mudança no Movr e nos arquivos de referência (<c>Fixtures/Percursos</c>).
/// </summary>
internal sealed class MetricasPercurso
{
    public const int Versao = 1;

    private const double PrecisaoMaximaMetros = 30;
    private const double LacunaSegundos = 30;
    private const double PonteSegundos = 120;
    private const double HistereseAltitudeMetros = 3;
    private const double JanelaVelocidadeMaximaSegundos = 10;
    private const double JanelaMovimentoSegundos = 15;
    private const int DescartesParaRecomecar = 5;
    private const double RaioTerraMetros = 6_371_008.8;
    private const double TauAltitudeGps = 10;
    private const double TauAltitudeBarometro = 4;
    private const double VarianciaVelocidadeInicial = 25;
    private const double PrecisaoPorDesvio = 1.51;

    private readonly Perfil _perfil;
    private readonly Eixo _x = new();
    private readonly Eixo _y = new();
    private readonly Altitude _altitudeGps = new(TauAltitudeGps);
    private readonly Altitude _altitudeBarometro = new(TauAltitudeBarometro);
    private readonly List<(double Ativo, double Distancia)> _serie = [];
    private readonly List<ParcialPercurso> _parciais = [];
    private readonly Queue<(DateTimeOffset Em, double X, double Y)> _janela = new();

    private (PontoPercurso Ponto, bool Barometrica)? _ancora;
    private double _origemLat;
    private double _origemLon;
    private double _cosOrigem;
    private bool _temOrigem;
    private double _contadoX;
    private double _contadoY;
    private int _descartes;
    private double _inicioParcial;

    public MetricasPercurso(string tipo)
    {
        _perfil = Perfil.Para(tipo);
    }

    /// <summary>Distância acumulada, em metros.</summary>
    public double DistanciaMetros { get; private set; }

    /// <summary>Tempo coberto pelos pontos, em segundos.</summary>
    public double SegundosAtivos { get; private set; }

    /// <summary>Tempo em movimento, em segundos.</summary>
    public double SegundosEmMovimento { get; private set; }

    /// <summary>Subida acumulada, ou <c>null</c> sem altitude.</summary>
    public double? GanhoElevacao => AltitudeAtual.Tem ? AltitudeAtual.Ganho : null;

    /// <summary>Descida acumulada, ou <c>null</c> sem altitude.</summary>
    public double? PerdaElevacao => AltitudeAtual.Tem ? AltitudeAtual.Perda : null;

    private Altitude AltitudeAtual => _altitudeBarometro.Tem ? _altitudeBarometro : _altitudeGps;

    /// <summary>Calcula tudo de uma vez, na ordem dos pontos.</summary>
    public static MetricasPercurso Calcular(IEnumerable<PontoPercurso> pontos, string tipo)
    {
        var metricas = new MetricasPercurso(tipo);
        foreach (var ponto in pontos)
        {
            metricas.Adicionar(ponto);
        }

        return metricas;
    }

    public static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = Radianos(lat2 - lat1);
        var dLon = Radianos(lon2 - lon1);
        var a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (Math.Cos(Radianos(lat1)) * Math.Cos(Radianos(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        return 2 * RaioTerraMetros * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>Soma um ponto. <c>true</c> quando ele foi aceito.</summary>
    /// <param name="barometrica">A altitude veio do barômetro (o Movr marca; o navegador nunca).</param>
    public bool Adicionar(PontoPercurso ponto, bool barometrica = false)
    {
        if (!double.IsFinite(ponto.Precisao) || ponto.Precisao > PrecisaoMaximaMetros)
        {
            return false;
        }

        if (_ancora is not { } ancora || ponto.Segmento != ancora.Ponto.Segmento)
        {
            Reancorar(ponto, barometrica);
            return true;
        }

        var dt = (ponto.Em - ancora.Ponto.Em).TotalSeconds;
        if (dt <= 0)
        {
            return false;
        }

        var reta = Haversine(ancora.Ponto.Latitude, ancora.Ponto.Longitude, ponto.Latitude, ponto.Longitude);
        if (dt > LacunaSegundos)
        {
            var ponte = dt <= PonteSegundos && reta / dt <= _perfil.VelocidadePonte;
            Avancar(dt, ponte ? reta : 0, ponte && reta / dt >= _perfil.VelocidadeMovimento);
            Reancorar(ponto, barometrica);
            return true;
        }

        if (reta / dt > _perfil.VelocidadeSalto)
        {
            _descartes++;
            if (_descartes < DescartesParaRecomecar)
            {
                return false;
            }

            Avancar(dt, 0, false);
            Reancorar(ponto, barometrica);
            return true;
        }

        _descartes = 0;
        var (x, y) = Projetar(ponto);
        var variancia = VarianciaMedida(ponto);
        _x.Passo(x, variancia, dt, _perfil.RuidoProcesso);
        _y.Passo(y, variancia, dt, _perfil.RuidoProcesso);

        var velocidade = Math.Sqrt((_x.Velocidade * _x.Velocidade) + (_y.Velocidade * _y.Velocidade));
        var movendo = velocidade >= _perfil.VelocidadeMovimento && SaiuDoLugar(ponto.Em);

        var passoX = _x.Posicao - _contadoX;
        var passoY = _y.Posicao - _contadoY;
        var passo = Math.Sqrt((passoX * passoX) + (passoY * passoY));
        var contado = 0.0;
        if (!movendo)
        {
            _contadoX = _x.Posicao;
            _contadoY = _y.Posicao;
        }
        else if (passo >= Math.Max(_perfil.CordaMinima, ponto.Precisao))
        {
            var ruido = 0.5 * (_x.Variancia + _y.Variancia);
            contado = Math.Sqrt(Math.Max(0, (passo * passo) - ruido));
            _contadoX = _x.Posicao;
            _contadoY = _y.Posicao;
        }

        var pendente = 0.0;
        if (movendo && contado == 0)
        {
            var ruido = 0.5 * (_x.Variancia + _y.Variancia);
            pendente = Math.Sqrt(Math.Max(0, (passo * passo) - ruido));
        }

        Avancar(dt, contado, movendo, pendente);
        RegistrarAltitude(ponto, barometrica, dt);
        _ancora = (ponto, barometrica);
        return true;
    }

    /// <summary>As parciais fechadas e a última, com o que sobrou (a partir de 1 m).</summary>
    public IReadOnlyList<ParcialPercurso> Parciais()
    {
        var parciais = new List<ParcialPercurso>(_parciais);
        var resto = DistanciaMetros - (_parciais.Count * _perfil.MetrosParcial);
        if (resto >= 1 && SegundosAtivos > _inicioParcial)
        {
            parciais.Add(new ParcialPercurso(
                (int)Math.Round(resto),
                (int)Math.Round(SegundosAtivos - _inicioParcial)));
        }

        return parciais;
    }

    /// <summary>
    /// A maior velocidade média numa janela de 10 s ou mais que também cubra a distância mínima do tipo, em m/s;
    /// <c>null</c> sem janela.
    /// </summary>
    public double? VelocidadeMaxima()
    {
        double? maxima = null;
        var j = 0;
        for (var i = 0; i < _serie.Count; i++)
        {
            bool Cabe(int inicio) =>
                _serie[i].Ativo - _serie[inicio].Ativo >= JanelaVelocidadeMaximaSegundos
                && _serie[i].Distancia - _serie[inicio].Distancia >= _perfil.JanelaVelocidadeMaximaMetros;

            while (j + 1 < i && Cabe(j + 1))
            {
                j++;
            }

            if (!Cabe(j))
            {
                continue;
            }

            var velocidade = (_serie[i].Distancia - _serie[j].Distancia) / (_serie[i].Ativo - _serie[j].Ativo);
            if (maxima is null || velocidade > maxima)
            {
                maxima = velocidade;
            }
        }

        return maxima;
    }

    private void Reancorar(PontoPercurso ponto, bool barometrica)
    {
        if (!_temOrigem)
        {
            _origemLat = ponto.Latitude;
            _origemLon = ponto.Longitude;
            _cosOrigem = Math.Cos(Radianos(ponto.Latitude));
            _temOrigem = true;
        }

        var (x, y) = Projetar(ponto);
        var variancia = VarianciaMedida(ponto);
        _x.Reiniciar(x, variancia, VarianciaVelocidadeInicial);
        _y.Reiniciar(y, variancia, VarianciaVelocidadeInicial);
        _janela.Clear();
        _janela.Enqueue((ponto.Em, x, y));
        _contadoX = x;
        _contadoY = y;
        _descartes = 0;
        RegistrarAltitude(ponto, barometrica, 0);
        _serie.Add((SegundosAtivos, DistanciaMetros));
        _ancora = (ponto, barometrica);
    }

    private bool SaiuDoLugar(DateTimeOffset em)
    {
        _janela.Enqueue((em, _x.Posicao, _y.Posicao));
        while (_janela.Count > 2 && (em - _janela.ElementAt(1).Em).TotalSeconds >= JanelaMovimentoSegundos)
        {
            _janela.Dequeue();
        }

        var (inicioEm, inicioX, inicioY) = _janela.Peek();
        var duracao = (em - inicioEm).TotalSeconds;
        if (duracao < JanelaMovimentoSegundos)
        {
            return true;
        }

        var dx = _x.Posicao - inicioX;
        var dy = _y.Posicao - inicioY;
        return Math.Sqrt((dx * dx) + (dy * dy)) >= _perfil.VelocidadeMovimento * duracao;
    }

    private void Avancar(double dt, double distancia, bool movendo, double pendente = 0)
    {
        var ativoAntes = SegundosAtivos;
        var distanciaAntes = DistanciaMetros;
        SegundosAtivos += dt;
        DistanciaMetros += distancia;
        if (movendo)
        {
            SegundosEmMovimento += dt;
        }

        while (DistanciaMetros >= (_parciais.Count + 1) * _perfil.MetrosParcial)
        {
            var marca = (_parciais.Count + 1) * _perfil.MetrosParcial;
            var fracao = distancia > 0 ? (marca - distanciaAntes) / distancia : 1;
            var em = ativoAntes + (fracao * dt);
            _parciais.Add(new ParcialPercurso((int)_perfil.MetrosParcial, (int)Math.Round(em - _inicioParcial)));
            _inicioParcial = em;
        }

        _serie.Add((SegundosAtivos, DistanciaMetros + pendente));
    }

    private void RegistrarAltitude(PontoPercurso ponto, bool barometrica, double dt)
    {
        if (ponto.Altitude is not { } altitude)
        {
            return;
        }

        (barometrica ? _altitudeBarometro : _altitudeGps).Adicionar(altitude, dt);
    }

    private (double X, double Y) Projetar(PontoPercurso ponto) =>
        (Radianos(ponto.Longitude - _origemLon) * RaioTerraMetros * _cosOrigem,
         Radianos(ponto.Latitude - _origemLat) * RaioTerraMetros);

    private static double Radianos(double graus) => graus * Math.PI / 180;

    private static double VarianciaMedida(PontoPercurso ponto)
    {
        var desvio = Math.Max(1, ponto.Precisao / PrecisaoPorDesvio);
        return desvio * desvio;
    }

    /// <summary>Os parâmetros de cada tipo; os mesmos do Movr (Caminhada, Corrida, Ciclismo).</summary>
    private sealed record Perfil(
        double VelocidadeSalto,
        double VelocidadeMovimento,
        double VelocidadePonte,
        double RuidoProcesso,
        double CordaMinima,
        double JanelaVelocidadeMaximaMetros,
        double MetrosParcial)
    {
        private static readonly Perfil Caminhada = new(6, 0.5, 3, 0.3, 10, 200, 1_000);
        private static readonly Perfil Corrida = new(10, 1.0, 7, 0.8, 10, 200, 1_000);
        private static readonly Perfil Ciclismo = new(25, 1.5, 20, 1.5, 20, 250, 5_000);

        public static Perfil Para(string tipo) => tipo switch
        {
            Contracts.TipoAtividade.Corrida => Corrida,
            Contracts.TipoAtividade.Ciclismo => Ciclismo,
            _ => Caminhada,
        };
    }

    /// <summary>Kalman de velocidade constante num eixo: estado (posição, velocidade).</summary>
    private sealed class Eixo
    {
        private double _p00;
        private double _p01;
        private double _p11;

        public double Posicao { get; private set; }

        public double Velocidade { get; private set; }

        public double Variancia => _p00;

        public void Reiniciar(double posicao, double varianciaPosicao, double varianciaVelocidade)
        {
            Posicao = posicao;
            Velocidade = 0;
            _p00 = varianciaPosicao;
            _p01 = 0;
            _p11 = varianciaVelocidade;
        }

        public void Passo(double medida, double varianciaMedida, double dt, double ruidoAceleracao)
        {
            Posicao += Velocidade * dt;
            var q = ruidoAceleracao * ruidoAceleracao;
            var dt2 = dt * dt;
            var p00 = _p00 + (2 * dt * _p01) + (dt2 * _p11) + (q * dt2 * dt2 / 4);
            var p01 = _p01 + (dt * _p11) + (q * dt2 * dt / 2);
            var p11 = _p11 + (q * dt2);

            var s = p00 + varianciaMedida;
            var k0 = p00 / s;
            var k1 = p01 / s;
            var inovacao = medida - Posicao;
            Posicao += k0 * inovacao;
            Velocidade += k1 * inovacao;
            _p00 = (1 - k0) * p00;
            _p01 = (1 - k0) * p01;
            _p11 = p11 - (k1 * p01);
        }
    }

    /// <summary>Subida e descida com média exponencial e histerese.</summary>
    private sealed class Altitude(double tauSegundos)
    {
        private double _suavizada;
        private double _referencia;

        public bool Tem { get; private set; }

        public double Ganho { get; private set; }

        public double Perda { get; private set; }

        public void Adicionar(double altitude, double dt)
        {
            if (!Tem)
            {
                _suavizada = altitude;
                _referencia = altitude;
                Tem = true;
                return;
            }

            var alpha = dt <= 0 ? 1 : 1 - Math.Exp(-dt / tauSegundos);
            _suavizada += alpha * (altitude - _suavizada);
            if (_suavizada - _referencia >= HistereseAltitudeMetros)
            {
                Ganho += _suavizada - _referencia;
                _referencia = _suavizada;
            }
            else if (_referencia - _suavizada >= HistereseAltitudeMetros)
            {
                Perda += _referencia - _suavizada;
                _referencia = _suavizada;
            }
        }
    }
}
