using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MisCatas.Application.Imagenes;
using MisCatas.Application.Vinos;
using MisCatas.Web.Seguridad;

namespace MisCatas.Web.Components.Pages.Admin;

public partial class VinoEditar
{
    [Inject]
    private IServicioVinos Servicio { get; set; } = default!;

    [Inject]
    private IServicioImagenes Imagenes { get; set; } = default!;

    [Inject]
    private NavigationManager Navegacion { get; set; } = default!;

    /// <summary>Identificador del vino a editar; nulo al crear.</summary>
    [Parameter]
    public int? Id { get; set; }

    private FormularioVino Datos { get; set; } = new();

    private bool EsNuevo => Id is null;

    private bool NoEncontrado { get; set; }

    private bool Guardando { get; set; }

    private bool Subiendo { get; set; }

    /// <summary>Hueco que se está procesando ahora mismo, para el indicador visual.</summary>
    private TipoImagenVino? SubiendoTipo { get; set; }

    /// <summary>Nombre del archivo en curso: se muestra junto al indicador.</summary>
    private string? ArchivoEnCurso { get; set; }

    private string? Error { get; set; }

    private string? Mensaje { get; set; }

    /// <summary>Texto del aviso de «guardado» que se muestra en la ventana.</summary>
    private string MensajeExito { get; set; } = string.Empty;

    /// <summary>La ventana de «guardado» está a la vista.</summary>
    private bool MostrarExito { get; set; }

    private bool TieneFoto { get; set; }

    private bool TieneEtiqueta { get; set; }

    /// <summary>Marca de tiempo para forzar que el navegador recargue la imagen.</summary>
    private long Version { get; set; } = DateTime.UtcNow.Ticks;

    private IEnumerable<HuecoImagen> Huecos =>
    [
        new(TipoImagenVino.Foto, "Foto de la botella", TieneFoto,
            $"/imagenes/vinos/{Id}/foto?v={Version}"),
        new(TipoImagenVino.Etiqueta, "Etiqueta", TieneEtiqueta,
            $"/imagenes/vinos/{Id}/etiqueta?v={Version}")
    ];

    protected override async Task OnParametersSetAsync()
    {
        NoEncontrado = false;
        Error = null;
        Mensaje = null;
        MostrarExito = false;

        if (Id is null)
        {
            Datos = new FormularioVino();
            TieneFoto = false;
            TieneEtiqueta = false;
            return;
        }

        await CargarAsync();
    }

    private async Task CargarAsync()
    {
        var vino = await Servicio.ObtenerAsync(Id!.Value);

        if (vino is null)
        {
            NoEncontrado = true;
            return;
        }

        Datos = new FormularioVino
        {
            Nombre = vino.Nombre,
            Anio = vino.Anio,
            Clase = vino.Clase,
            Dueno = vino.Dueno,
            PrecioEstimado = vino.PrecioEstimado,
            Bodega = vino.Bodega,
            DenominacionOrigen = vino.DenominacionOrigen,
            Comentarios = vino.Comentarios
        };

        TieneFoto = vino.TieneFoto;
        TieneEtiqueta = vino.TieneEtiqueta;
    }

    private async Task GuardarAsync()
    {
        Error = null;
        Mensaje = null;
        MostrarExito = false;
        Guardando = true;

        try
        {
            var dto = new GuardarVinoDto
            {
                Id = Id ?? 0,
                Nombre = Datos.Nombre,
                Anio = Datos.Anio,
                Clase = Datos.Clase,
                Dueno = Datos.Dueno,
                PrecioEstimado = Datos.PrecioEstimado,
                Bodega = Datos.Bodega,
                DenominacionOrigen = Datos.DenominacionOrigen,
                Comentarios = Datos.Comentarios
            };

            if (EsNuevo)
            {
                var creado = await Servicio.CrearAsync(dto);

                if (creado.EsFallo)
                {
                    Error = creado.Error;
                    return;
                }

                // Tras darlo de alta se vuelve al listado; las imágenes se suben
                // después entrando a editar el vino.
                MensajeExito = $"«{Datos.Nombre}» se ha dado de alta. Entra a editarlo para ponerle foto y etiqueta.";
                MostrarExito = true;
                return;
            }

            var editado = await Servicio.EditarAsync(dto);

            if (editado.EsFallo)
            {
                Error = editado.Error;
                return;
            }

            MensajeExito = $"Los cambios de «{Datos.Nombre}» se han guardado.";
            MostrarExito = true;
        }
        finally
        {
            Guardando = false;
        }
    }

    /// <summary>Al cerrarse el aviso se vuelve al listado de vinos.</summary>
    private void SalirTrasGuardar()
    {
        MostrarExito = false;
        Navegacion.NavigateTo(RutasApp.AdminVinos);
    }

    private async Task SubirAsync(TipoImagenVino tipo, InputFileChangeEventArgs e)
    {
        Error = null;
        Mensaje = null;

        var archivo = e.File;

        if (archivo is null)
        {
            return;
        }

        if (!Imagenes.EsExtensionPermitida(archivo.Name))
        {
            Error = "Ese tipo de archivo no está permitido. Usa JPG, PNG, WEBP, BMP o GIF.";
            return;
        }

        if (archivo.Size > IServicioImagenes.TamanoMaximoBytes)
        {
            Error = $"La imagen pasa de {IServicioImagenes.TamanoMaximoBytes / (1024 * 1024)} MB.";
            return;
        }

        Subiendo = true;
        SubiendoTipo = tipo;
        ArchivoEnCurso = archivo.Name;

        // Se fuerza un repintado y se cede el hilo para que el indicador llegue
        // al navegador ANTES de empezar la transferencia, que tarda bastante.
        StateHasChanged();
        await Task.Yield();

        try
        {
            await using var origen = archivo.OpenReadStream(IServicioImagenes.TamanoMaximoBytes);

            // Se copia a memoria porque el servicio necesita un stream con posición.
            using var memoria = new MemoryStream();
            await origen.CopyToAsync(memoria);
            memoria.Position = 0;

            var resultado = await Servicio.GuardarImagenAsync(Id!.Value, tipo, memoria, archivo.Name);

            if (resultado.EsFallo)
            {
                Error = resultado.Error;
                return;
            }

            Version = DateTime.UtcNow.Ticks;
            Mensaje = "Imagen guardada.";

            await CargarAsync();
        }
        finally
        {
            Subiendo = false;
            SubiendoTipo = null;
            ArchivoEnCurso = null;
        }
    }

    private async Task BorrarImagenAsync(TipoImagenVino tipo)
    {
        Error = null;
        Mensaje = null;

        var resultado = await Servicio.BorrarImagenAsync(Id!.Value, tipo);

        if (resultado.EsFallo)
        {
            Error = resultado.Error;
            return;
        }

        Version = DateTime.UtcNow.Ticks;
        Mensaje = "Imagen quitada.";

        await CargarAsync();
    }

    /// <summary>Describe uno de los dos huecos de imagen de la ficha.</summary>
    private sealed record HuecoImagen(TipoImagenVino Tipo, string Titulo, bool Existe, string Url);

    /// <summary>Datos del formulario con las validaciones de la interfaz.</summary>
    private sealed class FormularioVino
    {
        [Required(ErrorMessage = "El nombre del vino es obligatorio.")]
        [StringLength(120, ErrorMessage = "El nombre no puede pasar de 120 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Range(1900, 2100, ErrorMessage = "La añada debe estar entre 1900 y 2100.")]
        public int? Anio { get; set; }

        [StringLength(60, ErrorMessage = "La clase no puede pasar de 60 caracteres.")]
        public string? Clase { get; set; }

        [Required(ErrorMessage = "El dueño es obligatorio.")]
        [StringLength(100, ErrorMessage = "El dueño no puede pasar de 100 caracteres.")]
        public string Dueno { get; set; } = string.Empty;

        [Range(0, 100000, ErrorMessage = "El precio debe ser un número positivo.")]
        public decimal? PrecioEstimado { get; set; }

        [StringLength(120, ErrorMessage = "La bodega no puede pasar de 120 caracteres.")]
        public string? Bodega { get; set; }

        [StringLength(150, ErrorMessage = "La denominación de origen no puede pasar de 150 caracteres.")]
        public string? DenominacionOrigen { get; set; }

        public string? Comentarios { get; set; }
    }
}
