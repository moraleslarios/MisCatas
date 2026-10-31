using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using MisCatas.Application.Comun;
using MisCatas.Application.Usuarios;
using MisCatas.Domain.Enums;
using MisCatas.Web.Seguridad;

namespace MisCatas.Web.Components.Pages.Admin;

public partial class UsuarioEditar
{
    [Inject]
    private IServicioUsuarios Servicio { get; set; } = default!;

    [Inject]
    private NavigationManager Navegacion { get; set; } = default!;

    /// <summary>Identificador del usuario a editar; nulo al crear.</summary>
    [Parameter]
    public int? Id { get; set; }

    private FormularioUsuario Datos { get; set; } = new();

    private bool EsNuevo => Id is null;

    private bool NoEncontrado { get; set; }

    private bool Guardando { get; set; }

    private bool LoginEditadoAMano { get; set; }

    /// <summary>
    /// Para el rol Usuario el login es siempre el teléfono: ni se propone
    /// ni se puede editar a mano.
    /// </summary>
    private bool LoginEsTelefono => Datos.Rol == Rol.Usuario;

    private string? Error { get; set; }

    /// <summary>Texto del aviso de «guardado» que se muestra en la ventana.</summary>
    private string MensajeExito { get; set; } = string.Empty;

    /// <summary>La ventana de «guardado» está a la vista.</summary>
    private bool MostrarExito { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        NoEncontrado = false;
        Error = null;
        MostrarExito = false;

        if (Id is null)
        {
            Datos = new FormularioUsuario();
            LoginEditadoAMano = false;
            return;
        }

        var usuario = await Servicio.ObtenerAsync(Id.Value);

        if (usuario is null)
        {
            NoEncontrado = true;
            return;
        }

        Datos = new FormularioUsuario
        {
            Login = usuario.Login,
            Nombre = usuario.Nombre,
            Apellidos = usuario.Apellidos,
            Telefono = usuario.Telefono,
            Rol = usuario.Rol,
            Activo = usuario.Activo
        };

        LoginEditadoAMano = true;
    }

    /// <summary>Propone un login a partir del nombre mientras no se toque a mano.</summary>
    private async Task SugerirLoginAsync()
    {
        // Para los participantes el login lo manda el teléfono, no el nombre.
        if (LoginEsTelefono)
        {
            SincronizarLoginConTelefono();
            return;
        }

        if (!EsNuevo || LoginEditadoAMano)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Datos.Nombre) && string.IsNullOrWhiteSpace(Datos.Apellidos))
        {
            Datos.Login = string.Empty;
            return;
        }

        Datos.Login = await Servicio.SugerirLoginAsync(Datos.Nombre, Datos.Apellidos);
    }

    /// <summary>
    /// Copia el teléfono al login cuando el rol es Usuario. Se dispara al
    /// escribir el teléfono y al cambiar de rol.
    /// </summary>
    private void SincronizarLoginConTelefono()
    {
        if (LoginEsTelefono)
        {
            Datos.Login = Texto.NormalizarTelefono(Datos.Telefono);
        }
    }

    private void MarcarLoginManual()
    {
        if (!LoginEsTelefono)
        {
            LoginEditadoAMano = true;
        }
    }

    private async Task GuardarAsync()
    {
        Error = null;
        MostrarExito = false;
        Guardando = true;

        try
        {
            if (EsNuevo)
            {
                var creado = await Servicio.CrearAsync(new CrearUsuarioDto
                {
                    Login = Datos.Login,
                    Nombre = Datos.Nombre,
                    Apellidos = Datos.Apellidos,
                    Telefono = Datos.Telefono,
                    Rol = Datos.Rol,
                    Activo = Datos.Activo
                });

                if (creado.EsFallo)
                {
                    Error = creado.Error;
                    return;
                }
            }
            else
            {
                var editado = await Servicio.EditarAsync(new EditarUsuarioDto
                {
                    Id = Id!.Value,
                    Login = Datos.Login,
                    Nombre = Datos.Nombre,
                    Apellidos = Datos.Apellidos,
                    Telefono = Datos.Telefono,
                    Rol = Datos.Rol,
                    Activo = Datos.Activo
                });

                if (editado.EsFallo)
                {
                    Error = editado.Error;
                    return;
                }
            }

            MensajeExito = EsNuevo
                ? $"{Datos.Nombre} {Datos.Apellidos} ya puede entrar con su teléfono."
                : $"Los cambios de {Datos.Nombre} {Datos.Apellidos} se han guardado.";

            MostrarExito = true;
        }
        finally
        {
            Guardando = false;
        }
    }

    /// <summary>Al cerrarse el aviso se vuelve al listado de usuarios.</summary>
    private void SalirTrasGuardar()
    {
        MostrarExito = false;
        Navegacion.NavigateTo(RutasApp.AdminUsuarios);
    }

    /// <summary>Datos del formulario con las validaciones de la interfaz.</summary>
    private sealed class FormularioUsuario
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(60, ErrorMessage = "El nombre no puede pasar de 60 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Los apellidos son obligatorios.")]
        [StringLength(80, ErrorMessage = "Los apellidos no pueden pasar de 80 caracteres.")]
        public string Apellidos { get; set; } = string.Empty;

        [Required(ErrorMessage = "El login es obligatorio.")]
        [StringLength(50, ErrorMessage = "El login no puede pasar de 50 caracteres.")]
        public string Login { get; set; } = string.Empty;

        [Required(ErrorMessage = "El teléfono es obligatorio: es la contraseña inicial.")]
        [StringLength(25, MinimumLength = 6,
            ErrorMessage = "El teléfono debe tener entre 6 y 25 caracteres.")]
        public string Telefono { get; set; } = string.Empty;

        public Rol Rol { get; set; } = Rol.Usuario;

        public bool Activo { get; set; } = true;
    }
}
