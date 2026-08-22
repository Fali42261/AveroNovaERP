$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NativeInput {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@

function Get-AppProcess {
  Get-Process -Name 'AveroNova.App.UI' -ErrorAction SilentlyContinue | Select-Object -First 1
}

function Get-Root {
  $proc = Get-AppProcess
  if (-not $proc) { throw 'App process not running' }
  if ($proc.MainWindowHandle -ne [IntPtr]::Zero) {
    [void][NativeInput]::ShowWindow($proc.MainWindowHandle, 9)
    [void][NativeInput]::SetForegroundWindow($proc.MainWindowHandle)
  }
  return [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
}

function Dump-Tree([System.Windows.Automation.AutomationElement]$root, [int]$depth = 0, [int]$max = 4) {
  if ($depth -gt $max -or $null -eq $root) { return }
  $name = $root.Current.Name
  $type = $root.Current.ControlType.ProgrammaticName
  $auto = $root.Current.AutomationId
  if ($name -or $auto) {
    ('  ' * $depth) + "$type name='$name' id='$auto'"
  }
  $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
  $child = $walker.GetFirstChild($root)
  while ($null -ne $child) {
    Dump-Tree $child ($depth + 1) $max
    $child = $walker.GetNextSibling($child)
  }
}

function Find-ByName([System.Windows.Automation.AutomationElement]$root, [string]$name, [string]$controlType = $null) {
  $cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, $name)
  if ($controlType) {
    $typeId = switch ($controlType) {
      'Button' { [System.Windows.Automation.ControlType]::Button }
      'Text' { [System.Windows.Automation.ControlType]::Text }
      'Edit' { [System.Windows.Automation.ControlType]::Edit }
      'ListItem' { [System.Windows.Automation.ControlType]::ListItem }
      default { [System.Windows.Automation.ControlType]::Button }
    }
    $typeCond = New-Object System.Windows.Automation.PropertyCondition(
      [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $typeId)
    $cond = New-Object System.Windows.Automation.AndCondition($cond, $typeCond)
  }
  return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Invoke-Click([System.Windows.Automation.AutomationElement]$el) {
  if ($null -eq $el) { throw 'Element not found' }
  try {
    $pat = $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pat.Invoke()
    return
  } catch {}
  $rect = $el.Current.BoundingRectangle
  Add-Type -AssemblyName System.Windows.Forms
  [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(
    [int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2))
  $sig = @'
[DllImport("user32.dll")] public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);
'@
  $native = Add-Type -MemberDefinition $sig -Name MouseClickTmp -Namespace Win32 -PassThru
  $native::mouse_event(0x0002, 0, 0, 0, 0)
  $native::mouse_event(0x0004, 0, 0, 0, 0)
}

$root = Get-Root
Write-Output '=== WINDOW ==='
Write-Output $root.Current.Name
Write-Output '=== TREE ==='
Dump-Tree $root 0 3
Write-Output '=== LOOKUPS ==='
foreach ($n in @('Products','View','Edit Product','PRODUCT','PRICING','INVENTORY','Sign In','Product Overview','P-VIEW-001','ABC Premium Product International Edition Extra Long Name')) {
  $el = Find-ByName $root $n
  if ($el) { Write-Output "FOUND $n type=$($el.Current.ControlType.ProgrammaticName)" }
  else { Write-Output "MISSING $n" }
}
