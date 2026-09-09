Describe "AcuConnectionManager per-runspace connections" {
    BeforeAll {
        $modulePath = Join-Path $PSScriptRoot "..\AcuPackageTools\AcuPackageTools.psd1"
        Import-Module $modulePath -Force

        function New-ModuleRunspace {
            $iss = [System.Management.Automation.Runspaces.InitialSessionState]::CreateDefault()
            $iss.ImportPSModule($modulePath)
            $rs = [runspacefactory]::CreateRunspace($iss)
            $rs.Open()
            return $rs
        }

        function Invoke-InRunspace {
            param(
                [System.Management.Automation.Runspaces.Runspace]$Runspace,
                [string]$Script
            )
            $ps = [powershell]::Create()
            try {
                $ps.Runspace = $Runspace
                [void]$ps.AddScript($Script)
                $output = $ps.Invoke()
                [pscustomobject]@{
                    Output   = $output
                    Errors   = @($ps.Streams.Error)
                    Warnings = @($ps.Streams.Warning)
                }
            }
            finally {
                $ps.Dispose()
            }
        }

        $connectScript = @'
$client = [AcuPackageTools.Connection.AcuConnectionManager]::CreateNewClient()
[AcuPackageTools.Connection.AcuConnectionManager]::SetConnection($client, $args_url, $null)
'@
    }

    Context "Isolation between runspaces" {
        BeforeEach {
            $rsA = New-ModuleRunspace
            $rsB = New-ModuleRunspace
        }

        AfterEach {
            foreach ($rs in @($rsA, $rsB)) {
                if ($rs.RunspaceStateInfo.State -eq 'Opened') {
                    Invoke-InRunspace -Runspace $rs -Script 'Disconnect-AcuInstance -WarningAction SilentlyContinue' | Out-Null
                }
                $rs.Dispose()
            }
        }

        It "a connection in one runspace is invisible to another" {
            Invoke-InRunspace -Runspace $rsA -Script ($connectScript -replace '\$args_url', "'https://a.example.com'") | Out-Null

            $seenByA = Invoke-InRunspace -Runspace $rsA -Script '[AcuPackageTools.Connection.AcuConnectionManager]::IsConnected'
            $seenByB = Invoke-InRunspace -Runspace $rsB -Script '[AcuPackageTools.Connection.AcuConnectionManager]::IsConnected'

            $seenByA.Output[0] | Should -BeTrue
            $seenByB.Output[0] | Should -BeFalse
        }

        It "an API cmdlet in an unconnected runspace reports not connected" {
            Invoke-InRunspace -Runspace $rsA -Script ($connectScript -replace '\$args_url', "'https://a.example.com'") | Out-Null

            $result = Invoke-InRunspace -Runspace $rsB -Script 'Get-AcuPublishedPackages'

            $result.Errors.Count | Should -BeGreaterThan 0
            $result.Errors[0].Exception.Message | Should -Match 'Not connected to an Acumatica instance'
        }

        It "disconnecting one runspace does not clear another" {
            Invoke-InRunspace -Runspace $rsA -Script ($connectScript -replace '\$args_url', "'https://a.example.com'") | Out-Null
            Invoke-InRunspace -Runspace $rsB -Script ($connectScript -replace '\$args_url', "'https://b.example.com'") | Out-Null

            Invoke-InRunspace -Runspace $rsB -Script '[AcuPackageTools.Connection.AcuConnectionManager]::ClearConnection()' | Out-Null

            $urlInA = Invoke-InRunspace -Runspace $rsA -Script '[AcuPackageTools.Connection.AcuConnectionManager]::Url'
            $urlInA.Output[0] | Should -Be 'https://a.example.com'
            $stillB = Invoke-InRunspace -Runspace $rsB -Script '[AcuPackageTools.Connection.AcuConnectionManager]::IsConnected'
            $stillB.Output[0] | Should -BeFalse
        }

        It "Connect-AcuInstance in an already-connected runspace warns and returns" {
            Invoke-InRunspace -Runspace $rsA -Script ($connectScript -replace '\$args_url', "'https://a.example.com'") | Out-Null

            $result = Invoke-InRunspace -Runspace $rsA -Script @'
$cred = [pscredential]::new('user', (ConvertTo-SecureString 'x' -AsPlainText -Force))
Connect-AcuInstance -Url 'https://other.example.com' -Credential $cred
'@

            $result.Warnings.Count | Should -BeGreaterThan 0
            $result.Warnings[0].Message | Should -Match 'Already connected'
        }

        It "closing a runspace removes its entry" {
            Invoke-InRunspace -Runspace $rsA -Script ($connectScript -replace '\$args_url', "'https://a.example.com'") | Out-Null
            $key = $rsA.InstanceId

            $field = [AcuPackageTools.Connection.AcuConnectionManager].GetField(
                '_connections', [System.Reflection.BindingFlags]'NonPublic,Static')
            $dict = $field.GetValue($null)
            $dict.ContainsKey($key) | Should -BeTrue

            $rsA.Close()

            # StateChanged handlers may run just after Close returns.
            $deadline = [datetime]::UtcNow.AddSeconds(2)
            while ($dict.ContainsKey($key) -and [datetime]::UtcNow -lt $deadline) {
                Start-Sleep -Milliseconds 50
            }
            $dict.ContainsKey($key) | Should -BeFalse
        }
    }
}
