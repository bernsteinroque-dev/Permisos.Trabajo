# Instalación PTS Synthon en Windows Server 2022

## Requisitos Previos

| Componente | Versión mínima |
|---|---|
| Windows Server | 2022 |
| .NET Runtime | 8.0 (ASP.NET Core Hosting Bundle) |
| SQL Server | 2019 o SQL Server Express 2019 |
| IIS | 10.0 con módulo ASP.NET Core |
| Active Directory | Dominio SYNTHON configurado |

---

## 1. Instalar el Hosting Bundle de .NET 8

1. Descargar desde: https://dotnet.microsoft.com/download/dotnet/8.0  
   Seleccionar **ASP.NET Core Runtime 8.x.x – Windows Hosting Bundle**
2. Ejecutar el instalador como Administrador.
3. Reiniciar IIS:
   ```
   net stop was /y
   net start w3svc
   ```

---

## 2. Configurar SQL Server

### 2a. Instalar SQL Server Express (si no existe)
Descargar SQL Server 2019 Express desde Microsoft y ejecutar el instalador.  
Asegurarse de que la instancia quede como `.\SQLEXPRESS`.

### 2b. Habilitar autenticación Windows y TCP/IP
1. Abrir **SQL Server Configuration Manager**.
2. En *SQL Server Network Configuration → Protocols for SQLEXPRESS*: habilitar **TCP/IP**.
3. Reiniciar el servicio SQL Server.

### 2c. Ejecutar el script de base de datos
```sql
-- Ejecutar como sysadmin en SQL Server Management Studio:
-- Archivo: deploy\database\01_create_database.sql
```

---

## 3. Configurar Active Directory

Crear los siguientes grupos de seguridad en el dominio **SYNTHON**:

| Grupo | Descripción |
|---|---|
| `PTS_Admins` | Acceso completo: crear, editar, aprobar, eliminar |
| `PTS_Supervisores` | Aprobar y rechazar permisos |
| `PTS_Proveedores` | Crear y consultar sus propios permisos |
| `PTS_Lectura` | Solo lectura de todos los permisos |

Agregar los usuarios correspondientes a cada grupo según el rol deseado.

---

## 4. Configurar IIS

### 4a. Habilitar características de IIS
En **Server Manager → Add Roles and Features**:
- Web Server (IIS)
  - Common HTTP Features: Static Content, Default Document
  - Security: Windows Authentication
  - Application Development: (se instala con Hosting Bundle)

### 4b. Crear el Application Pool
1. Abrir **IIS Manager**.
2. Application Pools → Add Application Pool:
   - Name: `PTS_Synthon`
   - .NET CLR Version: **No Managed Code**
   - Managed Pipeline Mode: **Integrated**
3. Clic en el pool creado → Advanced Settings:
   - Identity: `ApplicationPoolIdentity` (o una cuenta de dominio con permisos en SQL)
   - Enable 32-Bit Applications: **False**

### 4c. Crear el sitio web
1. Sites → Add Website:
   - Site name: `PTS_Synthon`
   - Application Pool: `PTS_Synthon`
   - Physical path: `C:\inetpub\wwwroot\PTS_Synthon`
   - Binding: HTTPS, puerto 443, certificado SSL del servidor
2. Deshabilitar autenticación anónima y habilitar autenticación Windows:
   - Seleccionar el sitio → Authentication
   - Anonymous Authentication: **Disabled**
   - Windows Authentication: **Enabled**

---

## 5. Publicar la Aplicación

### Opción A: Publicación desde Visual Studio
1. Abrir solución `PTS_Synthon.sln` en Visual Studio 2022.
2. Clic derecho en `PTS_Synthon` → **Publish**.
3. Seleccionar **Folder**, destino: `C:\inetpub\wwwroot\PTS_Synthon`.
4. Configuration: **Release**, Target Framework: **net8.0**.
5. Clic en **Publish**.

### Opción B: Publicación desde línea de comandos
```powershell
cd src\PTS_Synthon
dotnet publish -c Release -r win-x64 --self-contained false -o C:\inetpub\wwwroot\PTS_Synthon
```

---

## 6. Configurar appsettings.json

Editar `C:\inetpub\wwwroot\PTS_Synthon\appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=PTS_Synthon;Integrated Security=True;TrustServerCertificate=True;"
  },
  "AdSettings": {
    "Domain": "SYNTHON",
    "AdminGroup": "PTS_Admins",
    "SupervisorGroup": "PTS_Supervisores",
    "ProveedorGroup": "PTS_Proveedores",
    "LecturaGroup": "PTS_Lectura"
  },
  "EmailSettings": {
    "SmtpServer": "smtp.office365.com",
    "SmtpPort": 587,
    "UseTLS": true,
    "SenderEmail": "pts@synthon.com.ar",
    "SenderName": "PTS Synthon Safety System",
    "SenderPassword": "YOUR_APP_PASSWORD_HERE"
  },
  "NotificacionSettings": {
    "SupervisorEmail": "supervisores.pts@synthon.com.ar",
    "DaysBeforeVencimientoAlert": 30
  }
}
```

**Importante:** Reemplazar `YOUR_APP_PASSWORD_HERE` con la contraseña de aplicación de Office 365.

---

## 7. Copiar web.config

Copiar `deploy\iis\web.config` al directorio de publicación:
```powershell
Copy-Item deploy\iis\web.config C:\inetpub\wwwroot\PTS_Synthon\web.config -Force
```

---

## 8. Permisos de Carpeta

Otorgar permisos al Application Pool sobre la carpeta:
```powershell
icacls "C:\inetpub\wwwroot\PTS_Synthon" /grant "IIS AppPool\PTS_Synthon:(OI)(CI)M"
icacls "C:\inetpub\wwwroot\PTS_Synthon\logs" /grant "IIS AppPool\PTS_Synthon:(OI)(CI)F"
```

Crear la carpeta de logs si no existe:
```powershell
New-Item -ItemType Directory -Path "C:\inetpub\wwwroot\PTS_Synthon\logs" -Force
```

---

## 9. Firewall

Abrir el puerto 443 (HTTPS) en el firewall de Windows:
```powershell
New-NetFirewallRule -DisplayName "PTS Synthon HTTPS" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow
```

---

## 10. Verificación

1. Abrir navegador en la red interna y navegar a `https://servidor.synthon.local/`
2. El sistema debería autenticar automáticamente con credenciales Windows.
3. Verificar que el usuario aparezca en la esquina superior derecha con su rol asignado.
4. Probar creación de un permiso de prueba.

### Logs
Los logs de la aplicación se encuentran en:
- `C:\inetpub\wwwroot\PTS_Synthon\logs\stdout_*.log`
- Event Viewer → Windows Logs → Application (fuente: IIS AspNetCore Module)

---

## Solución de Problemas

| Síntoma | Causa probable | Solución |
|---|---|---|
| Error 401 al acceder | Windows Auth no configurado | Verificar paso 4c |
| Error 500.30 | .NET 8 no instalado | Reinstalar Hosting Bundle |
| Sin conexión a BD | Cadena de conexión incorrecta | Verificar appsettings.json y permisos SQL |
| Usuario sin rol | No está en grupo AD | Agregar al grupo correspondiente en AD |
| Emails no enviados | Contraseña SMTP incorrecta | Actualizar SenderPassword en appsettings.json |

---

## Actualización de la Aplicación

```powershell
# 1. Hacer backup de la configuración actual
Copy-Item C:\inetpub\wwwroot\PTS_Synthon\appsettings.json C:\backups\appsettings.json.bak

# 2. Detener el sitio en IIS
Stop-WebSite -Name "PTS_Synthon"

# 3. Publicar nueva versión
cd path\to\source
dotnet publish -c Release -r win-x64 --self-contained false -o C:\inetpub\wwwroot\PTS_Synthon

# 4. Restaurar configuración
Copy-Item C:\backups\appsettings.json.bak C:\inetpub\wwwroot\PTS_Synthon\appsettings.json

# 5. Iniciar el sitio
Start-WebSite -Name "PTS_Synthon"
```

---

*PTS Synthon - Synthon Argentina S.A. | Sistema de Permisos de Trabajo*
