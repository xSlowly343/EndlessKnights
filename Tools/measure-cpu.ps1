param(
    [string]$ExePath = (Join-Path $PSScriptRoot '../Builds/Windows/EndlessKnights.exe'),
    [int]$WarmupSeconds = 10,
    [int]$SampleSeconds = 30,
    [int]$FrameRate = 0
)

$ErrorActionPreference = 'Stop'
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class CpuProbeNative {
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string className, string windowName);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
"@

$game = Start-Process -FilePath (Resolve-Path $ExePath) -PassThru
try {
    Start-Sleep -Seconds $WarmupSeconds
    if ($FrameRate -gt 0) {
        $tray = [CpuProbeNative]::FindWindow('EndlessKnightsTray', [NullString]::Value)
        [void][CpuProbeNative]::PostMessage($tray, 0x0111, [IntPtr]$FrameRate, [IntPtr]::Zero)
        Start-Sleep -Seconds 3
    }

    $game.Refresh()
    $cpuStart = $game.TotalProcessorTime
    $clock = [Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Seconds $SampleSeconds
    $game.Refresh()
    $cpuUsed = ($game.TotalProcessorTime - $cpuStart).TotalSeconds
    $elapsed = $clock.Elapsed.TotalSeconds

    $cores = [Environment]::ProcessorCount
    $percentOfMachine = 100 * $cpuUsed / ($elapsed * $cores)
    $percentOfOneCore = 100 * $cpuUsed / $elapsed
    $memoryMb = $game.WorkingSet64 / 1MB

    $limit = if ($FrameRate -gt 0) { "$FrameRate FPS" } else { 'default limit' }
    "{0}: CPU {1:N2}% of machine ({2} logical cores), {3:N1}% of one core, working set {4:N0} MB, sampled {5:N0} s" -f $limit, $percentOfMachine, $cores, $percentOfOneCore, $memoryMb, $elapsed
}
finally {
    if (-not $game.HasExited) { Stop-Process -Id $game.Id }
}
