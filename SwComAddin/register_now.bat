@echo off
cd /d "C:\Users\12938\Desktop\Software_Plugin\sw-ai-plugin\SwComAddin"
echo === Registering SwComAddin ===
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe /codebase /tlb "bin\Debug\net48\SwComAddin.dll"
echo === Done ===
pause
