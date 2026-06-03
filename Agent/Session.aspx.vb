Imports VideoKYC.Services

Partial Public Class AgentSession
    Inherits System.Web.UI.Page

    Protected lblCustName As Global.System.Web.UI.WebControls.Label
    Protected hdnSessionId As Global.System.Web.UI.WebControls.HiddenField

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        ' Verify agent authentication
        If Not User.Identity.IsAuthenticated OrElse Session("AgentId") Is Nothing Then
            FormsAuthentication.SignOut()
            Response.Redirect("Login.aspx")
            Return
        End If

        ' Read session ID from query parameters
        Dim sessionId = Request.QueryString("sid")
        If String.IsNullOrEmpty(sessionId) Then
            Response.Redirect("Queue.aspx")
            Return
        End If

        ' Load session and bind customer name
        Dim sessionSvc As New SessionService()
        Dim sessionData = sessionSvc.GetSession(sessionId)
        
        If sessionData IsNot Nothing Then
            lblCustName.Text = sessionData.CustomerName
            hdnSessionId.Value = sessionId
        Else
            Response.Redirect("Queue.aspx")
        End If
    End Sub
End Class
