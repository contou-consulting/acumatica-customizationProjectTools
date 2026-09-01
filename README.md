# acumatica-customizationProjectTools

A PowerShell Module for Building, Uploading and Publishing Customization Packages to Acumatica ERP.

Uses code for building customization packages from the CustomizationProjectTools command line application from Gabriel Michaud's excellent [Acumatica CI/CD Demo](https://github.com/VelixoSolutions/AcumaticaCIDemo).

## Installation

```powershell
Install-Module -Name AcuPackageTools
```

## Cmdlet Overview

| Cmdlet | Description |
|--------|-------------|
| `Connect-AcuInstance` | Establish a persistent connection to an Acumatica instance |
| `Disconnect-AcuInstance` | Close the connection to an Acumatica instance |
| `New-AcuPackage` | Build a customization package ZIP from a directory |
| `Import-AcuPackage` | Upload a package to an Acumatica instance |
| `Export-AcuPackage` | Download a package from an Acumatica instance |
| `Publish-AcuPackage` | Publish uploaded packages to an instance |
| `Unpublish-AcuPackages` | Unpublish all packages from an instance |
| `Get-AcuPublishedPackages` | List currently published packages on an instance |
| `Remove-AcuPackage` | Delete an unpublished package from an instance |

Discover all cmdlets:
```powershell
Get-Command -Module AcuPackageTools
# or
Get-Command -Noun Acu*
```

---

## Authentication

API cmdlets support two modes of authentication:

### Connection Mode (Recommended)

Establish a persistent connection once, then run multiple commands without re-authenticating:

```powershell
# Connect once
$cred = Get-Credential
Connect-AcuInstance -Url "https://acumatica.example.com" -Credential $cred

# Run multiple commands - no need to pass credentials
Import-AcuPackage -PackageName "MyPackage" -PackagePath ".\MyPackage.zip" -ReplacePackage
Publish-AcuPackage -ProjectNames @("MyPackage") -TenantMode Current
Export-AcuPackage -ProjectName "MyPackage" -OutputPath ".\backup.zip"

# Disconnect when done
Disconnect-AcuInstance
```

### One-Off Mode

Pass `-Url` and `-Credential` directly to each command (logs in/out per command):

```powershell
$cred = Get-Credential
Import-AcuPackage -Url "https://acumatica.example.com" -Credential $cred `
                  -PackageName "MyPackage" -PackagePath ".\MyPackage.zip"
```

### Creating Credentials

**Interactive:**
```powershell
$cred = Get-Credential -UserName "admin" -Message "Enter Acumatica credentials"
```

**Scripted/Automation:**
```powershell
$securePassword = ConvertTo-SecureString "YourPassword" -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential("admin", $securePassword)
```

**Secure Storage (for CI/CD):**
```powershell
# Save credentials (encrypted to current user)
Get-Credential | Export-Clixml -Path ".\acumatica-cred.xml"

# Load credentials
$cred = Import-Clixml -Path ".\acumatica-cred.xml"
```

---

## Connection Management

### Connect-AcuInstance

Establish a persistent connection to an Acumatica instance. The connection is reused by all subsequent API cmdlets until `Disconnect-AcuInstance` is called.

**Parameters:**

| Parameter | Alias | Type | Required | Description |
|-----------|-------|------|----------|-------------|
| Url | | string | Yes | URL of the Acumatica instance |
| Credential | | PSCredential | Yes | Credentials for authentication |
| Tenant | -t | string | No | Tenant to authenticate to |
| SkipCertificateCheck | | switch | No | Disable TLS certificate validation for this connection (e.g. self-signed certs). All subsequent cmdlets using the shared connection inherit the bypass |

**Example:**
```powershell
$cred = Get-Credential
Connect-AcuInstance -Url "https://acumatica.example.com" -Credential $cred -Tenant "MyCompany"
```

---

### Disconnect-AcuInstance

Close the connection to the Acumatica instance and clean up resources.

**Example:**
```powershell
Disconnect-AcuInstance
```

---

## Package Build

### New-AcuPackage

Wraps a directory saved from an Acumatica ERP instance into a ZIP that can be uploaded. Does not spin up an entire app domain like PX.Commandline.exe - much faster and does not require an existing Acumatica site.

**Parameters:**

| Parameter | Alias | Type | Required | Description |
|-----------|-------|------|----------|-------------|
| CustomizationPath | -cp | string | Yes | Path to the customization directory saved via source control |
| PackageFileName | -pfn | string | Yes | Output path and filename for the generated .zip |
| ProductVersion | -v | string | Yes | Acumatica version in format `##.###` (e.g., `24.200`) |
| Description | -d | string | No | Description displayed in customization project list |
| Level | -l | int | No | Package level for publish order |

**Example:**
```powershell
New-AcuPackage -CustomizationPath ".\MyCustomization" `
               -PackageFileName ".\Output\MyPackage.zip" `
               -ProductVersion "24.200" `
               -Description "My customization package" `
               -Level 1
```

---

## Package Management (REST API)

These cmdlets use the Customization REST API introduced in Acumatica 22R2.

> **Note:** When using `Connect-AcuInstance`, the `Url` and `Credential` parameters are optional for all API cmdlets below. If not connected, both parameters are required.

### Import-AcuPackage

Upload a customization package to an Acumatica ERP instance.

**Parameters:**

| Parameter | Alias | Type | Required | Description |
|-----------|-------|------|----------|-------------|
| Url | | string | Yes | URL of the Acumatica instance |
| Credential | | PSCredential | Yes | Credentials for authentication |
| Tenant | -t | string | No | Tenant to authenticate to |
| PackageName | -pn | string | Yes | Name of the package in Acumatica |
| PackagePath | -pp | string | Yes | Path to the .zip file to upload |
| ReplacePackage | -r | switch | No | Replace if package exists (otherwise throws error) |
| PackageDescr | -d | string | No | Description for the package |
| Level | -lvl | int | No | Package level for publish order |

**Example:**
```powershell
$cred = Get-Credential
Import-AcuPackage -Url "https://acumatica.example.com" `
                  -Credential $cred `
                  -PackageName "MyPackage" `
                  -PackagePath ".\Output\MyPackage.zip" `
                  -ReplacePackage
```

---

### Export-AcuPackage

Download a customization package from an Acumatica ERP instance. Useful for retrieving packages from one environment (e.g., development) to deploy on another (e.g., testing).

**Parameters:**

| Parameter | Alias | Type | Required | Description |
|-----------|-------|------|----------|-------------|
| Url | | string | Yes | URL of the Acumatica instance |
| Credential | | PSCredential | Yes | Credentials for authentication |
| Tenant | -t | string | No | Tenant to authenticate to |
| ProjectName | -pn | string | Yes | Name of the project to export |
| OutputPath | -o, -pp | string | Yes | Path where the .zip file will be saved |
| AutoResolveConflicts | | switch | No | Include file system changes if conflicts exist |

**Example:**
```powershell
$cred = Get-Credential

# Export a package
Export-AcuPackage -Url "https://acumatica.example.com" `
                  -Credential $cred `
                  -ProjectName "MyPackage" `
                  -OutputPath ".\Downloads\MyPackage.zip"

# Export with file system changes included
Export-AcuPackage -Url "https://acumatica.example.com" `
                  -Credential $cred `
                  -ProjectName "MyPackage" `
                  -OutputPath ".\Downloads\MyPackage.zip" `
                  -AutoResolveConflicts
```

---

### Get-AcuPublishedPackages

Retrieve the list of currently published customization packages and their items from an Acumatica ERP instance. Returns a response object containing `Projects` (names of published packages) and `Items` (customization items such as screens, DACs, and actions).

**Parameters:**

| Parameter | Alias | Type | Required | Description |
|-----------|-------|------|----------|-------------|
| Url | | string | Yes | URL of the Acumatica instance |
| Credential | | PSCredential | Yes | Credentials for authentication |
| Tenant | -t | string | No | Tenant to authenticate to |

**Example:**
```powershell
$cred = Get-Credential

# List published packages
$published = Get-AcuPublishedPackages -Url "https://acumatica.example.com" -Credential $cred
$published.Projects | ForEach-Object { Write-Host $_.Name }

# With connection mode
Connect-AcuInstance -Url "https://acumatica.example.com" -Credential $cred
$published = Get-AcuPublishedPackages
$published.Projects.Name  # List package names
$published.Items           # List customization items
```

---

### Publish-AcuPackage

Publish uploaded packages to an Acumatica ERP instance. Supports `-WhatIf` and `-Confirm`.

**Parameters:**

| Parameter | Alias | Type | Required | Description |
|-----------|-------|------|----------|-------------|
| Url | | string | Yes | URL of the Acumatica instance |
| Credential | | PSCredential | Yes | Credentials for authentication |
| Tenant | -t | string | No | Tenant to authenticate to |
| ProjectNames | -pn | string[] | Yes | Names of projects to publish |
| TenantMode | -tm | enum | Yes | `Current`, `All`, or `List` |
| MergeWithExisting | | switch | No | Merge with existing customization |
| OnlyValidate | | switch | No | Validate without publishing |
| DbUpdateOnly | | switch | No | Only update database |
| ExecuteAllScripts | | switch | No | Re-execute all scripts |
| TenantLoginNames | -tln | string[] | No | Tenant names when using `List` mode |

**Example:**
```powershell
$cred = Get-Credential
Publish-AcuPackage -Url "https://acumatica.example.com" `
                   -Credential $cred `
                   -ProjectNames @("MyPackage", "AnotherPackage") `
                   -TenantMode Current `
                   -MergeWithExisting

# Preview what would happen
Publish-AcuPackage -Url "https://acumatica.example.com" `
                   -Credential $cred `
                   -ProjectNames @("MyPackage") `
                   -TenantMode Current `
                   -WhatIf
```

---

### Unpublish-AcuPackages

Unpublish all packages from an Acumatica ERP instance. Supports `-WhatIf` and `-Confirm` (high impact - will prompt by default).

> **Note:** The Acumatica API only supports unpublishing ALL packages. You cannot unpublish individual packages.

**Parameters:**

| Parameter | Alias | Type | Required | Description |
|-----------|-------|------|----------|-------------|
| Url | | string | Yes | URL of the Acumatica instance |
| Credential | | PSCredential | Yes | Credentials for authentication |
| Tenant | -t | string | No | Tenant to authenticate to |
| TenantMode | -tm | enum | Yes | `Current`, `All`, or `List` |
| TenantLoginNames | -tln | string[] | No | Tenant names when using `List` mode |

**Example:**
```powershell
$cred = Get-Credential

# Will prompt for confirmation due to high impact
Unpublish-AcuPackages -Url "https://acumatica.example.com" `
                      -Credential $cred `
                      -TenantMode Current

# Skip confirmation (use with caution)
Unpublish-AcuPackages -Url "https://acumatica.example.com" `
                      -Credential $cred `
                      -TenantMode Current `
                      -Confirm:$false
```

---

### Remove-AcuPackage

Delete an unpublished customization project from an Acumatica ERP instance. Supports `-WhatIf` and `-Confirm` (high impact - will prompt by default).

> **Note:** You can only delete packages that are **not currently published**. The API deletes project data and project items from the database, but does not delete files/objects added by the project (site map nodes, reports, etc.).

**Parameters:**

| Parameter | Alias | Type | Required | Description |
|-----------|-------|------|----------|-------------|
| Url | | string | Yes | URL of the Acumatica instance |
| Credential | | PSCredential | Yes | Credentials for authentication |
| Tenant | -t | string | No | Tenant to authenticate to |
| ProjectName | -pn | string | Yes | Name of the project to delete |

**Example:**
```powershell
$cred = Get-Credential

# Will prompt for confirmation due to high impact
Remove-AcuPackage -Url "https://acumatica.example.com" `
                  -Credential $cred `
                  -ProjectName "MyOldPackage"

# Preview what would happen
Remove-AcuPackage -Url "https://acumatica.example.com" `
                  -Credential $cred `
                  -ProjectName "MyOldPackage" `
                  -WhatIf
```

---

## Complete CI/CD Example

```powershell
# Configuration
$acumaticaUrl = "https://acumatica.example.com"
$packageName = "MyCustomization"
$customizationPath = ".\src\MyCustomization"
$outputPath = ".\artifacts\$packageName.zip"
$acumaticaVersion = "24.200"

# Load saved credentials and connect
$cred = Import-Clixml -Path ".\acumatica-cred.xml"
Connect-AcuInstance -Url $acumaticaUrl -Credential $cred

try {
    # Build the package (local operation, no connection needed)
    New-AcuPackage -CustomizationPath $customizationPath `
                   -PackageFileName $outputPath `
                   -ProductVersion $acumaticaVersion `
                   -Description "Build $(Get-Date -Format 'yyyy-MM-dd HH:mm')" `
                   -Level 1

    # Upload to Acumatica
    Import-AcuPackage -PackageName $packageName `
                      -PackagePath $outputPath `
                      -ReplacePackage

    # Publish
    Publish-AcuPackage -ProjectNames @($packageName) `
                       -TenantMode Current `
                       -MergeWithExisting
}
finally {
    # Always disconnect
    Disconnect-AcuInstance
}
```

---

## Migration from v0.x

If upgrading from a previous version, note these breaking changes:

| Old Cmdlet | New Cmdlet |
|------------|------------|
| `Invoke-PackageBuild` | `New-AcuPackage` |
| `Invoke-ApiPackageUpload` | `Import-AcuPackage` |
| `Invoke-ApiPackagePublish` | `Publish-AcuPackage` |
| `Invoke-ApiPackageUnpublishAll` | `Unpublish-AcuPackages` |

**New in v1.0** (no v0.x equivalent):
- `Connect-AcuInstance` / `Disconnect-AcuInstance` — persistent connection mode
- `Export-AcuPackage` — download packages from an instance
- `Get-AcuPublishedPackages` — query published packages
- `Remove-AcuPackage` — delete unpublished packages

**Authentication changed** from `-Username`/`-Password` to `-Credential`:

```powershell
# Old (no longer works)
Invoke-ApiPackageUpload -Username "admin" -Password "pass" ...

# New
$cred = Get-Credential
Import-AcuPackage -Credential $cred ...
```

---

## License

MIT License - see [LICENSE](LICENSE) for details.
