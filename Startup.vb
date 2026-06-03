Imports Microsoft.Owin
Imports Owin

<Assembly: OwinStartup(GetType(VideoKYC.Startup))>

Namespace VideoKYC
    Public Class Startup
        Public Sub Configuration(app As IAppBuilder)
            ' Map SignalR hubs to /signalr
            app.MapSignalR()
        End Sub
    End Class
End Namespace
