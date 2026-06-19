namespace MarketWeb.Application.UsuariosPc;

/// <summary>Resultado de resolver el acceso web de un mail.</summary>
/// <param name="Estado">onboarding (sin PC) | pendiente (sin aprobar) | ok (aprobado).</param>
/// <param name="Perfil">Perfil efectivo (solo cuando Estado = ok).</param>
/// <param name="Pc">PC asociada, si hay.</param>
public sealed record AccesoResultado(string Estado, string? Perfil, string? Pc);
