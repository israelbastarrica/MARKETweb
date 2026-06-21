using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace MarketWeb.Application.Common;

/// <summary>Envío de mails por SMTP. Configuración en user-secrets/env (sección Smtp).</summary>
public interface ISmtpSender
{
    bool Configurado { get; }
    Task<bool> EnviarAsync(string destinatarios, string asunto, string htmlBody, CancellationToken ct = default);
}

public sealed class SmtpSender : ISmtpSender
{
    private readonly IConfiguration _cfg;
    public SmtpSender(IConfiguration cfg) => _cfg = cfg;

    private string Host => _cfg["Smtp:Host"] ?? "";
    public bool Configurado => !string.IsNullOrWhiteSpace(Host);

    public async Task<bool> EnviarAsync(string destinatarios, string asunto, string htmlBody, CancellationToken ct = default)
    {
        if (!Configurado) return false;

        var port = int.TryParse(_cfg["Smtp:Port"], out var p) ? p : 587;
        var user = _cfg["Smtp:User"] ?? "";
        var pass = _cfg["Smtp:Pass"] ?? "";
        var from = string.IsNullOrWhiteSpace(_cfg["Smtp:From"]) ? user : _cfg["Smtp:From"]!;
        var fromName = _cfg["Smtp:FromName"] ?? "MARKET";

        using var msg = new MailMessage { From = new MailAddress(from, fromName), Subject = asunto, Body = htmlBody, IsBodyHtml = true };
        foreach (var d in destinatarios.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            msg.To.Add(d);
        if (msg.To.Count == 0) return false;

        using var smtp = new SmtpClient(Host, port) { EnableSsl = true, Credentials = new NetworkCredential(user, pass) };
        await smtp.SendMailAsync(msg, ct);
        return true;
    }
}
