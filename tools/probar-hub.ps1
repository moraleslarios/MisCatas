# Sonda de regresión del canal en vivo de la cata.
#
# Comprueba que el «negotiate» del hub NO está bloqueado por antiforgery.
# Un 400 significa que ha vuelto una de estas dos trampas:
#   · falta DisableAntiforgery() en el MapHub
#   · UseStatusCodePagesWithReExecute vuelve a cubrir la ruta del hub
#     (reejecuta el 401 como POST a /not-found y lo convierte en 400)
#
# Resultado esperado con la app arrancada:
#   /_blazor/negotiate     -> 200   (control: el hub de Blazor)
#   /hubs/cata/negotiate   -> 401   (correcto: solo falta la cookie de sesión)

$ErrorActionPreference = 'Continue'

# El certificado de la red local es autofirmado: sin esto, PowerShell 5.1 corta
# la conexion antes de llegar al servidor y la sonda daria un falso negativo.
Add-Type -TypeDefinition @'
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
public static class ConfiarSiempre {
    public static void Activar() {
        ServicePointManager.ServerCertificateValidationCallback =
            delegate (object s, X509Certificate c, X509Chain ch, SslPolicyErrors e) { return true; };
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
    }
}
'@ -ErrorAction SilentlyContinue
[ConfiarSiempre]::Activar()

function Probar($url) {
    try {
        $r = Invoke-WebRequest -Uri $url -Method Post -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop
        return "POST $url -> $($r.StatusCode)"
    }
    catch {
        $resp = $_.Exception.Response
        $codigo = if ($resp) { $resp.StatusCode.value__ } else { 'SIN-RESPUESTA' }
        $cuerpo = ''
        if ($resp) {
            try {
                $lector = New-Object System.IO.StreamReader($resp.GetResponseStream())
                $cuerpo = $lector.ReadToEnd()
                $lector.Close()
            }
            catch { $cuerpo = '(ilegible)' }
        }
        if ($cuerpo.Length -gt 160) { $cuerpo = $cuerpo.Substring(0, 160) }
        return "POST $url -> $codigo | $cuerpo"
    }
}

Probar 'https://localhost:5443/_blazor/negotiate?negotiateVersion=1'
Probar 'https://localhost:5443/hubs/cata/negotiate?negotiateVersion=1'
