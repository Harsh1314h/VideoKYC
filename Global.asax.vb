Imports System.IO
Imports System.Web
Imports Serilog

Public Class Global_asax
    Inherits System.Web.HttpApplication

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
