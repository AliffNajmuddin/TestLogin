<#
Run from repo root:
  powershell -ExecutionPolicy Bypass -File .\scripts\Remove-DuplicateUI.ps1
#>
$files = @(
  'TestLogin/Views/DockableMainPane.cs',
  'TestLogin/Views/MainWindow.xaml',
  'TestLogin/Views/MainWindow.xaml.cs'
)

Write-Host "Deleting duplicate/unused UI files (git tracked only):"
foreach ($f in $files) {
    if (git ls-files --error-unmatch -- $f 2>$null) {
        git rm -f -- $f
        Write-Host "  removed: $f"
    } else {
        Write-Host "  skipping (not tracked): $f"
    }
}

git commit -m "chore: remove duplicate DockableMainPane.cs and unused MainWindow UI"
Write-Host "Done. Run: dotnet clean; dotnet build"