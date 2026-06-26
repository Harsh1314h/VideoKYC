Partial Public Class CustomerSession
    Inherits System.Web.UI.Page

    Protected hdnSessionId As Global.System.Web.UI.WebControls.HiddenField

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        If Session("SessionId") Is Nothing Then
            Response.Redirect("Register.aspx")
            Return
        End If
        
        Dim sessionId = Session("SessionId").ToString()
        Dim sessionSvc As New Services.SessionService()
        Dim sessionData = sessionSvc.GetSession(sessionId)
        
        If sessionData Is Nothing OrElse sessionData.Status = "Approved" OrElse sessionData.Status = "Rejected" Then
            ' Clear expired/completed session state and redirect
            Session("SessionId") = Nothing
            Session("CustomerName") = Nothing
            Session("CustomerId") = Nothing
            Response.Redirect("Register.aspx")
            Return
        End If
        
        hdnSessionId.Value = sessionId
    End Sub
End Class
