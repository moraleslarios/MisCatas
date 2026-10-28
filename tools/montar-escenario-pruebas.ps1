# Monta un escenario de prueba: vinos en la cata 3, 'pruebas' como participante
# y la cata en curso con el vino 1 en votacion.
$sql = @"
SET NOCOUNT ON;

DECLARE @cata INT = 3;
DECLARE @pruebas INT = (SELECT Id FROM Usuarios WHERE Login = 'pruebas');

-- Participante
IF NOT EXISTS (SELECT 1 FROM CataUsuarios WHERE CataId = @cata AND UsuarioId = @pruebas)
    INSERT INTO CataUsuarios (CataId, UsuarioId) VALUES (@cata, @pruebas);

-- Vinos (los 4 primeros disponibles)
DELETE FROM CataVinos WHERE CataId = @cata;
INSERT INTO CataVinos (CataId, VinoId, Orden, EstadoVotacion)
SELECT @cata, Id, ROW_NUMBER() OVER (ORDER BY Id), 0
FROM (SELECT TOP 4 Id FROM Vinos ORDER BY Id) v;

-- Primer vino cerrado, segundo abierto
UPDATE CataVinos SET EstadoVotacion = 2 WHERE CataId = @cata AND Orden = 1;
UPDATE CataVinos SET EstadoVotacion = 1, InicioVotacionUtc = SYSUTCDATETIME() WHERE CataId = @cata AND Orden = 2;

-- Cata en curso apuntando al vino 2
DECLARE @vino2 INT = (SELECT VinoId FROM CataVinos WHERE CataId = @cata AND Orden = 2);
UPDATE Catas SET Estado = 2, VinoActualId = @vino2, FechaApertura = SYSUTCDATETIME() WHERE Id = @cata;

-- Votos del vino 1 (todos los participantes)
DELETE FROM Votos WHERE CataId = @cata;
DECLARE @vino1 INT = (SELECT VinoId FROM CataVinos WHERE CataId = @cata AND Orden = 1);
INSERT INTO Votos (CataId, VinoId, UsuarioId, Nota, Comentario, SegundosEnVotar, FechaVotoUtc, FueRectificado, EsAutomatico)
SELECT @cata, @vino1, UsuarioId, 7.5, 'Muy redondo', 42, SYSUTCDATETIME(), 0, 0
FROM CataUsuarios WHERE CataId = @cata;

SELECT Orden, EstadoVotacion FROM CataVinos WHERE CataId = @cata ORDER BY Orden;
SELECT COUNT(*) AS Votos FROM Votos WHERE CataId = @cata;
"@

sqlcmd -S "7940HS\LOCAL" -E -d Catas -Q $sql -W -s "|"
