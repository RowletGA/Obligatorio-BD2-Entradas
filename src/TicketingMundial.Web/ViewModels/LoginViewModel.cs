using System.ComponentModel.DataAnnotations;

namespace TicketingMundial.Web.ViewModels;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Ingrese el correo electronico.")]
    [Display(Name = "Correo electronico")]
    [StringLength(254)]
    public string CorreoElectronico { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese la contraseña.")]
    [DataType(DataType.Password)]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre 8 y 128 caracteres.")]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Recordarme")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class SeleccionarPerfilViewModel
{
    public IReadOnlyList<PerfilDisponibleViewModel> Perfiles { get; init; } = [];
    public string? PerfilActivo { get; init; }
}

public sealed class PerfilDisponibleViewModel
{
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
}
