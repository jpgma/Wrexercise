using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Npgsql;
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
using Wrexercise.Infrastructure.Authentication;
using Wrexercise.Infrastructure.Database;
using Wrexercise.Infrastructure.Errors;
using Wrexercise.Infrastructure.Files;
using Wrexercise.Infrastructure.Json;
using Wrexercise.Infrastructure.StaticFiles;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Logging.ClearProviders();
// Mantém os acentos legíveis nos logs, que vão só para stdout.
builder.Logging.AddJsonConsole(options =>
    options.JsonWriterOptions = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

// A API só recebe JSON pequeno. O upload de evidências abrirá sua própria exceção.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1024 * 1024);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));
builder.Services.AddSingleton<NpgsqlDataSource>(DataSourceFactory.Create);
builder.Services.Configure<EvidenciasOptions>(builder.Configuration.GetSection(EvidenciasOptions.Section));
builder.Services.AddSingleton<ArmazenamentoEvidencias>();
builder.Services.Configure<AvataresOptions>(builder.Configuration.GetSection(AvataresOptions.Section));
builder.Services.AddSingleton<ArmazenamentoAvatares>();

// JSON malformado responde 400 em qualquer ambiente, em vez de virar exceção em Development.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

// Só o Caddy ou o túnel local alcançam a aplicação (ver deploy/), então o cabeçalho que definem é confiável.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddAutenticacaoRateLimiter();
builder.Services.AddAtividadesRateLimiter();
builder.Services.AddReacoesRateLimiter();
builder.Services.AddJogoRateLimiter();
builder.Services.AddHostedService<EncerramentoRevisoesBackgroundService>();

var app = builder.Build();

if (args is ["migrate"])
{
    return await MigrationCommand.RunAsync(app.Services);
}

if (args is ["reset-senha", var email])
{
    return await ResetSenhaCommand.RunAsync(app.Services, email);
}

app.UseForwardedHeaders();
app.UseExceptionHandler(handler => handler.Run(ApiProblem.WriteInternalErrorAsync));
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = CacheControleEstatico.Aplicar,
});
app.UseOriginCheck();
app.UseRateLimiter();

app.MapHealthRoutes();
app.MapAutenticacaoRoutes();
app.MapGruposRoutes();
app.MapConvitesRoutes();
app.MapDesafiosRoutes();
app.MapProgressoRoutes();
app.MapAtividadesRoutes();
app.MapReacoesRoutes();
app.MapUsuariosRoutes();
app.MapNotificacoesRoutes();
app.MapJogoRoutes();

// Rota de API inexistente responde 404 em JSON, nunca com o index.html da SPA.
app.Map("/api/{**rest}", () =>
    ApiProblem.Create(StatusCodes.Status404NotFound, "ROTA_NAO_ENCONTRADA", "Rota não encontrada."));
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;

// Exposto para o WebApplicationFactory dos testes de integração.
public partial class Program;
