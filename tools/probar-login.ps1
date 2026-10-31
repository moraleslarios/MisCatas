$ErrorActionPreference = 'Stop'
$baseUrl = 'https://localhost:5443'

# El certificado de la red local es autofirmado: sin esto, PowerShell 5.1 corta
# la conexion antes de llegar al servidor.
Add-Type -TypeDefinition @'
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
public static class ConfiarSiempreLogin {
    public static void Activar() {
        ServicePointManager.ServerCertificateValidationCallback =
            delegate (object s, X509Certificate c, X509Chain ch, SslPolicyErrors e) { return true; };
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
    }
}
'@ -ErrorAction SilentlyContinue
[ConfiarSiempreLogin]::Activar()

$sesion = New-Object Microsoft.PowerShell.Commands.WebRequestSession

# 1) GET del formulario: recoge la cookie de antiforgery y el token oculto.
$pagina = Invoke-WebRequest -Uri "$baseUrl/cuenta/entrar" -WebSession $sesion -UseBasicParsing -TimeoutSec 20
Write-Output "GET /cuenta/entrar -> HTTP $($pagina.StatusCode) LEN $($pagina.Content.Length)"

$coincidencia = [regex]::Match(
    $pagina.Content,
    'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
if (-not $coincidencia.Success) {
    $coincidencia = [regex]::Match(
        $pagina.Content,
        'value="([^"]+)"[^>]*name="__RequestVerificationToken"')
}
Write-Output "Token encontrado: $($coincidencia.Success)"

$cookies = $sesion.Cookies.GetCookies([Uri]$baseUrl)
foreach ($c in $cookies) { Write-Output "Cookie: $($c.Name)" }

if (-not $coincidencia.Success) {
    Write-Output 'SIN TOKEN: no se puede probar el POST.'
    return
}

# 2) POST con token correcto y credenciales inventadas: debe devolver 200 con
#    el mensaje de error de credenciales, NO un 400 de antiforgery.
$cuerpo = @{
    '_handler'                   = 'formulario-entrar'
    '__RequestVerificationToken' = $coincidencia.Groups[1].Value
    'Datos.Login'                = 'usuario-que-no-existe'
    'Datos.Contrasena'           = 'clave-mala'
    'Datos.Recordarme'           = 'true'
}

try {
    $respuesta = Invoke-WebRequest -Uri "$baseUrl/cuenta/entrar" -Method Post `
        -Body $cuerpo -WebSession $sesion -UseBasicParsing `
        -MaximumRedirection 0 -TimeoutSec 20
    Write-Output "POST con token -> HTTP $($respuesta.StatusCode) LEN $($respuesta.Content.Length)"
    $error1 = [regex]::Match($respuesta.Content, '<p class="error"[^>]*>([^<]+)</p>')
    if ($error1.Success) { Write-Output "Mensaje mostrado: $($error1.Groups[1].Value.Trim())" }
    if ($respuesta.Content -match 'antiforgery') { Write-Output 'OJO: aparece la palabra antiforgery' }
}
catch {
    $codigo = $_.Exception.Response.StatusCode.value__
    Write-Output "POST con token -> HTTP $codigo LOC=$($_.Exception.Response.Headers['Location'])"
}
