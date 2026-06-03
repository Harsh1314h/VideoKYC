Imports System.Web.Services
Imports VideoKYC.Services

Partial Public Class WaitingRoom
    Inherits System.Web.UI.Page

    Protected hdnSessionId As Global.System.Web.UI.WebControls.HiddenField

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        If Session("SessionId") Is Nothing Then
            Response.Redirect("Register.aspx")
        End If
        hdnSessionId.Value = Session("SessionId").ToString()
    End Sub

    <WebMethod>
    Public Shared Function CheckSessionStatus(sessionId As String) As String
        Try
            Dim svc As New SessionService()
            Dim sessionData = svc.GetSession(sessionId)
            If sessionData IsNot Nothing Then
                Return sessionData.Status
            End If
        Catch
            ' Fallback on error
        End Try
        Return "Waiting"
    End Function
End Class
