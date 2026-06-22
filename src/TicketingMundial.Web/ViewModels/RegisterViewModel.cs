using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using TicketingMundial.Application.Options;

namespace TicketingMundial.Web.ViewModels;

public sealed class RegisterViewModel
{
    [Required]
    [StringLength(20)]
    [Display(Name = "Tipo de documento")]
    public string TipoDocumento { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    [Display(Name = "País del documento")]
    public string PaisDocumento { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    [Display(Name = "Número de documento")]
    public string NumeroDocumento { get; set; } = string.Empty;

    [Required]
    [StringLength(60)]
    [Display(Name = "Primer nombre")]
    public string PrimerNombre { get; set; } = string.Empty;

    [Required]
    [StringLength(60)]
    [Display(Name = "Primer apellido")]
    public string PrimerApellido { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(254)]
    [Display(Name = "Correo electrónico")]
    public string CorreoElectronico { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "País de dirección")]
    public string? DireccionPais { get; set; }

    [StringLength(100)]
    [Display(Name = "Localidad")]
    public string? DireccionLocalidad { get; set; }

    [StringLength(100)]
    [Display(Name = "Calle")]
    public string? DireccionCalle { get; set; }

    [StringLength(20)]
    [Display(Name = "Número de puerta")]
    public string? DireccionNumero { get; set; }

    [StringLength(15)]
    [Display(Name = "Código postal")]
    public string? DireccionCodigoPostal { get; set; }

    [Display(Name = "Teléfonos")]
    public List<string> Telefonos { get; set; } = [string.Empty];

    public IReadOnlyList<SelectListItem> PaisesDocumento { get; set; } = [];
    public IReadOnlyList<SelectListItem> PaisesDireccion { get; set; } = [];
    public IReadOnlyList<SelectListItem> TiposDocumento { get; set; } = [];
    public IReadOnlyList<TipoDocumentoOption> TiposDocumentoCatalogo { get; set; } = [];

    [Required]
    [DataType(DataType.Password)]
    [StringLength(128, MinimumLength = 8)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "La confirmación de contraseña no coincide.")]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
