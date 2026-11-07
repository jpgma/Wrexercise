using System.Text.Json;
using System.Text.Json.Serialization;
using Wrexercise.Features.Atividades;
using Wrexercise.Features.Autenticacao;
using Wrexercise.Features.Convites;
using Wrexercise.Features.Desafios;
using Wrexercise.Features.Grupos;
using Wrexercise.Features.Health;
using Wrexercise.Features.Jogo;
using Wrexercise.Features.Notificacoes;
using Wrexercise.Features.Progresso;
using Wrexercise.Features.Reacoes;
using Wrexercise.Features.Usuarios;
using Wrexercise.Infrastructure.Errors;

namespace Wrexercise.Infrastructure.Json;

// Todo tipo serializado pela API precisa estar registrado aqui (exigência do Native AOT).
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(AlterarModoInicioRequest))]
[JsonSerializable(typeof(AlterarNomeRequest))]
[JsonSerializable(typeof(AlterarPapelRequest))]
[JsonSerializable(typeof(AlterarSenhaRequest))]
[JsonSerializable(typeof(AtividadeResponse))]
[JsonSerializable(typeof(AtividadesPaginadasResponse))]
[JsonSerializable(typeof(CriarAtividadeRequest))]
[JsonSerializable(typeof(EvidenciaCriadaResponse))]
[JsonSerializable(typeof(ComentarioRequest))]
[JsonSerializable(typeof(ComentarioResponse))]
[JsonSerializable(typeof(ComentariosResponse))]
[JsonSerializable(typeof(ConfiguracaoInicialRequest))]
[JsonSerializable(typeof(ConfiguracaoInicialResponse))]
[JsonSerializable(typeof(ConviteConsultaResponse))]
[JsonSerializable(typeof(ConviteAtualResponse))]
[JsonSerializable(typeof(ConviteResponse))]
[JsonSerializable(typeof(ConviteTokenRequest))]
[JsonSerializable(typeof(CriarDesafioRequest))]
[JsonSerializable(typeof(CriarGrupoRequest))]
[JsonSerializable(typeof(DesafioResponse))]
[JsonSerializable(typeof(DesafiosResponse))]
[JsonSerializable(typeof(EditarDesafioRequest))]
[JsonSerializable(typeof(EnergiaDetalhamentoResponse))]
[JsonSerializable(typeof(EnergiaResponse))]
[JsonSerializable(typeof(EntrarGrupoResponse))]
[JsonSerializable(typeof(FilaAvaliacaoResponse))]
[JsonSerializable(typeof(MeuPerfilResponse))]
[JsonSerializable(typeof(NotificacoesNaoLidasResponse))]
[JsonSerializable(typeof(NotificacoesResponse))]
[JsonSerializable(typeof(RedefinirSenhaRequest))]
[JsonSerializable(typeof(PerfilMembroResponse))]
[JsonSerializable(typeof(EntrarRequest))]
[JsonSerializable(typeof(GrupoResponse))]
[JsonSerializable(typeof(GruposResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(MembrosResponse))]
[JsonSerializable(typeof(ProblemResponse))]
[JsonSerializable(typeof(ClassificacaoParticipanteResponse))]
[JsonSerializable(typeof(ClassificacaoResponse))]
[JsonSerializable(typeof(ClassificacaoResumoItemResponse))]
[JsonSerializable(typeof(DesafioDestaqueResponse))]
[JsonSerializable(typeof(HistoricoSemanaResponse))]
[JsonSerializable(typeof(HistoricoSemanalResponse))]
[JsonSerializable(typeof(CenaResponse))]
[JsonSerializable(typeof(CartaCatalogoResponse))]
[JsonSerializable(typeof(CartaMaoResponse))]
[JsonSerializable(typeof(ComprarCartaRequest))]
[JsonSerializable(typeof(JogadaCriadaResponse))]
[JsonSerializable(typeof(JogadaResponse))]
[JsonSerializable(typeof(JogadasResponse))]
[JsonSerializable(typeof(JogarCartaRequest))]
[JsonSerializable(typeof(LojaResponse))]
[JsonSerializable(typeof(MarcarJogadasVistasRequest))]
[JsonSerializable(typeof(ResponderJogadaRequest))]
[JsonSerializable(typeof(MembroCenaResponse))]
[JsonSerializable(typeof(ProgressoResumoResponse))]
[JsonSerializable(typeof(ProgressoResponse))]
[JsonSerializable(typeof(ResumoGrupoResponse))]
[JsonSerializable(typeof(ReacaoRequest))]
[JsonSerializable(typeof(ReacaoItemResponse))]
[JsonSerializable(typeof(ReacoesResponse))]
[JsonSerializable(typeof(RevisaoRegistradaResponse))]
[JsonSerializable(typeof(RevisaoRequest))]
[JsonSerializable(typeof(RegistrarRequest))]
[JsonSerializable(typeof(SessaoResponse))]
[JsonSerializable(typeof(TransferirGrupoRequest))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
