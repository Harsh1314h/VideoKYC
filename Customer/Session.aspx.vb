Partial Public Class CustomerSession
    Inherits System.Web.UI.Page

    Protected hdnSessionId As Global.System.Web.UI.WebControls.HiddenField

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        If Session("SessionId") Is Nothing Then
            Response.Redirect("Register.aspx")
        End If
        
        hdnSessionId.Value = Session("SessionId").ToString()
    End Sub
End Class
