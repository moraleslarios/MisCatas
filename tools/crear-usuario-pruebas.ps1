# Crea un usuario administrador temporal para verificacion visual.
# Copia el hash de la contrasena del usuario 'escaparate' -> contrasena: escaparate
$sql = @"
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Login = 'pruebas')
BEGIN
    INSERT INTO Usuarios (Login, Nombre, Apellidos, Telefono, PasswordHash, Rol, DebeCambiarPassword, Activo)
    SELECT 'pruebas', 'Usuario', 'De Pruebas', '600000000', PasswordHash, 1, 0, 1
    FROM Usuarios WHERE Login = 'escaparate';
END
SELECT Id, Login, Rol FROM Usuarios WHERE Login = 'pruebas';
"@

sqlcmd -S "7940HS\LOCAL" -E -d Catas -Q $sql -W -s "|"
