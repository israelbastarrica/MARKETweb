namespace MarketWeb.Shared.UsuariosPc;

/// <summary>
/// Liga PC ↔ Mail (@marketarg.com) ↔ Perfil (tabla UsuariosPC).
///  - Perfil: qué módulos ve (lo usa frmPrincipal del desktop por PC; en web por Mail).
///  - PC: dispositivo físico, para procesos que filtran por máquina.
///  - Mail: identidad de la persona para el login web (Entra ID / Microsoft 365).
/// </summary>
public sealed class UsuarioPcDto
{
    public int Id { get; set; }
    public string Pc { get; set; } = "";
    public string Perfil { get; set; } = "";
    public string? Mail { get; set; }

    /// <summary>El match mail↔PC fue aprobado por un administrador.</summary>
    public bool MailAprobado { get; set; }
}

