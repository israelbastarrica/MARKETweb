using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace MarketWeb.Application.Common;

/// <summary>Envío de mails por SMTP. Configuración en user-secrets/env (sección Smtp).</summary>
public interface ISmtpSender
{
    bool Configurado { get; }
    Task<bool> EnviarAsync(string destinatarios, string asunto, string htmlBody, CancellationToken ct = default);
    /// <summary>Variante con un adjunto (ej. el PDF de reposición). adjunto=null → sin adjunto.</summary>
    Task<bool> EnviarAsync(string destinatarios, string asunto, string htmlBody,
        byte[]? adjunto, string? nombreAdjunto, CancellationToken ct = default);
}

public sealed class SmtpSender : ISmtpSender
{
    private readonly IConfiguration _cfg;
    public SmtpSender(IConfiguration cfg) => _cfg = cfg;

    private string Host => _cfg["Smtp:Host"] ?? "";
    public bool Configurado => !string.IsNullOrWhiteSpace(Host);

    public Task<bool> EnviarAsync(string destinatarios, string asunto, string htmlBody, CancellationToken ct = default)
        => EnviarAsync(destinatarios, asunto, htmlBody, null, null, ct);

    public async Task<bool> EnviarAsync(string destinatarios, string asunto, string htmlBody,
        byte[]? adjunto, string? nombreAdjunto, CancellationToken ct = default)
    {
        if (!Configurado) return false;

        var port = int.TryParse(_cfg["Smtp:Port"], out var p) ? p : 587;
        var user = _cfg["Smtp:User"] ?? "";
        var pass = _cfg["Smtp:Pass"] ?? "";
        var from = string.IsNullOrWhiteSpace(_cfg["Smtp:From"]) ? user : _cfg["Smtp:From"]!;
        var fromName = _cfg["Smtp:FromName"] ?? "MARKET";

        using var msg = new MailMessage
        {
            From = new MailAddress(from, fromName), Subject = asunto, Body = htmlBody, IsBodyHtml = true,
            BodyEncoding = System.Text.Encoding.UTF8, SubjectEncoding = System.Text.Encoding.UTF8
        };
        foreach (var d in destinatarios.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            msg.To.Add(d);
        if (msg.To.Count == 0) return false;

        MemoryStream? adj = null;
        if (adjunto is { Length: > 0 })
        {
            adj = new MemoryStream(adjunto);
            msg.Attachments.Add(new Attachment(adj, nombreAdjunto ?? "adjunto.pdf", "application/pdf"));
        }

        try
        {
            // Reintentos ante fallos transitorios del SMTP (timeout/rechazo momentáneo). En el último intento, la excepción se propaga (queda en el log de la tarea).
            const int intentos = 3;
            for (int i = 1; ; i++)
            {
                try
                {
                    using var smtp = new SmtpClient(Host, port)
                    {
                        EnableSsl = true,
                        Credentials = new NetworkCredential(user, pass),
                        Timeout = 30000
                    };
                    await smtp.SendMailAsync(msg, ct);
                    return true;
                }
                catch when (i < intentos && !ct.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                }
            }
        }
        finally
        {
            adj?.Dispose();
        }
    }
}
