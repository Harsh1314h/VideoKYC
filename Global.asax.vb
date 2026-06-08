Imports System.IO
Imports System.Web
Imports Serilog

Public Class Global_asax
    Inherits System.Web.HttpApplication

    <System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet:=System.Runtime.InteropServices.CharSet.Auto, SetLastError:=True)>
    Private Shared Function SetDllDirectory(lpPathName As String) As Boolean
    End Function

    Protected Sub Application_Start(sender As Object, e As EventArgs)
        ' Ensure the log directory exists
        Dim logDir = Server.MapPath("~/App_Data/Logs")
        If Not Directory.Exists(logDir) Then
            Directory.CreateDirectory(logDir)
        End If

        ' Configure Serilog
        Dim logPath = Path.Combine(logDir, "kyc-.log")
        Log.Logger = New LoggerConfiguration() _
            .MinimumLevel.Debug() _
            .WriteTo.File(logPath, 
                          rollingInterval:=RollingInterval.Day, 
                          outputTemplate:="{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}") _
            .CreateLogger()

        Log.Information("Video KYC Application Started.")

        ' Set DLL Directory for OpenCV and Tesseract native assemblies
        Try
            Dim baseDir = AppDomain.CurrentDomain.BaseDirectory
            Dim subFolder = If(Environment.Is64BitProcess, "x64", "x86")
            Dim dllPath = Path.Combine(baseDir, "bin", "dll", subFolder)
            If Not Directory.Exists(dllPath) Then
                dllPath = Path.Combine(baseDir, "dll", subFolder)
            End If
            If Directory.Exists(dllPath) Then
                SetDllDirectory(dllPath)
                Log.Information("Native DLL search directory successfully set to: " & dllPath)
            Else
                Log.Warning("Native DLL search directory not found at: " & dllPath)
            End If
        Catch ex As Exception
            Log.Error(ex, "Failed to set native DLL search directory.")
        End Try
    End Sub

    Protected Sub Session_Start(sender As Object, e As EventArgs)
        ' Initialize session defaults if needed
    End Sub

    Protected Sub Application_BeginRequest(sender As Object, e As EventArgs)
        ' Handle pre-flight CORS requests or configuration if needed
    End Sub

    Protected Sub Application_Error(sender As Object, e As EventArgs)
        Dim exc As Exception = Server.GetLastError()
        If exc IsNot Nothing Then
            Log.Error(exc, "Unhandled application error occurred.")
        End If
    End Sub

    Protected Sub Application_End(sender As Object, e As EventArgs)
        Log.Information("Video KYC Application Stopped.")
        Log.CloseAndFlush()
    End Sub
End Class
