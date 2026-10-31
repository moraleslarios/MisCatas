#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Instala MisCatas en el IIS de este equipo: https://miscatas.es desde el
    propio ordenador, y https://<ip-del-equipo>:5080 desde los moviles.

.DESCRIPTION
    Hace de una vez todo lo que normalmente se haría a mano. Se puede volver a
    ejecutar tantas veces como se quiera: cada paso comprueba antes si ya está
    hecho, así que repetirlo no rompe nada (sirve también para actualizar la web
    después de hacer cambios).

    La idea es que no haya que arrancar nada nunca: IIS tiene la web puesta y
    esperando, se enciende sola con el ordenador y responde por dos sitios, los
    dos cifrados:
      - Desde este equipo, por el nombre bonito: https://miscatas.es
      - Desde los móviles y el televisor, por la IP y el puerto 5080, que es
        la dirección que llevan los QR y los enlaces de WhatsApp. Ese nombre
        bonito solo existe aquí dentro, así que para ellos no vale.

    Pasos:
      1. Comprueba que IIS está instalado y, si no, lo instala.
      2. Comprueba el complemento de IIS para .NET (el "Hosting Bundle") y lo
         instala si falta. Sin él IIS no sabe arrancar la aplicación.
      3. Apunta el nombre miscatas.es a este mismo equipo.
      4. Fabrica el certificado de seguridad, que ampara tanto el nombre
         miscatas.es como la IP del equipo, y lo marca como de confianza.
      5. Escribe en web.config la IP de este equipo (la que va en los QR) y
         publica la aplicación en C:\inetpub\MisCatas.
      6. Crea el sitio web y su grupo de aplicaciones en IIS, le pone las
         direcciones por las que responde (incluido el puerto 5080 para los
         móviles) y abre ese puerto en el cortafuegos.
      7. Da permisos a la web sobre su carpeta y sobre la base de datos.
      8. Arranca el sitio y comprueba que responde por las dos direcciones.

.NOTES
    Hay que ejecutarlo como ADMINISTRADOR. La forma fácil es botón derecho
    sobre Desplegar-IIS.cmd -> "Ejecutar como administrador".

.EXAMPLE
    .\desplegar-iis.ps1
    Despliegue completo.

.EXAMPLE
    .\desplegar-iis.ps1 -SinPublicar
    Solo revisa y rehace la configuración de IIS, sin volver a compilar.
#>

[CmdletBinding()]
param(
    # Nombre con el que se verá la web desde este mismo ordenador.
    [string]$Dominio = 'miscatas.es',

    # Carpeta donde queda la web publicada.
    [string]$Carpeta = 'C:\inetpub\MisCatas',

    # Nombre del sitio y del grupo de aplicaciones dentro de IIS.
    [string]$Sitio = 'MisCatas',

    # No vuelve a compilar ni publicar: reutiliza lo que ya hay en $Carpeta.
    [switch]$SinPublicar,

    # No abre el navegador al terminar.
    [switch]$SinNavegador
)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$proyecto = Join-Path $raiz 'src\MisCatas.Web'
$rutaAjustes = Join-Path $proyecto 'appsettings.json'
$cssMinimoBytes = 14000
$totalPasos = 8

# Puerto por el que la web responde a los moviles y al televisor. Va sin cifrar
# y por la IP del equipo, porque el nombre miscatas.es solo existe aqui dentro.
$puertoRed = 5080

function Escribir-Paso {
    param([int]$Numero, [string]$Texto)
    Write-Host ''
    Write-Host "[$Numero/$totalPasos] $Texto" -ForegroundColor Cyan
}

function Escribir-Ok {
    param([string]$Texto)
    Write-Host "      OK  $Texto" -ForegroundColor Green
}

function Escribir-Info {
    param([string]$Texto)
    Write-Host "      --  $Texto" -ForegroundColor DarkGray
}

function Escribir-Aviso {
    param([string]$Texto)
    Write-Host "      !!  $Texto" -ForegroundColor Yellow
}

# IP de este equipo en la red de casa. Es la unica direccion que entienden el
# movil y el televisor, asi que es la que se pone en los QR y en los enlaces de
# WhatsApp. Se prefiere el cable sobre el wifi, y se descartan la direccion
# interna (127.x) y las que Windows se inventa cuando no hay red (169.254.x).
function Obtener-IpDeLaRed {
    $direcciones = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -notlike '127.*' -and
            $_.IPAddress -notlike '169.254.*' -and
            $_.AddressState -ne 'Invalid'
        })

    foreach ($familia in @('Ethernet', 'Wi-Fi', '')) {
        foreach ($direccion in $direcciones) {
            $tarjeta = Get-NetAdapter -InterfaceIndex $direccion.InterfaceIndex -ErrorAction SilentlyContinue
            if (-not $tarjeta) { continue }
            if ($familia -eq '' -or $tarjeta.InterfaceDescription -like "*$familia*" -or $tarjeta.Name -like "*$familia*") {
                return $direccion.IPAddress
            }
        }
    }

    return ($direcciones | Select-Object -First 1 -ExpandProperty IPAddress)
}

$ipRed = Obtener-IpDeLaRed
$direccionRed = if ($ipRed) { "https://${ipRed}:$puertoRed" } else { $null }

Write-Host ''
Write-Host '=============================================================' -ForegroundColor Magenta
Write-Host '  MisCatas - instalacion en el IIS de este equipo' -ForegroundColor Magenta
Write-Host "  Este equipo        : https://$Dominio" -ForegroundColor Magenta
if ($direccionRed) {
    Write-Host "  Moviles/televisor  : $direccionRed" -ForegroundColor Magenta
}
Write-Host '=============================================================' -ForegroundColor Magenta


# --- 1. IIS -----------------------------------------------------------------
# Se piden también los WebSockets: la cata en vivo (los votos que aparecen al
# instante en el escaparate) viaja por ahí. Sin esa pieza la pantalla se queda
# congelada aunque la web cargue bien.
Escribir-Paso 1 'Revisando IIS...'

$piezasIis = @(
    'IIS-WebServerRole',
    'IIS-WebServer',
    'IIS-CommonHttpFeatures',
    'IIS-StaticContent',
    'IIS-DefaultDocument',
    'IIS-HttpErrors',
    'IIS-HttpRedirect',
    'IIS-HttpCompressionStatic',
    'IIS-HttpCompressionDynamic',
    'IIS-RequestFiltering',
    'IIS-Security',
    'IIS-WebSockets',
    'IIS-ApplicationDevelopment',
    'IIS-NetFxExtensibility45',
    'IIS-ISAPIExtensions',
    'IIS-ISAPIFilter',
    'IIS-WebServerManagementTools',
    'IIS-ManagementConsole'
)

$faltan = @()
foreach ($pieza in $piezasIis) {
    $estado = Get-WindowsOptionalFeature -Online -FeatureName $pieza -ErrorAction SilentlyContinue
    if ($null -eq $estado) {
        Escribir-Info "La pieza '$pieza' no existe en esta version de Windows: se omite."
        continue
    }
    if ($estado.State -ne 'Enabled') {
        $faltan += $pieza
    }
}

if ($faltan.Count -eq 0) {
    Escribir-Ok 'IIS ya estaba instalado con todo lo necesario.'
}
else {
    Escribir-Info "Instalando $($faltan.Count) pieza(s) de IIS. Esto tarda un par de minutos..."
    $resultado = Enable-WindowsOptionalFeature -Online -FeatureName $faltan -All -NoRestart
    Escribir-Ok 'IIS instalado.'
    if ($resultado.RestartNeeded) {
        Escribir-Aviso 'Windows pide reiniciar. Si algo falla mas abajo, reinicia y vuelve a ejecutar este script.'
    }
}

if (-not (Get-Service -Name 'W3SVC' -ErrorAction SilentlyContinue)) {
    throw 'IIS sigue sin aparecer despues de instalarlo. Reinicia el equipo y vuelve a ejecutar este script.'
}

Import-Module WebAdministration -ErrorAction Stop
Escribir-Ok 'Herramientas de IIS disponibles.'


# --- 2. Complemento de IIS para .NET (Hosting Bundle) -----------------------
# IIS por si solo no sabe ejecutar una aplicacion .NET: necesita este modulo.
# Se instala DESPUES de IIS a proposito; si se hace antes, no se registra.
Escribir-Paso 2 'Revisando el complemento de IIS para .NET...'

$rutaModulo = Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll'

if (Test-Path $rutaModulo) {
    Escribir-Ok 'El complemento ya estaba instalado.'
}
else {
    $descarga = Join-Path $env:TEMP 'dotnet-hosting-win.exe'
    $url = 'https://aka.ms/dotnet/10.0/dotnet-hosting-win.exe'

    Escribir-Info 'Descargando el complemento (unos 100 MB)...'
    try {
        # TLS 1.2 explicito: PowerShell 5.1 negocia TLS 1.0 por defecto y la
        # descarga falla con un error de conexion que no dice nada.
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $url -OutFile $descarga -UseBasicParsing
    }
    catch {
        throw @"
No se ha podido descargar el complemento de IIS para .NET.

Descargalo a mano desde:
  https://dotnet.microsoft.com/download/dotnet/10.0
  (seccion Windows -> "Hosting Bundle")

Instalalo y vuelve a ejecutar este script.

Detalle del error: $($_.Exception.Message)
"@
    }

    if (-not (Test-Path $descarga) -or (Get-Item $descarga).Length -lt 1MB) {
        throw "La descarga de '$descarga' esta incompleta. Instala el Hosting Bundle a mano desde https://dotnet.microsoft.com/download/dotnet/10.0"
    }

    Escribir-Info 'Instalando el complemento (silencioso, puede tardar)...'
    $instalacion = Start-Process -FilePath $descarga -ArgumentList '/quiet', '/norestart' -Wait -PassThru
    Remove-Item $descarga -Force -ErrorAction SilentlyContinue

    # 0 = correcto, 3010 = correcto pero pide reiniciar, 1638 = ya habia otra version.
    if ($instalacion.ExitCode -notin @(0, 3010, 1638)) {
        throw "El instalador del complemento ha terminado con el codigo $($instalacion.ExitCode). Instalalo a mano desde https://dotnet.microsoft.com/download/dotnet/10.0"
    }

    Escribir-Info 'Reiniciando IIS para que reconozca el complemento...'
    & iisreset.exe /restart | Out-Null

    if (-not (Test-Path $rutaModulo)) {
        throw 'El complemento se ha instalado pero no aparece. Reinicia el equipo y vuelve a ejecutar este script.'
    }
    Escribir-Ok 'Complemento instalado.'
}


# --- 3. El nombre miscatas.es apunta a este equipo --------------------------
# No existe ningun servidor de nombres que sepa que miscatas.es es este PC, asi
# que se apunta a mano en el fichero hosts de Windows. Solo vale en este equipo.
Escribir-Paso 3 "Apuntando $Dominio a este equipo..."

$rutaHosts = Join-Path $env:SystemRoot 'System32\drivers\etc\hosts'
$lineas = @(Get-Content -Path $rutaHosts -ErrorAction SilentlyContinue)
$nombres = @($Dominio, "www.$Dominio")
$nuevas = @()

foreach ($nombre in $nombres) {
    # Se busca el nombre como palabra suelta para no confundir miscatas.es con
    # www.miscatas.es ni con un comentario que lo mencione.
    $yaEsta = $lineas | Where-Object {
        $_ -notmatch '^\s*#' -and $_ -match "(^|\s)$([regex]::Escape($nombre))(\s|$)"
    }
    if ($yaEsta) {
        Escribir-Info "$nombre ya estaba apuntado."
    }
    else {
        $nuevas += "127.0.0.1`t$nombre"
    }
}

if ($nuevas.Count -gt 0) {
    $bloque = @('', '# MisCatas - web local (lo pone tools\desplegar-iis.ps1)') + $nuevas
    Add-Content -Path $rutaHosts -Value $bloque -Encoding ASCII
    Escribir-Ok "Añadido(s) $($nuevas.Count) nombre(s) al fichero hosts."
}
else {
    Escribir-Ok 'El fichero hosts ya estaba correcto.'
}

# La cache de nombres de Windows guarda los fallos anteriores: se limpia.
& ipconfig.exe /flushdns | Out-Null


# --- 4. Certificado de seguridad de miscatas.es -----------------------------
# Hace falta uno propio porque ningun organismo oficial puede certificar un
# nombre que solo existe en esta casa. Se marca como de confianza en este equipo
# para que el navegador no muestre la pantalla roja de aviso.
#
# El certificado tiene que servir para DOS cosas: para el nombre bonito
# (miscatas.es, que es como se abre la web en este ordenador) y para la IP del
# equipo (que es como la abren el movil y el televisor). Si la IP no esta
# dentro, el movil dice que el certificado "no es de esta direccion" y no deja
# entrar ni dandole a Continuar.
#
# La IP se mete como "Direccion IP" y no como nombre: los navegadores de movil
# no aceptan una IP escrita en la casilla de los nombres.
Escribir-Paso 4 "Preparando el certificado de $Dominio..."

$nombreAmistoso = "MisCatas ($Dominio)"

# Lista de direcciones que el certificado debe amparar, en el formato que
# entiende Windows: los nombres con DNS= y la IP con IPAddress=.
$trozosSan = @()
foreach ($nombre in $nombres) { $trozosSan += "DNS=$nombre" }
if ($ipRed) { $trozosSan += "IPAddress=$ipRed" }
$extensionSan = '2.5.29.17={text}' + ($trozosSan -join '&')

# Se mira si ya hay uno hecho y si sigue valiendo. "Valer" es tener cuerda para
# un mes mas y, sobre todo, cubrir la IP de hoy: la IP la reparte el router y
# puede cambiar, y entonces hay que hacer el certificado otra vez.
$certificado = Get-ChildItem -Path Cert:\LocalMachine\My |
    Where-Object { $_.FriendlyName -eq $nombreAmistoso -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

$sirve = $false
if ($certificado) {
    $sirve = $true

    if ($ipRed) {
        $san = $certificado.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.17' }
        $textoSan = if ($san) { $san.Format($false) } else { '' }
        if ($textoSan -notmatch [regex]::Escape($ipRed)) {
            $sirve = $false
            Escribir-Info "El certificado de antes no incluye la IP $ipRed (la red ha cambiado): se hace uno nuevo."
        }
    }
}

if ($sirve) {
    Escribir-Info "Reutilizando el certificado existente (valido hasta $($certificado.NotAfter.ToString('dd/MM/yyyy')))."
}
else {
    # Se retiran los anteriores para no dejar cacharros a medias que luego
    # confundan a IIS ni al navegador.
    Get-ChildItem -Path Cert:\LocalMachine\My |
        Where-Object { $_.FriendlyName -eq $nombreAmistoso } |
        ForEach-Object {
            $huella = $_.Thumbprint
            Remove-Item -Path "Cert:\LocalMachine\My\$huella" -Force -ErrorAction SilentlyContinue
            Remove-Item -Path "Cert:\LocalMachine\Root\$huella" -Force -ErrorAction SilentlyContinue
        }

    $certificado = New-SelfSignedCertificate `
        -Subject "CN=$Dominio" `
        -TextExtension @($extensionSan) `
        -FriendlyName $nombreAmistoso `
        -CertStoreLocation 'Cert:\LocalMachine\My' `
        -KeyExportPolicy Exportable `
        -KeyLength 2048 `
        -NotAfter (Get-Date).AddYears(5)

    Escribir-Ok "Certificado creado para $($trozosSan -join ', '), valido hasta $($certificado.NotAfter.ToString('dd/MM/yyyy'))."
}

# Copia en el almacen de "entidades de confianza": sin esto el navegador de
# ESTE ordenador desconfia de su propio certificado.
#
# Ojo: esto vale solo para este equipo. El movil y el televisor no conocen el
# certificado, asi que la primera vez avisaran de que la conexion "no es de
# fiar"; hay que entrar en "Avanzado" y continuar. Es normal: el certificado lo
# ha hecho esta casa, no una autoridad de internet.
$yaConfiado = Get-ChildItem -Path Cert:\LocalMachine\Root |
    Where-Object { $_.Thumbprint -eq $certificado.Thumbprint }

if ($yaConfiado) {
    Escribir-Info 'El certificado ya estaba marcado como de confianza.'
}
else {
    $almacen = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root', 'LocalMachine')
    $almacen.Open('ReadWrite')
    $almacen.Add($certificado)
    $almacen.Close()
    Escribir-Ok 'Certificado marcado como de confianza en este equipo.'
}


# --- 5. Publicar la aplicacion ---------------------------------------------
# "Publicar" deja en una carpeta la web lista para servir: ya compilada, con el
# CSS minificado y con todos los ficheros estaticos de verdad en wwwroot.
Escribir-Paso 5 'Publicando la aplicacion...'

# Antes de nada, se graba en el web.config la direccion con la que se dibujan
# los QR y se escriben los enlaces de WhatsApp. Se hace aqui, de forma
# automatica, porque la IP la reparte el router y puede cambiar: si quedara
# escrita a mano, un dia los QR dejarian de abrirse sin que nadie supiera por
# que.
function Ajustar-DireccionPublica {
    param([string]$Ruta, [string]$Direccion)

    if (-not (Test-Path $Ruta)) { return $false }

    $texto = Get-Content -Path $Ruta -Raw -Encoding UTF8
    $patron = '(?<inicio><environmentVariable\s+name="Sitio__UrlPublica"\s+value=")(?<valor>[^"]*)(?<fin>")'
    $coincidencia = [regex]::Match($texto, $patron)

    if (-not $coincidencia.Success) {
        Escribir-Aviso "No se ha encontrado Sitio__UrlPublica en $Ruta."
        return $false
    }

    if ($coincidencia.Groups['valor'].Value -eq $Direccion) { return $false }

    $nuevo = [regex]::Replace($texto, $patron, "`${inicio}$Direccion`${fin}")
    [System.IO.File]::WriteAllText($Ruta, $nuevo, (New-Object System.Text.UTF8Encoding($false)))
    return $true
}

if (-not $direccionRed) {
    Escribir-Aviso 'No se ha podido averiguar la IP de este equipo: se deja la direccion que ya hubiera en web.config.'
}
else {
    $rutaWebConfig = Join-Path $proyecto 'web.config'
    if (Ajustar-DireccionPublica -Ruta $rutaWebConfig -Direccion $direccionRed) {
        Escribir-Ok "Direccion de los QR y de WhatsApp actualizada a $direccionRed"
    }
    else {
        Escribir-Info "La direccion de los QR ya era $direccionRed"
    }
}

if ($SinPublicar) {
    Escribir-Info 'Publicacion omitida (-SinPublicar).'
    if (-not (Test-Path (Join-Path $Carpeta 'MisCatas.Web.dll'))) {
        throw "No hay nada publicado en '$Carpeta'. Ejecuta el script sin -SinPublicar al menos una vez."
    }
    # Sin publicar no se copia nada, asi que la direccion hay que escribirla
    # tambien en la copia que ya esta servida.
    if ($direccionRed) {
        if (Ajustar-DireccionPublica -Ruta (Join-Path $Carpeta 'web.config') -Direccion $direccionRed) {
            Escribir-Ok "Direccion actualizada tambien en la web ya publicada."
        }
    }
}
else {
    # Si el sitio ya existe, se para: IIS mantiene los ficheros abiertos y la
    # copia fallaria a medias, dejando la web rota.
    if (Test-Path "IIS:\Sites\$Sitio") {
        Escribir-Info 'Parando el sitio para poder reemplazar los ficheros...'
        Stop-Website -Name $Sitio -ErrorAction SilentlyContinue
        Stop-WebAppPool -Name $Sitio -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
    }

    # La aplicacion arrancada por Arrancar.cmd bloquea los DLL de compilacion.
    # Solo se mata MisCatas.Web: parar todos los "dotnet" se llevaria por
    # delante Visual Studio y las extensiones del editor.
    $previos = @(Get-Process -Name 'MisCatas.Web' -ErrorAction SilentlyContinue)
    if ($previos.Count -gt 0) {
        Escribir-Info "Parando $($previos.Count) instancia(s) de pruebas..."
        $previos | Stop-Process -Force
        Start-Sleep -Seconds 2
    }

    if (-not (Test-Path $Carpeta)) {
        New-Item -Path $Carpeta -ItemType Directory -Force | Out-Null
    }

    Escribir-Info 'Compilando y copiando (incluye el CSS de Tailwind)...'
    & dotnet publish $proyecto -c Release -o $Carpeta --nologo -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "La publicacion ha fallado (codigo $LASTEXITCODE). Revisa los errores de arriba."
    }

    # Comprobaciones: estos son los fallos que dejan la web en blanco o sin
    # estilos, y es mucho mejor enterarse aqui que delante de los invitados.
    $cssPublicado = Join-Path $Carpeta 'wwwroot\css\estilos.css'
    if (-not (Test-Path $cssPublicado)) {
        throw "Falta '$cssPublicado'. Ejecuta tools\descargar-tailwind.ps1 y vuelve a publicar."
    }

    $bytesCss = (Get-Item $cssPublicado).Length
    if ($bytesCss -lt $cssMinimoBytes) {
        throw "El CSS publicado solo tiene $bytesCss bytes (se esperaban mas de $cssMinimoBytes). Tailwind ha purgado de mas y la web se veria sin estilos."
    }

    foreach ($critico in @('MisCatas.Web.dll', 'web.config', 'wwwroot\_framework\blazor.web.js', 'wwwroot\MisCatas.Web.styles.css')) {
        if (-not (Test-Path (Join-Path $Carpeta $critico))) {
            throw "Falta el fichero publicado '$critico'. La publicacion esta incompleta."
        }
    }

    Escribir-Ok "Publicado en $Carpeta (CSS de $bytesCss bytes)."
}


# --- 6. Sitio web y grupo de aplicaciones en IIS ---------------------------
Escribir-Paso 6 'Configurando el sitio en IIS...'

# Grupo de aplicaciones. "No Managed Code" porque la web es .NET moderno: no
# usa el .NET antiguo que IIS cargaria por defecto.
if (Test-Path "IIS:\AppPools\$Sitio") {
    Escribir-Info "El grupo de aplicaciones '$Sitio' ya existia."
}
else {
    New-WebAppPool -Name $Sitio | Out-Null
    Escribir-Ok "Grupo de aplicaciones '$Sitio' creado."
}

Set-ItemProperty "IIS:\AppPools\$Sitio" -Name managedRuntimeVersion -Value ''
Set-ItemProperty "IIS:\AppPools\$Sitio" -Name startMode -Value 'AlwaysRunning'
# Sin perfil de usuario, el guardado de las claves de cifrado falla.
Set-ItemProperty "IIS:\AppPools\$Sitio" -Name processModel.loadUserProfile -Value $true
# Sin apagado por inactividad ni reciclados: una cata en vivo puede estar horas
# abierta en el televisor sin que nadie toque nada.
Set-ItemProperty "IIS:\AppPools\$Sitio" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
Set-ItemProperty "IIS:\AppPools\$Sitio" -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
Escribir-Ok 'Grupo de aplicaciones ajustado (siempre activo, sin reciclados).'

# El sitio por defecto de IIS ocupa los puertos 80 y 443 sin nombre, lo que
# impide que miscatas.es responda. Se para (no se borra, por si hiciera falta).
$porDefecto = Get-Website -Name 'Default Web Site' -ErrorAction SilentlyContinue
if ($porDefecto -and $porDefecto.State -eq 'Started') {
    Stop-Website -Name 'Default Web Site'
    Set-ItemProperty "IIS:\Sites\Default Web Site" -Name serverAutoStart -Value $false
    Escribir-Info 'Parado el sitio de ejemplo de IIS ("Default Web Site"): ocupaba los puertos.'
}

# Sitio web.
if (Test-Path "IIS:\Sites\$Sitio") {
    Escribir-Info "El sitio '$Sitio' ya existia: se actualiza su configuracion."
    Set-ItemProperty "IIS:\Sites\$Sitio" -Name physicalPath -Value $Carpeta
    Set-ItemProperty "IIS:\Sites\$Sitio" -Name applicationPool -Value $Sitio
}
else {
    New-Website -Name $Sitio -PhysicalPath $Carpeta -ApplicationPool $Sitio `
        -HostHeader $Dominio -Port 80 -Force | Out-Null
    Escribir-Ok "Sitio '$Sitio' creado."
}

# Enlaces (por que direccion y puerto responde el sitio).
# Se usa SNI (SslFlags 1) para que varios sitios puedan compartir el puerto 443
# distinguiendose por el nombre.
#
# El ultimo enlace es el importante para los invitados: puerto 5080, cifrado y
# SIN NOMBRE. "Sin nombre" quiere decir que responde a cualquier direccion con
# la que se llame al equipo, y en particular a su IP, que es lo unico que
# entienden el movil y el televisor. Es el mismo puerto de siempre, el de
# Arrancar-Red.cmd, pero ahora lo sirve IIS: ya no hay que arrancar nada.
#
# Ese enlace NO puede llevar SNI: SNI consiste en elegir el certificado segun
# el nombre pedido, y aqui justamente no hay nombre. Por eso el certificado se
# le cuelga despues, a mano, un poco mas abajo.
$enlaces = @(
    @{ Protocolo = 'http';  Puerto = 80;         Nombre = $Dominio },
    @{ Protocolo = 'http';  Puerto = 80;         Nombre = "www.$Dominio" },
    @{ Protocolo = 'https'; Puerto = 443;        Nombre = $Dominio },
    @{ Protocolo = 'https'; Puerto = 443;        Nombre = "www.$Dominio" },
    @{ Protocolo = 'https'; Puerto = $puertoRed; Nombre = '' }
)

foreach ($enlace in $enlaces) {
    $informacion = "*:$($enlace.Puerto):$($enlace.Nombre)"
    $comoSeLlama = if ($enlace.Nombre) { "$($enlace.Protocolo)://$($enlace.Nombre)" } else { "$($enlace.Protocolo) puerto $($enlace.Puerto) (cualquier direccion)" }

    $existe = Get-WebBinding -Name $Sitio -Protocol $enlace.Protocolo -ErrorAction SilentlyContinue |
        Where-Object { $_.bindingInformation -eq $informacion }

    if ($existe) {
        Escribir-Info "El enlace $comoSeLlama ya existia."
        continue
    }

    if ($enlace.Protocolo -eq 'https' -and $enlace.Nombre) {
        New-WebBinding -Name $Sitio -Protocol 'https' -Port $enlace.Puerto `
            -HostHeader $enlace.Nombre -SslFlags 1 | Out-Null
    }
    elseif ($enlace.Protocolo -eq 'https') {
        # Sin nombre: nada de SNI (ver la explicacion de arriba).
        New-WebBinding -Name $Sitio -Protocol 'https' -Port $enlace.Puerto `
            -IPAddress '*' | Out-Null
    }
    else {
        New-WebBinding -Name $Sitio -Protocol 'http' -Port $enlace.Puerto `
            -HostHeader $enlace.Nombre | Out-Null
    }
    Escribir-Ok "Enlace $comoSeLlama creado."
}

# Se le cuelga el certificado a cada enlace seguro. Sin esto el navegador no
# llega ni a pedir la pagina: la conversacion se corta antes de empezar.
#
# Se repasan todos en cada despliegue (no solo los nuevos) porque si el
# certificado se ha vuelto a fabricar -por ejemplo, porque el router ha dado
# otra IP- los enlaces seguirian apuntando al viejo.
foreach ($enlace in (Get-WebBinding -Name $Sitio -Protocol 'https' -ErrorAction SilentlyContinue)) {
    $comoSeLlama = $enlace.bindingInformation

    if ($enlace.certificateHash -eq $certificado.Thumbprint) {
        Escribir-Info "El enlace seguro $comoSeLlama ya usaba este certificado."
        continue
    }

    try {
        $enlace.AddSslCertificate($certificado.Thumbprint, 'My')
        Escribir-Ok "Certificado puesto en el enlace seguro $comoSeLlama."
    }
    catch {
        # El enlace sin nombre (el del 5080) guarda su certificado en una tabla
        # de Windows aparte, y a veces hay que retirar el anterior antes de
        # poder poner el nuevo.
        $puertoEnlace = ($comoSeLlama -split ':')[1]
        & netsh.exe http delete sslcert ipport=0.0.0.0:$puertoEnlace | Out-Null

        try {
            $enlace.AddSslCertificate($certificado.Thumbprint, 'My')
            Escribir-Ok "Certificado puesto en el enlace seguro $comoSeLlama (al segundo intento)."
        }
        catch {
            Escribir-Aviso "No se ha podido poner el certificado en $comoSeLlama : $($_.Exception.Message)"
        }
    }
}

# Que el sitio vuelva solo cada vez que se enciende el ordenador. Junto con el
# grupo de aplicaciones "siempre activo", esto es lo que hace que no haya que
# dar a ningun Arrancar: la web esta puesta y esperando.
Set-ItemProperty "IIS:\Sites\$Sitio" -Name serverAutoStart -Value $true
Escribir-Ok 'El sitio se arrancara solo al encender el equipo.'

# WebSockets. Es imprescindible para la cata en vivo: los votos que aparecen
# al instante en el escaparate viajan por ahi. No se puede activar desde el
# web.config de la aplicacion porque IIS mantiene esa seccion cerrada con
# llave a nivel de servidor (si se intenta, la web responde con error 500.19),
# asi que se enciende aqui, en el propio sitio.
try {
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' `
                                 -Location $Sitio `
                                 -Filter 'system.webServer/webSocket' `
                                 -Name 'enabled' `
                                 -Value $true
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' `
                                 -Location $Sitio `
                                 -Filter 'system.webServer/webSocket' `
                                 -Name 'pingInterval' `
                                 -Value '00:00:10'
    Escribir-Ok 'WebSockets activados (la cata en vivo necesita esto).'
}
catch {
    Escribir-Aviso "No se han podido activar los WebSockets: $($_.Exception.Message)"
    Escribir-Aviso 'La web funcionara, pero el escaparate no se actualizara solo.'
}

# Cortafuegos. Windows bloquea por defecto todo lo que llega de fuera, asi que
# aunque IIS este escuchando en el 5080 el movil recibiria un silencio. Se abre
# solo ese puerto, y solo para entrar.
try {
    # El nombre de la regla se queda como estaba (dice "HTTP") para no duplicar
    # reglas en los equipos donde ya se creo. Lo que importa es el puerto.
    $nombreRegla = "MisCatas (HTTP $puertoRed)"
    $regla = Get-NetFirewallRule -DisplayName $nombreRegla -ErrorAction SilentlyContinue

    if ($regla) {
        Set-NetFirewallRule -DisplayName $nombreRegla -Enabled True -Action Allow `
            -Profile Domain, Private, Public | Out-Null
        Escribir-Info "La regla del cortafuegos '$nombreRegla' ya existia (revisada)."
    }
    else {
        New-NetFirewallRule -DisplayName $nombreRegla `
            -Description 'Permite que los moviles y el televisor de casa abran MisCatas.' `
            -Direction Inbound -Action Allow -Protocol TCP -LocalPort $puertoRed `
            -Profile Domain, Private, Public | Out-Null
        Escribir-Ok "Cortafuegos: abierto el puerto $puertoRed para la red de casa."
    }
}
catch {
    Escribir-Aviso "No se ha podido abrir el puerto $puertoRed en el cortafuegos: $($_.Exception.Message)"
    Escribir-Aviso 'Ejecuta tools\abrir-puerto-firewall.ps1 como administrador.'
}

# Si la red de casa esta marcada como "publica", Windows endurece el filtro y
# los moviles pueden seguir sin ver la web. Se avisa, pero no se cambia nada
# sin permiso.
$redesPublicas = @(Get-NetConnectionProfile -ErrorAction SilentlyContinue |
    Where-Object { $_.NetworkCategory -eq 'Public' })
if ($redesPublicas.Count -gt 0) {
    Escribir-Aviso "La red '$($redesPublicas[0].Name)' esta marcada como publica: puede bloquear el acceso."
    Escribir-Aviso 'Para arreglarlo: tools\abrir-puerto-firewall.ps1 (ofrece cambiarla a privada).'
}


# --- 7. Permisos -----------------------------------------------------------
Escribir-Paso 7 'Dando permisos a la web...'

$identidad = "IIS AppPool\$Sitio"

# a) Carpeta. La web necesita leer todo y escribir en 'claves', donde guarda
#    las claves de cifrado de las cookies. Si no puede escribir, cada reinicio
#    invalida las sesiones y los formularios dan error de token.
& icacls.exe $Carpeta /grant "$($identidad):(OI)(CI)(RX)" /T /C /Q | Out-Null

$carpetaClaves = Join-Path $Carpeta 'claves'
if (-not (Test-Path $carpetaClaves)) {
    New-Item -Path $carpetaClaves -ItemType Directory -Force | Out-Null
}
& icacls.exe $carpetaClaves /grant "$($identidad):(OI)(CI)(M)" /T /C /Q | Out-Null
Escribir-Ok 'Permisos de carpeta concedidos.'

# b) Base de datos. La conexion usa "autenticacion de Windows", asi que quien
#    entra en SQL Server es la propia identidad del sitio de IIS, no el usuario
#    que ejecuta este script. Hay que darle de alta expresamente.
$servidor = $null
$baseDatos = $null
try {
    $ajustes = Get-Content -Raw -Path $rutaAjustes | ConvertFrom-Json
    $cadena = $ajustes.ConnectionStrings.Default
    if ($cadena -match 'Server\s*=\s*([^;]+)') { $servidor = $Matches[1].Trim() }
    if ($cadena -match 'Database\s*=\s*([^;]+)') { $baseDatos = $Matches[1].Trim() }
}
catch {
    Escribir-Aviso "No se ha podido leer '$rutaAjustes': $($_.Exception.Message)"
}

if (-not $servidor -or -not $baseDatos) {
    Escribir-Aviso 'No se ha podido averiguar el servidor de base de datos. Da de alta el acceso a mano (ver el aviso del final).'
}
else {
    Escribir-Info "Dando acceso a '$identidad' en $servidor / $baseDatos..."

    # El acceso se concede con SQL directo para no depender de modulos extra
    # (sqlcmd o SqlServer) que pueden no estar instalados.
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$identidad')
    CREATE LOGIN [$identidad] FROM WINDOWS;

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$identidad')
    CREATE USER [$identidad] FOR LOGIN [$identidad];

ALTER ROLE [db_owner] ADD MEMBER [$identidad];
"@

    # db_owner porque la aplicacion aplica ella misma las migraciones al
    # arrancar: necesita poder crear y modificar tablas.
    try {
        $conexion = New-Object System.Data.SqlClient.SqlConnection
        $conexion.ConnectionString = "Server=$servidor;Database=$baseDatos;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;Connect Timeout=15"
        $conexion.Open()

        $comando = $conexion.CreateCommand()
        $comando.CommandText = $sql
        $comando.ExecuteNonQuery() | Out-Null

        $conexion.Close()
        Escribir-Ok "Acceso a la base de datos concedido a '$identidad'."
    }
    catch {
        Escribir-Aviso "No se ha podido dar el acceso a la base de datos: $($_.Exception.Message)"
        Escribir-Aviso 'Hazlo a mano con SQL Server Management Studio (ver el aviso del final).'
    }
    finally {
        if ($conexion -and $conexion.State -eq 'Open') { $conexion.Close() }
    }
}


# --- 8. Arrancar y comprobar ----------------------------------------------
Escribir-Paso 8 'Arrancando el sitio...'

Start-WebAppPool -Name $Sitio -ErrorAction SilentlyContinue
Start-Website -Name $Sitio

# El primer arranque es lento: la aplicacion aplica las migraciones de base de
# datos y siembra los datos iniciales antes de responder.
Escribir-Info 'Esperando a que responda (el primer arranque tarda)...'

$codigo = $null
for ($intento = 1; $intento -le 12; $intento++) {
    Start-Sleep -Seconds 5
    # -k porque el certificado es propio; curl no conoce el almacen de Windows.
    $codigo = (& curl.exe -k -s -o NUL -w '%{http_code}' "https://$Dominio/" 2>$null)
    if ($codigo -and $codigo -ne '000') { break }
    Escribir-Info "Intento $intento de 12..."
}

# Y ahora la puerta de los invitados: la IP con el puerto 5080. Si esta no
# contesta, el QR no se podra abrir desde el movil, asi que se comprueba
# aparte. Tambien con -k, porque el certificado es de casa.
$codigoRed = $null
if ($direccionRed) {
    for ($intento = 1; $intento -le 6; $intento++) {
        $codigoRed = (& curl.exe -k -s -o NUL -w '%{http_code}' "$direccionRed/" 2>$null)
        if ($codigoRed -and $codigoRed -ne '000') { break }
        Start-Sleep -Seconds 3
    }
}

Write-Host ''
Write-Host '=============================================================' -ForegroundColor Magenta

# 200 = la pagina se ha servido. 302 = redirige a la pantalla de entrada
# (normal, porque hay que identificarse). Las dos cosas significan que va bien.
if ($codigo -in @('200', '302')) {
    Write-Host '  LISTO. La web ya esta instalada en IIS.' -ForegroundColor Green
    Write-Host ''
    Write-Host "  Este equipo       : https://$Dominio" -ForegroundColor White
    Write-Host "  Tambien           : https://www.$Dominio" -ForegroundColor White
    if ($direccionRed) {
        Write-Host "  Moviles/televisor : $direccionRed" -ForegroundColor White
    }
    Write-Host "  Carpeta           : $Carpeta" -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '  Se arranca sola al encender el equipo: no hay que dar a' -ForegroundColor DarkGray
    Write-Host '  ningun Arrancar nunca mas.' -ForegroundColor DarkGray
    Write-Host '  Para quitarla: tools\retirar-iis.ps1 (como administrador).' -ForegroundColor DarkGray
    Write-Host ''

    if (-not $direccionRed) {
        Write-Host '  OJO: no se ha podido averiguar la IP de este equipo, asi que' -ForegroundColor Yellow
        Write-Host '  los QR pueden estar apuntando a una direccion antigua.' -ForegroundColor Yellow
    }
    elseif ($codigoRed -in @('200', '302')) {
        Write-Host "  Los QR y los enlaces de WhatsApp llevan a $direccionRed," -ForegroundColor Green
        Write-Host '  que ya responde: se pueden abrir desde el movil.' -ForegroundColor Green
        Write-Host ''
        Write-Host '  La primera vez, el movil avisara de que la conexion "no es' -ForegroundColor DarkGray
        Write-Host '  privada": el certificado lo ha hecho esta casa, no una' -ForegroundColor DarkGray
        Write-Host '  autoridad de internet. Hay que darle a "Avanzado" y' -ForegroundColor DarkGray
        Write-Host '  continuar. Solo pasa la primera vez en cada aparato.' -ForegroundColor DarkGray
    }
    else {
        Write-Host "  OJO: $direccionRed no responde (codigo '$codigoRed')." -ForegroundColor Yellow
        Write-Host '  Los QR no se podran abrir desde el movil. Cosas que mirar:' -ForegroundColor Yellow
        Write-Host '   - El cortafuegos: tools\abrir-puerto-firewall.ps1' -ForegroundColor Yellow
        Write-Host '   - Que el certificado este puesto en el puerto 5080:' -ForegroundColor Yellow
        Write-Host '     netsh http show sslcert ipport=0.0.0.0:5080' -ForegroundColor Yellow
    }
}
else {
    Write-Host '  La web no responde todavia.' -ForegroundColor Red
    Write-Host ''
    Write-Host "  Respuesta recibida: '$codigo' (000 = nadie contesta)" -ForegroundColor Yellow
    Write-Host ''
    Write-Host '  Cosas que mirar, por orden:' -ForegroundColor White
    Write-Host '   1. Que el grupo de aplicaciones siga arrancado:' -ForegroundColor DarkGray
    Write-Host "      Get-WebAppPoolState -Name $Sitio" -ForegroundColor DarkGray
    Write-Host '   2. El registro de errores de Windows (Visor de eventos ->' -ForegroundColor DarkGray
    Write-Host '      Registros de Windows -> Aplicacion), buscando "IIS AspNetCore".' -ForegroundColor DarkGray
    Write-Host '   3. Que la web tenga acceso a la base de datos: el usuario' -ForegroundColor DarkGray
    Write-Host "      '$identidad' debe existir en SQL Server con permisos sobre '$baseDatos'." -ForegroundColor DarkGray
    Write-Host '   4. Que nadie mas ocupe los puertos 443 o 5080:' -ForegroundColor DarkGray
    Write-Host '      Get-NetTCPConnection -LocalPort 443,5080 -State Listen' -ForegroundColor DarkGray
}
