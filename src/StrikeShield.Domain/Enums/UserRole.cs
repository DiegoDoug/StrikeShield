namespace StrikeShield.Domain.Enums;

/// <summary>
/// Full per-organization RBAC enforcement lands in Phase 10 — Phase 1 only
/// seeds a single Owner/admin user, but the role is stored from day one so
/// the schema doesn't need an RBAC migration bolted on later.
/// </summary>
public enum UserRole
{
    Owner = 0,
    Admin = 1,
    Analyst = 2,
    Viewer = 3
}
