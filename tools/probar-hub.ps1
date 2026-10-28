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

Probar 'http://localhost:5080/_blazor/negotiate?negotiateVersion=1'
Probar 'http://localhost:5080/hubs/cata/negotiate?negotiateVersion=1'
