Imports SolidWorks.Interop.sldworks
Imports SolidWorks.Interop.swconst
Imports System
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Module SwAddinLoader

    Dim swApp As Object
    Dim addinPanel As Object

    Sub Main()
        ' This macro connects to SolidWorks and launches the WPF panel
        Try
            swApp = GetObject(, "SldWorks.Application")
            If swApp Is Nothing Then
                MsgBox("Please run this macro from inside SolidWorks", vbExclamation, "SW AI Plugin")
                Exit Sub
            End If

            LaunchPlugin()
        Catch ex As Exception
            MsgBox("Error: " & ex.Message, vbCritical, "SW AI Plugin")
        End Try
    End Sub

    Sub LaunchPlugin()
        ' Create TaskPane and embed our WPF control
        Try
            Dim taskPaneView As Object
            taskPaneView = swApp.CreateTaskpaneView2("", "SW AI Plugin")

            If taskPaneView Is Nothing Then
                ' Fallback: just launch the standalone WPF app
                LaunchStandalone()
                Exit Sub
            End If

            ' Launch WPF app and get its window handle
            Dim proc As System.Diagnostics.Process
            proc = System.Diagnostics.Process.Start(
                "C:\Users\12938\Desktop\sw-ai-plugin\SwAiPlugin\bin\Debug\net9.0-windows\SwAiPlugin.exe")

            MsgBox("SW AI Plugin launched!" & vbCrLf & _
                   "Use the standalone window to control SolidWorks.", vbInformation, "SW AI Plugin")

        Catch ex As Exception
            ' Fallback to standalone
            LaunchStandalone()
        End Try
    End Sub

    Sub LaunchStandalone()
        Dim proc As System.Diagnostics.Process
        proc = System.Diagnostics.Process.Start(
            "C:\Users\12938\Desktop\sw-ai-plugin\SwAiPlugin\bin\Debug\net9.0-windows\SwAiPlugin.exe")
    End Sub

End Module
