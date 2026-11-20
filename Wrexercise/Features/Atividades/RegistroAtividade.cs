using Npgsql;
using Wrexercise.Features.Atividades.Contracts;
using Wrexercise.Features.Jogo.Contracts;
using Wrexercise.Features.Notificacoes.Contracts;
using Wrexercise.Infrastructure.Database;

namespace Wrexercise.Features.Atividades;

/// <summary>
/// O que acontece quando um treino entra para a avaliação do grupo: ao registrar um treino já feito e ao
/// terminar um treino ao vivo (ADR 0078). Roda na transação de quem chama.
/// </summary>
internal static class RegistroAtividade
{
    public static async Task FinalizarAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid atividadeId,
        Guid grupoId,
        Guid usuarioId,
        DateTimeOffset startedAt,
        int duracaoSegundos,
        DateTimeOffset submittedAt,
        bool temEvidencia,
        CancellationToken cancellationToken)
    {
        if (AvisosEvidencia.IsRegistroTardio(startedAt, duracaoSegundos, submittedAt))
        {
            await ConsultasAtividade.InserirAvisoAsync(
                connection, transaction, atividadeId, TipoAviso.RegistroTardio, null, null, cancellationToken);
        }

        // Quem dorme no grupo não revisa (decisão 0062). Sem ninguém acordado, a atividade não tem revisores
        // e é verificada logo abaixo ou ao receber a evidência.
        await using (var revisores = new NpgsqlCommand($"""
            INSERT INTO revisores_atividade (atividade_id, usuario_id)
            SELECT $1, m.usuario_id
            FROM membros_grupo m
            WHERE m.grupo_id = $2 AND m.removed_at IS NULL AND m.usuario_id <> $3
              AND {MembrosDormindo.AcordadoSql("m")}
            """, connection, transaction))
        {
            revisores.Parameters.AddWithValue(atividadeId);
            revisores.Parameters.AddWithValue(grupoId);
            revisores.Parameters.AddWithValue(usuarioId);
            await revisores.ExecuteNonQueryAsync(cancellationToken);
        }

        // Com uma evidência já anexada, vale o mesmo que a primeira foto em AnexarEvidencia.
        await using (var verificar = new NpgsqlCommand("""
            UPDATE atividades
            SET situacao_verificacao = 'Verificada', verified_at = now()
            WHERE id = $1
              AND (tipo_comprovacao = 'RegistroManual' OR $2)
              AND NOT EXISTS (SELECT 1 FROM revisores_atividade r WHERE r.atividade_id = $1)
            """, connection, transaction))
        {
            verificar.Parameters.AddWithValue(atividadeId);
            verificar.Parameters.AddWithValue(temEvidencia);
            if (await verificar.ExecuteNonQueryAsync(cancellationToken) > 0)
            {
                await JogadasConcluidas.NotificarAsync(connection, transaction, atividadeId, cancellationToken);
            }
        }

        await NotificacoesAtividade.NotificarNovaAtividadeAsync(connection, transaction, atividadeId, cancellationToken);
    }

    /// <summary>A pessoa tem um treino ao vivo aberto, em qualquer grupo.</summary>
    public static async Task<bool> ExisteEmAndamentoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM atividades WHERE usuario_id = $1 AND situacao_verificacao = 'EmAndamento')
            """, connection, transaction);
        command.Parameters.AddWithValue(usuarioId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
